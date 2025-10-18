using System;
using System.Collections.Generic;
using DewDifficultyCustomizeMod.i18n;
using UnityEngine;

namespace DewDifficultyCustomizeMod.ui
{
    public static class ConfigArrayDrawer
    {
        // 搜索关键字缓存列表
        private static readonly List<string> _skillRemoveSearchKeywords = new();
        private static readonly List<string> _gemRemoveSearchKeywords = new();
        private static readonly List<string> _skillStartSearchKeywords = new();
        private static readonly List<string> _gemStartSearchKeywords = new();

        // 搜索结果缓存
        private static readonly List<(string id, string name)> SkillSearchResults = new();
        private static readonly List<(string id, string name)> GemSearchResults = new();

        // ======================
        // 开局技能与等级数组
        // ======================
        public static void DrawStartSkillAndLevelArray(ref string[] skills, ref int[] levels)
        {
            GUILayout.Label(LocalizationConfig.Get("section_resources_start_skills"));

            // 确保搜索关键词缓存与数组长度一致
            while (_skillStartSearchKeywords.Count < skills.Length) _skillStartSearchKeywords.Add("");
            while (_skillStartSearchKeywords.Count > skills.Length)
                _skillStartSearchKeywords.RemoveAt(_skillStartSearchKeywords.Count - 1);

            for (int i = 0; i < skills.Length; i++)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label($"{LocalizationConfig.Get("section_resources_label_skill")}{i + 1}",
                    GUILayout.Width(50));

                // 显示技能名称
                string name = SafeGetSkillName(skills[i]);
                GUILayout.Label(name, GUILayout.Width(120));

                // 等级输入框
                GUILayout.Label(LocalizationConfig.Get("level"), GUILayout.Width(35));
                string levelInput = GUILayout.TextField(levels[i].ToString(), GUILayout.Width(40));
                if (int.TryParse(levelInput, out int levelResult))
                    levels[i] = levelResult;

                // 搜索关键词输入框
                _skillStartSearchKeywords[i] = GUILayout.TextField(_skillStartSearchKeywords[i], GUILayout.Width(120));

                // 删除按钮
                if (GUILayout.Button(LocalizationConfig.Get("delete"), GUILayout.Width(50)))
                {
                    skills = RemoveAt(skills, i);
                    levels = RemoveAt(levels, i);
                    _skillStartSearchKeywords.RemoveAt(i);
                    GUILayout.EndHorizontal();
                    break;
                }

                GUILayout.EndHorizontal();

                // 搜索结果展示
                string keyword = _skillStartSearchKeywords[i];
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    SkillSearchResults.Clear();
                    foreach (var kv in DewSave.profileMain.skills)
                    {
                        string id = kv.Key;
                        string skillName = DewLocalization.GetSkillName(DewLocalization.GetSkillKey(id), 0);
                        if (id.Contains(keyword, StringComparison.InvariantCultureIgnoreCase) ||
                            skillName.Contains(keyword, StringComparison.InvariantCultureIgnoreCase))
                        {
                            SkillSearchResults.Add((id, skillName));
                        }
                    }

                    foreach (var result in SkillSearchResults)
                    {
                        if (GUILayout.Button($"{result.name} ({result.id})", GUILayout.Width(350)))
                        {
                            skills[i] = result.id;
                            _skillStartSearchKeywords[i] = ""; // 清空搜索输入框
                        }
                    }
                }
            }

            // 添加新项按钮
            if (GUILayout.Button(LocalizationConfig.Get("add"), GUILayout.Width(100)))
            {
                Array.Resize(ref skills, skills.Length + 1);
                Array.Resize(ref levels, levels.Length + 1);
                skills[^1] = "";
                levels[^1] = 3;
                _skillStartSearchKeywords.Add("");
            }
        }


        // ======================
        // 移除技能数组
        // ======================
        public static void DrawRemoveSkillArray(ref string[] skills)
        {
            GUILayout.Label(LocalizationConfig.Get("section_resources_remove_skills"));

            // 确保搜索关键词缓存与数组长度一致
            while (_skillRemoveSearchKeywords.Count < skills.Length) _skillRemoveSearchKeywords.Add("");
            while (_skillRemoveSearchKeywords.Count > skills.Length)
                _skillRemoveSearchKeywords.RemoveAt(_skillRemoveSearchKeywords.Count - 1);

            for (int i = 0; i < skills.Length; i++)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label($"{LocalizationConfig.Get("section_resources_label_skill")}{i + 1}",
                    GUILayout.Width(50));

                // 显示技能名称
                string name = SafeGetSkillName(skills[i]);
                GUILayout.Label(name, GUILayout.Width(120));

                // 搜索关键词输入框
                _skillRemoveSearchKeywords[i] =
                    GUILayout.TextField(_skillRemoveSearchKeywords[i], GUILayout.Width(180));

                // 删除按钮
                if (GUILayout.Button(LocalizationConfig.Get("delete"), GUILayout.Width(50)))
                {
                    skills = RemoveAt(skills, i);
                    _skillRemoveSearchKeywords.RemoveAt(i);
                    GUILayout.EndHorizontal();
                    break;
                }

                GUILayout.EndHorizontal();

                // 搜索结果展示
                string keyword = _skillRemoveSearchKeywords[i];
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    SkillSearchResults.Clear();
                    foreach (var kv in DewSave.profileMain.skills)
                    {
                        string id = kv.Key;
                        string skillName = DewLocalization.GetSkillName(DewLocalization.GetSkillKey(id), 0);
                        if (id.Contains(keyword, StringComparison.InvariantCultureIgnoreCase) ||
                            skillName.Contains(keyword, StringComparison.InvariantCultureIgnoreCase))
                        {
                            SkillSearchResults.Add((id, skillName));
                        }
                    }

                    foreach (var result in SkillSearchResults)
                    {
                        if (GUILayout.Button($"{result.name} ({result.id})", GUILayout.Width(350)))
                        {
                            skills[i] = result.id;
                            _skillRemoveSearchKeywords[i] = "";
                        }
                    }
                }
            }

            // 添加新项按钮
            if (GUILayout.Button(LocalizationConfig.Get("add"), GUILayout.Width(100)))
            {
                Array.Resize(ref skills, skills.Length + 1);
                skills[^1] = "";
                _skillRemoveSearchKeywords.Add("");
            }
        }


        // ======================
        // 开局精华与品质数组
        // ======================
        public static void DrawStartGemAndQualityArray(ref string[] gems, ref int[] qualities)
        {
            GUILayout.Label(LocalizationConfig.Get("section_resources_start_gems"));

            while (_gemStartSearchKeywords.Count < gems.Length) _gemStartSearchKeywords.Add("");
            while (_gemStartSearchKeywords.Count > gems.Length)
                _gemStartSearchKeywords.RemoveAt(_gemStartSearchKeywords.Count - 1);

            for (int i = 0; i < gems.Length; i++)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label($"{LocalizationConfig.Get("section_resources_label_gem")}{i + 1}", GUILayout.Width(50));

                // 显示宝石名称
                string name = SafeGetGemName(gems[i]);
                GUILayout.Label(name, GUILayout.Width(120));

                // 品质输入框
                GUILayout.Label(LocalizationConfig.Get("quality"), GUILayout.Width(45));
                string qualityInput = GUILayout.TextField(qualities[i].ToString(), GUILayout.Width(40));
                if (int.TryParse(qualityInput, out int qualityResult))
                    qualities[i] = qualityResult;

                // 搜索关键词输入框
                _gemStartSearchKeywords[i] = GUILayout.TextField(_gemStartSearchKeywords[i], GUILayout.Width(120));

                // 删除按钮
                if (GUILayout.Button(LocalizationConfig.Get("delete"), GUILayout.Width(50)))
                {
                    gems = RemoveAt(gems, i);
                    qualities = RemoveAt(qualities, i);
                    _gemStartSearchKeywords.RemoveAt(i);
                    GUILayout.EndHorizontal();
                    break;
                }

                GUILayout.EndHorizontal();

                // 搜索结果展示
                string keyword = _gemStartSearchKeywords[i];
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    GemSearchResults.Clear();
                    foreach (var kv in DewSave.profileMain.gems)
                    {
                        string id = kv.Key;
                        string gemName = DewLocalization.GetGemName(DewLocalization.GetGemKey(id));
                        if (id.Contains(keyword, StringComparison.InvariantCultureIgnoreCase) ||
                            gemName.Contains(keyword, StringComparison.InvariantCultureIgnoreCase))
                        {
                            GemSearchResults.Add((id, gemName));
                        }
                    }

                    foreach (var result in GemSearchResults)
                    {
                        if (GUILayout.Button($"{result.name} ({result.id})", GUILayout.Width(350)))
                        {
                            gems[i] = result.id;
                            _gemStartSearchKeywords[i] = "";
                        }
                    }
                }
            }

            // 添加新项按钮
            if (GUILayout.Button(LocalizationConfig.Get("add"), GUILayout.Width(100)))
            {
                Array.Resize(ref gems, gems.Length + 1);
                Array.Resize(ref qualities, qualities.Length + 1);
                gems[^1] = "";
                qualities[^1] = 40;
                _gemStartSearchKeywords.Add("");
            }
        }


        // ======================
        // 移除精华数组
        // ======================
        public static void DrawRemoveGemArray(ref string[] gems)
        {
            GUILayout.Label(LocalizationConfig.Get("section_resources_remove_gems"));

            while (_gemRemoveSearchKeywords.Count < gems.Length) _gemRemoveSearchKeywords.Add("");
            while (_gemRemoveSearchKeywords.Count > gems.Length)
                _gemRemoveSearchKeywords.RemoveAt(_gemRemoveSearchKeywords.Count - 1);

            for (int i = 0; i < gems.Length; i++)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label($"{LocalizationConfig.Get("section_resources_label_gem")}{i + 1}", GUILayout.Width(50));

                // 显示宝石名称
                string name = SafeGetGemName(gems[i]);
                GUILayout.Label(name, GUILayout.Width(120));

                // 搜索关键词输入框
                _gemRemoveSearchKeywords[i] = GUILayout.TextField(_gemRemoveSearchKeywords[i], GUILayout.Width(180));

                // 删除按钮
                if (GUILayout.Button(LocalizationConfig.Get("delete"), GUILayout.Width(50)))
                {
                    gems = RemoveAt(gems, i);
                    _gemRemoveSearchKeywords.RemoveAt(i);
                    GUILayout.EndHorizontal();
                    break;
                }

                GUILayout.EndHorizontal();

                // 搜索结果展示
                string keyword = _gemRemoveSearchKeywords[i];
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    GemSearchResults.Clear();
                    foreach (var kv in DewSave.profileMain.gems)
                    {
                        string id = kv.Key;
                        string gemName = DewLocalization.GetGemName(DewLocalization.GetGemKey(id));
                        if (id.Contains(keyword, StringComparison.InvariantCultureIgnoreCase) ||
                            gemName.Contains(keyword, StringComparison.InvariantCultureIgnoreCase))
                        {
                            GemSearchResults.Add((id, gemName));
                        }
                    }

                    foreach (var result in GemSearchResults)
                    {
                        if (GUILayout.Button($"{result.name} ({result.id})", GUILayout.Width(350)))
                        {
                            gems[i] = result.id;
                            _gemRemoveSearchKeywords[i] = "";
                        }
                    }
                }
            }

            if (GUILayout.Button(LocalizationConfig.Get("add"), GUILayout.Width(100)))
            {
                Array.Resize(ref gems, gems.Length + 1);
                gems[^1] = "";
                _gemRemoveSearchKeywords.Add("");
            }
        }


        // ======================
        // 辅助方法：从数组中删除指定索引元素
        // ======================
        private static T[] RemoveAt<T>(T[] array, int index)
        {
            var list = new List<T>(array);
            list.RemoveAt(index);
            return list.ToArray();
        }

        private static string SafeGetSkillName(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
                return skillId;
            string key = DewLocalization.GetSkillKey(skillId);
            var skillName = DewLocalization.GetSkillName(key, 0);
            if (string.IsNullOrEmpty(skillName))
            {
                return skillId;
            }

            return skillName;
        }

        private static string SafeGetGemName(string gemId)
        {
            if (string.IsNullOrEmpty(gemId))
                return gemId;
            var gemKey = DewLocalization.GetGemKey(gemId);
            var gemName = DewLocalization.GetGemName(gemKey);
            if (string.IsNullOrEmpty(gemName))
            {
                return gemId;
            }

            return gemName;
        }
    }
}