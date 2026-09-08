# Generate Delivery models

Turn the environment's content types into strongly typed Delivery records with `Kontent.Ai.ModelGenerator`. The generator knows the content model, not the site: it never implies routes, pages or navigation.

## Use a local tool

A repository-local manifest makes generation reproducible for the next person:

```bash
dotnet new tool-manifest      # skip when .config/dotnet-tools.json already exists
dotnet tool install Kontent.Ai.ModelGenerator
```

In an existing repository restore the manifest and keep its pinned version unless the user asked for an upgrade. The generator's major follows the Delivery major it emits models for, so check its README when the app is not on the current Delivery line.

Run from a directory where the manifest is in scope:

```bash
dotnet tool run KontentModelGenerator \
  --environmentId "<environment-id>" \
  --namespace "<RootNamespace>.Content" \
  --outputdir "<generated-directory>" \
  --nullability strict
```

Delivery mode is the default; `--management` emits Management SDK models, which an MVC site does not need. `--outputdir` is resolved against the working directory, so pass an absolute path or one relative to where the command runs.

### Nullability

`strict` (the default) makes every element property nullable, so a property projected away with `WithElements` is `null` and distinguishable from a fetched empty value. `semantic` generates non-nullable text, rich-text and collection properties with empty defaults, which reads better in Razor but makes "not fetched" and "fetched empty" identical. Prefer `strict`; switch to `semantic` only when the user accepts that trade-off.

## Protected environments

The generator reads its command line and an optional `appSettings.json` in its working directory, nothing else: not user secrets, not environment variables. The key therefore has to be on the command line, referenced by variable so the value never appears in the command text, the transcript or shell history:

```bash
test -n "$KONTENT_SECURE_ACCESS_KEY" || { echo "export KONTENT_SECURE_ACCESS_KEY first"; exit 1; }
dotnet tool run KontentModelGenerator \
  --environmentId "<environment-id>" \
  --namespace "<RootNamespace>.Content" \
  --outputdir "<generated-directory>" \
  --nullability strict \
  --DeliveryOptions:UseSecureAccess true \
  --DeliveryOptions:SecureAccessApiKey "$KONTENT_SECURE_ACCESS_KEY"
```

Preview content works the same way with `--DeliveryOptions:UsePreviewApi true --DeliveryOptions:PreviewApiKey "$KONTENT_PREVIEW_API_KEY"`, and only when the user explicitly needs an unpublished content model.

User secrets are for the running app, not for the generator: `dotnet user-secrets list` prints values into the transcript, so never use it to recover a key. When only user secrets hold it, ask the user to export `KONTENT_SECURE_ACCESS_KEY` and continue from there. Do not write the key into a temporary `appSettings.json` outside the repository either: `dotnet tool run` finds the manifest by walking up from the working directory, so the local tool is not available there.

## Generated-code boundary

Generate into a dedicated directory such as `Content/Generated`. Each file carries an auto-generated header and a `[ContentTypeCodename]` partial record with `<Element>Codename` constants and `init`-only properties. Keep the namespace one level above the directory (`Acme.Web.Content` for `Content/Generated/`) so hand-written partials next to it share the namespace without living in the generated folder.

- Never hand-edit generated files; regeneration overwrites them.
- Put custom members in a separate partial record in the same namespace outside the generated directory (`Content/Article.cs` extending `Content/Generated/Article.cs`).
- The project compiling these files must reference `Kontent.Ai.Delivery.SourceGeneration`; keep all attributed models in that one compilation unless the solution already registers a custom `ITypeProvider`.
- Do not write or maintain a manual type-provider map; the source generator produces one.

## Regeneration in an existing project

Generate into a temporary directory first and diff it against the generated directory. When that directory contains only generated files, replace it wholesale so removed or renamed content types disappear instead of lingering as stale records that no longer match the environment. When hand-written files were mixed in, move them out first rather than deleting around them. Rebuild, then list renamed and removed types in the summary: partial extensions of a removed type stop compiling, and the user needs to know why.

## Verify the result

- every expected model has `[ContentTypeCodename("...")]` and lives in the intended namespace;
- the project builds against the resolved Delivery packages;
- a typed query such as `client.GetItems<ChosenType>()` compiles without any manual type-provider registration;
- the documented command reproduces the same files.
