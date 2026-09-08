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

| Package line | Target framework | Registration form |
| --- | --- | --- |
| Kontent.Ai.Delivery 19.x, Kontent.Ai.AspNetCore 0.16.x, Kontent.Ai.ModelGenerator 10.x | `net8.0` (installs into newer frameworks) | `AddDeliveryClient(builder.Configuration)` |
| Kontent.Ai.Delivery 20.x, Kontent.Ai.AspNetCore 1.x, Kontent.Ai.ModelGenerator 11.x | `net10.0` only | `AddDeliveryClient(delivery => delivery.Options.BindConfiguration("DeliveryOptions"))` |

The current line is the default even while it is a release candidate; `dotnet package search <id> --exact-match --prerelease` shows the newest build of it. `Kontent.Ai.Delivery.SourceGeneration` is `netstandard2.0` and follows the Delivery version, not the framework. `Kontent.Ai.AspNetCore` depends on `Kontent.Ai.Delivery`, so its line fixes the Delivery line, and the model generator's major follows the Delivery major it emits models for. Pick all three from one line.

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

Use an installed stable SDK and the current supported stable framework unless the user requested one. Respect a requested name, solution layout and authentication mode. Styling systems, JavaScript frameworks, persistence, authentication, caching, webhooks, Smart Link and preview switching are separate requests; the template plus the Kontent.ai packages is the deliverable.

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
// Delivery 20.x
builder.Services.AddDeliveryClient(delivery => delivery.Options.BindConfiguration("DeliveryOptions"));

// Delivery 19.x
builder.Services.AddDeliveryClient(builder.Configuration);
```

On 20.x, `Options` is an `OptionsBuilder<DeliveryOptions>`, so `Configure`, `Bind`, `PostConfigure` and `Validate` are available on it, binding this way keeps `IOptionsMonitor` reloads working, and pipeline customisation (`delivery.ConfigureResilience(...)`, `delivery.HttpClient`) chains on the same builder. The 19.x overload binds the `DeliveryOptions` section by name.

The environment ID may live in tracked configuration when the repository permits it. `PreviewApiKey` and `SecureAccessApiKey` go to user secrets in development and to environment variables such as `DeliveryOptions__SecureAccessApiKey` in deployment:

```bash
dotnet user-secrets init --project <web-project>
dotnet user-secrets set "DeliveryOptions:SecureAccessApiKey" "$KONTENT_SECURE_ACCESS_KEY" --project <web-project>
```

The variable reference is deliberate: the shell expands it inside the process, so the value stays out of the command text and the transcript. When the user prefers to run that command themselves, tell them the exact line and continue once they confirm. Set `UseSecureAccess` (or `UsePreviewApi`) to `true` in the tracked `DeliveryOptions` section so the app reads the key it was given. Never place example keys in `launchSettings.json`, tracked `appsettings*.json`, generated code or shell scripts.
