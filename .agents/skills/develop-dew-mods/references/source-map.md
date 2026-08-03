# GameSource Map

`GameSource` is a large, versioned evidence set. Search only the smallest relevant area first.

## Route By Question

| Question | Start path | Notes |
| --- | --- | --- |
| Runtime type, field, method, or lifecycle | `GameSource/code/Dewr.1.3.1.3_s/` | Decompiled C# for game version 1.3.1.3. Read declarations, base types, and call sites. |
| Core gameplay and mod APIs | `GameSource/code/Dewr.1.3.1.3_s/Dew.Core/` | Includes `ModBehaviour`, `ModConfig`, actors, managers, networking helpers, combat, and shared systems. |
| Skills, gems, heroes, monsters, zones, and other content types | `GameSource/code/Dewr.1.3.1.3_s/Dew.Contents/` | Search concrete content class names and their base classes. |
| Menus, HUD, widgets, and UI behavior | `GameSource/code/Dewr.1.3.1.3_s/Dew.UI/` | Pair with exported prefabs and UI assets when hierarchy matters. |
| External or integration code | `GameSource/code/Dewr.1.3.1.3_s/Dew.External/` | Inspect only when the target crosses an external-system boundary. |
| API overview or cross-reference | `GameSource/doc/api/`, `GameSource/doc/md/`, `GameSource/doc/xrefmap.yml` | Generated DocFX material. Use source for implementation details. |
| Prefab, ScriptableObject, material, sprite, scene, or serialized value | `GameSource/asset/ExportedProject/Assets/` | Unity export with `.meta` pairs. Begin under `Assets/Dew/` for game content. |
| Unity project/package settings | `GameSource/asset/ExportedProject/Packages/`, `GameSource/asset/ExportedProject/ProjectSettings/` | Useful for Unity/package/version assumptions, not mod packaging. |
| Assembly availability or exact reference name | `GameSource/asset/AuxiliaryFiles/GameAssemblies/` | Contains `Dew.*`, Mirror, Unity, and third-party managed assemblies. Do not add every DLL to a project. |
| Export path-ID lookup | `GameSource/asset/AuxiliaryFiles/path_id_map.json` | Parse as JSON; do not edit with string replacement. |
| New gem architecture example | `GameSource/Gem_E_NewGem/` | Treat as a reference implementation, not authoritative runtime API. Verify every API against decompiled source and current repository patterns. |

## Search Recipes

Search source declarations and usages:

```powershell
rg -n --glob '*.cs' 'class LootManager|OnStartServer' GameSource/code/Dewr.1.3.1.3_s
rg -n --glob '*.cs' 'CallOnNetworkedManager|customData' GameSource/code/Dewr.1.3.1.3_s . -g '!GameSource/**'
```

Find a content or resource by filename before opening serialized files:

```powershell
rg --files GameSource/asset/ExportedProject/Assets | rg 'WorldCracker|GoldenBurst'
```

Inspect a Unity asset together with its `.meta` file and referenced GUIDs. Search `m_Name`, `m_Script`, component fields, and asset references; do not assume the directory name is the runtime resource key.

Find documentation without searching generated JavaScript or CSS:

```powershell
rg -n 'ModBehaviour|ModConfig' GameSource/doc/api GameSource/doc/md GameSource/doc/xrefmap.yml
```

## Evidence Rules

- Prefer method bodies in decompiled source for control flow and DocFX pages for quick navigation.
- Prefer the installed game's managed assemblies when validating compatibility with a build newer than 1.3.1.3.
- Distinguish a type existing from it being initialized at the patch point.
- For serialized assets, distinguish the asset definition from the runtime instance created from it.
- Record the exact source path supporting fragile Harmony signatures or field access.
