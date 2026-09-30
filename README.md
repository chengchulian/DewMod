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
DewMod.sln                                    # 解决方案
build.ps1                                     # MSBuild 构建脚本
Dew<ModName>/Dew<ModName>.csproj              # 单个 Mod 项目
Dew<ModName>/about/metadata.json              # Mod 元数据
Dew<ModName>/about/description.txt            # Mod 描述
Dew<ModName>/i18n/*.json                      # 本地化文本
Dew<ModName>/src/Dew<ModName>.cs              # ModBehaviour 入口
Dew<ModName>/src/Properties/AssemblyInfo.cs   # 程序集信息
Dew<ModName>/src/config/PluginConfig.cs       # Mod 配置
Dew<ModName>/src/config/LocalizationSource.cs # 统一本地化入口
Dew<ModName>/src/patch/*.cs                   # Harmony Patch
Dew<ModName>/src/ui/*.cs                      # UI 视图与控件
Dew<ModName>/src/controller/*.cs              # 控制器与流程协调
Dew<ModName>/src/util/*.cs                    # 公共工具
```

`DewTestCode` 是开发/测试项目，不作为正式发布 Mod 列入下表。

## Mod 列表

| 项目 | 显示名称 | 版本 | 简介 |
| --- | --- | --- | --- |
| `DewAnyWhereOpenModManager` | 快捷键打开Mod管理器 | 1.0 | 可在任意界面通过快捷键打开 Mod 管理器。 |
| `DewAttackSpeedConvertDamage` | 攻速上限转增伤 / AttackSpeedConvertDamage | 1.1.0 | 房主侧限制攻速上限，并将溢出攻速转换为伤害加成。 |
| `DewBootcamp` | 训练营 | 1.1.0 | 生成测试单位，方便测试 DPS 与 build。 |
| `DewGemSlotCount` | 精华槽数量 / SkillGemCount | 1.4.0 | 调整技能基础精华槽数量，以及堕落混沌可增加到的上限。 |
| `DewGoldenBurstAutoTarget` | Golden Burst Auto Target（金色爆发自动瞄准） | 1.0.0 | 自动选择目标并从 Q 技能位释放 Golden Burst。 |
| `DewHeroSkillJonas` | 出售英雄技能的乔纳斯 | 1.0.0 | 在礼物房间加入额外商人，用于出售英雄技能。 |
| `DewIdentityChange` | 转职 / IdentityChange | 1.3.0 | 允许英雄装备其他角色技能、使用跨角色天赋，并可将角色技能加入全局掉落池。 |
| `DewJonasEnhance` | 商人乔纳斯增强 / JonasEnhance | 1.1.2 | 增强乔纳斯商店，支持商品刷新、列数和初始白金币等配置。 |
| `DewLanMode` | LanMode / 局域网模式 | 1.4.5 | 提供局域网联机、房间发现和 IP 直连，保留官方联机模式。 |
| `DewLobbyListEnhance` | 大厅列表增强 | 1.0 | 为大厅列表增加难度筛选和状态信息。 |
| `DewModConfigListSupport` | Mod配置界面列表支持 / ModConfigListSupport | 1.0.0 | 为 Mod 配置界面增加 `List` 类型支持。 |
| `DewMorePlayers` | 更多的玩家人数 / MorePlayers | 1.0.1 | 房主侧自定义玩家数量。 |
| `DewMoreVision` | 无限视距 / MoreVision | 1.1.0 | 客户端扩展摄像机视野范围，并支持调整缩放步长。 |
| `DewPrimusHand` | 普里穆斯之手 / PrimusHand | 1.2.0 | 调整怪物、Boss 与战斗强度相关参数。 |
| `DewRoomGuidance` | 房间指引 / Room Guidance | 1.0.0 | 显示屏幕外可用祭坛及未拾取宝石、技能和神器的方向。 |
| `DewSafeShare` | SafeShare / 安全共享 | 1.0.0 | 掉落物在玩家主动标记前仅所有者可见。 |
| `DewShieldModDetection` | ShieldModDetection | 1.0.0 | 安装本地 Mod 时允许跨平台联机，保留原有玩法 Mod 标识。 |
| `DewSuperSmart` | SuperSmart 超级智能 | 1.0.0 | 显示攻击/技能范围和怪物/飞弹威胁区域，支持按住闪避键自动规避。 |
| `DewUnLock` | 一键解锁 / UnLock | 1.0.1 | 通过 F11 工具窗口解锁本地存档内容与进度。 |
| `DewVascularThief` | 血管小偷 / Vascular Thief | 1.0.0 | 新增可窃取 Boss 能力的专属技能。 |
| `DewZoneTwistedPath` | 区域重排 / ZoneTwistedPath | 1.0.0 | 房主侧重排区域顺序。 |

## DewSuperSmart / SuperSmart 超级智能

`DewSuperSmart` 是本地客户端战斗辅助显示与自动躲避 Mod，不修改房主规则，主要用于提升怪物技能、飞弹和英雄技能范围的可读性。

### 显示功能

- 绘制本地英雄普通攻击范围。
- 分别绘制英雄 Q、W、E、R、位移技能和身份技能范围。
- 绘制怪物攻击与技能预测范围，形状包括圆形、扇形和方框/直线区域。
- 绘制敌方飞弹路径和碰撞范围，飞弹以方框/直线威胁区域显示。
- 威胁绘制层使用置顶材质，尽量避免被场景、地形或单位遮挡。

### 威胁颜色

未释放的怪物攻击/技能预览固定显示为绿色，不参与距离和时间判定。

已释放/正在释放的威胁会同时根据角色距离和预计命中时间判定颜色，并取更危险的等级：

- 绿色：距离或命中时间大于 `2.0`。
- 黄色：距离 `<= 1.8`，或预计 `<= 1.8` 秒命中。
- 红色：距离 `<= 0.8`，或预计 `<= 0.8` 秒命中。

距离判定使用角色碰撞半径加威胁 padding 后，到威胁区域边缘的距离。

### 自动躲避

- 默认按键为 `None`，持续自动躲避；设置其他按键后按住启用。
- 自动采集怪物攻击、怪物正在释放的技能区域、已释放技能实例和敌方飞弹威胁。
- 优先使用普通移动规避；预测普通移动不安全且命中迫近时才尝试位移技能。
- 会在角色周围采样安全点，评估终点风险、路径风险和预计命中时间后执行躲避；命中时间越近，搜索半径会自适应扩大。
- 面对怪物定向攻击时优先选择侧向安全点，但复杂地形下会保留其他可达点作为退路；导航优先使用线性扫掠 API，失败时回退最近合法点。
- 位移技能参数优先读取通用 `Ai_GenericDodge` API，自定义技能才使用字段回退，减少反射和版本差异造成的误判。
- `Dodge Interval / 躲避间隔` 默认 `0.05` 秒，内部最低保护值为 `0.05` 秒。

### 躲避等级

`AutoDodgeLevel / 躲避等级` 用于控制自动躲避触发范围：

- `Green`：响应已释放/正在释放威胁，距离或预计命中时间阈值为 `1.5`。
- `Yellow`：距离或预计命中时间阈值为 `1.0`。
- `Red`：距离或预计命中时间阈值为 `0.5`。

### 配置项

当前公开配置项：

- `ShowAttackRange`：显示普攻范围。
- `ShowQRange`：显示 Q 技能范围。
- `ShowWRange`：显示 W 技能范围。
- `ShowERange`：显示 E 技能范围。
- `ShowRRange`：显示 R 技能范围。
- `ShowMovementRange`：显示位移技能范围。
- `ShowIdentityRange`：显示身份技能范围。
- `ShowMonsterThreatRanges`：显示怪物威胁范围。
- `ShowProjectileThreatRanges`：显示飞弹威胁范围。
- `EnableAutoDodge`：启用自动躲避。
- `AutoDodgeUseMovementSkill`：普通移动不安全时紧急使用位移技能。
- `AutoDodgeLevel`：选择红/黄/绿躲避等级。
- `AutoDodgeCommandInterval`：自动躲避指令间隔。
- `AutoDodgeKey`：自动躲避按键。

### 本地化

`DewSuperSmart/i18n` 提供 13 个语言 JSON：

`de-DE`、`en-US`、`es-MX`、`fr-FR`、`it-IT`、`ja-JP`、`ko-KR`、`pl-PL`、`pt-BR`、`ru-RU`、`tr-TR`、`zh-CN`、`zh-TW`。

## Mod 开发规范

仓库现有 21 个 Mod 和 `DewTestCode` 测试项目（共 22 个项目），所有项目及后续新增代码统一遵循以下规范。`DewTestCode` 没有 `ModBehaviour` 入口，其元数据中的 `assemblies` 保持为空，仅用于开发测试。

### 命名规范

- **项目名称统一以 `Dew` 开头**：项目目录、`.csproj` 文件名、解决方案项目名、`AssemblyName`、`RootNamespace`、`ModBehaviour` 入口类及文件名保持一致，例如 `DewRoomGuidance/DewRoomGuidance.csproj` 和 `src/DewRoomGuidance.cs`。原 `RoomGuidance`、`GoldenBurstAutoTarget` 分别使用 `DewRoomGuidance`、`DewGoldenBurstAutoTarget`。
- **Steam 展示名称移除 `Dew` 前缀**：`about/metadata.json` 的 `name`、Steam 创意工坊标题和 `about/description.txt` 中的展示标题使用无前缀名称，例如 `Room Guidance`、`SafeShare` 或对应中文名称。双语标题的各语言名称均遵循此规则；描述中引用的实际项目路径和技术标识保持准确。
- **重命名保留已有身份**：不得因纯命名调整修改 metadata `id`、创意工坊 `publishedfileid.txt`、既有配置文件路径或序列化配置键。入口类改名之前检查游戏如何生成配置文件名；默认路径受入口名影响时，通过重写 `GetModConfigFilePath` 保留旧文件名，继续读取已有设置。命名空间或类型改名对存档的影响以实际序列化方式为准。
- **引用同步更新**：重命名时同步更新源码引用、解决方案和项目路径、入口类、程序集信息、打包路径、构建脚本与文档。除非发布要求另有规定，纯命名调整不单独增加版本号。

### 源码目录规范

1. **根目录齐全**：每个 Mod 根目录必须包含 `about/`、`i18n/`、`src/`。`about/` 至少包含 `metadata.json` 和 `description.txt`；预览图与图标分别放在 `about/preview.png`、`about/icon.png`。暂时没有翻译文本时也保留 `i18n/`，可用 `.gitkeep` 跟踪空目录。
2. **源码集中**：全部维护的 C# 源码迁入 `src/`，包括 Mod 入口、辅助类与 `Properties/AssemblyInfo.cs`；`bin/`、`obj/` 中的构建产物不纳入迁移。项目文件保留在 Mod 根目录，运行时资源仍放在根目录的 `about/`、`i18n/` 等资源目录。
3. **配置与本地化**：配置代码放在 `src/config/`，包含配置类（通常为 `PluginConfig.cs`）及 `LocalizationSource.cs`。配置分组、标签、说明使用本地化键，并在根目录 `i18n/<语言区域代码>.json` 中维护对应文本，例如 `zh-CN.json`、`en-US.json`。所有维护中的语言文件应同步更新键与格式化占位符。
4. **统一本地化入口**：加载、查询和 UI 本地化统一使用 `LocalizationSource`，迁移已有其他命名或分散实现并更新调用点。入口在使用文本前调用 `LocalizationSource.Init(this)`；文本查询使用 `GetLocalizationText`，配置控件可在 `BuildWidgets` 中调用 `LocalizeUI`。从 Mod 根目录加载 `i18n/`，文件读取显式指定 UTF-8；保留缺失语言、缺失键的回退处理。
5. **补丁独立**：Harmony patch 类统一迁入 `src/patch/`，按目标或功能拆分；补丁只负责拦截与转发，UI 和控制流程放入各自目录。
6. **UI 与控制器分离**：视图、控件和显示交互代码放入 `src/ui/`，控制器、状态与流程协调代码放入 `src/controller/`，公共工具放入 `src/util/`。其他功能目录同样位于 `src/` 下。

### 迁移验收

- 同步更新旧式 `.csproj` 中的 `Compile`、`Content`、相关资源路径及 `AppDesignerFolder`（指向 `src\Properties`），移除失效条目，避免漏编译或重复编译。
- 检查迁移涉及的命名空间、`using`、反射类型名与本地化调用点；目录迁移本身不要求变更对外类型名、Mod ID、配置键或程序集名称。
- 确认根目录不再遗留维护中的 `.cs` 或旧源码目录，补丁、UI、controller 已分别归档。
- 检查本地化 JSON 可解析、键与占位符匹配，并构建受影响项目的 Release 配置；涉及控件、生命周期或 Harmony 的变更还需进行游戏内验证。
- 函数和特殊逻辑添加中文注释，日志尽量使用中文，所有文本保持 UTF-8。

代理开发时还应遵循 [开发技能中的项目约定](.agents/skills/develop-dew-mods/references/project-conventions.md)。

本地化入口的独立行为检查（使用最小游戏对象替身，覆盖中文、回退、格式化和重复初始化）：

```powershell
pwsh -NoProfile -File .\scripts\Test-Localization.ps1
```

此检查覆盖本次新增或重命名的四个入口；UI 布局、Harmony 回调与多人行为仍需游戏内验证。

## 常见问题

**找不到游戏程序集**

确认 `ShapeOfDreamsHome` 指向《Shape of Dreams》的安装根目录，而不是 `Shape of Dreams_Data` 子目录。

**脚本找不到 MSBuild**

修改 `build.ps1` 中的 `$msbuild` 路径，使其指向你本机 Visual Studio 安装目录下的 `MSBuild.exe`。

**中文显示乱码**

确认文件编码为 UTF-8。使用 Windows PowerShell 5.1 读取中文文件时可加上 `-Encoding UTF8`。
