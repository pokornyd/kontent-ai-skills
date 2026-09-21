# Generate Delivery models

Turn the environment's content types into strongly typed Delivery records with `Kontent.Ai.ModelGenerator`. The generator knows the content model, not the site: it never implies routes, pages or navigation.

## Use a local tool

A repository-local manifest makes generation reproducible for the next person:

```bash
dotnet new tool-manifest      # skip when .config/dotnet-tools.json already exists
mkdir -p .config && mv dotnet-tools.json .config/   # the .NET 10 SDK writes it to the current directory
dotnet tool install Kontent.Ai.ModelGenerator
```

No `--prerelease`; 11.x is stable. The tool needs the .NET 10 runtime, which is the only thing tying it to this package line: its Delivery output is unchanged from 10.x, so regenerating with a newer tool produces no diff by itself, and the project's own target framework is constrained by the SDK the models reference, not by the tool.

In an existing repository restore the manifest and keep its pinned version unless the user asked for an upgrade. When that pin is below 11.0.1, including any `11.0.0-rc`, say so in the report and recommend the bump: those builds exit 0 with nothing written when the Delivery request fails (see below), which is the failure a regeneration pipeline can least afford.

Run from a directory where the manifest is in scope:

```bash
dotnet tool run KontentModelGenerator \
  --environmentId "<environment-id>" \
  --namespace "<RootNamespace>.Content" \
  --outputdir "<generated-directory>" \
  --nullability strict
```

Delivery mode is the default; `--management` emits Management SDK models, which an MVC site does not need. `--outputdir` is resolved against the working directory, so pass an absolute path or one relative to where the command runs. Arguments are validated before any request, so a malformed `--environmentId` or a flag belonging to the other mode fails immediately instead of part-way through a run.

### When generation fails

From 11.0.1 a failed Delivery call aborts the run with exit code 1 and the API's own diagnosis:

```text
Failed to list content types from the Delivery API (404): The specified environment with the ID '<environment-id>' doesn't exist. Check for the correct environment ID in Kontent.ai > Environment settings > General. … Request ID: df98da3777b89689.
```

Read the status before retrying. A 404 is a wrong environment ID. A 401 means the environment has Secure Access on, or the key was rejected; the message for it says to check `--apikey`, which is misleading in Delivery mode, because `--apikey` is the Management API key and is refused without `--management`. The key belongs in `--DeliveryOptions:SecureAccessApiKey` as shown under Protected environments. Do not add `--management` to make the flag acceptable: that generates Management models over the output directory.

Older builds report the same failures as "No content type available for the environment", write nothing and exit 0. With such a pin, zero generated files means the request failed; `curl -s -o /dev/null -w '%{http_code}\n' https://deliver.kontent.ai/<environment-id>/types` tells you which way.

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

Generate into a temporary directory first and diff it against the generated directory. The generator writes a file per type that currently exists and never deletes one, so a type removed from the environment lingers as a record that still compiles and still registers with the source generator. Nothing announces it.

Before removing anything, search the app for each type that disappeared. What you do next depends on what you find, and the split matters:

- **Nothing references it.** Delete it. It is dead weight, removing it is what "nothing stale" means, and there is no way for that to surprise anyone.
- **Something references it.** Stop and tell the user. Name the content type and every file that uses it, and leave both the code and the generated record in place. Deleting the record breaks their build; deleting the code they wrote destroys work; keeping quiet leaves them querying a content type that no longer exists and getting empty results forever. Which of those to do is a product decision, and a deleted type is often an accident or a migration in progress, so it is not yours to make. Say what you found and let them choose.

Rebuild after the removals you did make, and list in the summary what was removed, what was added, and any type you left in place along with the reason.

The opposite drift, a type added to the environment since the last generation, never breaks a build either. At runtime the SDK falls back to `IDynamicElements` for it and logs warning `1408` (`Content type '<codename>' has no mapped model`) once per type, so when the user reports linked items or components missing from a page, that warning in the app's log is the evidence that regeneration is the fix.

## Verify the result

- every expected model has `[ContentTypeCodename("...")]` and lives in the intended namespace;
- the project builds against the resolved Delivery packages;
- after `dotnet build`, `grep -a -c GeneratedTypeProvider "$(dotnet build <models-project> -getProperty:TargetPath | tail -1)"` returns 1, so the provider reached the compiled assembly (generator output is never written under `obj/`, and `-getProperty:TargetPath` avoids hard-coding a framework or configuration); this shows the generator ran, not that a query resolves at runtime, and needs no probe file;
- the documented command reproduces the same files.
