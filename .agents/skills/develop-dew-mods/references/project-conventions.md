# DewMod Project Conventions

## Repository Shape

- `DewMod.sln` contains independent .NET Framework 4.8.1 mod projects.
- A formal mod normally contains `<ModName>.csproj`, an entry class derived from `ModBehaviour`, `Properties/AssemblyInfo.cs`, `about/metadata.json`, and `about/description.txt`.
- Configuration commonly lives under `config/`, Harmony patches under `patch/`, shared helpers under `util/`, and translations under `i18n/`.
- Existing old-style project files explicitly list `Compile` and `Content` items and reference game DLLs through `$(ShapeOfDreamsHome)\Shape of Dreams_Data\Managed\`.

## Implementation Checklist

### Entry And Lifecycle

- Expose a public `ModConfig`-derived field when the game must discover configuration; `ModBehaviour.modConfigFields` reflects public instance fields whose types derive from `ModConfig`.
- Initialize localization and owned patches at an established lifecycle point.
- Use `CallOnManager<T>` or `CallOnNetworkedManager<T>` for manager-scoped work when possible.
- Remove subscriptions, objects, and Harmony patches owned by the mod on teardown.

### Harmony

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

- Follow the target mod's `PluginConfig` and `LocalizationSource` pattern; repository projects are not fully uniform.
- Keep label and description keys synchronized with all maintained locale JSON files.
- Do not infer that overriding `CopyTo` or calling a base callback is universally safe; inspect the current `ModConfig` implementation and the closest working mod.

### Metadata And Packaging

- Keep `id` unique and stable, bump `modVer` only when the requested release policy calls for it, and retain the repository's assembly glob convention.
- Include new source, metadata, descriptions, translations, icons, and previews in the `.csproj` when explicit items are used.
- Copy only assets required at runtime into the mod project. `GameSource/asset/ExportedProject` is reference material, not a distributable bundle.

## Verification Commands

Build one project:

```powershell
.\build.ps1 -Project .\DewMoreVision\DewMoreVision.csproj -Configuration Release
```

Validate JSON files in PowerShell:

```powershell
Get-Content -Raw -LiteralPath '.\DewMoreVision\about\metadata.json' | ConvertFrom-Json | Out-Null
```

Inspect relevant changes without disturbing other worktree edits:

```powershell
git diff -- .\DewMoreVision .\DewMod.sln
git status --short
```
