using Kontent.Ai.Delivery.Abstractions;
using KontentMvc.Web.Content;

namespace KontentMvc.Web.Models.Mappers;

/// <summary>
/// Maps the generated <see cref="Article"/> Delivery record onto the view model the Razor views
/// render. Takes <see cref="IContentItem{T}"/> rather than the bare record so System.Id is available.
/// Nothing here awaits, so this is the synchronous contract.
/// </summary>
public sealed class ArticleMapper(PersonMapper personMapper)
    : IMapper<IContentItem<Article>, ArticleViewModel>
{
    public ArticleViewModel Map(IContentItem<Article> source)
    {
        var elements = source.Elements;
        var author = elements.Author?.OfType<IContentItem<Person>>().FirstOrDefault();

        return new ArticleViewModel
        {
            ItemId = source.System.Id,
            Title = string.IsNullOrWhiteSpace(elements.Title) ? source.System.Name : elements.Title,
            Slug = elements.UrlSlug ?? string.Empty,
            Summary = elements.Introduction,
            PublishedOn = elements.PublishDate?.Value,
            Body = elements.BodyCopy,               // null when the listing query projected it away
            Image = elements.Image?.FirstOrDefault(),
            Author = author is null ? null : personMapper.Map(author),
        };
    }
}
