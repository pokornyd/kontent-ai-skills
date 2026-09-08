# Kontent.ai agent skills

[Agent Skills](https://agentskills.io) for working with Kontent.ai tools. Each skill is a folder with a `SKILL.md` and optional `references/`, `scripts/`, `assets/` and `evals/`, following the [Agent Skills specification](https://agentskills.io/specification), so it works in any client that supports the format (Claude Code, Cursor, Copilot, Codex and others).

## Layout

Skills are grouped by the ecosystem they target:

```
skills/
  dotnet/                   Skills for the Kontent.ai .NET tools (Delivery, Management, Sync, ASP.NET Core, model generator)
    build-kontent-aspnetcore-mvc/
  typescript/               Skills for the Kontent.ai JavaScript and TypeScript tools (planned)
```

The `skills/<scope>/<skill>/` shape is one of the layouts `gh skill` discovers, so every skill here can be installed with the GitHub CLI.

| Skill | What it does |
|---|---|
| [`build-kontent-aspnetcore-mvc`](skills/dotnet/build-kontent-aspnetcore-mvc/SKILL.md) | Scaffold a new ASP.NET Core MVC app on Kontent.ai, or add Kontent.ai Delivery to an existing one: packages, configuration, generated models, source-generated type resolution and Razor rendering. |

## Using a skill

With the [GitHub CLI](https://cli.github.com) (`gh skill` is in preview), for the agent of your choice and either the current project or your user profile:

```bash
gh skill install pokornyd/kontent-ai-skills build-kontent-aspnetcore-mvc --agent claude-code --scope user
gh skill preview pokornyd/kontent-ai-skills build-kontent-aspnetcore-mvc
```

With the [`skills`](https://skills.sh) CLI:

```bash
npx skills add pokornyd/kontent-ai-skills --skill build-kontent-aspnetcore-mvc
```

Or by hand: copy or symlink the skill folder into the location your agent reads skills from, for Claude Code `.claude/skills/<skill-name>/` in a project or `~/.claude/skills/<skill-name>/` for every project. Other clients are listed at [agentskills.io/clients](https://agentskills.io/clients).

## Contributing

- Validate a skill before opening a PR:

  ```bash
  npx -y skills-ref validate skills/dotnet/build-kontent-aspnetcore-mvc
  gh skill publish --dry-run
  ```

- Keep `SKILL.md` under 500 lines and move detail into `references/`, telling the agent when to read each file.
- Verify every API claim against the current source of the tool the skill targets, not memory. Skills go stale the moment a major changes.
- Each skill carries its test cases in `evals/evals.json` (prompt, expected output, assertions) with any input fixtures under `evals/files/`. Eval runs write to a sibling `<skill>-workspace/` folder, which is gitignored.
