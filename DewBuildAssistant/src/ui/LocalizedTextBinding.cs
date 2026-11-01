using System;
using TMPro;
using UnityEngine;

namespace DewBuildAssistant.ui;

// 保存翻译 key 或资源 key 的解析函数；接收游戏原生语言回调，避免再次翻译显示文本。
internal sealed class LocalizedTextBinding : MonoBehaviour, ILangaugeChangedCallback
{
    private TMP_Text _text;
    private Func<string> _resolve;

    public static void Bind(TMP_Text text, Func<string> resolve)
    {
        var binding = text.GetComponent<LocalizedTextBinding>() ?? text.gameObject.AddComponent<LocalizedTextBinding>();
        binding._text = text;
        binding._resolve = resolve;
        binding.OnLanguageChanged();
    }

    public void OnLanguageChanged()
    {
        if (_text != null && _resolve != null) _text.text = _resolve();
    }

    private void OnEnable() => OnLanguageChanged();
}
