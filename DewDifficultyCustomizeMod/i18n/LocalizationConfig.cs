// 国际化语言表配置类

using System.Collections.Generic;
using DewDifficultyCustomizeMod.util;

namespace DewDifficultyCustomizeMod.i18n
{
    public static class LocalizationConfig
    {
        // key: 语言代码 
        // value: 键值对映射
        public static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
        {
            ["zh-CN"] = new()
            {
                ["config_editor_title"] = $"难度自定制 v{AttrCustomizeConstant.PluginVersion}（F8 显示/隐藏） qq群: 866951784",
                ["save_config"] = "💾 保存配置",
                ["reset_config"] = "🔄 重置配置",
                ["section_general"] = "🎮 通用设置",
                ["section_shield"] = "🎮 护盾设置",
                ["section_enemy"] = "👾 敌人设置",
                ["section_boss"] = "👹 Boss 设置",
                ["section_skillslot"] = "🔮 技能与精华槽",
                ["section_toggles"] = "⚙️ 游戏机制开关",
                ["section_resources"] = "⚙️ 游戏内资源相关",
                ["add"] = "➕ 添加",
                ["delete"] = "删除",
                ["search"] = "搜索",
                ["level"] = "等级",
                ["quality"] = "品质",
                ["section_resources_start_skills"] = "开局技能与等级",
                ["section_resources_remove_skills"] = "移除技能",
                ["section_resources_start_gems"] = "开局精华与品质",
                ["section_resources_remove_gems"] = "移除精华",
                ["section_resources_label_gem"] = "精华",
                ["section_resources_label_skill"] = "技能",
                ["label_max_players"] = "房间最大人数",
                ["label_population_multiplier"] = "人口过剩人口倍数",
                ["label_shop_items"] = "商店物品数量",
                ["label_shop_refreshes"] = "商店刷新次数",
                ["label_first_visit_gold"] = "未访问过的图发钱数量",
                ["label_first_visit_gold_loop"] = "未访问过的图发钱数量每周目添加数量",
                ["label_first_visit_gold_zone"] = "未访问过的图发钱数量每关添加数量",
                ["label_beneficial_node_multiplier"] = "引导祭坛数量倍数",
                ["label_heal_raw_multiplier"] = "治疗效果百分比",
                ["label_num_of_nodes"] = "节点数量",
                ["label_num_of_merchants"] = "商人节点数量",
                
                

                ["label_enemy_movement_speed"] = "所有敌人移动速度百分比",
                ["label_enemy_attack_speed"] = "所有敌人攻击速度百分比",
                ["label_enemy_ability_haste"] = "所有敌人技能急速百分比",
                ["label_little_monster_health"] = "小怪生命百分比",
                ["label_little_monster_damage"] = "小怪伤害百分比",
                ["label_miniboss_health"] = "miniBoss生命百分比",
                ["label_miniboss_damage"] = "miniBoss伤害百分比",
                ["label_monster_base_armor"] = "怪物基础护甲",
                ["label_monster_armor_percentage_add_by_zone"] = "怪物每关增加基础护甲百分比",
                ["label_enemy_health_growth"] = "怪物额外生命成长倍率(此数值为 n^当前关卡数)",
                ["label_enemy_health_growth_example"] = "例: 怪物原始血量为100 当前关卡数为10 值为2 血量为100*2^10",
                ["label_enemy_damage_growth"] = "怪物额外伤害成长倍率(此数值为 n^当前关卡数)",
                ["label_monster_mirage_chance"] = "小怪产生紫皮概率倍数",

                ["label_boss_count"] = "Boss数量",
                ["label_boss_count_loop"] = "每周目添加Boss数量",
                ["label_boss_count_zone"] = "每关添加Boss数量",
                ["label_boss_health"] = "Boss生命百分比",
                ["label_boss_damage"] = "Boss伤害百分比",
                ["label_boss_injury_limit"] = "Boss单次受伤血量百分比",
                ["label_boss_mirage_chance"] = "Boss幻想化概率",
                ["label_boss_hunter_chance"] = "Boss猎手化概率",

                ["label_skill_q_gem_count"] = "Q技能精华槽数量",
                ["label_skill_w_gem_count"] = "W技能精华槽数量",
                ["label_skill_e_gem_count"] = "E技能精华槽数量",
                ["label_skill_r_gem_count"] = "R技能精华槽数量",
                ["label_skill_identity_gem_count"] = "身份技能精华槽数量",
                ["label_skill_movement_gem_count"] = "位移技能精华槽数量",

                ["label_enable_gem_merge"] = "启用精华合并",
                ["label_enable_all_skill_edit"] = "启用全技能编辑",
                ["label_enable_hero_skill_add_shop"] = "开启转职",
                ["label_enable_mist_allow_any_direction"] = "薄雾全方位招架",
                ["label_enable_world_reveal"] = "开启千里眼",
                ["label_enable_boss_spawn_all_once"] = "开启所有boss一次性生成",
                ["label_enable_artifact_quest"] = "开启遗物任务",
                ["label_enable_fragment_of_radiance_boss_quest"] = "开启光辉BOSS任务",
                ["label_enable_health_reduce_multiplier_add_by_zone"] = "开启幻想上限每周关增加",
                ["label_enable_current_node_generate_lost_soul"] = "开启当前节点生成迷失灵魂",
                ["label_enable_boss_room_generate_lost_soul"] = "开启Boss房生成迷失灵魂",
                ["label_enable_quest_hunted_by_obliviax_repeatable"] = "开启遗忘猎手任务可重复生成",
                ["label_enable_damage_ranking"] = "开启每关发送伤害排行榜",
                
                ["label_disable_deja_vu"] = "禁用既视感",

                ["label_max_shield_multiplier"] = "最大护盾量百分比",
                ["label_shield_cool_down_sceonds"] = "护盾冷却时间（秒）",
                ["label_igore_shield_cool_down_from_others"] = "队友护盾不占用护盾冷却",




            },
            ["en-US"] = new()
            {
                ["config_editor_title"] = $"Dew Customize v{AttrCustomizeConstant.PluginVersion} (Press F8 to toggle)",
                ["save_config"] = "💾 Save Config",
                ["reset_config"] = "🔄 Reset Config",
                ["section_general"] = "🎮 General Settings",
                ["section_shield"] = "🎮 Shield Settings",
                ["section_enemy"] = "👾 Enemy Settings",
                ["section_boss"] = "👹 Boss Settings",
                ["section_skillslot"] = "🔮 Skill & Gem Slots",
                ["section_toggles"] = "⚙️ Gameplay Toggles",
                ["section_resources"] = "⚙️ In-Game Resources",
                ["add"] = "➕ Add",
                ["delete"] = "Delete",
                ["search"] = "Search",
                ["level"] = "Level",
                ["quality"] = "Quality",
                ["section_resources_start_skills"] = "Starting Skills and Level",
                ["section_resources_remove_skills"] = "Remove Skills",
                ["section_resources_start_gems"] = "Starting Gems and Quality",
                ["section_resources_remove_gems"] = "Remove Gems",
                ["section_resources_label_gem"] = "gem",
                ["section_resources_label_skill"] = "skill",
                
                ["label_max_players"] = "Max players",
                ["label_population_multiplier"] = "Overpopulation multiplier",
                ["label_shop_items"] = "shop items",
                ["label_shop_refreshes"] = "Shop refresh count",
                ["label_first_visit_gold"] = "Gold on first visit",
                ["label_first_visit_gold_loop"] = "Gold per loop",
                ["label_first_visit_gold_zone"] = "Gold per zone",
                ["label_beneficial_node_multiplier"] = "Guide Shrine Count Multiplier",
                ["label_heal_raw_multiplier"] = "Heal Raw Multiplier",
                ["label_num_of_nodes"] = "Number of nodes",
                ["label_num_of_merchants"] = "Number of merchant nodes",
                
                
                
                ["label_enemy_movement_speed"] = "Enemy move speed (%)",
                ["label_enemy_attack_speed"] = "Enemy attack speed (%)",
                ["label_enemy_ability_haste"] = "Enemy ability haste (%)",
                ["label_little_monster_health"] = "Mob health (%)",
                ["label_little_monster_damage"] = "Mob damage (%)",
                ["label_miniboss_health"] = "Mini-boss health (%)",
                ["label_miniboss_damage"] = "Mini-boss damage (%)",
                ["label_monster_base_armor"] = "monster basic armor",
                ["label_monster_armor_percentage_add_by_zone"] = "monster add basic armor percentage by zone",
                ["label_enemy_health_growth"] = "Enemy health growth (n^zone)",
                ["label_enemy_health_growth_example"] = "eg:monster original hp is 100,current zone 10 value 2 than hp 100*2^10",
                ["label_enemy_damage_growth"] = "Enemy damage growth (n^zone)",
                ["label_monster_mirage_chance"] = "Mob mirage chance multiplier",

                
                ["label_boss_count"] = "Boss count",
                ["label_boss_count_loop"] = "Boss count per loop",
                ["label_boss_count_zone"] = "Boss count per zone",
                ["label_boss_health"] = "Boss health (%)",
                ["label_boss_damage"] = "Boss damage (%)",
                ["label_boss_injury_limit"] = "Boss max injury per hit (%)",
                ["label_boss_mirage_chance"] = "Boss mirage chance (%)",
                ["label_boss_hunter_chance"] = "Boss hunter chance (%)",

                ["label_skill_q_gem_count"] = "Q skill gem slots",
                ["label_skill_w_gem_count"] = "W skill gem slots",
                ["label_skill_e_gem_count"] = "E skill gem slots",
                ["label_skill_r_gem_count"] = "R skill gem slots",
                ["label_skill_identity_gem_count"] = "Identity skill gem slots",
                ["label_skill_movement_gem_count"] = "Movement skill gem slots",
                ["label_enable_gem_merge"] = "Enable gem merge",
                ["label_enable_all_skill_edit"] = "Enable all skill edit",
                
                ["label_enable_hero_skill_add_shop"] = "Enable hero job change",
                ["label_enable_mist_allow_any_direction"] = "Mist parry any direction",
                ["label_enable_world_reveal"] = "Reveal full map",
                ["label_enable_boss_spawn_all_once"] = "Spawn all bosses at once",
                ["label_enable_artifact_quest"] = "Enable artifact quest",
                ["label_enable_fragment_of_radiance_boss_quest"] = "Enable Radiance Boss Quest",
                ["label_enable_health_reduce_multiplier_add_by_zone"] = "Enable Health Reduce Multiplier Add By Zone",
                ["label_enable_current_node_generate_lost_soul"] = "Enable Current Node Generate Lost Soul",
                ["label_enable_boss_room_generate_lost_soul"] = "Enable Boss Room Generate Lost Soul",
                ["label_enable_quest_hunted_by_obliviax_repeatable"] = "Enable Quest Hunted By Obliviax Repeatable",
                ["label_enable_damage_ranking"] = "Enable Damage Ranking",
                
                ["label_disable_deja_vu"] = "Disable DejaVu",

                ["label_max_shield_multiplier"] = "Max Shield Multiplier",
                ["label_shield_cool_down_sceonds"] = "Shield Cool Down (seconds)",
                ["label_igore_shield_cool_down_from_others"] = "Igore Shield Cool Down From Others",


            }
        };

        public static string Get(string key)
        {
            var lang = DewSave.profileMain.language;
            if (!Translations.ContainsKey(lang)) lang = "en-US";
            return Translations[lang].GetValueOrDefault(key, key);
        }
    }
}