---
name: develop-dew-mods
description: Source-backed workflow for developing, debugging, reviewing, and extending Shape of Dreams C#/.NET Framework mods in this DewMod repository. Use when work involves game API behavior, Harmony patches, ModBehaviour or ModConfig lifecycle, Mirror networking, skills, gems, heroes, content registration, Unity assets or prefabs, localization and metadata, project references, or searches across the GameSource decompiled code, DocFX docs, game assemblies, and exported Unity resources.
---

# Develop Dew Mods

Use the repository's game dump as evidence and the existing mods as implementation examples.

## Start Here

1. Resolve the repository root with `git rev-parse --show-toplevel` and preserve unrelated worktree changes.
2. Read [references/source-map.md](references/source-map.md) before searching `GameSource`.
3. Read [references/project-conventions.md](references/project-conventions.md) before creating a mod, migrating source layout, or changing networking, configuration, metadata, localization, or project files.
4. Inspect the affected mod and the closest working mod before designing a change.

Treat `GameSource` as read-only reference material unless the user explicitly asks to refresh or repair the dump. When multiple versioned decompiled source directories exist, use the highest version available; the bundled search helper resolves this automatically. Do not silently assume the dump matches a newer installed build when no newer evidence is present.

## Investigate Before Editing

1. Turn the request into concrete questions: the target type, method signature, lifecycle point, object owner, and client/server authority.
2. Search declarations and usages. Read the containing type, relevant base types, and call sites rather than relying on a matching method name alone.
3. Compare at least one existing repository implementation for Harmony setup, manager lifecycle, configuration, networking, content registration, or localization as applicable.
4. Decide whether the behavior belongs on the client, server, or both. Account for host mode, late join, manager teardown, repeated patch callbacks, and stale subscriptions.
5. State any conclusion that remains inferred because the dump lacks a complete method body or runtime evidence.

Use the bundled search helper from the repository root:

```powershell
pwsh -File .agents/skills/develop-dew-mods/scripts/search-game-source.ps1 -Query 'LootManager' -Scope Code
pwsh -File .agents/skills/develop-dew-mods/scripts/search-game-source.ps1 -Query 'WorldCracker' -Scope Assets -FilesOnly
pwsh -File .agents/skills/develop-dew-mods/scripts/search-game-source.ps1 -Query 'Mirror.dll' -Scope Assemblies -FilesOnly
```

Use `rg` directly for compound searches, contextual lines, or multiple globs.

## Implement Conservatively

- Keep changes inside the target mod unless a shared contract genuinely requires broader edits.
- Apply the project/display naming rules in the project conventions: internal project and entry names use the `Dew` prefix; metadata and Steam display names omit it. Preserve established Mod IDs, Workshop IDs, and configuration identities when renaming.
- Apply the standard layout in the project conventions: root `about/`, `i18n/`, and `src/`; all maintained C# under `src/`, with separate `config/`, `patch/`, `ui/`, and `controller/` subdirectories. Use `src/config/LocalizationSource.cs` as the localization entry point. Existing legacy layouts are migration inputs, not alternative conventions.
- Follow the target project's language style and old-style `.csproj` structure. Add new source and content files to explicit `Compile` or `Content` items when that project requires them.
- Keep all text files UTF-8. Maintain `about/metadata.json`, `about/description.txt`, and relevant `i18n/*.json` when behavior or user-facing text changes.
- Add Chinese comments for functions and special logic, and prefer Chinese runtime logs.
- Prefer public APIs and existing lifecycle helpers such as `CallOnManager<T>` or `CallOnNetworkedManager<T>` when they fit. Use Harmony only at the narrowest stable method boundary supported by source evidence.
- Unsubscribe events and undo owned patches or runtime objects during teardown. Make registration and patch callbacks idempotent when they may run more than once.
- Never trust client-provided gameplay state without checking server authority. For synchronized settings, define ownership, initial snapshot behavior, revision handling, late join behavior, and disconnect cleanup.
- Do not copy large source or asset trees out of `GameSource`; reference exact paths or copy only a required distributable asset after checking how the target mod packages it.

## Verify

1. Re-read the diff for accidental changes and source-dump edits.
2. Parse every changed JSON file with a structured parser.
3. For code or project changes, build the narrowest changed project in `Release` with `build.ps1`; expand to the solution when shared contracts changed. For documentation-only changes, check consistency and links without requiring a game build.
4. Confirm `ShapeOfDreamsHome` is set when project references require installed game assemblies.
5. Check that metadata assembly globs match the produced output and that new source/content files are included by the project.
   For layout migrations, also check the migration acceptance criteria in the project conventions.
6. Report runtime-only behavior that cannot be proven without launching the game, especially Harmony targets, Unity resource loading, and multiplayer flows.
