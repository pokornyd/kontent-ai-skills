using Kontent.Ai.Delivery.Abstractions;
using KontentMvc.Web.Content;
using KontentMvc.Web.Models;

namespace KontentMvc.Web.Services.Content;

public sealed class ContentService(IDeliveryClient client, ILogger<ContentService> logger) : IContentService
{
    public async Task<PagedResult<IContentItem<Article>>> GetArticlesAsync(
        string languageCodename, int skip, int limit, CancellationToken cancellationToken)
    {
        // The typed query adds system.type=article from [ContentTypeCodename]; WithElements keeps the
        // payload to what a card renders, so the body copy arrives as null on the listing.
        var result = await client.GetItems<Article>()
            .WithLanguage(languageCodename, LanguageFallbackMode.Enabled)
            .WithElements(
                Article.TitleCodename,
                Article.UrlSlugCodename,
                Article.IntroductionCodename,
                Article.ImageCodename,
                Article.PublishDateCodename)
            .OrderByElement(Article.PublishDateCodename, OrderingMode.Descending)
            .Skip(skip)
            .Limit(limit)
            .WithTotalCount()
            .ExecuteAsync(cancellationToken);

        EnsureSuccess(result, $"the article listing in language '{languageCodename}'");

        return new PagedResult<IContentItem<Article>>(
            [.. result.Value.Items], result.Value.Pagination.TotalCount, skip, limit);
    }

    public async Task<IContentItem<Article>?> GetArticleBySlugAsync(
        string slug, string languageCodename, CancellationToken cancellationToken)
    {
        var result = await client.GetItems<Article>()
            .WithLanguage(languageCodename, LanguageFallbackMode.Enabled)
            .Where(f => f.Element(Article.UrlSlugCodename).IsEqualTo(slug))
            .Depth(1)               // the author (a linked Person item) is what the detail view renders
            .Limit(1)
            .ExecuteAsync(cancellationToken);

        EnsureSuccess(result, $"article '{slug}' in language '{languageCodename}'");

        // An unknown slug is an empty listing, not a failure; the controller turns it into a 404.
        return result.Value.Items.FirstOrDefault();
    }

    /// <summary>
    /// A Delivery failure is a result, not an exception (only cancellation throws). Empty content
    /// would hide the failure, so it is logged with the SDK's diagnostics and surfaced as the SDK's
    /// own exception type, which the app's error policy handles.
    /// </summary>
    private void EnsureSuccess<T>(IDeliveryResult<T> result, string what)
    {
        if (result.IsSuccess)
        {
            return;
        }

        logger.LogError(result.Error?.Exception,
            "Delivery request for {What} failed with {StatusCode}: {Message} (request {RequestId})",
            what, result.StatusCode, result.Error?.Message, result.Error?.RequestId);

        throw new DeliveryRequestException(
            $"Delivery request for {what} failed.", result.StatusCode, result.Error, result.RequestUrl);
    }
}
