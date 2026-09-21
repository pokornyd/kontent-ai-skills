# KontentMvc

ASP.NET Core MVC (`net10.0`) rendering content from a Kontent.ai environment.

## Kontent.ai

- Content comes from the Kontent.ai Delivery API through `IDeliveryClient` (`Kontent.Ai.Delivery`), registered in `Program.cs` from the `DeliveryOptions` configuration section. `PreviewApiKey` and `SecureAccessApiKey` live in user secrets or environment variables, never in tracked files.
- `KontentMvc.Web/Content/Generated/*.cs` are generated Delivery DTOs. Never edit them; regenerate with `dotnet tool restore && dotnet tool run KontentModelGenerator --environmentId "8a80c10d-3530-016a-4222-675cc7ba3019" --namespace "KontentMvc.Web.Content" --outputdir "KontentMvc.Web/Content/Generated" --nullability strict` (run from the repository root). Extend a type in a partial record next to the generated directory, in the same namespace.
- Generated records never reach a view. Each feature maps `IContentItem<T>` to a view model in `Models/` through a mapper in `Models/Mappers/`, one per content type (`IMapper<IContentItem<T>, TViewModel>`, or `IAsyncMapper` when the mapping awaits, composed by constructor injection for nested types). Delivery queries live in a content service; controllers orchestrate and return `NotFound()` for a missing item.
- View models pass `IRichTextContent` and `IAsset` through unchanged and expose `Guid? ItemId` from `System.Id`. Razor renders them with `<rich-text content="…" />` and `<img-asset asset="…" />` from `Kontent.Ai.AspNetCore`; the tag helpers are registered in `Views/_ViewImports.cshtml`, the resolver with `AddKontentRichText`, the `srcset` width ladder with `ImageTransformationOptions`.
- Queries return `IDeliveryResult<T>`: check `IsSuccess`; `StatusCode == NotFound` is a missing item, any other failure is an error to log and surface, never empty content. Cancellation throws; pass the request token to `ExecuteAsync`.
- The environment has two languages (`default` = English, `es-ES`). A query without a language silently serves the default, so every content-service method takes a language codename explicitly (`ContentLanguages`); do not read it from the ambient culture.
- Rich text embeds content the models cannot show. Before rendering a new type's rich text, inventory what it contains (the Delivery response's `modular_content` and `links` per rich-text element) and register a resolver per type with `AddKontentRichText`, or the page renders a `Missing resolver` HTML comment. Today `article.body_copy` embeds `disclaimer`, which has a resolver in `Program.cs`.
