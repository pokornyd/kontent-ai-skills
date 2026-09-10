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
- [ ] 5. Wire Razor rendering and leave the conventions in the repo - read `references/razor-rendering.md`
- [ ] 6. Build a content slice only when asked - read `references/content-presentation.md`, and `references/localization.md` when the environment has more than one language
- [ ] 7. Validate and report - read `references/validation.md`

Read the [Gotchas](#gotchas) before step 3. They are the facts most likely to be wrong in memory.

Not every request needs all seven. A regeneration-only request ("the content model changed, regenerate the models") is steps 2, 4 and 7: inspect, regenerate, validate and report. Adding packages, registration or rendering to an app that asked only for fresh models is scope the user did not ask for.

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
- how many languages the environment has (`/languages` on the Delivery API), because more than one changes the shape of every query and every route;
- plumbing only (packages, configuration, models) or a working content slice for a named content type;
- any explicitly requested .NET or package versions.

Ask only for what cannot be inferred safely; an environment ID and "make it work" is enough to scaffold plumbing. This skill reads the content model and content through the Delivery API. It never creates or changes an environment; that is the Management API and out of scope.

## 2. Inspect before changing

Read repository instructions (`CLAUDE.md`, `AGENTS.md`, contributing docs), `git status`, the solution layout, `global.json`, target frameworks, `Directory.Packages.props`, existing tool manifests, configuration conventions and any existing Kontent.ai integration. Preserve the architecture of an existing app unless it is incompatible or the user asks to change it; the goal is an integration the team recognises as theirs.

## 3. Scaffold or integrate

Follow `references/scaffold-and-packages.md` for template, framework and package selection, then:

- new app: current stable `dotnet new mvc` from an installed stable SDK;
- existing app: touch only the MVC project and any deliberately separate models project;
- install `Kontent.Ai.Delivery`, `Kontent.Ai.Delivery.SourceGeneration` (in the project that compiles the models) and `Kontent.Ai.AspNetCore` from one package line;
- bind `DeliveryOptions` from configuration and keep Preview and Secure Access keys in user secrets or environment variables.

This skill targets one package line: Delivery 20.x, AspNetCore 1.x and ModelGenerator 11.x, all `net10.0`. Everything it says about registration, querying, error handling and the tag helpers is written for that line and does not hold on Delivery 19.x. Install the newest stable release, adding `--prerelease` only while no stable release of the line exists yet, in which case say so in the report.

A project that cannot move to `net10.0` is out of scope, and the honest answer is better than a half-guided one: report that the current packages are `net10.0` only, that .NET 8 itself reaches end of support in November 2026, and offer the framework upgrade as the decision it is. Do not install the 19.x line as a consolation, because the code this skill would then have you write does not compile against it.

## 4. Generate models

Follow `references/model-generation.md`. Use `Kontent.Ai.ModelGenerator` as a local .NET tool, generate into a dedicated directory and an app-specific namespace, prefer `--nullability strict`, and build immediately afterwards. Then confirm the source generator ran, asking MSBuild where the assembly landed rather than assuming a framework or configuration:

```bash
dotnet build
grep -a -c GeneratedTypeProvider "$(dotnet build <models-project> -getProperty:TargetPath | tail -1)"
```

A count of 1 means the generator emitted the provider into the compilation that carries the attributed models; Roslyn keeps generator output in memory, so nothing under `obj/` will show it. That plus a clean build is as far as a compile-time check goes. It does not prove a query resolves at runtime, which only a real request against the environment shows, so report it as what it is. Do not write a throwaway probe file to check that a typed query compiles: it costs a build cycle and proves nothing this does not.

## 5. Wire Razor rendering and leave the conventions in the repo

Register `IDeliveryClient` in DI and add `@addTagHelper *, Kontent.Ai.AspNetCore` to `_ViewImports.cshtml`. In a new app also register `AddKontentRichText()` and bind `ImageTransformationOptions`, using the snippets in `references/razor-rendering.md`: both are inert until a view uses them, they cost three lines, and without them the first `<rich-text>` or `<img-asset>` someone writes half-works. In an existing app add each only when the app renders that element type, so the team's startup stays theirs.

Then make the architecture survive the session. A plumbing-only scaffold contains generated DTOs and nothing that says they must not be bound straight into views, so the next developer or agent will do exactly that. For a new app, copy `assets/kontent-conventions.md` into `AGENTS.md` at the repository root (create the file; append the section when one already exists), replacing the two placeholders with the generated directory and the regeneration command. For an existing app, put the section in the report as a proposal; the team's instructions file is theirs to edit.

## 6. Choose the presentation boundary

Generated records are Delivery element models, not view models.

- Scaffolding and models only: stop at a clean, compiling integration with the conventions file in place. Empty repository, service or mapper folders and interfaces without an implementation add nothing; the conventions file carries the intent without dead code.
- A working page, or a named content type: read `references/content-presentation.md` and implement one vertical slice.
- Several content types could plausibly drive the first page and the choice affects routes: recommend one, or ask, before building.
- Whole-site requests ("build the site this environment describes"): explain that navigation, URL hierarchy and page composition cannot be derived from content-type names alone, then offer a bounded first slice.

## 7. Validate and report

Follow `references/validation.md`: restore tools and packages, regenerate with the documented command, build with warnings visible, run existing tests, inspect the diff for secrets and unrelated changes. Then report with this template:

```markdown
## Kontent.ai MVC integration

- Environment: <id>, which returned <n> content types (<the codenames, or the first few and a count>)
- Target framework: <tfm>; packages: Kontent.Ai.Delivery <v>, .SourceGeneration <v>, Kontent.Ai.AspNetCore <v>; tool: Kontent.Ai.ModelGenerator <v>
- Configuration: DeliveryOptions bound from <source>; still needed from you: <keys, or nothing>
- Models: <namespace> in <directory>; regenerate with `<command>`
- Content slice: <what was built and the assumptions behind it, or "none requested">
- Validation: <what ran and passed>; blockers: <none, or list>
- Next step: <the first slice you would build and why, or the conventions proposal for an existing app>
```

Report the environment from what it actually returned, not from what its ID resembles. Kontent.ai publishes sample environments and their IDs circulate in documentation, so naming one from memory is how a report ends up confidently describing the wrong content model. The codenames the generator just fetched are the evidence.

## Gotchas

- **Registration takes one builder callback:** `builder.Services.AddDeliveryClient(delivery => delivery.Options.BindConfiguration("DeliveryOptions"))`. The `IConfiguration`, `Action<DeliveryOptions>` and `DeliveryClientBuilder` overloads that older samples and most training data still show were removed in this major; caching attaches to the same builder (`delivery.UseMemoryCache()` from `Kontent.Ai.Delivery.Caching`). The extension lives in the `Kontent.Ai.Delivery` namespace, which implicit usings do not cover, so Program.cs needs `using Kontent.Ai.Delivery;` or the build fails on the first try.
- **The packages are `net10.0` only.** There is no multi-targeting, so an older project fails restore with `NU1202`. Never retarget an app to make restore pass, and never drop to the 19.x line to get a green build: that line predates the builder registration, `OrderByElement` and `DeliveryRequestException`, so the code in these references would not compile. An app below `net10.0` needs the framework upgrade first, which is the user's decision to make.
- **`Kontent.Ai.Delivery.SourceGeneration` is a Roslyn generator.** Reference it in the project that compiles the attributed models and keep its version equal to `Kontent.Ai.Delivery`. Auto-discovery of the generated `GeneratedTypeProvider` expects the attributed models in one compilation; models split across projects need an explicit `ITypeProvider` registration.
- **The model generator reads only its command line and an `appSettings.json` in its working directory.** It does not read user secrets, environment variables or the app's `appsettings.json`. Its command name is `KontentModelGenerator`; as a local tool run it with `dotnet tool run KontentModelGenerator`. For a protected environment pass `--DeliveryOptions:UseSecureAccess true --DeliveryOptions:SecureAccessApiKey "$KONTENT_SECURE_ACCESS_KEY"`; a temporary config file outside the repository does not work because the local tool manifest is only found by walking up from the working directory.
- **Editing an existing project preserves its bytes.** A .NET template writes CRLF line endings and gives `_ViewImports.cshtml` a UTF-8 byte-order mark. Rewriting a file wholesale flattens both, so a one-line change lands as a whole-file diff and reviewers cannot see what you did. Edit in place rather than regenerating a file, and check `git diff --stat` for files that grew far more changed lines than you touched.
- **On the .NET 10 SDK `dotnet new tool-manifest` writes `dotnet-tools.json` into the current directory**, not `.config/`. Move it (`mkdir -p .config && mv dotnet-tools.json .config/`) before installing the tool, so the manifest sits where every other SDK and every reader expects it.
- **Generated files are `partial record`s and are overwritten on regeneration.** Extend a model in a separate partial file in the same namespace outside the generated directory; never edit generated files.
- **A query without a language silently serves the environment's default.** There is no global setting to change that, so in a multilingual environment a forgotten parameter looks exactly like success. Read `references/localization.md` before writing the first query.
- **Typed queries filter by type for you.** `GetItems<Article>()` adds `system.type=article` from `[ContentTypeCodename]`. Use the generated `<Element>Codename` constants (`Article.TitleCodename`) in `WithElements`, `Where` and `OrderByElement(Article.PublishDateCodename, OrderingMode.Descending)` instead of retyped strings; `OrderByElement` and `OrderBySystem` (rc.3) add the `elements.`/`system.` prefix, `OrderBy` still wants the full path.
- **Failures are results; cancellation throws.** `ExecuteAsync(cancellationToken)` returns `IDeliveryResult<T>`: a missing item is `IsSuccess == false` with `StatusCode == NotFound`; a transport failure has `StatusCode == 0` and `Error.Exception`. Only `OperationCanceledException` is thrown from a single request, so a `catch (HttpRequestException)` around `ExecuteAsync` is dead code. The one place that throws is a walk: `EnumerateAsync()` on the feed and used-in queries raises `DeliveryRequestException` on a failed page (rc.3) instead of ending early, so an export cannot mistake a partial result for a complete one.
- **`<rich-text>` renders the resolver output in place**, with no wrapper element. Without `AddKontentRichText` it still renders text and inline images, and emits HTML comments where embedded content and content-item links have no resolver, so those need explicit configuration before they appear.
- **An asset with no description is an empty string as often as it is `null`.** `IAsset.Description` is typed nullable, but environments send `""` for an unfilled description, so `Description ?? Title` compiles, reads correctly and silently renders `alt=""` on every image. Test for blank instead, and put the test on the view model so every view inherits it: `string.IsNullOrWhiteSpace(Image?.Description) ? Title : Image.Description`.
- **`<img-asset>` builds `srcset`/`sizes` from `ResponsiveWidths`.** Setting `width` or `height` on the tag switches to a single transformed size and drops the responsive attributes; `rendition="default"` does the same because a rendition is one chosen crop.
