---
name: build-kontent-aspnetcore-mvc
description: Scaffold a new ASP.NET Core MVC application on Kontent.ai, or add Kontent.ai Delivery to an existing MVC application - compatible Kontent.Ai.Delivery, SourceGeneration and AspNetCore packages, DeliveryOptions configuration, strongly typed content models generated with the Kontent.Ai.ModelGenerator local tool, source-generated type resolution, and Razor rich-text and image tag helpers. Use whenever the user wants a server-rendered .NET MVC site that reads Kontent.ai content, mentions Kontent.ai together with Razor, controllers, views, IDeliveryClient or model generation, or asks to regenerate Kontent.ai models in an MVC project, even if they never say "MVC". Do not use for Web API plus SPA or BFF backends, Blazor, Management API writes, or generating a whole site from a content model.
license: MIT
compatibility: Requires a stable .NET SDK, NuGet access and Delivery API read access to the target Kontent.ai environment. Designed for coding agents with shell access (Claude Code or similar).
metadata:
  author: kontent-ai
  version: "0.2.0"
---

# Build a Kontent.ai ASP.NET Core MVC application

Produce a compiling MVC application that reads Kontent.ai content through the Delivery SDK, either from the standard `dotnet new mvc` template or by integrating into an existing MVC project. The framework template and the Kontent.ai tooling are the substrate: no starter application is copied, and nothing is hand-written that the tooling can generate.

## Workflow

Work through the steps in order; each later step assumes the earlier one compiled.

- [ ] 1. Establish the task
- [ ] 2. Inspect before changing
- [ ] 3. Scaffold or integrate - read `references/scaffold-and-packages.md`
- [ ] 4. Generate models - read `references/model-generation.md`
- [ ] 5. Wire Razor rendering - read `references/razor-rendering.md` when models contain rich text or assets
- [ ] 6. Build a content slice only when asked - read `references/content-presentation.md`
- [ ] 7. Validate and report - read `references/validation.md`

Read the [Gotchas](#gotchas) before step 3. They are the facts most likely to be wrong in memory.

## What the user provides

There is no parameter mechanism; everything arrives through the conversation and the shell environment.

| Input | How it arrives | If missing |
| --- | --- | --- |
| Environment ID (required) | In the prompt, or already in the target app's `DeliveryOptions` configuration | Ask; nothing else can supply it |
| Secure Access needed | The user says so, or the Delivery API answers 401 for the environment | Assume public |
| Secure Access key | `KONTENT_SECURE_ACCESS_KEY` exported in the shell the agent runs in, or already seeded in the app's user secrets | Ask the user to export it or seed user secrets themselves, then continue |
| Preview key (only when preview content is requested) | `KONTENT_PREVIEW_API_KEY`, same rules | Same |
| Content type for a working slice | Named in the prompt | Plumbing only, or recommend one when a page was asked for |

A key value is referenced by variable name only: `"$KONTENT_SECURE_ACCESS_KEY"` in a command is fine, the literal value is not. It never goes into command text, a file, a diff or the final report, and the agent never asks the user to paste it into chat; a value pasted into chat is already in the transcript, which is why the variable exists. When neither the variable nor user secrets hold the key, stop before model generation and say exactly what to export.

## 1. Establish the task

Determine:

- new app or existing MVC project, and which project or solution is the target;
- the environment ID and, when the environment is protected, that the key variable is present (`test -n "$KONTENT_SECURE_ACCESS_KEY"` reports presence without printing it);
- plumbing only (packages, configuration, models) or a working content slice for a named content type;
- any explicitly requested .NET or package versions.

Ask only for what cannot be inferred safely; an environment ID and "make it work" is enough to scaffold plumbing. This skill reads the content model and content through the Delivery API. It never creates or changes an environment; that is the Management API and out of scope.

## 2. Inspect before changing

Read repository instructions (`CLAUDE.md`, `AGENTS.md`, contributing docs), `git status`, the solution layout, `global.json`, target frameworks, `Directory.Packages.props`, existing tool manifests, configuration conventions and any existing Kontent.ai integration. Preserve the architecture of an existing app unless it is incompatible or the user asks to change it; the goal is an integration the team recognises as theirs.

## 3. Scaffold or integrate

Follow `references/scaffold-and-packages.md` for template, framework and package selection, then:

- new app: current stable `dotnet new mvc` from an installed stable SDK;
- existing app: touch only the MVC project and any deliberately separate models project;
- install `Kontent.Ai.Delivery`, `Kontent.Ai.Delivery.SourceGeneration` (in the project that compiles the models) and `Kontent.Ai.AspNetCore` at mutually compatible stable versions;
- bind `DeliveryOptions` from configuration and keep Preview and Secure Access keys in user secrets or environment variables.

Stable releases are the default. Choose a preview SDK or a prerelease package only when the user asks for it or no stable path compiles, and say so in the report.

## 4. Generate models

Follow `references/model-generation.md`. Use `Kontent.Ai.ModelGenerator` as a local .NET tool, generate into a dedicated directory and an app-specific namespace, prefer `--nullability strict`, and build immediately afterwards. The build proves that the source generator saw the attributed models and that a typed query compiles with no hand-written type registry.

## 5. Wire Razor rendering

Register `IDeliveryClient` in DI and add `@addTagHelper *, Kontent.Ai.AspNetCore` to `_ViewImports.cshtml`. Configure rich-text resolution and responsive-image widths only when the app renders those element types; `references/razor-rendering.md` has the exact registrations.

## 6. Choose the presentation boundary

Generated records are Delivery element models, not view models.

- Scaffolding and models only: stop at a clean, compiling integration. Empty repository, service or mapper folders add nothing.
- A working page, or a named content type: read `references/content-presentation.md` and implement one vertical slice.
- Several content types could plausibly drive the first page and the choice affects routes: recommend one, or ask, before building.
- Whole-site requests ("build the site this environment describes"): explain that navigation, URL hierarchy and page composition cannot be derived from content-type names alone, then offer a bounded first slice.

## 7. Validate and report

Follow `references/validation.md`: restore tools and packages, regenerate with the documented command, build with warnings visible, run existing tests, inspect the diff for secrets and unrelated changes. Then report with this template:

```markdown
## Kontent.ai MVC integration

- Target framework: <tfm>; packages: Kontent.Ai.Delivery <v>, .SourceGeneration <v>, Kontent.Ai.AspNetCore <v>; tool: Kontent.Ai.ModelGenerator <v>
- Configuration: DeliveryOptions bound from <source>; still needed from you: <keys, or nothing>
- Models: <namespace> in <directory>; regenerate with `<command>`
- Content slice: <what was built and the assumptions behind it, or "none requested">
- Validation: <what ran and passed>; blockers: <none, or list>
```

## Gotchas

- **Registration differs between Delivery 19 and 20.** Delivery 20 removed the `IConfiguration`, `Action<DeliveryOptions>` and `DeliveryClientBuilder` overloads and takes one builder callback: `builder.Services.AddDeliveryClient(delivery => delivery.Options.BindConfiguration("DeliveryOptions"))`. On Delivery 19 the equivalent is `AddDeliveryClient(builder.Configuration)`. Match the form to the resolved package version; the README of that version is authoritative, not memory.
- **Two package lines, two target frameworks.** Kontent.Ai.Delivery 19.x, Kontent.Ai.AspNetCore 0.16.x and ModelGenerator 10.x target `net8.0` and install into any newer framework. Delivery 20.x, AspNetCore 1.x and ModelGenerator 11.x target `net10.0` only, with no multi-targeting, so a `net8.0` project fails restore with `NU1202`. Check nuget.org for which line is stable today rather than assuming; never retarget an existing app or fall back to a prerelease to make restore pass.
- **`Kontent.Ai.Delivery.SourceGeneration` is a Roslyn generator.** Reference it in the project that compiles the attributed models and keep its version equal to `Kontent.Ai.Delivery`. Auto-discovery of the generated `GeneratedTypeProvider` expects the attributed models in one compilation; models split across projects need an explicit `ITypeProvider` registration.
- **The model generator reads only its command line and an `appSettings.json` in its working directory.** It does not read user secrets, environment variables or the app's `appsettings.json`. Its command name is `KontentModelGenerator`; as a local tool run it with `dotnet tool run KontentModelGenerator`. For a protected environment pass `--DeliveryOptions:UseSecureAccess true --DeliveryOptions:SecureAccessApiKey "$KONTENT_SECURE_ACCESS_KEY"`; a temporary config file outside the repository does not work because the local tool manifest is only found by walking up from the working directory.
- **Generated files are `partial record`s and are overwritten on regeneration.** Extend a model in a separate partial file in the same namespace outside the generated directory; never edit generated files.
- **Typed queries filter by type for you.** `GetItems<Article>()` adds `system.type=article` from `[ContentTypeCodename]`. Use the generated `<Element>Codename` constants (`Article.TitleCodename`) in `WithElements`, `OrderByElement` and `Where` instead of retyped strings.
- **Failures are results; cancellation throws.** `ExecuteAsync(cancellationToken)` returns `IDeliveryResult<T>`: a missing item is `IsSuccess == false` with `StatusCode == NotFound`; a transport failure has `StatusCode == 0` and `Error.Exception`. Only `OperationCanceledException` is thrown, so a `catch (HttpRequestException)` around a query is dead code.
- **`<rich-text>` renders the resolver output in place**, with no wrapper element. Without `AddKontentRichText` it still renders text and inline images, and emits HTML comments where embedded content and content-item links have no resolver, so those need explicit configuration before they appear.
- **`<img-asset>` builds `srcset`/`sizes` from `ResponsiveWidths`.** Setting `width` or `height` on the tag switches to a single transformed size and drops the responsive attributes; `rendition="default"` does the same because a rendition is one chosen crop.
