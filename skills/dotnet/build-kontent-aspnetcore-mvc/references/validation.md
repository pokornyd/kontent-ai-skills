# Validation

Validation proves the scaffold is reproducible and integrated, not that files exist.

## Required checks

1. `dotnet tool restore` from the repository's manifest.
2. Run the documented model-generation command against the intended environment.
3. `dotnet restore`, then read any downgrade or compatibility warnings (`NU1202`, `NU1605`).
4. `dotnet build --no-restore` for the affected solution or projects, with warnings visible.
5. Existing tests, format and lint checks, and repository-specific validation when present.
6. `git diff --check` and `git status --short`: no secrets, build output, framework upgrades or unrelated edits.
7. `git diff --stat` on an existing project: a file showing far more changed lines than you edited means its line endings or byte-order mark were rewritten; restore them.

Run `dotnet test --no-build` only when the preceding build included the test project. A compile-only check says nothing about Delivery connectivity; report it as a compile check.

## Integration invariants

- `DeliveryOptions` is bound in `AddDeliveryClient` and required values come from an allowed configuration source;
- no Preview or Secure Access key is tracked, and its value appears in no command text, generated file or report (variable references only);
- generated models are isolated in their directory and reproducible from the documented command;
- `Kontent.Ai.Delivery.SourceGeneration` is referenced by the compilation containing the attributed models;
- typed queries compile without a hand-written type registry;
- `_ViewImports.cshtml` enables the `Kontent.Ai.AspNetCore` tag helpers, and a new app registers `AddKontentRichText` and binds `ImageTransformationOptions`;
- a new app has the Kontent.ai section in `AGENTS.md` with the real generated directory and regeneration command filled in;
- any content slice keeps Delivery result and error handling out of controllers;
- rich text stays structured until Razor resolves it;
- the app references only content types and elements that exist in the generated model.

## Runtime smoke test

For a plumbing-only task, model generation is already the connectivity proof: it authenticated as far as the environment requires, fetched the content types over the network and wrote a file per type. Say that, rather than leaving the report silent on whether Delivery was ever reached. Starting the app adds nothing beyond it, because with no route querying content the homepage exercises no Delivery call, so skip it. When the task includes a functional slice and environment access is available, start the app with `dotnet run --no-launch-profile --urls http://127.0.0.1:<port>` (without `--no-launch-profile`, `launchSettings.json` overrides the URL), request the implemented route, check both the response and the logs, and stop the process afterwards. A default MVC homepage proves startup, not Delivery connectivity, so say which one was tested.

If the page loads but arrives unstyled, check the template's static-asset pipeline before suspecting your own markup. On some .NET 10 SDK builds `MapStaticAssets` answers a compressed request with a 200 and an empty body, so every browser gets a zero-byte stylesheet. One command tells you:

```bash
curl -s -H 'Accept-Encoding: gzip' -o /dev/null -w '%{size_download}\n' http://127.0.0.1:<port>/css/site.css
```

Zero bytes there and a correct size without the header is the bug. Confirm it on a throwaway `dotnet new mvc` before changing anything, since that separates an SDK defect from something you wrote; then replace `app.MapStaticAssets()` with `app.UseStaticFiles()`, note in the report that it is an SDK workaround rather than a preference, and leave a comment saying why. Do not carry the workaround into a project where the check passes: this will be fixed in a later SDK, and by then swapping the call is a downgrade.

If Secure Access, network policy or missing content blocks the runtime check, report the blocker and leave the build green. Hardcoded sample content standing in for a failed Delivery request hides the failure from the user; never substitute it.
