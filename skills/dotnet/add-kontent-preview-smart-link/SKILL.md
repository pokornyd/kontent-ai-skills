---
name: add-kontent-preview-smart-link
description: Add Kontent.ai preview mode and Smart Link click-to-edit to a server-rendered ASP.NET Core app (MVC or Razor Pages) that already reads content through Kontent.Ai.Delivery - a second Delivery client on the Preview API, a secret-gated and iframe-safe preview cookie, the Smart Link SDK loaded for editors only, data-kontent attributes in views and rich-text resolvers, and the preview URLs to configure in Kontent.ai. Use whenever the user wants editors to see unpublished or draft content, mentions preview, live preview, Web Spotlight, Smart Link, click-to-edit, edit overlays or preview URLs together with Kontent.ai and .NET, or asks why their site does not load inside Kontent.ai's preview pane. Do not use for a first-time Kontent.ai integration (that is build-kontent-aspnetcore-mvc), for SPA frontends, or for caching and webhooks.
license: MIT
compatibility: Requires the .NET 10 SDK, Kontent.Ai.Delivery 20.x already wired into a server-rendered ASP.NET Core app, a Delivery Preview API key, and Python 3 for the bundled check. Designed for coding agents with shell access (Claude Code or similar).
metadata:
  author: kontent-ai
  version: "0.1.1"
---

# Add Kontent.ai preview mode and Smart Link

Let editors open the site on unpublished content and click from any field straight into the Kontent.ai editor, while visitors keep getting published content from the same deployment. This is a small change: one copied file, two partials, a few lines of wiring and attributes in the views. Most of what can go wrong is silent and only shows up inside Kontent.ai's preview iframe, which is why the code is bundled rather than described.

## Scope

Server-rendered ASP.NET Core (MVC or Razor Pages) with `Kontent.Ai.Delivery` already registered. For a SPA behind a .NET API, step 2 still holds because preview mode lives on the server, and so does the attribute contract in step 3; loading the SDK and emitting attributes happen in the frontend's own idiom (the `@kontent-ai/smart-link` npm package and JSX), which this skill does not cover. Creating preview URLs in Kontent.ai is the user's job: it needs the Kontent.ai app or a Management API key, so step 5 tells them exactly what to enter.

## Workflow

- [ ] 1. Inspect the app
- [ ] 2. Add preview mode - copy `assets/KontentPreview.cs`
- [ ] 3. Add Smart Link - copy the two partials, add the attributes
- [ ] 4. Verify - run `scripts/verify_preview.py`
- [ ] 5. Report, with the Kontent.ai configuration

Read the [Gotchas](#gotchas) before step 2.

## What the user provides

| Input | How it arrives | If missing |
| --- | --- | --- |
| Preview API key | `KONTENT_PREVIEW_API_KEY` exported in the shell, or already in the app's user secrets | Ask the user to export it; never ask them to paste it into chat |
| Gate secret | Not needed from the user | Generate one for local testing (`openssl rand -hex 24`), keep it in a shell variable, and tell the user to generate their own for each deployment |

Refer to keys by variable name only (`"$KONTENT_PREVIEW_API_KEY"`). A value never goes into a command's text, a file, a diff or the report, and `dotnet user-secrets list` is never run, because it prints them.

## 1. Inspect the app

Find, before changing anything: where `AddDeliveryClient` is called; which class owns the Delivery queries and how it receives `IDeliveryClient`; the layout; the views that render content and whether their view models expose the item ID; where rich-text resolvers are registered; the language codenames in use; whether the app already sets a `Content-Security-Policy`, uses antiforgery forms, or caches output. Keep the project's conventions and line endings.

## 2. Add preview mode

Copy `assets/KontentPreview.cs` into the web project and change its namespace. It contains the options, a scoped `IPreviewContext`, the cookie protector, the middleware and the registration helpers. It was compiled and run against `Kontent.Ai.Delivery` 20.0.1 (`IDeliveryClientFactory.Get()` and `TryGet(name)`, `UsePreviewApi(key)`, `WaitForLoadingNewContent(bool)`), so its API calls need no re-checking against the package; spend that time on the app instead. Then:

```csharp
builder.Services.AddDeliveryClient(delivery => delivery.Options.BindConfiguration("DeliveryOptions")); // unchanged
builder.Services.AddKontentPreview(builder.Configuration);

app.UseHttpsRedirection();
app.UseKontentPreview();   // before the endpoints
```

In the class that owns the queries, inject `IDeliveryClientFactory` and `IPreviewContext` instead of `IDeliveryClient`, and pick the client per request:

```csharp
public sealed class ContentService(IDeliveryClientFactory clients, IPreviewContext preview, ILogger<ContentService> logger)
{
    var result = await clients.ForRequest(preview).GetItems<Article>()
        .WaitForLoadingNewContent(preview.IsPreview)   // an editor who just saved must not get the pre-save copy
        ...
```

`ForRequest` returns the named `"preview"` client for a valid grant and the default client otherwise, so visitors run on the registration they always did. Nothing else in the app needs to know about preview.

How it behaves, so you can explain it: a request carrying `?secret=<PreviewOptions:Secret>` gets a protected, time-limited cookie and a redirect to the same URL without the secret; a wrong secret is a 404; `/preview/exit?returnUrl=/path` clears the cookie. Preview responses carry `Cache-Control: no-store`, `X-Robots-Tag: noindex` and `Content-Security-Policy: frame-ancestors https://app.kontent.ai`, and lose any `X-Frame-Options`. With no secret or no key configured, preview cannot be switched on at all.

Configuration: `PreviewOptions:Secret` and `DeliveryOptions:PreviewApiKey` go to user secrets locally and to environment variables (`PreviewOptions__Secret`, `DeliveryOptions__PreviewApiKey`) in deployment. If the project has no `<UserSecretsId>`, add one by editing the project file in place, `<UserSecretsId>` with the output of `uuidgen` inside the existing `<PropertyGroup>`. Do not run `dotnet user-secrets init`: it rewrites the whole file with a byte-order mark and new line endings, and undoing that costs more than the one line it adds.

```bash
dotnet user-secrets set "DeliveryOptions:PreviewApiKey" "$KONTENT_PREVIEW_API_KEY" --project <web-project>
```

Read `references/preview-mode.md` when the app already sets a CSP or uses output caching, runs on more than one instance, needs real authentication in front of preview, or is not a plain MVC or Razor Pages app.

## 3. Add Smart Link

Copy `assets/_SmartLinkScript.cshtml` and `assets/_PreviewBanner.cshtml` into `Views/Shared/` (`Pages/Shared/` for Razor Pages), change the namespace in their `@inject` line, and reference them from the layout along with the two page-level attributes:

```razor
@inject IConfiguration Configuration
<head>
    ...
    <partial name="_SmartLinkScript" />
</head>
<body data-kontent-environment-id="@Configuration["DeliveryOptions:EnvironmentId"]"
      data-kontent-language-codename="default">
    <partial name="_PreviewBanner" />
```

The language is the Kontent.ai language **codename** of the content on the page, which is whatever the project calls it and is often literally `default`; in a multilingual app emit the codename the request was served in, not a culture name.

Then mark up what the views render, from the outside in:

```razor
@using YourApp.Content   @* only for the generated ...Codename constants *@
<article data-kontent-item-id="@Model.ItemId">
    <h1 data-kontent-element-codename="@Article.TitleCodename">@Model.Title</h1>
    <img-asset asset="@Model.Image" data-kontent-element-codename="@Article.ImageCodename" ... />
    <div data-kontent-element-codename="@Article.BodyCopyCodename">
        <rich-text content="@Model.Body" />
    </div>
</article>
```

- **Item**: `data-kontent-item-id` on the container of everything that came from one content item, from `IContentItem<T>.System.Id`. If a view model lacks it, add `Guid? ItemId` and set it in the mapper; Razor omits the attribute when the value is `null`. Each card in a listing is its own item.
- **Element**: `data-kontent-element-codename` on the tag that renders a field. Use the generated constants so a typo cannot compile. `<rich-text>` renders in place with no wrapper element, so it needs a wrapping tag to carry the attribute; `<img-asset>` passes the attribute through to the `<img>`.
- **Linked item** rendered inside another item's markup (an author, a related card): its own `data-kontent-item-id`, nested, with its own element codenames inside.
- **Rich-text component**: the resolver adds the component id to the root tag it returns. Its argument is an `IEmbeddedContent<T>`, which is an `IContentItem<T>`, so `System.Id` is there next to `Elements`:

  ```csharp
  .WithContentResolver<Disclaimer>(component =>
      $"<aside data-kontent-component-id=\"{component.System.Id}\">" +
      $"<strong data-kontent-element-codename=\"{Disclaimer.HeadlineCodename}\">{Encode(component.Elements.Headline)}</strong></aside>")
  ```

  A component is addressed by component id, a linked item by item id; mixing them up opens the wrong thing or nothing.

Emit the attributes unconditionally. They are inert without the SDK, which only loads for preview requests, and resolvers are singletons that cannot see the request anyway.

Read `references/smart-link.md` when the user asks for add buttons, when a page mixes languages, when the app has a CSP that restricts `script-src`, or when editors report that overlays do not appear.

## 4. Verify

Build, then check the behaviour by requesting pages rather than by reading code. Start the app with both secrets in its environment and run the bundled check against one page that renders an item, preferably one whose rich text contains a component:

```bash
export PREVIEW_SECRET="$(openssl rand -hex 24)"
env "DeliveryOptions__PreviewApiKey=$KONTENT_PREVIEW_API_KEY" "PreviewOptions__Secret=$PREVIEW_SECRET" \
  dotnet run --project <web-project> --no-launch-profile --urls http://127.0.0.1:<port> &
python3 <skill-dir>/scripts/verify_preview.py http://127.0.0.1:<port> /articles/<slug> \
  --environment-id <environment-id> --item-codename <item_codename>
```

It looks the item's real ids up from the Delivery API and checks the visitor page, the gate, the cookie flags, the framing headers, the SDK and every attribute, exiting non-zero on a failure. Fix what it reports and rerun until it passes, then stop the process you started. Plain `http` is enough for this check because it sends the cookie itself; a browser only keeps a `Secure` cookie over HTTPS (`dotnet dev-certs https --trust`) or on `localhost`.

What proves preview works is the host the app called (`preview-deliver.kontent.ai`) and asset URLs on `preview-assets-…`. Item counts prove nothing: a draft of a published item is the normal case, so both APIs return the same number of items. There is also no silent fallback to worry about: a preview client without a valid key gets 401, never published content.

The check cannot open Kontent.ai. Whether overlays appear inside live preview is for the user to confirm after step 5; say so.

## 5. Report

```markdown
## Kontent.ai preview and Smart Link

- Preview mode: <files added and changed>; visitors still served by the default client
- Smart Link: SDK <version pin>, loaded for preview requests only; marked up: <items, elements, components>
- Verification: `verify_preview.py` <n> checks passed against <page>; not verifiable from here: overlays inside Kontent.ai live preview
- Secrets: `DeliveryOptions:PreviewApiKey` and `PreviewOptions:Secret` in <user secrets / environment variables>; generate a new gate secret per deployment

### Configure in Kontent.ai
1. Environment settings > Preview URLs > Preview URLs for content types: for each routable type, the page's real URL plus the secret, for example `https://<host>/articles/{URLslug}?secret=<gate secret>` (`{URLslug}`, `{Lang}`, `{Codename}` and `{ItemId}` are Kontent.ai macros). If the environment uses spaces, set each space's domain on the Space domains tab.
2. Environment settings > General: activate live preview if it is not on yet.
3. The host must be HTTPS, because Kontent.ai is, and editors' browsers must allow third-party cookies for it.

### Before this is reachable from the internet
The URL secret switches preview display on; it is not access control. Anyone who learns it sees drafts. Put authentication or a network rule in front of preview requests, or serve preview from a separate, non-public host.
```

Give the preview URL for every content type the app routes, using its real route template.

## Gotchas

- **`SameSite=None; Secure` is what makes the cookie work in Kontent.ai.** Live preview loads the site in a cross-site iframe. ASP.NET Core's default, `Lax`, works perfectly in a browser tab and is silently dropped in the iframe, so the editor sees published content and nothing is logged. The bundled middleware sets it; do not "tidy" it to `Lax` or `Strict`.
- **Antiforgery adds `X-Frame-Options: SAMEORIGIN`** to any page that renders a form token, which blocks the iframe for that page only. The middleware removes it on preview responses and sets `frame-ancestors` instead; if the app sets `X-Frame-Options` elsewhere (a security-headers package, a reverse proxy), that has to allow Kontent.ai too.
- **The Preview API and Secure Access are mutually exclusive on one client.** Options validation throws when `UsePreviewApi` and `UseSecureAccess` are both true, which is why the preview client binds the shared section and then switches Secure Access off. Never flip `UsePreviewApi` on the default client: that turns the public site into a draft viewer.
- **A preview client is never cached by the SDK**, and preview pages must not be cached by anything else. If the app uses output caching or a CDN, preview requests have to bypass it; `no-store` is set for you, but an output-cache policy keyed only on the path will still serve a visitor's copy to an editor.
- **The cookie is protected with ASP.NET Core Data Protection.** On more than one instance, or across restarts of a container without a persisted key ring, a cookie issued by one instance is rejected by the next and the editor drops back to published content for no visible reason. Persist and share the key ring (`PersistKeysTo…`) in that case and say so in the report.
- **Smart Link v5 renamed `data-kontent-project-id` to `data-kontent-environment-id`.** Older samples and most training data show the v4 name, which v5 ignores. The script tag pins the major version for the same reason: `@latest` will one day be v6.
- **Generated records stay out of `_ViewImports.cshtml`.** A view that needs `Article.TitleCodename` gets its own `@using` for the content namespace; that imports constants for attributes, not models for rendering.
- **Outside Kontent.ai the overlays need `?ksl-enabled`** in the URL. Inside live preview the SDK activates by itself. An editor reporting "no overlays" in a plain tab is usually this.
- **Rich-text resolvers are singletons.** They cannot read `IPreviewContext`, so they emit `data-kontent-component-id` always. That is fine: without the SDK the attribute does nothing.
