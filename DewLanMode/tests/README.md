# 续局流程回归测试

链接 LAN MOD 的实际续局补丁、网络设置补丁，以及未修改的 DewMorePlayers 续局补丁，使用游戏自带 Harmony 实际安装并执行补丁。游戏入口及窗口用替身模拟，不需要启动 Unity。

在仓库根目录使用 PowerShell 7：

```powershell
./build.ps1 -Project ./DewLanMode/tests/ContinueFlow.csproj -Configuration Release
./DewLanMode/tests/bin/Release/DewLanMode.ContinueFlowTests.exe
```

覆盖单独启用 LAN MOD、两种补丁注册顺序、各窗口只弹一次、保留人数与存档设置、取消后重试、官方模式与 LAN 模式、无关启动流程、加载中重复请求及异常后的状态清理。

此测试不验证 Unity 弹窗布局、实际存档加载或主客机连接，这些仍需游戏内验证。
