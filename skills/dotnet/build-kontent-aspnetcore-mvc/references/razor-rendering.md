# Razor rendering

`Kontent.Ai.AspNetCore` already renders rich text and responsive images; hand-rolled HTML for either loses encoding, resolver support and `srcset` generation. Current reference: https://github.com/kontent-ai/dotnet/blob/main/src/aspnetcore/README.md

## Tag helpers

In `Views/_ViewImports.cshtml`, next to the standard MVC registration:

```razor
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, Kontent.Ai.AspNetCore
```

## Rich text

Register the shared resolver (always in a new app; in an existing app once it renders rich text):

```csharp
using Kontent.Ai.AspNetCore.RichText;

builder.Services.AddKontentRichText();
```

Keep `IRichTextContent` on the view model and let Razor resolve it:

```razor
<rich-text content="@Model.Body" />
```

The resolver output replaces the element; there is no `<rich-text>` wrapper in the page, and a `null` content renders nothing, so no guard is needed. The default resolver encodes text nodes and renders inline images. An embedded component, a linked item or a content-item link has no sensible default, because only the application knows the markup or the URL, so the SDK reports it instead of dropping it silently:

```html
<!-- [Kontent.ai SDK] Missing resolver for embedded content of type "disclaimer" (item: …, codename: …) -->
```

That comment is the intended behaviour and the signal to act on: the page still renders, and the editor's component is missing from it.

### Find out what the rich text contains before wiring it

The generated models cannot tell you. A rich-text property is `RichTextContent?` whatever editors put inside it, so a component used in one article out of fifty is invisible in the code and in any page you happen to open. Ask the content, for every content type the slice renders rich text from, nested types included:

```bash
python3 <skill-dir>/scripts/rich_text_inventory.py <environment-id> <content-type-codename>
```

It lists the types embedded in that type's rich text, the types its content-item links point at, and the items they occur in. Without Python, the same facts are in the Delivery response: each rich-text element carries `modular_content` (codenames whose `system.type` is in the response's top-level `modular_content`) and `links` (each with a `type`). Register a resolver for every type it reports, with the real generated types and the app's real routes:

```csharp
builder.Services.AddKontentRichText(resolvers => resolvers
    .WithContentResolver<Quote>(quote =>
        $"<blockquote>{HtmlEncoder.Default.Encode(quote.Elements.Text ?? string.Empty)}</blockquote>")
    .WithContentItemLinkResolver("article", async (link, resolveChildren) =>
    {
        var slug = HtmlEncoder.Default.Encode(link.Metadata?.UrlSlug ?? link.ItemId.ToString());
        var inner = await resolveChildren(link.Children);   // already HTML; encoding it again would escape the markup
        return $"<a href=\"/articles/{slug}\">{inner}</a>";
    }));
```

A type that has no page of its own still needs a link resolver if editors link to it; render the link text without an anchor rather than inventing a route. When an unhandled type should fail loudly instead of leaving a comment, which suits a development environment or a test, add `.ThrowOnMissingResolver()` to the builder.

Then prove it on the pages the inventory named, not on whichever item is first: request each one and search the HTML for `Missing resolver`. A smoke test of an item with plain rich text passes whether or not the resolvers exist.

A link resolver renders the **whole** anchor, not its opening tag: there is no `link.Text`, so the authored link text and its inline formatting come from `await resolveChildren(link.Children)`, and returning only `<a href=...>` produces an unclosed, empty anchor. A resolver's return value is inserted unescaped, so encode every editor-controlled value that lands in handcrafted HTML, the slug included (`HtmlEncoder` is in `System.Text.Encodings.Web`), and leave the output of `resolveChildren` alone. When link targets need routing services, use the overload that exposes `IServiceProvider` and resolve only singleton-safe dependencies, because the resolver is built once per application, not per request. For partial views or an explicit cancellation token there is `@await Model.Body.ToHtmlContentAsync(Resolver, ViewContext.HttpContext.RequestAborted)`, but pass the resolver explicitly (`@inject IHtmlResolver Resolver`): an extension method cannot reach the container, so without one it uses the SDK's built-in defaults, not what `AddKontentRichText` registered. Only the tag helper picks that up on its own.

## Assets

Configure responsive widths (always in a new app; in an existing app once it renders assets):

```json
{
  "ImageTransformationOptions": {
    "ResponsiveWidths": [ 320, 480, 768, 1024, 1440 ]
  }
}
```

```csharp
using Kontent.Ai.AspNetCore.ImageTransformation;

builder.Services.Configure<ImageTransformationOptions>(
    builder.Configuration.GetSection(nameof(ImageTransformationOptions)));
```

Render an `IAsset` with meaningful alternative text. An asset with no description arrives as `null` from some environments and as `""` from others, so a `??` fallback fires in only half the cases; test for blank instead:

```razor
<img-asset asset="@Model.HeroImage"
           title="@(string.IsNullOrWhiteSpace(Model.HeroImage?.Description) ? Model.Title : Model.HeroImage.Description)"
           default-width="768" />
```

In practice that check belongs on the view model as a computed property, so every view gets the same fallback.

A `null` asset renders nothing, so `asset="@Model.Image"` needs no guard. `<media-condition>` children map viewport ranges to image widths for the `sizes` attribute; `srcset` candidates are capped at the asset's own width because the CDN never upscales. Fixed `width`/`height` attributes request one transformed size and intentionally drop `srcset`/`sizes`; use them only when the layout needs a single size.

## Out of scope by default

Preview switching, Smart Link, webhook cache invalidation, multi-space routing and custom rich-text component templates are each a separate request. Add one only when asked, from the current SDK documentation and the target app's architecture.
