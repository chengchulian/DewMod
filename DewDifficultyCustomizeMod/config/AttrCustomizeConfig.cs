using System;
using System.Reflection;

namespace DewDifficultyCustomizeMod.config;

public class AttrCustomizeConfig
{
    public static readonly AttrCustomizeConfig DefaultConfig = new()
    {
        maxPlayer = 4,
        enemyMovementSpeedPercentage = 1,
        enemyAttackSpeedPercentage = 1,
        enemyAbilityHasteFlat = 1,
        bossHealthMultiplier = 1,
        bossDamageMultiplier = 1,
        miniBossHealthMultiplier = 1,
        miniBossDamageMultiplier = 1,
        littleMonsterHealthMultiplier = 1,
        littleMonsterDamageMultiplier = 1,
        monsterBaseArmor = 0,
        monsterArmorPercentageAddByZone = 0,
        extraHealthGrowthMultiplier = 0,
        extraDamageGrowthMultiplier = 0,
        beneficialNodeMultiplier = 1,
        skillQGemCount = 3,
        skillWGemCount = 3,
        skillEGemCount = 3,
        skillRGemCount = 3,
        skillIdentityGemCount = 0,
        skillMovementGemCount = 0,
        shopItems = 3,
        bossCount = 1,
        bossCountAddByLoop = 0,
        bossCountAddByZone = 0,
        maxAndSpawnedPopulationMultiplier = 1.5f,
        startSkills = new string[] { },
        startSkillsLevel = new int[] { },
        startGems = new string[] { },
        startGemsQuality = new int[] { },
        enableHeroSkillAddShop = false,
        removeSkills = new string[] { },
        removeGems = new string[] { },
        firstVisitDropGoldCount = 0,
        firstVisitDropGoldCountAddByLoop = 0,
        firstVisitDropGoldCountAddByZone = 0,
        enableHealthReduceMultiplierAddByZone = false,
        enableCurrentNodeGenerateLostSoul = false,
        enableBossRoomGenerateLostSoul = false,
        enableArtifactQuest = true,
        enableFragmentOfRadianceBossQuest = true,
        enableQuestHuntedByObliviaxRepeatable = false,
        enableDamageRanking = false,
        enableBossSpawnAllOnce = false,
        bossMirageChance = 0f,
        bossHunterChance = 0f,
        monsterMirageChanceMultiple = 1f,
        enableWorldReveal = false,
        bossSingleInjuryHealthMultiplier = 1f,
        healRawMultiplier = 1f,
        enableGemMerge = true,
        numOfNodes = -1,
        numOfMerchants = -1,
        enableAllSkillEdit = false,
        disableDejaVu = false,
        maxShieldMultiplier = -1f,
        shieldCoolDownSeconds = -1f,
        igoreShieldCoolDownFromOthers = false,
    };

    /**
 * 房间最大人数
 */
    public int maxPlayer;

    /**
 * 所有敌人移动速度百分比
 */
    public float enemyMovementSpeedPercentage;

    /**
 * 所有敌人攻击速度百分比
 */
    public float enemyAttackSpeedPercentage;

    /**
 * 所有敌人技能急速百分比
 */
    public float enemyAbilityHasteFlat;

    /**
 * boss生命百分比
 */
    public float bossHealthMultiplier;

    /**
 * boss伤害百分比
 */
    public float bossDamageMultiplier;

    /**
 * miniBoss生命百分比
 */
    public float miniBossHealthMultiplier;

    /**
 * miniBoss伤害百分比
 */
    public float miniBossDamageMultiplier;

    /**
 * 小怪生命百分比
 */
    public float littleMonsterHealthMultiplier;

    /**
 * 小怪伤害百分比
 */
    public float littleMonsterDamageMultiplier;


    /**
      * 怪物基础护甲
      */
    public float monsterBaseArmor;

    /**
      * 额外生命成长倍率(此数值为 n^当前关卡数 例: 怪物原始血量为100 当前关卡数为10 值为2 血量为100*2^10)
      */
    public float monsterArmorPercentageAddByZone;

    /**
      * 额外生命成长倍率(此数值为 n^当前关卡数 例: 怪物原始血量为100 当前关卡数为10 值为2 血量为100*2^10)
      */
    public float extraHealthGrowthMultiplier;

    /**
      * 额外伤害成长倍率(此数值为 n^当前关卡数)
      */
    public float extraDamageGrowthMultiplier;

    /**
      * 引导祭坛数量倍数
      */
    public float beneficialNodeMultiplier;

    /**
      * Q技能精华槽数量
      */
    public int skillQGemCount;

    /**
      * W技能精华槽数量
      */
    public int skillWGemCount;

    /**
      * E技能精华槽数量
      */
    public int skillEGemCount;

    /**
      * R技能精华槽数量
      */
    public int skillRGemCount;

    /**
      * 身份技能精华槽数量
      */
    public int skillIdentityGemCount;

    /**
      * 位移技能精华槽数量
      */
    public int skillMovementGemCount;

    /**
      * 商店物品数量
      */
    public int shopItems;

    /**
      * boss数量
      */
    public int bossCount;

    /**
        * 每周目添加boss数量
        */
    public int bossCountAddByLoop;

    /**
 * 每关添加boss数量
 */
    public int bossCountAddByZone;

    /**
    * 人口过剩人口倍数
     */
    public float maxAndSpawnedPopulationMultiplier;
    
    /**
    * 开局发放技能
    */
    public string[] startSkills;

    /**
 * 开局发放技能等级
 */
    public int[] startSkillsLevel;

    /**
    * 开局发放精华
    */
    public string[] startGems;

    /**
 * 开局发放精华品质
 */
    public int[] startGemsQuality;
    
    /**
 * 开启转职
 */
    public bool enableHeroSkillAddShop;

    /**
 * 移除技能
 */
    public string[] removeSkills;

    /**
 * 移除精华
 */
    public string[] removeGems;


    /**
 * 未访问过的图发钱数量
 */
    public int firstVisitDropGoldCount;


    /**
 * 未访问过的图发钱数量每周目添加数量
 */
    public int firstVisitDropGoldCountAddByLoop;

    /**
 * 未访问过的图发钱数量每关添加数量
 */
    public int firstVisitDropGoldCountAddByZone;

    /**
 * 开启幻想上限每周关增加
 */
    public bool enableHealthReduceMultiplierAddByZone;

    /**
 * 开启当前节点生成迷失灵魂
 */
    public bool enableCurrentNodeGenerateLostSoul;

    /**
 * 开启Boss房生成迷失灵魂
 */
    public bool enableBossRoomGenerateLostSoul;

    /**
 * 开启遗物任务
 */
    public bool enableArtifactQuest;

    /**
 * 开启光辉BOSS任务
 */
    public bool enableFragmentOfRadianceBossQuest;

    /**
      * 开启遗忘猎手任务可重复生成
      */
    public bool enableQuestHuntedByObliviaxRepeatable;

    /**
      * 开启每关发送伤害排行榜
      */
    public bool enableDamageRanking;

    /**
      * 开启所有boss一次性生成
      */
    public bool enableBossSpawnAllOnce;

    /**
      * boss幻想化概率
      */
    public float bossMirageChance;

    /**
      * boss猎手化概率
      */
    public float bossHunterChance;

    /**
      * 小怪产生紫皮概率倍数
      */
    public float monsterMirageChanceMultiple;

    /**
      * 开启千里眼
      */
    public bool enableWorldReveal;

    /**
      * Boss单次受伤血量百分比(限伤)
      */
    public float bossSingleInjuryHealthMultiplier;

    /**
      * 治疗效果百分比
      */
    public float healRawMultiplier;

    /**
      * 精华合并
      */
    public bool enableGemMerge;

    /**
 * 节点数量
 */
    public int numOfNodes;

    /**
 * 商人节点数量
 */
    public int numOfMerchants;

    /**
 * 全技能编辑
 */
    public bool enableAllSkillEdit;
    /**
 * 既视感
 */
    public bool disableDejaVu;

    /**
* 最大护盾比例
*/
    public float maxShieldMultiplier;

    /**
* 护盾获取冷却时间
*/
    public float shieldCoolDownSeconds;

    /**
* 忽略队友护盾冷却
*/
    public bool igoreShieldCoolDownFromOthers;

    public void Reset()
    {
        var fields = typeof(AttrCustomizeConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in fields)
        {
            var defaultValue = field.GetValue(DefaultConfig);

            // 处理数组类型需要深拷贝
            if (field.FieldType.IsArray && defaultValue != null)
            {
                var elementType = field.FieldType.GetElementType();
                var array = defaultValue as Array;
                var copiedArray = Array.CreateInstance(elementType, array.Length);
                Array.Copy(array, copiedArray, array.Length);
                field.SetValue(this, copiedArray);
            }
            else
            {
                field.SetValue(this, defaultValue);
            }
        }
    }
}