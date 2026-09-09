# Scaffold and package selection

Read before scaffolding a new MVC project or changing package references.

## Sources of truth

Prefer current primary sources and the actual target project over remembered snippets. Repository `main` can describe an unreleased version, so use it for API shape and NuGet for versions:

- Delivery SDK: https://github.com/kontent-ai/dotnet/blob/main/src/delivery/README.md, plus `docs/upgrade/` for the current major
- ASP.NET Core extensions: https://github.com/kontent-ai/dotnet/blob/main/src/aspnetcore/README.md
- Model generator: https://github.com/kontent-ai/dotnet/blob/main/src/model-generator/README.md
- `dotnet package search <id> --exact-match` for the latest stable version, and restore diagnostics for compatibility
- `dotnet --list-sdks` and the repository's `global.json` for the SDK actually available

## Framework compatibility

| Package line | Target framework |
| --- | --- |
| Kontent.Ai.Delivery 19.x, Kontent.Ai.AspNetCore 0.16.x, Kontent.Ai.ModelGenerator 10.x | `net8.0` (installs into newer frameworks) |
| Kontent.Ai.Delivery 20.x, Kontent.Ai.AspNetCore 1.x, Kontent.Ai.ModelGenerator 11.x | `net10.0` only |

The current line is the default even while it is a release candidate. A new app needs no version lookup: `--prerelease` on the add and install commands resolves the newest build. Look versions up (`dotnet package search <id> --exact-match --prerelease`) only when an existing app forces the older line. `Kontent.Ai.Delivery.SourceGeneration` is `netstandard2.0` and follows the Delivery version, not the framework. `Kontent.Ai.AspNetCore` depends on `Kontent.Ai.Delivery`, so its line fixes the Delivery line, and the model generator's major follows the Delivery major it emits models for. Pick all three from one line.

When the current line is incompatible with the target framework:

1. read NuGet's `NU1202` message for the supported frameworks;
2. select the newest release of the previous line that supports the user's framework;
3. offer a framework upgrade as a separate, explicit choice when it would materially improve the result;
4. never resolve it by silently retargeting the app.

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
dotnet add <web-project> package Kontent.Ai.Delivery --prerelease
dotnet add <models-project> package Kontent.Ai.Delivery.SourceGeneration --prerelease
dotnet add <web-project> package Kontent.Ai.AspNetCore --prerelease
```

`--prerelease` resolves the newest build of the current line while it is a release candidate; drop it once the line is stable. Under central package management add the same versions as `PackageVersion` entries instead.

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

`delivery.Options` is an `OptionsBuilder<DeliveryOptions>`, so `Configure`, `Bind`, `BindConfiguration`, `PostConfigure` and `Validate` are all there, and `BindConfiguration` keeps `IOptionsMonitor` reloads working. The pipeline hangs off the same builder: `delivery.HttpClient` (an `IHttpClientBuilder`), `delivery.ConfigureResilience(...)`, and, when the user asks for caching, `delivery.UseMemoryCache(cache => ...)` from `Kontent.Ai.Delivery.Caching`. A named client is `AddDeliveryClient("preview", delivery => ...)`. The `using Kontent.Ai.Delivery;` line is required; implicit usings do not include it. On the 19.x line the equivalent is `AddDeliveryClient(builder.Configuration)`.

The environment ID may live in tracked configuration when the repository permits it. `PreviewApiKey` and `SecureAccessApiKey` go to user secrets in development and to environment variables such as `DeliveryOptions__SecureAccessApiKey` in deployment:

```bash
dotnet user-secrets init --project <web-project>
dotnet user-secrets set "DeliveryOptions:SecureAccessApiKey" "$KONTENT_SECURE_ACCESS_KEY" --project <web-project>
```

The variable reference is deliberate: the shell expands it inside the process, so the value stays out of the command text and the transcript. When the user prefers to run that command themselves, tell them the exact line and continue once they confirm. Set `UseSecureAccess` (or `UsePreviewApi`) to `true` in the tracked `DeliveryOptions` section so the app reads the key it was given. Never place example keys in `launchSettings.json`, tracked `appsettings*.json`, generated code or shell scripts.
