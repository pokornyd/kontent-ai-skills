using Kontent.Ai.Delivery.Abstractions;

namespace KontentMvc.Web.Models;

/// <summary>
/// What the article listing and the article detail page render. Generated Delivery records never
/// reach a view; <see cref="IRichTextContent"/> and <see cref="IAsset"/> do, because the
/// Kontent.Ai.AspNetCore tag helpers are the presentation boundary for them.
/// </summary>
public sealed record ArticleViewModel
{
    /// <summary>System.Id of the content item; what Smart Link and cache tags need later.</summary>
    public Guid? ItemId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Summary { get; init; }

    public DateTime? PublishedOn { get; init; }

    /// <summary>Null on the listing, where the query projects the body away.</summary>
    public IRichTextContent? Body { get; init; }

    public IAsset? Image { get; init; }

    public PersonViewModel? Author { get; init; }

    /// <summary>
    /// Alternative text for <c>&lt;img-asset&gt;</c>. Environments send an unfilled asset description
    /// as an empty string as often as null, so a ?? fallback would render alt="" half the time.
    /// </summary>
    public string ImageAltText =>
        string.IsNullOrWhiteSpace(Image?.Description) ? Title : Image.Description;
}
