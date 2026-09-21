using Kontent.Ai.Delivery.Abstractions;

namespace KontentMvc.Web.Models;

/// <summary>The author of an article, as the detail page shows them.</summary>
public sealed record PersonViewModel
{
    public Guid? ItemId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? JobTitle { get; init; }

    public IAsset? Photo { get; init; }

    public string PhotoAltText =>
        string.IsNullOrWhiteSpace(Photo?.Description) ? Name : Photo.Description;
}
