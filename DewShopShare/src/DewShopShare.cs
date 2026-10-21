using DewShopShare.controller;
using UnityEngine;

namespace DewShopShare;

public sealed class DewShopShare : ModBehaviour
{
    // 声明玩法改动并安装补丁；具体处理入口只在服务端修改库存。
    private void Start()
    {
        instance.isAlteringGameplay = true;
        harmony.PatchAll(typeof(DewShopShare).Assembly);
        CallOnNetworkedManager<PingManager>(ShopShareController.Clear, ShopShareController.Clear);
        Debug.Log("[DewShopShare] 商店共享已加载：房主安装后，标记商品会追加到所有人的当前商店。");
    }

    // 卸载时撤销本 Mod 的补丁与状态。
    private void OnDestroy()
    {
        harmony.UnpatchAll(harmony.Id);
        ShopShareController.Clear();
    }
}
