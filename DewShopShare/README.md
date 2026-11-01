# 商店共享 / Shop Share

仅房主安装。任何玩家使用原版标记操作标记在售商品，该商品就会追加到其他玩家在同一商人的商店中。客机使用原版界面和网络消息，无需安装 Mod。

- 保留每个人原有的商品，复制标记时的剩余数量、等级/品质、基础价格和附加数据。
- 原商品与全队共享条目共用一份库存。任意玩家买走后，所有人的数量一起减少；库存为 1 时全队合计只能购买 1 份。
- 扣款、个人折扣和物品生成使用原生购买流程。余额不足或购买条件失败时不扣共享库存，售罄后的过期请求不会重复发货。
- 同一原商品重复标记、标记共享副本都不再追加。同款但独立刷出的商品仍算独立来源。
- 个人刷新保留当前商人的共享商品及全队剩余数量，已售罄条目不会补货。标记者刷新后也保留旧共享商品及其剩余数量。
- 后加入玩家获得当前商人的共享商品及当前剩余数量；商人销毁或退出联机时清理共享来源记录。
- 原版存档会保存商品数组。共享来源记录仅保留在本次房主运行中，重启读档后重新开始记录。

## 安装

将发布包中的 `DewShopShare` 文件夹放入游戏目录的 `Mods` 文件夹，在房主的 Mod 管理器中启用“商店共享 / Shop Share”，再创建房间。客机按平常方式加入。

发布包目录包含 `about/metadata.json`、`i18n/` 和 `obj/Release/DewShopShare.dll`，请保留目录结构。也可以在开发模式下直接加载本项目目录。

## 构建和验证

设置 `ShapeOfDreamsHome` 后，在仓库根目录运行：

```powershell
.\build.ps1 -Project .\DewShopShare\DewShopShare.csproj -Configuration Release
.\build.ps1 -Project .\DewShopShare\tests\RegressionHarness.csproj -Configuration Release
& .\DewShopShare\tests\bin\Release\DewShopShare.RegressionTests.exe
```

若本机 `build.ps1` 的 MSBuild 固定路径失效，使用 Visual Studio Installer 的 `vswhere.exe -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe'` 查找本机 MSBuild，再以相同参数构建上述项目。

回归程序直接链接生产库存状态、控制器和商人补丁，并使用游戏自带的 Harmony 对游戏替身执行真实补丁。覆盖全队扣库存、原商品和共享条目的相互同步、最后一份的竞争请求、购买失败、事件重入、购买事件中的刷新、异常后解锁、刷新售罄、晚加入、重复传播、相同商品不同来源及服务端权限。测试不验证实际网络传输或 Unity 界面。

游戏内验收：房主启用 Mod，至少一名客机保持原版；双方分别标记并购买商品，确认原有商品保留、原商品和共享条目购买后全队数量同步减少、最后一份全队仅购买一次、购买失败不扣库存、重复标记去重；再验证刷新、后加入、换房、商人销毁和退出后重新开局。大量共享商品时还需观察原版商店布局。

## 实现依据

参考 `GameSource/code/Dewr.1.4.0.13_s`：

- `Dew.Core/PingManager.cs`：原生指令先检查连接及商品下标、设置真实标记者，再调用 `BroadcastPing`；补丁在该 RPC 发送入口处理已校验的标记。
- `Dew.Core/PropEnt_Merchant_Base.cs`：`merchandises` 是原生 `SyncDictionary<string, MerchandiseData[]>`。追加时写回新数组，由原版同步；`CmdPurchase` 根据个人 GUID 和下标完成购买。
- 实际购买入口为 Mirror 生成的 `UserCode_CmdPurchase__Int32__NetworkConnectionToClient`。前缀检查房主共享库存并锁定同一商品，Finalizer 根据原生实际扣除的数量同步全队并释放锁；余额不足等提前返回不消耗库存。购买事件若立即刷新商店，在覆盖原数组前先结算该次扣库存。
- `Dew.UI/UI_InGame_FloatingWindow_Shop.cs`：原版界面按个人数组长度动态生成商品格子，支持服务端追加商品。

未新增网络消息、网络组件、物品类型或客机 UI。游戏内的跨平台联机限制仍遵循游戏自身的 Mod 设置。
