# Scaffold and package selection

Read before scaffolding a new MVC project or changing package references.

## Sources of truth

Prefer current primary sources and the actual target project over remembered snippets. Repository `main` can describe an unreleased version, so use it for API shape and NuGet for versions:

- Delivery SDK: https://github.com/kontent-ai/dotnet/blob/main/src/delivery/README.md covers installation, registration and configuration only. Querying, models and rich text moved to guides beside it: `docs/queries.md`, `docs/models.md` and `docs/rich-text-customization.md`, with `docs/upgrade/19-to-20.md` for anything a 19.x sample does differently
- Each package's `CHANGELOG.md` (`src/delivery/`, `src/aspnetcore/`, `src/model-generator/`) for what a patch release changed; behaviour fixes land there before they reach a guide
- ASP.NET Core extensions: https://github.com/kontent-ai/dotnet/blob/main/src/aspnetcore/README.md
- Model generator: https://github.com/kontent-ai/dotnet/blob/main/src/model-generator/README.md
- `dotnet package search <id> --exact-match` for the latest stable version, and restore diagnostics for compatibility
- `dotnet --list-sdks` and the repository's `global.json` for the SDK actually available

## Framework compatibility

One line, one framework: Kontent.Ai.Delivery 20.x, Kontent.Ai.AspNetCore 1.x and Kontent.Ai.ModelGenerator 11.x, all `net10.0` with no multi-targeting. `Kontent.Ai.Delivery.SourceGeneration` is `netstandard2.0` and follows the Delivery version rather than the framework. AspNetCore depends on Delivery, and the generator's major follows the Delivery major whose models it emits, so all three move together.

A new app needs no version lookup: the add and install commands resolve the newest release on their own. The previous line, Delivery 19.x with AspNetCore 0.16.x and generator 10.x, targets `net8.0` and still exists on nuget.org, but this skill does not cover it. Its registration, ordering and error-handling APIs differ, so following these references against it produces code that does not compile.

When the target project is below `net10.0`, restore fails with `NU1202`. Do not resolve that by installing the older line or by retargeting the app. Report it:

1. name the constraint, quoting NuGet's message;
2. say that the current Kontent.ai packages require `net10.0`, and that .NET 8 reaches end of support in November 2026, so the upgrade is the direction regardless;
3. offer the framework upgrade as an explicit choice, and stop until the user makes it.

## New application

```bash
dotnet new sln --name <SolutionName>
dotnet new mvc --name <ProjectName> --framework <TargetFramework>
dotnet sln add <ProjectName>/<ProjectName>.csproj
```

When the folder has no git repository, add `dotnet new gitignore` at the root so `bin/` and `obj/` never reach a first commit. Use an installed stable SDK and the current supported stable framework unless the user requested one. Respect a requested name, solution layout and authentication mode. Styling systems, JavaScript frameworks, persistence, authentication, caching, webhooks, Smart Link and preview switching are separate requests; the template plus the Kontent.ai packages is the deliverable.

## Existing application

Inspect before choosing versions:

- `TargetFramework`/`TargetFrameworks` and `global.json`;
- `Directory.Packages.props` or other central package management;
- existing Kontent.ai package references, including transitive versions;
- whether generated models belong in the web project or an existing class library;
- existing configuration section names and DI registration style.

Preserve central package management: add `<PackageVersion>` entries there and no inline `Version` attributes. Do not upgrade the target framework or unrelated packages as an incidental fix; that is a separate change the user owns.

## Package policy

```bash
dotnet add <web-project> package Kontent.Ai.Delivery
dotnet add <models-project> package Kontent.Ai.Delivery.SourceGeneration
dotnet add <web-project> package Kontent.Ai.AspNetCore
```

No `--prerelease`: the line has been stable since Delivery 20.0.0, AspNetCore 1.0.0 and ModelGenerator 11.0.0, and the flag would now resolve a preview of whatever comes next. Take the newest patch rather than the `.0`, because the early patches fix silent failures: Delivery 20.0.1 stops an unmapped typed listing from returning every content type, and ModelGenerator 11.0.1 stops a failed generation from exiting 0. Under central package management put the resolved versions in `Directory.Packages.props` as `PackageVersion` entries instead.

Keep `Kontent.Ai.Delivery.SourceGeneration` at the same version as `Kontent.Ai.Delivery`; a plain `PackageReference` is enough, NuGet places it in the analyzers folder itself. `Kontent.Ai.Delivery.Caching`, `Kontent.Ai.Urls` or a direct `Kontent.Ai.Delivery.Abstractions` reference are added only when code needs them, not because a sample app lists them.

After restore, list resolved packages (`dotnet package list` on current SDKs, `dotnet list package` on older ones) and resolve downgrades and major-version mismatches before writing integration code.

## Configuration

Bind the SDK's standard section:

```json
{
  "DeliveryOptions": {
    "EnvironmentId": "<environment-id>",
    "UsePreviewApi": false
  }
}
```

```csharp
using Kontent.Ai.Delivery;

builder.Services.AddDeliveryClient(delivery => delivery.Options.BindConfiguration("DeliveryOptions"));
```

`delivery.Options` is an `OptionsBuilder<DeliveryOptions>`, so `Configure`, `Bind`, `BindConfiguration`, `PostConfigure` and `Validate` are all there, and `BindConfiguration` keeps `IOptionsMonitor` reloads working. The pipeline hangs off the same builder: `delivery.HttpClient` (an `IHttpClientBuilder`), `delivery.TuneRetry(retry => retry.MaxRetryAttempts = 5)` to adjust the default retries while keeping the SDK's `Retry-After` handling, `delivery.ConfigureResilience(...)` to replace the pipeline outright, and, when the user asks for caching, `delivery.UseMemoryCache(cache => ...)` from `Kontent.Ai.Delivery.Caching`. A named client is `AddDeliveryClient("preview", delivery => ...)`. The `using Kontent.Ai.Delivery;` line is required; implicit usings do not include it.

The environment ID may live in tracked configuration when the repository permits it. `PreviewApiKey` and `SecureAccessApiKey` go to user secrets in development and to environment variables such as `DeliveryOptions__SecureAccessApiKey` in deployment:

```bash
dotnet user-secrets init --project <web-project>
dotnet user-secrets set "DeliveryOptions:SecureAccessApiKey" "$KONTENT_SECURE_ACCESS_KEY" --project <web-project>
```

The variable reference is deliberate: the shell expands it inside the process, so the value stays out of the command text and the transcript. When the user prefers to run that command themselves, tell them the exact line and continue once they confirm. Set `UseSecureAccess` (or `UsePreviewApi`) to `true` in the tracked `DeliveryOptions` section so the app reads the key it was given. Never place example keys in `launchSettings.json`, tracked `appsettings*.json`, generated code or shell scripts.
