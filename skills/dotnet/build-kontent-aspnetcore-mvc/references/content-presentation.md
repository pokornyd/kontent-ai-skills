# Content-to-presentation boundary

Read only when implementing a working MVC content slice. A plumbing-only scaffold gets none of these layers; the conventions file already tells the next session what they will look like.

The shape below is the one the official [Kontent.ai MVC sample app](https://github.com/kontent-ai/sample-app-net-mvc) uses. Follow it unless the target project already has an equivalent convention, in which case match the project.

## Layout

```text
Services/Content/IContentService.cs, ContentService.cs   queries, projection, paging, result handling
Models/PagedResult.cs                                     app-owned page type, so no Delivery envelope escapes
Models/<Type>ViewModel.cs                                 what a view renders
Models/Mappers/IMapper.cs, IAsyncMapper.cs               the two mapper contracts
Models/Mappers/<Type>Mapper.cs                            one mapper per content type
Controllers/<Feature>Controller.cs                        orchestration only
Views/<Feature>/...
```

Generated records describe Delivery element payloads; the SDK returns them inside `IContentItem<T>` together with `System` metadata. They never reach a view. A generic repository or unit of work adds indirection with nothing behind it; the content service is the one seam, and `IDeliveryClient` sits behind it.

## Mappers

```csharp
public interface IMapper<in TSource, TDestination>
{
    TDestination Map(TSource source);
}

public interface IAsyncMapper<in TSource, TDestination>
{
    Task<TDestination> MapAsync(TSource source);
}
```

One mapper per content type, taking `IContentItem<T>` rather than the bare `T` so the item ID is available for Smart Link and cache tags, registered scoped in DI, and composing the mappers of linked types through constructor injection:

```csharp
public sealed class ArticleMapper(PersonMapper personMapper) : IAsyncMapper<IContentItem<Article>, ArticleViewModel>
{
    public async Task<ArticleViewModel> MapAsync(IContentItem<Article> source)
    {
        var e = source.Elements;
        var author = e.Author?.OfType<IContentItem<Person>>().FirstOrDefault();

        return new ArticleViewModel
        {
            ItemId = source.System.Id,
            Title = e.Title ?? source.System.Name,
            Slug = e.UrlSlug ?? string.Empty,
            Summary = e.Introduction,
            Body = e.BodyCopy,                    // null when a listing query projected it away
            HeroImage = e.Image?.FirstOrDefault(),
            Author = author is null ? null : await personMapper.MapAsync(author),
        };
    }
}
```

Use a synchronous `IMapper` when nothing in the mapping awaits; do not make a pure transformation asynchronous for symmetry.

## View models

```csharp
public sealed record ArticleViewModel
{
    public Guid? ItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public IRichTextContent? Body { get; init; }
    public IAsset? HeroImage { get; init; }
    public PersonViewModel? Author { get; init; }
}
```

Select what the view renders. `IRichTextContent` and `IAsset` pass through untouched because the tag helpers are the presentation boundary: `<rich-text>` picks up the configured `IHtmlResolver`, `<img-asset>` builds `srcset` from the configured widths. `ItemId` and the generated element codenames are what Smart Link needs later, at no cost now. A view model that mirrors every generated property has no reason to exist; neither does passing the generated record because it is there.

## Content service

```csharp
public sealed class ContentService(IDeliveryClient client, ILogger<ContentService> logger) : IContentService
{
    public async Task<IContentItem<Article>?> GetArticleBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var result = await client.GetItems<Article>()
            .Where(f => f.Element(Article.UrlSlugCodename).IsEqualTo(slug))
            .Depth(1)
            .Limit(1)
            .ExecuteAsync(cancellationToken);

        if (result.IsSuccess)
        {
            return result.Value.Items.FirstOrDefault();
        }

        logger.LogError(result.Error?.Exception, "Delivery request for article '{Slug}' failed with {StatusCode}: {Message}",
            slug, result.StatusCode, result.Error?.Message);
        throw new DeliveryRequestException($"Delivery request for article '{slug}' failed.", result.StatusCode, result.Error, result.RequestUrl);
    }

    public async Task<PagedResult<IContentItem<Article>>> GetArticlesAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var result = await client.GetItems<Article>()
            .WithElements(Article.TitleCodename, Article.UrlSlugCodename, Article.IntroductionCodename, Article.ImageCodename)
            .OrderByElement(Article.PublishDateCodename, OrderingMode.Descending)
            .Skip(skip).Limit(take).WithTotalCount()
            .ExecuteAsync(cancellationToken);

        EnsureSuccess(result, "the article listing");

        return new PagedResult<IContentItem<Article>>(
            result.Value.Items, result.Value.Pagination.TotalCount, skip, take);
    }
}
```

```csharp
// Models/PagedResult.cs - app-owned, so no Delivery envelope escapes the service.
public record PagedResult<T>(IReadOnlyList<T> Items, int? TotalCount, int Skip, int Limit)
{
    public int PageNumber => Limit > 0 ? (Skip / Limit) + 1 : 1;
    public bool HasPrevious => Skip > 0;
    public bool HasNext => TotalCount is { } total ? Skip + Items.Count < total : Items.Count == Limit;
}
```

`TotalCount` stays nullable rather than defaulting to the number of items returned: the Delivery API only sends a total when the query asked for one with `WithTotalCount()`, and substituting the page size would render "showing 1-12 of 12" on the first page of a hundred. `HasNext` falls back to a full page instead. Name it `PagedResult`, not `ContentPage`: `Page` is a common content type codename, so a generated `Page` record often sits in the same solution.

Once a second listing appears, project the paging metadata onto a small item-agnostic view model so one `_Pager.cshtml` partial serves every listing. One listing does not need that yet.

Draw the boundary precisely, because "keep Delivery out of the controller" is not the same as "keep every Delivery type out". `IDeliveryResult`, `IDeliveryItemListingResponse` and `Pagination` stop at the service, which is why the listing returns an app-owned `PagedResult<T>`. `IContentItem<T>` deliberately does cross, because it is the mapper's declared source type: `System.Id` lives there and Smart Link needs it later. `IRichTextContent` and `IAsset` cross too, all the way to Razor, because the tag helpers consume them. The rule is that nothing describing a *request* escapes the service, while the types describing *content* travel to the layer that renders them.

The rest of the shape: the request token is passed through, a missing item becomes `null` (an empty listing for a slug lookup, `StatusCode == NotFound` for `GetItem<T>(codename)`), and every other failure is logged with the SDK's diagnostics and surfaced through the app's error policy instead of being swallowed as empty content. `DeliveryRequestException` is the SDK's own type for exactly this, sealed, carrying the status code, the API's `IError` and the request ID; an app with a result type or an error view uses that instead. Listings project with `WithElements` to the fields the card needs and page with `Skip`/`Limit`/`WithTotalCount()` (`Pagination.TotalCount`, `HasNextPage`); the detail query keeps the full element set. `Depth` fetches linked items; keep it at what the view renders. Reserve `GetItemsFeed` for bulk traversal.

## Controller

```csharp
public sealed class ArticlesController(IContentService content, ArticleMapper articleMapper) : Controller
{
    [Route("[controller]/{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var article = await content.GetArticleBySlugAsync(slug, cancellationToken);
        return article is null ? NotFound() : View(await articleMapper.MapAsync(article));
    }
}
```

Validate route input, call the service, map, return `NotFound()` for `null`. Delivery envelopes, linked-item mapping and error branching stay out of controllers.

## Selecting a first content type

Use the type the user named. Otherwise inspect the generated models and representative content, recommend a type with obvious display fields, and ask before assuming routes or information architecture when more than one candidate is plausible. A listing plus detail pair is a good first slice only when the model exposes suitable title, slug, summary, rich-text and asset elements; use the generated codename constants rather than invented field names. Global navigation, URL hierarchy and page composition are not derivable from content-type names.
