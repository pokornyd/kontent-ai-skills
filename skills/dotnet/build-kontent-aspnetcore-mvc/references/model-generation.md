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

The generator reads its command line and an optional `appSettings.json` in its working directory, nothing else. For Secure Access:

1. create a temporary working directory outside the repository;
2. write a minimal `appSettings.json` there with restricted permissions (`chmod 600`);
3. run the local tool from that directory with an absolute `--outputdir`;
4. delete the directory when the command finishes.

```json
{
  "DeliveryOptions": {
    "EnvironmentId": "<environment-id>",
    "UseSecureAccess": true,
    "SecureAccessApiKey": "<secure-access-key>"
  },
  "Namespace": "<RootNamespace>.Content",
  "OutputDir": "<absolute-generated-directory>",
  "Nullability": "strict"
}
```

`--DeliveryOptions:SecureAccessApiKey <key>` on the command line also works but exposes the key to process listings and shell history; use it only when a temporary file is impossible. Never print the key or include it in the final report. Use Preview API access only when the user explicitly needs an unpublished content model.

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
