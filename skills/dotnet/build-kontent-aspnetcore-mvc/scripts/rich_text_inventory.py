#!/usr/bin/env python3
"""List what the rich text of one content type actually embeds and links to.

Generated models cannot tell you this: every rich-text property is RichTextContent,
whatever editors put inside it. Each type printed here needs a resolver registered with
AddKontentRichText, or the SDK renders a "Missing resolver" HTML comment in its place.

usage: rich_text_inventory.py <environment-id> <content-type-codename> [language-codename]
Secure Access: export KONTENT_SECURE_ACCESS_KEY; it is sent as a bearer token, never printed.
Standard library only.
"""
import json
import os
import sys
import urllib.parse
import urllib.request


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    environment, content_type = sys.argv[1], sys.argv[2]
    query = {"system.type": content_type, "depth": "1"}
    if len(sys.argv) > 3:
        query["language"] = sys.argv[3]
    key = os.environ.get("KONTENT_SECURE_ACCESS_KEY")
    url = f"https://deliver.kontent.ai/{environment}/items-feed?{urllib.parse.urlencode(query)}"

    embedded, links, items = {}, {}, 0
    continuation = None
    while True:
        page, token = fetch_page(url, key, continuation)
        modular = page.get("modular_content", {})
        for item in page.get("items", []):
            items += 1
            for element_codename, element in item["elements"].items():
                if element.get("type") != "rich_text":
                    continue
                where = f"{item['system']['codename']}.{element_codename}"
                for codename in element.get("modular_content", []):
                    linked_type = modular.get(codename, {}).get("system", {}).get("type", "<not returned>")
                    embedded.setdefault(linked_type, []).append(where)
                for link in element.get("links", {}).values():
                    links.setdefault(link["type"], []).append(where)
        if not token:
            break
        continuation = token

    print(f"{items} '{content_type}' item(s) inspected")
    report("Embedded components and linked items (WithContentResolver<T>)", embedded)
    report("Content item links (WithContentItemLinkResolver)", links)


def fetch_page(url, key, continuation):
    request = urllib.request.Request(url)
    if key:
        request.add_header("Authorization", f"Bearer {key}")
    if continuation:
        request.add_header("X-Continuation", continuation)
    with urllib.request.urlopen(request) as response:
        return json.load(response), response.headers.get("X-Continuation")


def report(title, found):
    print(f"\n{title}:")
    if not found:
        print("  none")
    for type_codename in sorted(found):
        places = sorted(set(found[type_codename]))
        shown = ", ".join(places[:5]) + (f" and {len(places) - 5} more" if len(places) > 5 else "")
        print(f"  {type_codename}: {shown}")


if __name__ == "__main__":
    main()
