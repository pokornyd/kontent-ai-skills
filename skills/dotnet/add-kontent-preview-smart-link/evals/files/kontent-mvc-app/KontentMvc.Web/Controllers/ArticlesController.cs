using Microsoft.AspNetCore.Mvc;
using KontentMvc.Web.Content;
using KontentMvc.Web.Models.Mappers;
using KontentMvc.Web.Services.Content;

namespace KontentMvc.Web.Controllers;

/// <summary>
/// Orchestration only: call the content service, map, choose a result. Delivery queries, envelopes
/// and error branching stay in <see cref="IContentService"/>.
/// </summary>
[Route("articles")]
public sealed class ArticlesController(IContentService content, ArticleMapper articleMapper) : Controller
{
    private const int PageSize = 6;

    [HttpGet("", Name = "ArticleListing")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return RedirectToRoute("ArticleListing");
        }

        var articles = await content.GetArticlesAsync(
            ContentLanguages.Default, (page - 1) * PageSize, PageSize, cancellationToken);

        // An empty first page means there is no article yet; an empty later page is a page that
        // does not exist, which is a 404 rather than an "no articles published" message.
        if (articles.Items.Count == 0 && page > 1)
        {
            return NotFound();
        }

        return View(articles.Map(articleMapper.Map));
    }

    [HttpGet("{slug}", Name = "ArticleDetail")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var article = await content.GetArticleBySlugAsync(slug, ContentLanguages.Default, cancellationToken);

        return article is null ? NotFound() : View(articleMapper.Map(article));
    }
}
