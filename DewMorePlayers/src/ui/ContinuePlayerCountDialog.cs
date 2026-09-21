using System.Globalization;
using DewMorePlayers.config;
using DewMorePlayers.patch;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DewMorePlayers;

internal static class ContinuePlayerCountDialog
{
    private static ContinuePlayerCountDialogController _controller;

    public static void Show(TitleManager titleManager)
    {
        if (_controller != null)
        {
            _controller.transform.SetAsLastSibling();
            _controller.FocusInput();
            return;
        }

        UI_Window window = Object.Instantiate(DewGUI.widgetWindow, DewGUI.canvasTransform);
        window.name = DewMorePlayersUiCleanup.ContinueDialogName;
        window.isDraggable = false;
        window.enableBackdrop = true;
        window.SetWidth(760f);

        _controller = window.gameObject.AddComponent<ContinuePlayerCountDialogController>();
        _controller.Initialize(window, titleManager);
        Debug.Log($"[DewMorePlayers] Continue player-count dialog shown: {DewMorePlayers.MaxPlayers}.");
    }

    public static void Close()
    {
        if (_controller == null)
        {
            return;
        }

        Object.Destroy(_controller.gameObject);
        _controller = null;
    }
}
