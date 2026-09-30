# DewMod Project Conventions

## Repository Shape

- `DewMod.sln` contains independent .NET Framework 4.8.1 mod projects.
- Every mod must have root-level `about/`, `i18n/`, and `src/` directories. Keep `<ModName>.csproj` at the mod root, metadata and descriptions under `about/`, and locale JSON under `i18n/`. Track an otherwise empty required directory with `.gitkeep`.
- All maintained C# source belongs under `src/`, including the `ModBehaviour` entry class and `src/Properties/AssemblyInfo.cs`. Generated `bin/` and `obj/` files are not migration inputs.
- Use `src/config/` for configuration and `LocalizationSource.cs`, `src/patch/` for Harmony patches, `src/ui/` for views and widgets, `src/controller/` for controllers and flow coordination, and `src/util/` for shared helpers. Any additional source directories also belong under `src/`.
- These are the required target conventions for new mods and migrations. Legacy projects may still differ; do not copy their old layout as a competing convention.
- Existing old-style project files explicitly list `Compile` and `Content` items and reference game DLLs through `$(ShapeOfDreamsHome)\Shape of Dreams_Data\Managed\`.

## Project And Display Names

- Use the `Dew` prefix for every mod's project directory, `.csproj` filename, solution project name, `AssemblyName`, `RootNamespace`, and `ModBehaviour` entry class/file. Keep these names aligned, for example `DewRoomGuidance/DewRoomGuidance.csproj` and `src/DewRoomGuidance.cs`.
- Omit the project prefix from player-facing names: `about/metadata.json` `name`, Steam Workshop titles, and display titles in `about/description.txt` use `Room Guidance`, `SafeShare`, or localized equivalents instead of `DewRoomGuidance` or `DewSafeShare`. Apply this to each language in a bilingual title. Keep technical paths and identifiers accurate when mentioned in descriptions.
- Preserve established identities during a naming-only change: metadata `id`, Workshop `publishedfileid.txt`, existing configuration file paths, and serialized configuration keys must not change. Inspect how the game derives config filenames before renaming an entry class; where its name affects the default path, override `GetModConfigFilePath` to keep the established filename. Check serializer behavior before assuming a namespace or type rename changes stored data.
- When renaming an existing project, update source references, solution/project paths, entry classes, assembly information, packaging paths, build scripts, and documentation together. Do not bump `modVer` solely for naming unless the requested release policy requires it.

## Implementation Checklist

### Entry And Lifecycle

- Expose a public `ModConfig`-derived field when the game must discover configuration; `ModBehaviour.modConfigFields` reflects public instance fields whose types derive from `ModConfig`.
- Initialize localization and owned patches at an established lifecycle point.
- Use `CallOnManager<T>` or `CallOnNetworkedManager<T>` for manager-scoped work when possible.
- Remove subscriptions, objects, and Harmony patches owned by the mod on teardown.

### Harmony

- Move Harmony patch classes into `src/patch/`, grouped by target or feature. Keep interception and delegation here; place UI and controller implementations in their respective directories.
- Verify the declaring type, overload, parameter order, return type, and static/instance status in `GameSource`.
- Prefer a typed patch when the target is stable. Use reflective targeting only when overload selection or optional-version compatibility requires it.
- Keep prefix/postfix behavior idempotent and consider whether the original method runs on client, server, and host.
- Avoid patching a broad update loop when an event or narrower lifecycle method exists.

### Multiplayer

- Identify the authoritative side before changing gameplay state.
- Test the reasoning for dedicated client, server, host, late join, manager restart, and disconnect.
- Avoid feedback loops when applying a server snapshot to a local config callback.
- Reject stale or malformed payloads and ensure repeated delivery is harmless.

### Configuration And Localization

- Place configuration classes (normally `PluginConfig.cs`) and localization configuration/loading in `src/config/`.
- Standardize localization on `LocalizationSource` in `src/config/LocalizationSource.cs`; migrate differently named or scattered implementations and update all callers. Initialize with `LocalizationSource.Init(this)` before text is used, query through `GetLocalizationText`, and localize config widgets with `LocalizeUI` after they are built.
- Load locale JSON from the mod root's `i18n/`, not from `src/`. Use language-region filenames such as `zh-CN.json` and `en-US.json`, and explicit UTF-8 file reads. Preserve missing-language and missing-key fallbacks.
- Use localization keys for config groups, labels, and descriptions. Keep UI text lookup behind `LocalizationSource` rather than introducing another translation entry point.
- Keep label and description keys synchronized with all maintained locale JSON files.
- Keep formatting placeholders consistent across maintained locales.
- Do not infer that overriding `CopyTo` or calling a base callback is universally safe; inspect the current `ModConfig` implementation and the closest working mod.

### UI And Controllers

- Put views, widgets, and display interactions in `src/ui/`; put controllers, state, and flow coordination in `src/controller/`.
- Keep patch classes separate even when their targets are UI methods. Delegate view work to UI code and orchestration to controllers.
- Add Chinese comments for functions and special logic, prefer Chinese logs, and keep text files UTF-8.

### Metadata And Packaging

- Keep `id` unique and stable, bump `modVer` only when the requested release policy calls for it, and retain the repository's assembly glob convention.
- Include new source, metadata, descriptions, translations, icons, and previews in the `.csproj` when explicit items are used.
- Copy only assets required at runtime into the mod project. `GameSource/asset/ExportedProject` is reference material, not a distributable bundle.

## Layout Migration Acceptance

- Root `about/`, `i18n/`, and `src/` exist; `about/metadata.json` and `about/description.txt` are present.
- All maintained `.cs` files are under `src/`, including assembly information; old source directories and duplicate originals are removed after migration.
- Configuration/localization, patches, UI, and controllers are in their designated subdirectories; localization callers use `LocalizationSource`.
- Update explicit `.csproj` `Compile` and `Content` entries and related resource paths. Each source is compiled once, no old paths remain, and `AppDesignerFolder` points to `src\Properties`.
- Check namespaces, `using` directives, reflected type names, and localization callers where affected. A directory move alone does not require changing public type names, Mod IDs, config keys, or assembly names.
- Parse changed JSON, check locale keys and formatting placeholders, and build affected projects in Release. Verify changed UI, lifecycle, and Harmony behavior in-game; report anything not exercised at runtime.

## Verification Commands

Build one project:

```powershell
.\build.ps1 -Project .\DewMoreVision\DewMoreVision.csproj -Configuration Release
```

Validate JSON files in PowerShell:

```powershell
Get-Content -Raw -LiteralPath '.\DewMoreVision\about\metadata.json' -Encoding utf8 | ConvertFrom-Json | Out-Null
```

Inspect relevant changes without disturbing other worktree edits:

```powershell
git diff -- .\DewMoreVision .\DewMod.sln
git status --short
```
