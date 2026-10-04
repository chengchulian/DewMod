# 自动躲避几何回归

无需启动游戏，使用 .NET Framework 4.8.1 开发包和 Visual Studio MSBuild：

```powershell
pwsh -NoProfile -File DewSuperSmart/tests/run.ps1
```

`RegressionHarness.csproj` 直接链接生产的 `ThreatZone.cs`、`ThreatPathSafety.cs`、`TreantPowerBombPreview.cs` 和 `AutoDodgeController.cs`，不复制其算法。用确定的几何尺寸验证边缘、角色半径、补偿距离，以及细线穿越、相邻威胁、当前重叠、凹多边形再次进入等路径。

`ControllerTests.cs` 通过反射执行真实控制器的 `Update` 与私有入口，检查移动命令去重、超过卡住超时但持续行进时复用目标、中断后恢复、到达后不被危险鼠标位置拉回、附近多个未相交威胁不触发躲避、新威胁/障碍使旧路径失效，以及飞弹落点和未达到触发等级的投射物安全检查。

`TreantPowerBombPreviewTests.cs` 检查树灵爆破预警读取实际圆形伤害碰撞体和引导结束后的伤害延迟；普攻测试检查远离怪物的安全落点和紧急闪避时机。

`MonsterRushProjectionTests.cs` 检查叶犬碰撞体独立移动、旋转、缩放和远端陈旧坐标时的投影，以及普通冲撞保留局部偏移。实际联机显示仍需游戏内验证。

`UnityStubs.cs`、`GameStubs.cs`、`ControllerStubs.cs` 仅提供编译、基础平面数学、可控输入/时钟/导航结果及移动指令计数。配置类使用测试桩，不链接生产 `PluginConfig`。真实 Unity 原生物理、场景发现、导航寻路、网络指令和角色运动不在本测试范围内；调用物理或场景发现桩会直接抛出异常。游戏内的碰撞体同步、实际导航和实际移动仍需运行游戏确认。
