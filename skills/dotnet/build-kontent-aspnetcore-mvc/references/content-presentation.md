# Content-to-presentation boundary

Read only when implementing a working MVC content slice. A plumbing-only scaffold gets none of these layers.

## Boundary

Generated records describe Delivery element payloads; the SDK returns them inside `IContentItem<T>` together with `System` metadata. Keep query building and result handling out of controllers, and keep generated records out of views when a view-specific shape is clearer. One vertical slice per user-visible feature:

```text
Features/<Feature>/
  <Feature>ContentService.cs
  <Feature>ViewModel.cs
Controllers/<Feature>Controller.cs
Views/<Feature>/...
```

Match an existing project convention when one exists. A generic repository, unit of work or universal mapper adds indirection with nothing behind it; `IDeliveryClient` already is the repository.

## Content service

```csharp
public sealed class ArticleContentService(IDeliveryClient client, ILogger<ArticleContentService> logger)
{
    public async Task<ArticleDetailViewModel?> GetAsync(string codename, CancellationToken cancellationToken)
    {
        var result = await client.GetItem<Article>(codename)
            .Depth(1)
            .ExecuteAsync(cancellationToken);

        if (result.IsSuccess)
        {
            var item = result.Value;
            return new ArticleDetailViewModel(
                Title: item.Elements.Title ?? item.System.Name,
                Body: item.Elements.BodyCopy,
                HeroImage: item.Elements.TeaserImage?.FirstOrDefault());
        }

        if (result.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        logger.LogError("Delivery request for {Codename} failed with {StatusCode}: {Message}",
            codename, result.StatusCode, result.Error?.Message);
        throw new DeliveryRequestException(
            result.Error?.Message ?? "Delivery request failed.", result.StatusCode, result.Error, result.RequestUrl);
    }
}
```

The shape that matters: the request token is passed through, a missing item becomes `null`, and every other failure is logged with the SDK's own diagnostics and surfaced through the app's error policy instead of being swallowed as empty content. Throwing is the placeholder; an app with a result type or an error view uses that.

Listings use `client.GetItems<Article>()` with `WithElements(Article.TitleCodename, ...)`, `OrderByElement`, `Skip`/`Limit` and `WithTotalCount()` for paging. Reserve `GetItemsFeed` for bulk traversal such as index building. `Depth` fetches linked items; a large value stands in for knowing which links the view renders, so keep it at what the view needs.

## Controller

```csharp
public sealed class ArticlesController(ArticleContentService articles) : Controller
{
    [HttpGet("articles/{codename}")]
    public async Task<IActionResult> Detail(string codename, CancellationToken cancellationToken)
        => await articles.GetAsync(codename, cancellationToken) is { } model ? View(model) : NotFound();
}
```

Validate route input, call the service, convert `null` to `NotFound()`, pass the view model. Delivery envelopes, linked-item mapping and error branching stay in the service.

## View models

Select what the view renders. The MVC-specific layer keeps:

- `IRichTextContent` for `<rich-text>`;
- `IAsset` for `<img-asset>`;
- the item ID and element codenames when Smart Link or live editing is requested;
- already-mapped nested view models for linked items the view renders.

Leave rich text structured; the tag helper is the presentation boundary and picks up the configured `IHtmlResolver`. A view model that mechanically mirrors every generated property has no reason to exist; neither does passing the raw generated record just because it is there.

## Selecting a first content type

Use the type the user named. Otherwise inspect the generated models and representative content, recommend a type with obvious display fields, and ask before assuming routes or information architecture when more than one candidate is plausible. A listing plus detail pair is a good first slice only when the model exposes suitable title, slug, summary, rich-text and asset elements; use the generated codename constants rather than invented field names. Global navigation, URL hierarchy and page composition are not derivable from content-type names.
