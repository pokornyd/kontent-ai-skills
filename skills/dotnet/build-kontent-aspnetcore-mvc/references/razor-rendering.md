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
@if (Model.Body is not null)
{
    <rich-text content="@Model.Body" />
}
```

The resolver output replaces the element; there is no `<rich-text>` wrapper in the page. The default resolver encodes text nodes and renders inline images. Embedded content and content-item links render as HTML comments until a resolver is configured, so when the generated models contain them, configure the builder with the real types and routes:

```csharp
builder.Services.AddKontentRichText(resolvers => resolvers
    .WithContentResolver<Quote>(quote =>
        $"<blockquote>{HtmlEncoder.Default.Encode(quote.Elements.Text ?? string.Empty)}</blockquote>")
    .WithContentItemLinkResolver("article", (link, _) =>
        ValueTask.FromResult($"<a href=\"/articles/{link.ItemId}\">")));
```

Encode every editor-controlled value that lands in handcrafted HTML. When link targets need routing services, use the overload that exposes `IServiceProvider` and resolve only singleton-safe dependencies, because the resolver is built once per application, not per request. For partial views or an explicit cancellation token, `@await Model.Body.ToHtmlContentAsync(resolver, ViewContext.HttpContext.RequestAborted)` is the equivalent extension method.

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

Render an `IAsset` with meaningful alternative text:

```razor
<img-asset asset="@Model.HeroImage"
           title="@(Model.HeroImage.Description ?? Model.Title)"
           default-width="768" />
```

`<media-condition>` children map viewport ranges to image widths for the `sizes` attribute. Fixed `width`/`height` attributes request one transformed size and intentionally drop `srcset`/`sizes`; use them only when the layout needs a single size.

## Out of scope by default

Preview switching, Smart Link, webhook cache invalidation, multi-space routing and custom rich-text component templates are each a separate request. Add one only when asked, from the current SDK documentation and the target app's architecture.
