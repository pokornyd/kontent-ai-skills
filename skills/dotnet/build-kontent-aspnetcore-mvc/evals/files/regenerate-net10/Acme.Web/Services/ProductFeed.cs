using Acme.Web.Content;
using Kontent.Ai.Delivery.Abstractions;

namespace Acme.Web.Services;

/// <summary>
/// Feeds the product carousel on the marketing pages.
/// </summary>
public sealed class ProductFeed(IDeliveryClient client)
{
    public async Task<IReadOnlyList<IContentItem<Coffee>>> GetFeaturedAsync(CancellationToken cancellationToken)
    {
        var result = await client.GetItems<Coffee>()
            .WithElements(Coffee.ProductNameCodename, Coffee.PriceCodename)
            .Limit(6)
            .ExecuteAsync(cancellationToken);

        return result.IsSuccess ? result.Value.Items : [];
    }
}
