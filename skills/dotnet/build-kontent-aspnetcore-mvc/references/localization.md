# More than one language

Read this when the environment has more than one language. A single-language environment needs none
of it.

This file holds what the Delivery API does with languages, because every one of these behaviours is
silent and none of them is guessable. It does not cover building a multilingual site; see the last
section.

## What the environment really has

```bash
curl -s https://deliver.kontent.ai/<environment-id>/languages
```

Take language codenames from that response, never from a culture name. They are whatever the project
calls them, and the default language is often literally `default` rather than `en-US`.

Then find out how much content each language has, which a plain per-language request will not tell
you: with fallback on, `?language=es-ES` returns every item, translated or not, so an untranslated
environment looks fully translated. Count the real variants by filtering on the language the item
came back in:

```bash
curl -s "https://deliver.kontent.ai/<environment-id>/items?language=es-ES&system.language=es-ES&elements=_none&limit=1&includeTotalCount=true"
```

`pagination.total_count` is the number of items that exist in that language. Zero means every page in
that language would be fallback content. Say it in the report in those words: "the language is
configured and nothing is translated yet" is a different message from "Spanish content exists".

## Every query names its language

```csharp
public async Task<PagedResult<IContentItem<Article>>> GetArticlesAsync(
    string languageCodename, int skip, int take, CancellationToken cancellationToken)
{
    var result = await client.GetItems<Article>()
        .WithLanguage(languageCodename, LanguageFallbackMode.Enabled)
        .WithElements(Article.TitleCodename, Article.UrlSlugCodename)
        .Skip(skip).Limit(take).WithTotalCount()
        .ExecuteAsync(cancellationToken);
    ...
}
```

`DeliveryOptions` has no default-language property, so nothing can be configured globally: a query
without a language serves the environment's default, which is why a forgotten parameter looks like
success. In an environment with more than one language this is not optional, whatever the user asked
for. Take the codename as a parameter of every content-service method rather than reading the current
culture inside the service, so a background job or a sitemap builder can ask for either language.
When the request says nothing about languages, the controller passes the default language, and the
report says a second one exists. The listing and feed queries take the fallback mode;
`GetItem<T>(codename)` takes the codename alone.

## A codename that does not exist is not an error

The API answers 200 with zero items, so the page renders empty and nothing is logged. Keep the map
from culture or URL segment to codename in one place. Wherever a language arrives from a request (a
route segment, a query parameter, an API endpoint), validate it before it reaches a query, against
that map or against `client.GetLanguages()` fetched once and cached, and answer an unknown one with
404 or 400. Passing it through turns a mistyped or stale link into an empty page with a 200, which no
monitor will flag.

## Fallback is visible only if you look

With fallback enabled, an item with no variant in the requested language comes back in the default
language rather than not at all. The only trace is on the item:

```csharp
var isFallback = !string.Equals(item.System.Language, requested, StringComparison.OrdinalIgnoreCase);
```

Carry that onto the view model so a page can say so. `LanguageFallbackMode.Disabled` makes a missing
translation a missing item instead, which is a product decision to put to the user, not to assume.

## Slugs are translated

A URL slug is an element, so it has a value per language: the same article can be `/articles/on-roasts`
in one language and `/articles/en-asados` in another. Anything that links across languages therefore
has to look the same item up by codename in the target language, which costs a Delivery request per
language; rewriting the current path will start returning 404 the day an editor translates a slug.

## What this skill does not cover

Culture-prefixed routes, translated interface strings, request localization and a language switcher
are a feature built on top of the starter, and mostly an ASP.NET Core matter rather than a Kontent.ai
one. This skill gives no guidance for them. Do not build them because the environment happens to have
two languages. When the user asks for a multilingual site, deliver the starter with the behaviours
above in place, build the feature from your own knowledge of ASP.NET Core localization if the request
calls for it, and say in the report that this part goes beyond what the skill covers, so the user
knows which decisions were the skill's and which were yours.
