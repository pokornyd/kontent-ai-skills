# More than one language

Read this when `GetLanguages()` returns more than one language, or the user asks for a site in more
than one. A single-language environment needs none of it.

Check first, before deciding anything else: `curl -s https://deliver.kontent.ai/<environment-id>/languages`.
Then find out how much content each language really has, which a plain per-language request will not
tell you: with fallback on, `?language=es-ES` returns every item, translated or not, so an untranslated
environment looks fully translated. Count the real variants by filtering on the language the item came
back in:

```bash
curl -s "https://deliver.kontent.ai/<environment-id>/items?language=es-ES&system.language=es-ES&elements=_none&limit=1&includeTotalCount=true"
```

`pagination.total_count` there is the number of items that exist in that language. Zero means every
page in that language will be fallback content, which belongs in the report in those words: "the
language is configured and nothing is translated yet" is a different message from "Spanish content
exists", and content that exists in only one language is what makes the decisions below real rather
than theoretical.

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

A wrong codename is just as quiet. Language codenames are whatever the project calls them, and the
default language is often literally `default` rather than `en-US`. Asking for a codename that does
not exist is not an error: the API answers 200 with zero items, so the English site renders empty
and nothing is logged. Take the codenames from `/languages`, never from the culture name, and keep
the culture-to-codename map in one place.

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

## How much to build

Two different things get called "multilingual", and only one of them is always in scope.

- **An explicit language on every query** is not optional in an environment with more than one
  language, whatever the user asked for, because the alternative is the silent default above. When
  the request says nothing about languages, take the language as a service parameter, pass the
  default language from the controller, and say in the report that a second language exists.
- **Culture-prefixed routes, translated interface strings and a language switcher** are a feature.
  Build them when the user asks for a site in more than one language, not because the environment
  happens to have two. An unrequested switcher is scope the user has to review, and it is not free:
  see the cost below.

## Slugs are translated

A URL slug is an element, so it has a value per language. The same article is `/en-us/articles/on-roasts`
and `/es-es/articles/en-asados`. A language switcher therefore cannot rewrite the current path: it has
to look up the counterpart slug for the same content item, by codename, in the target language. On a
listing page there is no item to resolve, so the switcher points at the listing route.

Decide what a switcher does when the target language has no variant. Linking to the fallback is
reasonable; linking to a slug that does not exist is not.

That lookup is a Delivery request per additional language on every detail page render. Keep it in
the content service as its own method (`GetSlugAsync(itemCodename, languageCodename, …)`, projected
to the slug element with `WithElements`) so the controller only assembles links, and name the cost
in the report: with two languages it doubles the requests behind a detail page, and
`delivery.UseMemoryCache()` from `Kontent.Ai.Delivery.Caching` is the follow-up that removes it.
Caching is still the user's decision, so recommend it rather than adding it.

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
