using System;
using System.Globalization;
using System.Text;

namespace DewBuildAssistant.util;

internal static class BuildAppliedAnnouncement
{
    // 每次发送读取游戏当前语言；为固定话术和署名预留长度，不截断 Unicode 字符簇。
    public static string Format(string heroKey, string title, int maxLength)
    {
        const string action = "]一键星座 ";
        const string suffix = " 已应用。 --Bd助手mod";
        int budget = maxLength - 1 - action.Length - suffix.Length;
        if (budget < 2) throw new ArgumentOutOfRangeException(nameof(maxLength));
        DewLocalization.TryGetUIValue(heroKey + "_Name", out string localizedHero);
        string hero = Clean(localizedHero);
        if (string.IsNullOrEmpty(hero)) hero = Clean(heroKey);
        hero = Fit(hero, budget / 2);
        return "[" + hero + action + Fit(Clean(title), budget - hero.Length) + suffix;
    }

    // 工坊标题按普通单行文字展示，避免换行或富文本标签干扰聊天布局。
    private static string Clean(string text)
    {
        var result = new StringBuilder();
        foreach (char c in text ?? "")
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                if (result.Length > 0 && result[result.Length - 1] != ' ') result.Append(' ');
            }
            else result.Append(c == '<' ? '＜' : c == '>' ? '＞' : c);
        }
        return result.ToString().Trim();
    }

    private static string Fit(string text, int limit)
    {
        if (text.Length <= limit) return text;
        var elements = StringInfo.GetTextElementEnumerator(text);
        int end = 0;
        while (elements.MoveNext())
        {
            int next = elements.ElementIndex + elements.GetTextElement().Length;
            if (next > limit) break;
            end = next;
        }
        return text.Substring(0, end);
    }
}
