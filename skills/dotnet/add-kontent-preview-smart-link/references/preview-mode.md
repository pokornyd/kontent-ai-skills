# Preview mode: the cases the default does not cover

Read when the app already sets a Content-Security-Policy, caches output, runs on more than one instance, needs
real authentication in front of preview, or is not a plain MVC or Razor Pages app. The default path in
`SKILL.md` needs none of this.

## Why it is built this way

- **The gate is a query parameter on the real URL**, not a dedicated `/preview?type=…&slug=…` endpoint. Kontent.ai
  preview URLs are configured per content type, so `https://host/articles/{URLslug}?secret=…` mirrors the app's
  own route and a new routable type needs a new preview URL and no code. An entry endpoint needs a
  type-to-route switch that grows with every type.
- **The secret is stripped by a redirect** so it does not end up in links rendered on the page, in the URL an
  editor copies out of the preview pane, in referrers or in access logs behind the middleware.
- **The cookie value is protected and time-limited.** A plain `preview=true` can be typed into devtools by
  anyone; a protected value cannot be forged, and the time limit bounds a leaked one.
- **A wrong secret is a 404, and an unconfigured secret means preview is off.** Failing closed matters more
  than convenience: a deployment that forgot the secret must not accept any secret.
- **A second named client, never a flag on the default one.** Visitors keep running on the registration they
  had, so preview cannot change what the public site serves.

## The app already sets a Content-Security-Policy

The middleware appends `frame-ancestors https://app.kontent.ai` when the response has no `frame-ancestors`
directive, and leaves an existing one alone. If the app's policy already has `frame-ancestors 'self'` or
`'none'`, extend that directive for preview responses instead of adding a second header: browsers enforce
every CSP header they receive, so the stricter one wins and the iframe stays blocked. A policy with
`script-src` also has to allow `https://cdn.jsdelivr.net` for the Smart Link SDK, or the SDK has to be
self-hosted (download the pinned bundle into `wwwroot/lib/` and point the partial at it).

Kontent.ai also documents `sandbox` directives (`allow-forms`, `allow-popups-to-escape-sandbox`,
`allow-downloads`) for sites that set a sandboxing CSP; only relevant if the app sets one.

## Output caching, response caching, a CDN

The SDK never caches a preview client's responses, and the middleware marks preview responses `no-store`.
Neither stops a cache in front of the app from answering an editor with a visitor's copy, because the cache
decides before the app runs. Make the preview cookie part of the decision:

```csharp
builder.Services.AddOutputCache(options => options.AddBasePolicy(policy => policy
    .With(context => !context.HttpContext.Request.Cookies.ContainsKey(".Kontent.Preview"))));
```

`UseKontentPreview()` must come before `UseOutputCache()`. On a CDN, bypass on the same cookie or serve
preview from a host the CDN does not front.

## More than one instance, or containers that restart

Data Protection keys are per machine by default. A cookie issued by one instance fails `Unprotect` on another,
the middleware treats it as absent, and the editor silently sees published content. Persist and share the key
ring (`PersistKeysToFileSystem` on a shared volume, `PersistKeysToAzureBlobStorage`, `PersistKeysToStackExchangeRedis`,
`PersistKeysToDbContext`) and set a stable application name with `SetApplicationName`. This is the most common
cause of "preview worked yesterday".

## Real authentication in front of preview

The URL secret decides whether preview *displays*; it does not identify anyone. For a reachable host, keep the
secret and add a boundary that runs before `UseKontentPreview()`:

```csharp
app.Use(async (context, next) =>
{
    var entering = context.Request.Query.ContainsKey("secret");
    var inPreview = context.Request.Cookies.ContainsKey(".Kontent.Preview");
    if ((entering || inPreview) && context.User.Identity?.IsAuthenticated != true)
    {
        await context.ChallengeAsync();
        return;
    }

    await next();
});
```

An identity provider's own cookies must also be `SameSite=None; Secure` to survive the iframe, and many
providers refuse to render their sign-in page inside one; editors then sign in once in a normal tab. The
alternatives are a network rule (IP allow-list, Cloudflare Access, Front Door rules) or the separate preview
instance Kontent.ai's documentation recommends (`preview.example.org`, preview key only there, not public).
Which one is the user's decision; describe the options, do not pick one silently.

## Preview URLs in Kontent.ai

*Environment settings > Preview URLs > Preview URLs for content types > Set up preview for a content type*.
Macros: `{URLslug}`, `{Lang}` (language codename), `{Codename}`, `{ItemId}`, `{EnvironmentId}`, `{Collection}`
and `{Space}` (the space's domain, set on the Space domains tab). URLs must be absolute and HTTPS. Activating
live preview is under *Environment settings > General* and needs the Manage environments permission; with
spaces, each space also needs a root item (*Environment settings > Spaces*).

A multilingual app puts `{Lang}` where its routes expect the language. Map the codename to the route's culture
segment in the app if they differ; `{Lang}` is the Kontent.ai codename, not a culture name.

## Not MVC or Razor Pages

Minimal APIs and Blazor Server use the same middleware and registration. A Web API serving a SPA keeps step 2
unchanged, and the API then returns preview content to a caller that holds the cookie; the cookie must be sent
by the browser on API calls (`credentials: "include"`, and CORS with credentials if the origins differ).
