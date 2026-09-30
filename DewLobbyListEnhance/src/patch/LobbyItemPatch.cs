using TMPro;
using UnityEngine;
using HarmonyLib;

namespace DewLobbyListEnhance.patch;

[HarmonyPatch(typeof(UI_Title_FindLobby_LobbyItem))]
internal static class LobbyItemPatch
{
    [HarmonyPostfix, HarmonyPatch("Setup")]
    private static void SetupPostfix(UI_Title_FindLobby_LobbyItem __instance, LobbyInstance lobby)
    {
        Transform pingTransform = __instance.transform.Find("Ping");
        TextMeshProUGUI text = pingTransform != null
            ? pingTransform.Find("DewLobbyListEnhance.Info")?.GetComponent<TextMeshProUGUI>()
            : null;
        if (text == null)
        {
            if (pingTransform == null) return;
            // Ping 容器宽 294，图标在容器中部；文字排在图标左侧。
            GameObject info = new GameObject("DewLobbyListEnhance.Info", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            info.transform.SetParent(pingTransform, false);
            text = info.GetComponent<TextMeshProUGUI>();
            // 复制大厅项现有文本样式，保证附加信息与原列表字体、大小一致。
            TextMeshProUGUI source = __instance.descriptionText != null ? __instance.descriptionText : __instance.nameText;
            if (source != null)
            {
                text.font = source.font;
                text.fontSize = source.fontSize;
                text.fontStyle = source.fontStyle;
                text.characterSpacing = source.characterSpacing;
                text.lineSpacing = source.lineSpacing;
                text.enableAutoSizing = true;
                text.fontSizeMax = source.fontSize;
                text.fontSizeMin = source.fontSize * 0.75f;
            }
            text.color = __instance.nameText.color;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
        }
        // 原版 Setup 会切换延迟图标对象；每次重用列表项后恢复附加文本并置顶。
        text.gameObject.SetActive(true);
        text.enabled = true;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        // 状态信息比原平台标签更长，每次 Setup 都重设区域以适应复用项和已有文本对象。
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-210f, 0f);
        rect.sizeDelta = new Vector2(360f, 32f);
        text.transform.SetAsLastSibling();
        // 使用游戏源码确认存在的字段，避免依赖自定义 LobbyInstance 扩展字段。
        string platform = GetPlatform(lobby);
        string visibility = lobby.isInviteOnly ? "仅邀请" : "公开";
        string joinability = lobby.allowJoin ? "可加入" : "不可加入";
        string latency = GetExactLatency(lobby);
        text.text = string.IsNullOrEmpty(latency)
            ? $"{platform}  {visibility}  {joinability}"
            : $"{platform}  {latency}ms  {visibility}  {joinability}";
    }

    private static string GetPlatform(LobbyInstance lobby)
    {
        if (lobby.crossPlayGate == "STEAM") return "仅 Steam";
        if (lobby.crossPlayGate == "ALL") return "跨平台";
        return "平台未知";
    }

    private static string GetExactLatency(LobbyInstance lobby)
    {
        if (lobby.customData == null) return null;
        if (lobby.customData.TryGetValue("latencyMs", out string value) && int.TryParse(value, out int latency) && latency >= 0) return latency.ToString();
        if (lobby.customData.TryGetValue("pingMs", out value) && int.TryParse(value, out latency) && latency >= 0) return latency.ToString();
        return null;
    }

}
