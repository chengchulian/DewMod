using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DewDifficultyCustomizeMod.config;
using HarmonyLib;
using Newtonsoft.Json;

namespace DewDifficultyCustomizeMod.util;

public class GameManagerUtil
{
    public static void LoadThisModBehavior()
    {
        
        //挂载伤害排行榜
        NetworkedManagerBase<ZoneManager>.instance.ClientEvent_OnZoneLoaded += DamageRanking;
        //挂载移除RoomMod事件
        NetworkedManagerBase<ZoneManager>.instance.ClientEvent_OnZoneLoaded += RemoveRoomMod;
        
        //挂载发钱
        NetworkedManagerBase<ZoneManager>.instance.ClientEvent_OnRoomLoaded += FirstVisitDropGold;

        //挂载遗忘猎手任务重复生成
        NetworkedManagerBase<ZoneManager>.instance.ClientEvent_OnRoomLoaded += QuestHuntedByObliviaxRepeatable;
        
        //挂载透视
        NetworkedManagerBase<ZoneManager>.instance.ClientEvent_OnRoomLoaded += WorldReveal;
        
        //挂载发送客户端需要同步的数据
        NetworkedManagerBase<ZoneManager>.instance.ClientEvent_OnRoomLoaded += SendSynchronizeClient;
        
    }
    public static void UnLoadThisModBehavior()
    {
        
        if (NetworkedManagerBase<ZoneManager>.instance == null)
        {
            return;
        }
        
        NetworkedManagerBase<ZoneManager>.instance.ClientEvent_OnZoneLoaded -= DamageRanking;

        NetworkedManagerBase<ZoneManager>.instance.ClientEvent_OnZoneLoaded -= RemoveRoomMod;
        
        NetworkedManagerBase<ZoneManager>.instance.ClientEvent_OnRoomLoaded -= FirstVisitDropGold;
        
        NetworkedManagerBase<ZoneManager>.instance.ClientEvent_OnRoomLoaded -= QuestHuntedByObliviaxRepeatable;
        
        NetworkedManagerBase<ZoneManager>.instance.ClientEvent_OnRoomLoaded -= WorldReveal;
        
        NetworkedManagerBase<ZoneManager>.instance.ClientEvent_OnRoomLoaded -= SendSynchronizeClient;


    }
    

    public static void SendSynchronizeClient(EventInfoLoadRoom obj)
    {
        SendSynchronizeClient();
    }

    public static void SendSynchronizeClient()
    {
        var synchronizeClient = new SynchronizeClient
        {
            skillQGemCount = AttrCustomizeResources.Config.skillQGemCount,
            skillWGemCount = AttrCustomizeResources.Config.skillWGemCount,
            skillEGemCount = AttrCustomizeResources.Config.skillEGemCount,
            skillRGemCount = AttrCustomizeResources.Config.skillRGemCount,
            skillIdentityGemCount = AttrCustomizeResources.Config.skillIdentityGemCount,
            skillMovementGemCount = AttrCustomizeResources.Config.skillMovementGemCount,
            enableAllSkillEdit = AttrCustomizeResources.Config.enableAllSkillEdit,
        };

        string text = JsonConvert.SerializeObject(synchronizeClient);

        Dew.CallDelayed(delegate
        {
            NetworkedManagerBase<ChatManager>.instance.BroadcastChatMessage(new ChatManager.Message
            {
                type = ChatManager.MessageType.Raw,
                content = $"<size=0%>{text}</size>"
            });
        }, 100);
    }

    public static void ClientSyncData(ChatManager.Message obj)
    {
        var gameManager = NetworkedManagerBase<GameManager>.instance;

        if (gameManager == null)
        {
            return;
        }

        if (gameManager.isServer)
        {
            return;
        }

        if (obj.args != null)
        {
            return;
        }

        if (obj.type != ChatManager.MessageType.Raw)
        {
            return;
        }

        var content = obj.content;
        if (content.StartsWith("<size=0%>{") && content.EndsWith("}</size>"))
        {
            string text = content.Substring("<size=0%>".Length,
                content.Length - "<size=0%>".Length - "</size>".Length);

            SynchronizeClient synchronizeClient = JsonConvert.DeserializeObject<SynchronizeClient>(text);

            AttrCustomizeResources.Config.skillQGemCount = synchronizeClient.skillQGemCount;
            AttrCustomizeResources.Config.skillWGemCount = synchronizeClient.skillWGemCount;
            AttrCustomizeResources.Config.skillEGemCount = synchronizeClient.skillEGemCount;
            AttrCustomizeResources.Config.skillRGemCount = synchronizeClient.skillRGemCount;
            AttrCustomizeResources.Config.skillIdentityGemCount = synchronizeClient.skillIdentityGemCount;
            AttrCustomizeResources.Config.skillMovementGemCount = synchronizeClient.skillMovementGemCount;
            AttrCustomizeResources.Config.enableAllSkillEdit = synchronizeClient.enableAllSkillEdit;
        }
    }

    public class SynchronizeClient
    {
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
         * 开启全技能编辑
         */
        public bool enableAllSkillEdit;
    }

    private static void WorldReveal(EventInfoLoadRoom obj)
    {
        if (AttrCustomizeResources.Config.enableWorldReveal)
        {
            NetworkedManagerBase<ZoneManager>.instance.RevealWorld(true);
        }
    }


    private static void DamageRanking(EventInfoLoadZone e)
    {
        if (!AttrCustomizeResources.Config.enableDamageRanking) return;

        if (NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex == 0)
        {
            return;
        }

        DewGameResult tracked = NetworkedManagerBase<GameResultManager>.instance.tracked;

        if (tracked != null)
        {
            // 用一个小的结构体/record 代替 tuple，更直观
            var dmgList = new List<(string Name, float Total, float Max, int Deaths)>();

            // 遍历玩家数据
            for (int i = 0; i < tracked.players.Count; i++)
            {
                DewGameResult.PlayerData playerData = tracked.players[i];
                string playerProfileName = playerData.playerProfileName;
                float totalDmg = playerData.dealtDamageToEnemies;
                float maxDmg = playerData.maxDealtSingleDamageToEnemy;
                int deaths = playerData.deaths;
                dmgList.Add((playerProfileName, totalDmg, maxDmg, deaths));
            }

            // 按总伤害降序排序
            dmgList.Sort((a, b) => b.Total.CompareTo(a.Total));

            StringBuilder sb = new StringBuilder();
            sb.Append("伤害排行\n");

            // 输出排行信息
            for (int j = 0; j < dmgList.Count; j++)
            {
                var data = dmgList[j];
                string totalDmgFormatted = data.Total.ToString("#,0", CultureInfo.InvariantCulture);
                string maxDmgFormatted = data.Max.ToString("#,0", CultureInfo.InvariantCulture);

                sb.Append($"{j + 1}. {data.Name}: 总伤害 {totalDmgFormatted} | 最强一击 {maxDmgFormatted} | 死亡 {data.Deaths}\n");
            }

            // 延迟发送消息
            Dew.CallDelayed(delegate
            {
                ChatManager.Message message = new ChatManager.Message();
                message.type = ChatManager.MessageType.Raw;
                message.content = sb.ToString();
                NetworkedManagerBase<ChatManager>.instance.BroadcastChatMessage(message);
            }, 100);
        }
    }



    private static void QuestHuntedByObliviaxRepeatable(EventInfoLoadRoom obj)
    {
        if (!AttrCustomizeResources.Config.enableQuestHuntedByObliviaxRepeatable)
        {
            return;
        }

        GameMod_Obliviax gameModObliviax = Dew.FindActorOfType<GameMod_Obliviax>();

        // 创建访问 _lastObliviaxQuestZoneIndexRef 的字段引用
        AccessTools.FieldRef<GameMod_Obliviax, int> _lastObliviaxQuestZoneIndexRef =
            AccessTools.FieldRefAccess<GameMod_Obliviax, int>("_lastObliviaxQuestZoneIndex");

        int lastObliviaxQuestZoneIndex = _lastObliviaxQuestZoneIndexRef(gameModObliviax);


        if (NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex == lastObliviaxQuestZoneIndex
            || !NetworkedManagerBase<ZoneManager>.instance.isCurrentNodeHunted
            || lastObliviaxQuestZoneIndex < 0)
        {
            return;
        }

        NetworkedManagerBase<QuestManager>.instance.StartQuest<Quest_HuntedByObliviax>();
        _lastObliviaxQuestZoneIndexRef(gameModObliviax) =
            NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex;
    }

    private static void RemoveRoomMod(EventInfoLoadZone e)
    {
        List<WorldNodeData> worldNodeDatas = NetworkedManagerBase<ZoneManager>.instance.nodes.ToList();

        if (!AttrCustomizeResources.Config.enableArtifactQuest)
        {
            for (int i = 0; i < worldNodeDatas.Count; i++)
            {
                NetworkedManagerBase<ZoneManager>.instance.RemoveModifier<RoomMod_Artifact>(i);
            }
        }

        if (!AttrCustomizeResources.Config.enableFragmentOfRadianceBossQuest)
        {
            for (int i = 0; i < worldNodeDatas.Count; i++)
            {
                NetworkedManagerBase<ZoneManager>.instance.RemoveModifier<RoomMod_FragmentOfRadiance_StartProp>(i);
            }
        }
    }


    private static void FirstVisitDropGold(EventInfoLoadRoom obj)
    {
        if (!obj.isTraveling || SingletonDewNetworkBehaviour<Room>.instance.isRevisit)
        {
            return;
        }

        int zoneAddCount = (NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex) *
                           AttrCustomizeResources.Config.firstVisitDropGoldCountAddByZone;
        int loopAddCount = (NetworkedManagerBase<ZoneManager>.instance.loopIndex) *
                           AttrCustomizeResources.Config.firstVisitDropGoldCountAddByLoop;
        int count = AttrCustomizeResources.Config.firstVisitDropGoldCount;
        int value = count + loopAddCount + zoneAddCount;
        foreach (var humanPlayer in DewPlayer.allHumanPlayers)
        {
            NetworkedManagerBase<PickupManager>.instance.DropGold(isKillGold: false, isGivenByOtherPlayer: false, value,
                humanPlayer.hero.position, humanPlayer.hero);
        }
    }


}