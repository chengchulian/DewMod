using System.Collections.Generic;
using UnityEngine;

namespace DewBuildAssistant.ui;

// 战绩与大厅共用的原生技能栏素材；不依赖 Unity 编辑器内置资源或局内场景。
internal sealed class NativeSkillSprites
{
    public Sprite Mask;
    public Sprite Background;
    public Sprite SkillBorder;
    public Sprite GemBorder;

    public static bool TryResolve(IEnumerable<Sprite> available, out NativeSkillSprites result)
    {
        result = null;
        if (available == null) return false;
        var sprites = new Dictionary<string, Sprite>();
        foreach (var sprite in available)
        {
            if (sprite == null) continue;
            switch (sprite.name)
            {
                case "ActionButton_Background":
                case "ActionButton_Border":
                case "ActionBar_Level_Background":
                case "ActionBar_Level_Border":
                    sprites[sprite.name] = sprite;
                    break;
            }
        }
        if (!sprites.TryGetValue("ActionButton_Background", out var mask) ||
            !sprites.TryGetValue("ActionButton_Border", out var border) ||
            !sprites.TryGetValue("ActionBar_Level_Background", out var background)) return false;
        result = new NativeSkillSprites
        {
            Mask = mask,
            Background = background,
            SkillBorder = border,
            GemBorder = sprites.TryGetValue("ActionBar_Level_Border", out var gemBorder) ? gemBorder : border
        };
        return true;
    }
}
