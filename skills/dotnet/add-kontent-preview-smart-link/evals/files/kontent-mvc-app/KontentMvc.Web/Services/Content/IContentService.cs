using Kontent.Ai.Delivery.Abstractions;
using KontentMvc.Web.Content;
using KontentMvc.Web.Models;

namespace KontentMvc.Web.Services.Content;

/// <summary>
/// The one seam in front of <see cref="IDeliveryClient"/>: queries, projection, paging and result
/// handling live here, so no Delivery request or response envelope reaches a controller.
/// The language is an explicit parameter because the environment has more than one; reading it from
/// the ambient culture would tie content retrieval to a web request.
/// </summary>
public interface IContentService
{
    Task<PagedResult<IContentItem<Article>>> GetArticlesAsync(
        string languageCodename, int skip, int limit, CancellationToken cancellationToken);

    Task<IContentItem<Article>?> GetArticleBySlugAsync(
        string slug, string languageCodename, CancellationToken cancellationToken);
}
