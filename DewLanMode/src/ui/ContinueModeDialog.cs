using UnityEngine;

namespace DewLanMode;

internal static class ContinueModeDialog
{
    private static ContinueModeDialogController _controller;

    // 保留本次续局生成的完整网络设置，不重新读取存档或重跑其他 MOD 的入口。
    public static void Show(TransitionManager transitionManager, DewNetworkStartSettings settings)
    {
        if (_controller != null)
        {
            _controller.transform.SetAsLastSibling();
            return;
        }

        UI_Window window = Object.Instantiate(DewGUI.widgetWindow, DewGUI.canvasTransform);
        window.name = "DewLanMode - Continue Mode Dialog";
        window.isDraggable = false;
        window.enableBackdrop = true;
        window.SetWidth(760f);
        _controller = window.gameObject.AddComponent<ContinueModeDialogController>();
        _controller.Initialize(window, transitionManager, settings);
    }

    public static void Close()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.gameObject.SetActive(false);
        Object.Destroy(_controller.gameObject);
        _controller = null;
    }
}
