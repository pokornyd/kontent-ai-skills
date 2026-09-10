# More than one language

Read this when `GetLanguages()` returns more than one language, or the user asks for a site in more
than one. A single-language environment needs none of it.

Check first, before deciding anything else: `curl -s https://deliver.kontent.ai/<environment-id>/languages`.
Item counts per language are worth a look too, because they differ, and content that exists in only
one language is what makes the decisions below real rather than theoretical.

## Language is a query parameter, not ambient state

Every content method takes the language codename explicitly. Reading it from the current thread's
culture inside the content service couples content retrieval to a web request, which breaks the
moment a background job, a sitemap builder or a language switcher needs the other language.

```csharp
public async Task<PagedResult<IContentItem<Article>>> GetArticlesAsync(
    string languageCodename, int skip, int take, CancellationToken cancellationToken)
{
    var result = await client.GetItems<Article>()
        .WithLanguage(languageCodename, LanguageFallbackMode.Enabled)
        .WithElements(Article.TitleCodename, Article.SlugCodename)
        .Skip(skip).Limit(take).WithTotalCount()
        .ExecuteAsync(cancellationToken);
    ...
}
```

The listing and feed queries take the fallback mode; `GetItem<T>(codename)` takes the codename alone.
`DeliveryOptions` has no default-language property, so there is nothing to configure globally: an
unspecified language means the environment's default, which is why a forgotten parameter looks like
success and silently serves the wrong language.

## Fallback is a state the page should admit to

With fallback enabled, an item with no variant in the requested language comes back in the default
language rather than not at all. That is usually what you want on a listing, because the alternative
is a hole. It is not something to hide: compare what the API returned against what you asked for.

```csharp
var isFallback = !string.Equals(item.System.Language, requested, StringComparison.OrdinalIgnoreCase);
```

Carry that onto the view model and say so in the interface, in the reader's language. `Disabled` is
the right mode when a missing translation should be a 404 instead, which is a product decision worth
asking about rather than assuming.

## Slugs are translated

A URL slug is an element, so it has a value per language. The same article is `/en-us/articles/on-roasts`
and `/es-es/articles/en-asados`. A language switcher therefore cannot rewrite the current path: it has
to look up the counterpart slug for the same content item, by codename, in the target language. On a
listing page there is no item to resolve, so the switcher points at the listing route.

Decide what a switcher does when the target language has no variant. Linking to the fallback is
reasonable; linking to a slug that does not exist is not.

## ASP.NET Core traps that only appear at runtime

These are framework behaviours rather than Kontent.ai ones, and all three compile cleanly.

- **A culture route value is not carried into a link for a different action.** Anything generated
  across actions renders `href=""`, and a route whose culture segment is required returns 500 when it
  is missing. Pass the culture explicitly in every link and redirect rather than relying on ambient
  route values.
- **A generic `{controller}/{action}` route wins link generation over specific routes**, turning
  readable URLs into query strings. Register an explicit conventional route per action instead of
  keeping the template's catch-all.
- **A shared resource marker class must not live in the folder named by `ResourcesPath`.** The
  embedded resource name is derived from the type's namespace, and a class sitting beside its own
  `.resx` files ends up looking for them one level deeper, so every string renders as its key. Put
  the marker class at the project root.

Verify all three by requesting pages, not by building. Sweep every route in both languages, including
one item that exists in only one, and check for empty `href` attributes and for interface strings that
came back as their keys.
