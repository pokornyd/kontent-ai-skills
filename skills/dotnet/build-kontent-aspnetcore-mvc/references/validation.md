# Validation

Validation proves the scaffold is reproducible and integrated, not that files exist.

## Required checks

1. `dotnet tool restore` from the repository's manifest.
2. Run the documented model-generation command against the intended environment.
3. `dotnet restore`, then read any downgrade or compatibility warnings (`NU1202`, `NU1605`).
4. `dotnet build --no-restore` for the affected solution or projects, with warnings visible.
5. Existing tests, format and lint checks, and repository-specific validation when present.
6. `git diff --check` and `git status --short`: no secrets, build output, framework upgrades or unrelated edits.

Run `dotnet test --no-build` only when the preceding build included the test project. A compile-only check says nothing about Delivery connectivity; report it as a compile check.

## Integration invariants

- `DeliveryOptions` is bound in `AddDeliveryClient` and required values come from an allowed configuration source;
- no Preview or Secure Access key is tracked;
- generated models are isolated in their directory and reproducible from the documented command;
- `Kontent.Ai.Delivery.SourceGeneration` is referenced by the compilation containing the attributed models;
- typed queries compile without a hand-written type registry;
- `_ViewImports.cshtml` enables the `Kontent.Ai.AspNetCore` tag helpers;
- any content slice keeps Delivery result and error handling out of controllers;
- rich text stays structured until Razor resolves it;
- the app references only content types and elements that exist in the generated model.

## Runtime smoke test

When the task includes a functional slice and environment access is available, start the app on an ephemeral port, request the implemented route, and check both the response and the logs; stop the process afterwards. A default MVC homepage proves startup, not Delivery connectivity, so say which one was tested.

If Secure Access, network policy or missing content blocks the runtime check, report the blocker and leave the build green. Hardcoded sample content standing in for a failed Delivery request hides the failure from the user; never substitute it.
