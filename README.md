# Kontent.ai agent skills

[Agent Skills](https://agentskills.io) for working with Kontent.ai tools. Each skill is a folder with a `SKILL.md` and optional `references/`, `scripts/`, `assets/` and `evals/`, following the [Agent Skills specification](https://agentskills.io/specification), so it works in any client that supports the format (Claude Code, Cursor, Copilot, Codex and others).

## Layout

Skills are grouped by the ecosystem they target:

```
dotnet/                     Skills for the Kontent.ai .NET tools (Delivery, Management, Sync, ASP.NET Core, model generator)
  build-kontent-aspnetcore-mvc/
typescript/                 Skills for the Kontent.ai JavaScript and TypeScript tools (planned)
```

| Skill | What it does |
|---|---|
| [`dotnet/build-kontent-aspnetcore-mvc`](dotnet/build-kontent-aspnetcore-mvc/SKILL.md) | Scaffold a new ASP.NET Core MVC app on Kontent.ai, or add Kontent.ai Delivery to an existing one: packages, configuration, generated models, source-generated type resolution and Razor rendering. |

## Using a skill

Copy or symlink a skill folder into the location your agent reads skills from. For Claude Code that is `.claude/skills/<skill-name>/` in a project or `~/.claude/skills/<skill-name>/` for every project:

```bash
ln -s "$PWD/dotnet/build-kontent-aspnetcore-mvc" ~/.claude/skills/build-kontent-aspnetcore-mvc
```

Other clients are listed at [agentskills.io/clients](https://agentskills.io/clients) with their own install paths.

## Contributing

- Validate a skill before opening a PR:

  ```bash
  npx -y skills-ref validate dotnet/build-kontent-aspnetcore-mvc
  ```

- Keep `SKILL.md` under 500 lines and move detail into `references/`, telling the agent when to read each file.
- Verify every API claim against the current source of the tool the skill targets, not memory. Skills go stale the moment a major changes.
- Each skill carries its test cases in `evals/evals.json` (prompt, expected output, assertions) with any input fixtures under `evals/files/`. Eval runs write to a sibling `<skill>-workspace/` folder, which is gitignored.
