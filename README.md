# Dew Mod

Dew Mod 是一组用于《Shape of Dreams》的 C# / .NET Framework Mod 项目。仓库通过 `DewMod.sln` 管理多个独立 Mod，覆盖客户端显示、房主规则、商人/技能调整、区域调整、多人配置与开发辅助等功能。

## 编码约定

本仓库所有文本文件统一使用 UTF-8 编码，包括 `*.cs`、`*.csproj`、`*.sln`、`*.json`、`*.txt`、`*.ps1`、`*.md` 等文件。

- 新建或修改文件时不要使用 GBK、ANSI 或其他本地代码页编码。
- 中文本地化、`about/description.txt` 和 `README.md` 必须保持 UTF-8。
- 在 Windows PowerShell 5.1 中读写中文文本时，建议显式指定 `-Encoding UTF8`，避免乱码。
- 如果看到中文变成乱码，先确认编辑器或命令行读取编码是否为 UTF-8，再继续修改文件。

## 环境要求

- Windows
- Visual Studio / MSBuild
- .NET Framework 4.8.1 Developer Pack
- 已安装《Shape of Dreams》

## 依赖配置

所有项目通过 `ShapeOfDreamsHome` 环境变量引用游戏目录下的托管程序集，例如：

```powershell
$env:ShapeOfDreamsHome = 'D:\Steam\steamapps\common\Shape of Dreams'
```

如需写入当前用户环境变量：

```powershell
[Environment]::SetEnvironmentVariable('ShapeOfDreamsHome', 'D:\Steam\steamapps\common\Shape of Dreams', 'User')
```

设置后重新打开 Rider、Visual Studio 或终端，确保 IDE/MSBuild 能读取到该变量。项目引用路径通常形如：

```text
$(ShapeOfDreamsHome)\Shape of Dreams_Data\Managed\*.dll
```

## 构建

可以直接用 IDE 打开 `DewMod.sln` 构建，也可以使用仓库根目录的脚本：

```powershell
.\build.ps1
```

构建单个项目：

```powershell
.\build.ps1 -Project .\DewMoreVision\DewMoreVision.csproj -Configuration Release
```

`build.ps1` 中的 `$msbuild` 是本机 MSBuild 路径。如果你的 Visual Studio 安装位置不同，需要先修改该路径。当前脚本默认使用 `Release` 配置，并且不执行 NuGet restore。

各 Mod 的 `about/metadata.json` 当前使用 `obj/Release/*.dll` 作为程序集匹配路径，因此发布或本地加载前请先执行 Release 构建。

## 目录结构

```text
DewMod.sln                         # 解决方案
build.ps1                          # MSBuild 构建脚本
<ModName>/<ModName>.csproj         # 单个 Mod 项目
<ModName>/about/metadata.json      # Mod 元数据
<ModName>/about/description.txt    # Mod 描述
<ModName>/i18n/*.json              # 本地化文本
<ModName>/config/*.cs              # 配置与本地化加载代码
<ModName>/patch/*.cs               # Harmony Patch
```

`DewTestCode` 是开发/测试项目，不作为正式发布 Mod 列入下表。

## Mod 列表

| 项目 | 显示名称 | 版本 | 简介 |
| --- | --- | --- | --- |
| `DewAnyWhereOpenModManager` | 快捷键打开Mod管理器 | 1.0 | 可在任意界面通过快捷键打开 Mod 管理器。 |
| `DewAttackSpeedConvertDamage` | 攻速上限转增伤 / AttackSpeedConvertDamage | 1.1.0 | 房主侧限制攻速上限，并将溢出攻速转换为伤害加成。 |
| `DewBootcamp` | 训练营 | 1.1.0 | 生成测试单位，方便测试 DPS 与 build。 |
| `DewGemSlotCount` | 精华槽数量 / SkillGemCount | 1.3.0 | 调整技能基础精华槽数量，以及堕落混沌可增加到的上限。 |
| `DewHeroSkillJonas` | 出售英雄技能的乔纳斯 | 1.0.0 | 在礼物房间加入额外商人，用于出售英雄技能。 |
| `DewIdentityChange` | 转职 / IdentityChange | 1.1.0 | 允许英雄装备其他角色技能，并使用不同角色的天赋/星座组合。 |
| `DewJonasEnhance` | 商人乔纳斯增强 / JonasEnhance | 1.1.2 | 增强乔纳斯商店，支持商品刷新、列数和初始白金币等配置。 |
| `DewModConfigListSupport` | Mod配置界面列表支持 / ModConfigListSupport | 1.0.0 | 为 Mod 配置界面增加 `List` 类型支持。 |
| `DewMorePlayers` | 更多的玩家人数 / MorePlayers | 1.0.1 | 房主侧自定义玩家数量。 |
| `DewMoreVision` | 无限视距 / MoreVision | 1.1.0 | 客户端扩展摄像机视野范围，并支持调整缩放步长。 |
| `DewPrimusHand` | 普里穆斯之手 / PrimusHand | 1.1.0 | 调整怪物、Boss 与战斗强度相关参数。 |
| `DewSafeShare` | DewSafeShare / 安全共享 | 1.0.0 | 掉落物在玩家主动标记前仅所有者可见。 |
| `DewSuperSmart` | DewSuperSmart | 1.0.0 | 显示攻击/技能范围和威胁区域，支持按住闪避键自动规避。 |
| `DewVascularThief` | 血管小偷 / Vascular Thief | 1.0.0 | 新增可窃取 Boss 能力的专属技能。 |
| `DewZoneTwistedPath` | 区域重排 / ZoneTwistedPath | 1.0.0 | 房主侧重排区域顺序。 |
| `GoldenBurstAutoTarget` | Golden Burst Auto Target（金色爆发自动瞄准） | 1.0.0 | 自动选择目标并从 Q 技能位释放 Golden Burst。 |

## 开发约定

- 每个正式 Mod 项目应包含 `about/metadata.json` 和 `about/description.txt`。
- 有预览图或图标时放在 `about/preview.png` 与 `about/icon.png`。
- 本地化文件放在 `i18n` 目录，文件名使用语言区域代码，例如 `zh-CN.json`、`en-US.json`。
- 配置项优先沿用现有 `config/PluginConfig.cs` 和 `config/LocalizationSource.cs` 的写法。
- Harmony Patch 按功能放在 `patch` 目录，公共工具代码放在 `util` 或项目内已有目录。

## 常见问题

**找不到游戏程序集**

确认 `ShapeOfDreamsHome` 指向《Shape of Dreams》的安装根目录，而不是 `Shape of Dreams_Data` 子目录。

**脚本找不到 MSBuild**

修改 `build.ps1` 中的 `$msbuild` 路径，使其指向你本机 Visual Studio 安装目录下的 `MSBuild.exe`。

**中文显示乱码**

确认文件编码为 UTF-8。使用 Windows PowerShell 5.1 读取中文文件时可加上 `-Encoding UTF8`。
