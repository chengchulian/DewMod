using DewBuildAssistant.config;
using DewBuildAssistant.controller;
using UnityEngine;

namespace DewBuildAssistant;

public sealed class DewBuildAssistant : ModBehaviour
{
    public PluginConfig Config = new();
    private BuildAssistantController _controller;

        // 将工坊助手绑定到房间大厅生命周期，退出时清理请求和窗口。
    private void Start()
    {
        LocalizationSource.Init(this);
        _controller = gameObject.AddComponent<BuildAssistantController>();
        _controller.Config = Config;
        CallOnManager<PlayLobbyManager>(() => _controller.EnterLobby(), () =>
        {
            if (_controller != null) _controller.LeaveLobby();
        });
        Debug.Log("[BD 助手] 已加载，支持创意工坊 Build 上传下载与当前栏位配装");
    }

    private void OnDestroy()
    {
        if (_controller != null)
        {
            _controller.LeaveLobby();
            Destroy(_controller);
        }
    }
}
