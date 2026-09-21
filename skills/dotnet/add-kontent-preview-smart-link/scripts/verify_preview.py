#!/usr/bin/env python3
"""Check a preview + Smart Link integration by requesting pages from the running app.

usage: verify_preview.py <base-url> <page-path> --environment-id ID --item-codename CODENAME [options]
  base-url            where the app listens, e.g. http://127.0.0.1:5080 (plain http is fine for this check)
  page-path           path of a page that renders the item, e.g. /articles/my-article
  --secret-env NAME   environment variable holding the preview gate secret (default: PREVIEW_SECRET)
  --secret-param P    query parameter that carries it (default: secret)
  --cookie-name N     preview cookie name (default: .Kontent.Preview)

Run the app with the same gate secret (PreviewOptions__Secret) and a Preview API key
(DeliveryOptions__PreviewApiKey). Expected ids are read from the published Delivery API; for an
environment with Secure Access export KONTENT_SECURE_ACCESS_KEY. Secrets are read from the environment
and never printed. Exit code 1 when a required check fails. Standard library only.
"""
import argparse, http.client, json, os, re, sys, urllib.error, urllib.parse, urllib.request

failed = 0


def check(label, ok, detail="", required=True):
    global failed
    failed += 0 if ok or not required else 1
    print(f"{'PASS' if ok else ('FAIL' if required else 'NOTE')}  {label}" + (f"  [{detail}]" if detail != "" else ""))


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("base_url"); p.add_argument("page_path")
    p.add_argument("--environment-id", required=True); p.add_argument("--item-codename", required=True)
    p.add_argument("--secret-env", default="PREVIEW_SECRET"); p.add_argument("--secret-param", default="secret")
    p.add_argument("--cookie-name", default=".Kontent.Preview")
    a = p.parse_args()
    secret = os.environ.get(a.secret_env) or sys.exit(f"{a.secret_env} is not set: export the gate secret the app runs with")

    api = urllib.request.Request(f"https://deliver.kontent.ai/{a.environment_id}/items/{a.item_codename}?depth=1")
    if os.environ.get("KONTENT_SECURE_ACCESS_KEY"):
        api.add_header("Authorization", "Bearer " + os.environ["KONTENT_SECURE_ACCESS_KEY"])
    try:
        data = json.load(urllib.request.urlopen(api))
    except urllib.error.HTTPError as e:
        sys.exit(f"Delivery API answered {e.code} for item '{a.item_codename}': check the environment ID and codename")
    item, modular = data["item"], data["modular_content"]
    components = [modular[c]["system"] for e in item["elements"].values() if e["type"] == "rich_text"
                  for c in e.get("modular_content", []) if c in modular]
    known = set(item["elements"]) | {k for m in modular.values() for k in m["elements"]}

    u = urllib.parse.urlparse(a.base_url)

    def get(path, cookie=None):
        conn = (http.client.HTTPSConnection if u.scheme == "https" else http.client.HTTPConnection)(u.hostname, u.port, timeout=30)
        conn.request("GET", path, headers={"Cookie": cookie} if cookie else {})
        r = conn.getresponse(); body = r.read().decode("utf-8", "replace")
        return r.status, {k.lower(): v for k, v in r.getheaders()}, r.msg.get_all("Set-Cookie") or [], body

    sep = "&" if "?" in a.page_path else "?"
    print("-- visitor")
    s, _, _, html = get(a.page_path)
    check("page answers 200", s == 200, s)
    check("no preview-assets URLs: visitors get published content", "preview-assets" not in html)
    check("Smart Link SDK not loaded for visitors", not re.search(r"<script[^>]+smart-?link", html, re.I))
    check("data-kontent-* attributes absent for visitors (harmless when present)", "data-kontent-" not in html, required=False)

    print("-- entering preview")
    s, _, _, _ = get(f"{a.page_path}{sep}{a.secret_param}=not-{secret[::-1]}")
    check("wrong secret is rejected", s in (401, 403, 404), s)
    s, h, cookies, _ = get(f"{a.page_path}{sep}{a.secret_param}={urllib.parse.quote(secret)}")
    location = h.get("location", "")
    check("correct secret redirects", s in (301, 302, 303, 307), s)
    check("secret is stripped from the redirect target", bool(location) and secret not in location and a.secret_param + "=" not in location, location)
    raw = next((c for c in cookies if c.startswith(a.cookie_name + "=")), cookies[0] if cookies else "")
    low = raw.lower()
    check("preview cookie is set", bool(raw), raw.split("=")[0])
    check("cookie is SameSite=None (needed inside the Kontent.ai iframe)", "samesite=none" in low)
    check("cookie is Secure", "; secure" in low); check("cookie is HttpOnly", "httponly" in low)
    name, _, value = raw.split(";")[0].partition("=")
    check("cookie value is protected, not a plain flag", len(value) > 40, f"{len(value)} chars")

    print("-- previewer")
    path = urllib.parse.urlparse(location).path or a.page_path
    s, h, _, html = get(path, cookie=f"{name}={value}")
    check("page answers 200", s == 200, s)
    check("assets come from preview-assets: the Preview API answered", "preview-assets" in html)
    check("no X-Frame-Options header", "x-frame-options" not in h, h.get("x-frame-options", ""))
    csp = h.get("content-security-policy", "")
    check("CSP frame-ancestors allows app.kontent.ai", "frame-ancestors" in csp and "kontent.ai" in csp, csp)
    check("Cache-Control: no-store", "no-store" in h.get("cache-control", ""), h.get("cache-control", ""))
    script = re.search(r'<script[^>]+src="([^"]*smart-?link[^"]*)"', html, re.I)
    check("Smart Link SDK is loaded", bool(script), script.group(1) if script else "")
    check("SDK version is pinned (not @latest)", bool(script) and "@latest" not in script.group(1))
    check("environment id reaches the SDK", f'data-kontent-environment-id="{a.environment_id}"' in html
          or re.search(r'environmentId"?\s*:\s*"' + re.escape(a.environment_id), html) is not None)
    language = item["system"]["language"]
    check(f"language codename reaches the SDK as '{language}'", f'data-kontent-language-codename="{language}"' in html
          or re.search(r'languageCodename"?\s*:\s*"' + re.escape(language) + '"', html) is not None,
          re.findall(r'data-kontent-language-codename="([^"]*)"', html)[:2])
    check("v4 attribute data-kontent-project-id is not used", "data-kontent-project-id" not in html)
    check("item id of the page's item", f'data-kontent-item-id="{item["system"]["id"]}"' in html)
    codes = set(re.findall(r'data-kontent-element-codename="([^"]*)"', html))
    check("element codenames exist in the content model", bool(codes) and codes <= known, sorted(codes))
    for c in components:
        check(f"rich-text component '{c['type']}' carries its component id", f'data-kontent-component-id="{c["id"]}"' in html)

    print("-- forged cookie")
    _, _, _, html = get(a.page_path, cookie=f"{name}=true")
    check("a hand-made cookie does not switch preview on", "preview-assets" not in html)
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
