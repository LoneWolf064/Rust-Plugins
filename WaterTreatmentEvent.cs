using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Oxide.Core;
using Oxide.Core.Plugins;

using UnityEngine;
namespace Oxide.Plugins
{
    [Info("WaterTreatmentEvent", "Yac Vaguer", "1.7.1")]
    [Description("In the desolate expanse of Rust's wasteland, the Water Treatment Plant becomes a battleground in Operation Cobalt Siege, where survivors clash with elite soldiers over precious resources amidst an unforgiving apocalypse. Bravery and strategy will decide who claims dominion over this contaminated yet coveted stronghold.")]
    public class WaterTreatmentEvent : RustPlugin
    {
        [PluginReference]
        Plugin? NpcSpawn, SkillTree, Economics, ServerRewards, GUIAnnouncements;

        private List<BaseEntity> entities = new List<BaseEntity>();

        private ListHashSet<BasePlayer> playersOnTheEvent = new ListHashSet<BasePlayer>();

        private List<RadiationZone> radiationZones = new List<RadiationZone>();

        private NotificationCenter notification;
        private PluginConfig config;

        private Dictionary<string, Vector3[]>? BaseEntities;

        private bool startEnding = false;

        private bool waitingForRocket = false;

        private Dictionary<string, string> prefabPaths;

        private EventSphere sphere;

        private PVEMode pveMode;

        private DateTime eventStartTime;

        private Timer? eventDurationTimer;

        private VendingMarkerFx? vendingMarker = null;

        protected override void SaveConfig() => Config.WriteObject(config);

        #region Translations
        private const string compressed = "ewogICJTdGFydGluZ0V2ZW50IjogIlVudXN1YWwgYWN0aXZpdHkgZGV0ZWN0ZWQgYXQgdGhlIFdhdGVyIFRyZWF0bWVudCBQbGFudC4gUmVjb25uYWlzc2FuY2UgYWR2aXNlZCBmb3IgcG90ZW50aWFsIG9wcG9ydHVuaXRpZXMuIiwKICAiU3RhcnRlZEV2ZW50IjogIkhpZ2ggYWxlcnQ6IEhlYXZ5IGd1YXJkIHByZXNlbmNlIGNvbmZpcm1lZCBhdCB0aGUgV2F0ZXIgVHJlYXRtZW50IFBsYW50LiBJbnRlbGxpZ2VuY2Ugc3VnZ2VzdHMgdGhleSdyZSBndWFyZGluZyBjcml0aWNhbCBhc3NldHMuIEV4ZXJjaXNlIGNhdXRpb24uIiwKICAiRW5kaW5nRXZlbnQiOiAiVXBkYXRlOiBDb2JhbHQgZm9yY2VzIGFyZSBpbml0aWF0aW5nIHdpdGhkcmF3YWwgZnJvbSB0aGUgV2F0ZXIgVHJlYXRtZW50IFBsYW50LiBXaW5kb3cgb2Ygb3Bwb3J0dW5pdHkgY2xvc2luZyBzb29uLiIsCiAgIkVuZEV2ZW50IjogIk9wZXJhdGlvbiBjb21wbGV0ZTogV2F0ZXIgVHJlYXRtZW50IFBsYW50IGlzIG5vdyBjbGVhcmVkIG9mIENvYmFsdCBvY2N1cGF0aW9uLiBBcmVhIHNlY3VyZWQgZm9yIHJlc291cmNlIGFjcXVpc2l0aW9uLiIsCiAgIlBsYXllckVudGVyaW5nVGhlWm9uZSI6ICJIZXkgezB9LCB0aGUgYXJlYSBpcyB1bmRlciB0aHJlYXQgZHVlIHRvIGEgbWlzaGFwIGluIENvYmFsdCdzIGV4cGVyaW1lbnRzLiBUaGUgc2l0dWF0aW9uIGlzIHZvbGF0aWxlIGFuZCB1bnByZWRpY3RhYmxlLiBFeGVyY2lzZSBleHRyZW1lIGNhdXRpb24gaW4geW91ciBhcHByb2FjaC4iLAogICJLaWxsZWRCcmFkbGV5IjogInswfSBhbmQgdGhlaXIgdGVhbSBkZXN0cm95ZWQgQnJhZGxleUFQQyIsCiAgIkFsZXJ0T2ZOdWtlIjogIlVyZ2VudCBXYXJuaW5nOiBJbmNvbWluZyBGMTUgamV0IHdpdGggbnVjbGVhciB0aHJlYXQgZGV0ZWN0ZWQgYXQgV2F0ZXIgVHJlYXRtZW50IFBsYW50ISBFdmFjdWF0ZSBpbW1lZGlhdGVseSB0byBhdm9pZCBsZXRoYWwgcmFkaWF0aW9uIGV4cG9zdXJlLiIsCiAgIk5vdFRoZVBsYXllck93bmVyT3JUZWFtIjogIlRoZSBldmVudCBpcyBvd25lZCBieSBzb21lb25lIGVsc2UsIHlvdSBjYW5ub3QgZW50ZXIgdG8gdGhpcyBhcmVhIHVudGlsIHRoZSBldmVudCBmaW5pc2hlZCIsCiAgIllvdUFyZVRoZU5ld093bmVyIjogIllvdSBiZWNvbWUgdGhlIG93bmVyIG9mIHRoZSBldmVudCwgeW91IGFuZCB5b3VyIHRlYW0gYXJlIHRoZSBvbmx5IGFsbG93ZWQgaGVyZSIsCiAgInZlbmRpbmdNYWNoaW5lVGl0bGUiOiAiezB9IHsxfSAtIFRpbWUgTGVmdDogezJ9IG1pbnV0ZXMiCn0=";

        private Dictionary<string, string> DecompressMessages() => JsonConvert.DeserializeObject<Dictionary<string, string>>(Encoding.UTF8.GetString(Convert.FromBase64String(compressed)));

        protected override void LoadDefaultMessages() => lang.RegisterMessages(DecompressMessages(), this, "en");

        private string GetMessage(string key, string userId) => lang.GetMessage(key, this, userId);
        #endregion

        private bool started = false;

        private MissionZone? missionZone;
        private Timer? timerCheckingPlayers;

        private Rewards rewards;

        private const string AdminPermission = "watertreatmentevent.admin";
        private NetworkableId? bradleyId;

        private List<NetworkableId>? hackableCrate = new List<NetworkableId>();

        private DateTime eventEndTime;

        private readonly HashSet<string> hooks = new HashSet<string>(){
            "OnLootEntity",
            "OnEntityDeath",
            "OnCollisionEnter"
        };

        private void SuscribeHooks()
        {
            log.Debug("Subscribe to all the necessary hooks for the event");
            foreach (string hook in hooks)
            {
                Subscribe(hook);
                log.Debug("Subscribed to the hook: " + hook);
            }
        }

        private void UnSubscribeHooks()
        {
            log.Debug("Unsubscribe to all the necessary hooks for the event");
            foreach (string hook in hooks)
            {
                Unsubscribe(hook);
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
            SaveConfig();
            Puts("Creation of the configuration file completed");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            VersionNumber serverVersion = new VersionNumber(config.version.major, config.version.minor, config.version.patch);
            if (serverVersion < Version)
            {
                ConciliateConfiguration(serverVersion);
            }
        }

        private void ConciliateConfiguration(VersionNumber serverVersion)
        {
            if (serverVersion < new VersionNumber(1, 6, 0))
            {
                config.onlyManualRun = false;
            }

            config.version.major = Version.Major;
            config.version.minor = Version.Minor;
            config.version.patch = Version.Patch;
            SaveConfig();
        }

        private void AlertAboutConfigurationVersionError(string configVersion = "")
        {
            Puts("");
            Puts($"Your configuration is an older version than the actual plugin, " +
                "please update the config accordignly. You might experience issues in the plugin otherwise");

            if (configVersion != "")
            {
                Puts($"Expected Version {configVersion}");
            }

            Puts($"Newest Version {Version.ToString()}");
            Puts("");
        }

        private void CheckConfigVersion()
        {

            if (config.version == null)
            {
                AlertAboutConfigurationVersionError();
                return;
            }

            string configVersion = $"{config.version.major}.{config.version.minor}.{config.version.patch}";

            if (configVersion == Version.ToString())
            {
                return;
            }

            AlertAboutConfigurationVersionError(configVersion);
        }

        private void CheckIfNewPlayersEnteredTheZone()
        {
            List<BasePlayer> playersNearby = GetPlayersInRadius(missionZone.EventCenter, missionZone.ZoneRadius);

            foreach (BasePlayer player in playersNearby)
            {
                if (config.pveMode && !pveMode.CheckIfIsAllowed(player))
                {
                    continue;
                }

                if (playersOnTheEvent.Contains(player))
                {
                    continue;
                }

                playersOnTheEvent.Add(player);
                log.Debug($"Player nearby: {player.displayName} (ID: {player.userID}, Position: {player.transform.position})");

                notification.Send(player, "PlayerEnteringTheZone", player.displayName);
            }
        }

        private List<BasePlayer> GetPlayersInRadius(Vector3 center, float radius)
        {
            Collider[] colliders = Physics.OverlapSphere(center, radius);
            List<BasePlayer> playersInRadius = new List<BasePlayer>();
            foreach (Collider collider in colliders)
            {
                BasePlayer player = collider.GetComponent<BasePlayer>();

                if (player != null && player.userID.Get().IsSteamId() && !playersInRadius.Contains(player))
                {
                    playersInRadius.Add(player);
                }
            }
            return playersInRadius;
        }


        #region Rewards
        /**
         * Handle all the rewards during the event
         */
        private class Rewards
        {
            private readonly PluginConfig.RewardsConfig config;
            private readonly WaterTreatmentEvent parent;

            public Rewards(PluginConfig configFromEvent, WaterTreatmentEvent parentInstance)
            {
                config = configFromEvent.Rewards;
                parent = parentInstance;
            }

            public void RewardForCrate(BasePlayer player)
            {
                ProcessReward(player, "hackableCrate");
            }

            public void RewardForBradley(BasePlayer player)
            {
                ProcessReward(player, "bradley");
            }

            private void ProcessReward(BasePlayer player, string rewardType)
            {
                if (config.Economics.Enable)
                {
                    RewardWithEconomics(player, GetAmountBasedOnConfiguration(config.Economics, rewardType));
                }

                if (config.XP.Enable)
                {
                    RewardWithXP(player, GetAmountBasedOnConfiguration(config.XP, rewardType));
                }

                if (config.RP.Enable)
                {
                    RewardWithRP(player, GetAmountBasedOnConfiguration(config.RP, rewardType));
                }
            }

            private int GetAmountBasedOnConfiguration(PluginConfig.RewardSettings settings, string rewardType)
            {
                switch (rewardType)
                {
                    case "hackableCrate":
                        return settings.RewardForLootHackableCrate;
                    case "bradley":
                        return settings.RewardForDestroyBradley;
                    default:
                        return 0;
                }
            }

            private void RewardWithXP(BasePlayer player, int amount)
            {
                parent.log.Debug("Trying to reward XP to " + player.displayName + " with " + amount + " We assume that you have the plugin enabled");
                try
                {
                    parent.SkillTree?.Call("AwardXP", player, amount);
                    parent.log.Debug($"{player.displayName} rewarded with {amount} of XP");


                }
                catch (Exception ex)
                {
                    parent.log.Error($"Error occurred while RewardWithXP: {ex.Message}");
                    parent.log.Warning("We couldn't give XP to the player as reward");
                }

            }

            private void RewardWithRP(BasePlayer player, int amount)
            {
                parent.log.Debug("Trying to reward RP to " + player.displayName + " with " + amount + " We assume that you have the plugin enabled");

                parent.log.Debug("ServerRewards is " + parent.ServerRewards == null ? "null" : "not null");
                parent.log.Debug($"{player.displayName} rewarded with {amount} of RP");
                parent.log.Debug("player.UserId: " + player.userID);
                try
                {
                    parent.ServerRewards.Call("AddPoints", player.userID, amount);
                    parent.log.Debug($"{player.displayName} rewarded with {amount} of RP");
                }
                catch (Exception ex)
                {
                    parent.log.Error($"Error occurred while RewardWithRP: {ex.Message}");
                    parent.log.Warning("We couldn't give Reward Points to the player as reward");
                }
            }

            private void RewardWithEconomics(BasePlayer player, int amount)
            {
                parent.log.Debug("Trying to reward Economics to " + player.displayName + " with " + amount + " We assume that you have the plugin enabled");
                try
                {
                    parent.Economics?.Call("Deposit", player.UserIDString, (double)amount);
                    parent.log.Debug($"{player.displayName} rewarded with {amount} of Economics");
                }
                catch (Exception ex)
                {
                    parent.log.Error($"Error occurred while RewardWithEconomics: {ex.Message}");
                    parent.log.Warning("We couldn't give Economics Points to the player as reward");
                }
            }
        }
        #endregion

        #region Zone Location
        internal class MissionZone
        {
            private MonumentInfo _eventLocation;
            private Vector3? _eventCenterLocation = null;
            private readonly float _zoneRadius = 120f;
            private string monumentName = "water treatment plant";

            public MissionZone()
            {
                foreach (var monument in TerrainMeta.Path.Monuments)
                {
                    if (monument.displayPhrase.english.ToLower().Contains(monumentName))
                    {
                        _eventLocation = monument;
                        return;
                    }
                }

                throw new InvalidOperationException("Water Treatment Plant monument not found. This plugin only works in the Water Treatment Plant monument");
            }

            public Vector3 GetLocationFromPoint(Vector3 point) => _eventLocation.transform.TransformPoint(point);

            public Vector3 EventCenter
            {
                get
                {
                    if (!_eventCenterLocation.HasValue)
                    {
                        Vector3 newCenter = new Vector3(-13.62f, 4.36f, -69.07f);
                        _eventCenterLocation = _eventLocation.transform.TransformPoint(newCenter);
                    }

                    return _eventCenterLocation.Value;
                }
            }

            public float ZoneRadius
            {
                get { return _zoneRadius; }
            }

            public MonumentInfo Location
            {
                get { return _eventLocation; }
            }
        }
        #endregion

        /**
         * Hook that is triggered when players started looting the hackable crates
         */
        private void OnLootEntity(BasePlayer player, HackableLockedCrate entity)
        {
            if (!started)
            {
                return;
            }

            log.Debug($"Checking the opening of the crate{entity.net.ID}");

            if (player == null || entity == null || hackableCrate == null)
            {
                return;
            }

            if (rewards == null)
            {
                log.Warning("Rewards is null on OnLootEntity");
                rewards = new Rewards(config, this);
            }

            if (!hackableCrate.IsEmpty() && hackableCrate.Contains(entity.net.ID))
            {
                log.Debug($"Hackable Crate has been looted by {player.displayName}");
                rewards.RewardForCrate(player);
                hackableCrate.Remove(entity.net.ID);
                StartEndingEventDueToUserCompleted();
            }
        }
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is ScientistNPC npc)
            {
                OnEntityDeathNpc(npc, info);
            }
            else if (entity is BradleyAPC apc)
            {
                OnEntityDeathBradley(apc, info);
            }
        }


        private void OnEntityDeathNpc(ScientistNPC npc, HitInfo info)
        {
            try
            {
                if (info != null && info.InitiatorPlayer != null && info.InitiatorPlayer.IsNpc)
                {
                    return;
                }

                if (!config.pveMode || !started)
                {
                    log.Debug("PVE Mode false or the event didn't started");
                    return;
                }
                if (pveMode.HasOwner())
                {
                    log.Debug("The Event Already has an owner");
                    return;
                }

                if (entities.Count == 0)
                {
                    log.Debug("No entities in the event");
                    return;
                }

                var foundNpc = entities.FirstOrDefault(entity => entity != null && entity.net != null && entity.net.ID == npc.net.ID);

                if (foundNpc == null)
                {
                    log.Debug("NPC dead is not part of the Event");
                    return;
                }

                if (info.InitiatorPlayer == null)
                {
                    log.Debug("NPC dead is not killed by a player");
                    return;
                }

                if (config.onlyPlayersInTheSphere && !playersOnTheEvent.Contains(info.InitiatorPlayer))
                {
                    log.Debug("Player is not in the sphere so It cannot be an owner");
                    return;
                }

                log.Debug("NPC dead is killed by a player");
                pveMode.AddOwner(info.InitiatorPlayer);
                vendingMarker?.SetEventOwner(info.InitiatorPlayer.displayName);

            }
            catch (NullReferenceException ex)
            {
                log.Error($"Error occurred while OnEntityDeath: {ex.Message}");
            }
        }

        private void OnEntityDeathBradley(BradleyAPC apc, HitInfo info)
        {
            if (!started)
            {
                return;
            }

            if (apc == null)
            {
                log.Warning("Apc is null so we cannot calculate who destroyed Bradley");
                return;
            }

            if (apc.net.ID != bradleyId)
            {
                return;
            }

            var player = info.InitiatorPlayer;

            notification.SendToAll(BasePlayer.activePlayerList, "KilledBradley", player.displayName);

            log.Debug($"Bradley was taken down by {player.displayName}");

            rewards.RewardForBradley(player);

        }

        #region Configuration
        internal class PluginConfig
        {
            //Timing
            [JsonProperty("Event Start every [sec]")] public float eventInterval { get; set; }
            [JsonProperty("Duration of the event [sec]")] public float eventDuration { get; set; }

            [JsonProperty("Run the event ONLY manually with the commands")] public bool onlyManualRun { get; set; }
            [JsonProperty("Warning time before the Event Spawn [sec]")] public float eventStartWarningTime { get; set; }
            [JsonProperty("Warning time before the Event Ends [sec]")] public float eventEndWarningTime { get; set; }
            [JsonProperty("Time after player loot the hackable crate to end the event and drop the nukes, this need to be lower than Warning time before the event ends [sec]")] public float cooldownAfterCompletedEvent { get; set; }

            // NPC 
            [JsonProperty("Amount of Zombies around the crates")] public int zombiesAmount { get; set; }
            [JsonProperty("Guards Settings")] public NpcSettings guardsSettings { get; set; }
            [JsonProperty("Snipers Settings")] public NpcSettings snipersSettings { get; set; }
            [JsonProperty("Discord Webhook URL")] public string discordWebhookUrl { get; set; }
            [JsonProperty("Send Info message to discord true|false")] public bool discord { get; set; }

            [JsonProperty("Activate PVE Mode where only the event owner can access to the event")] public bool pveMode { get; set; }
            [JsonProperty("Only if players are inside of the Sphere can become owners")] public bool onlyPlayersInTheSphere { get; set; }
            [JsonProperty("Radius of the PVE Sphere")] public float PVEModeSphereRadius { get; set; }

            // Loot
            [JsonProperty("Elite crate amount to spawn [0-3]")] public int eliteCratesAmount { get; set; }
            [JsonProperty("Hackable crate amount to spawn [0-2]")] public int hackableCratesAmount { get; set; }

            //Featured Flags
            [JsonProperty("Activate verbose debug mode")] public bool debug { get; set; }
            [JsonProperty("Spawn Bradley in the event")] public bool spawnBradley { get; set; }
            [JsonProperty("Spawn Snipers in the event")] public bool spawnSnipers { get; set; }
            [JsonProperty("Spawn Guards in the event")] public bool spawnGuards { get; set; }
            [JsonProperty("Spawn Zombies in the event")] public bool spawnZombies { get; set; }
            [JsonProperty("Spawn Sam Sites in the event")] public bool spawnSamSites { get; set; }
            [JsonProperty("Add Radiation when the F15 drop the nuke")] public bool addRadiationForEndOfEvent { get; set; }
            [JsonProperty("Create explosion in the main building when the event start")] public bool createExplosionInMainBuilding { get; set; }

            [JsonProperty("Rewards Settings, you can activate more than one at the same time")] public RewardsConfig Rewards { get; set; }

            [JsonProperty("Activate Vending Machine marker true|false")] public bool activateVendingMachine { get; set; }
            [JsonProperty("Vending Mark title for the event")] public string nameOfTheEventForThePlayers { get; set; }
            [JsonProperty("Vending Mark title to show owner of the event")] public string vendingMachineTitleOwner { get; set; }
            [JsonProperty("Vending Mark title to show the time left of the event")] public string vendingMachineTitleTimer { get; set; }

            [JsonProperty("Alpha for the Event Marker")] public float markerAlpha { get; set; }
            [JsonProperty("Radius for the Event Marker")] public float markerRadius { get; set; }
            [JsonProperty("Color for the Event Marker")] public string markerColor { get; set; }

            [JsonProperty("Notification Settings")] public NotificationConfig notificationConfig { get; set; }

            [JsonProperty("Event Version")] public VersionConfig version { get; set; }


            public class VersionConfig
            {
                [JsonProperty("Major")] public int major { get; set; }
                [JsonProperty("Minor")] public int minor { get; set; }
                [JsonProperty("Patch")] public int patch { get; set; }
            }

            //Messaging
            public class NotificationConfig
            {
                [JsonProperty("Send missions details to the players?")] public bool sendMessagesToPlayers { get; set; }
                [JsonProperty("Message system to use? Please use one of the supported one [Chat|GUIAnnouncement]")] public string notificationAdapter { get; set; }
                [JsonProperty("Chat message configuration")] public ChatConfiguration chatConfiguration { get; set; }
                [JsonProperty("GUIAnnouncement message configuration")] public GUIAnnouncementConfiguration guiAnnouncementConfiguration { get; set; }
                [JsonProperty("Message Prefix Text")] public string messagePrefix { get; set; }

            }

            public class ChatConfiguration
            {
                [JsonProperty("Icon to use for messages")] public long messageIcon { get; set; }
                [JsonProperty("Message Prefix Size")] public uint messagePrefixSize { get; set; }
                [JsonProperty("Message Prefix Color")] public string messagePrefixColor { get; set; }
                [JsonProperty("Message Color")] public string messageColor { get; set; }
                [JsonProperty("Message Size")] public uint messageSize { get; set; }
            }

            public class GUIAnnouncementConfiguration
            {
                [JsonProperty("banner Tint Color")] public string bannerTintColor { get; set; }
                [JsonProperty("text Color")] public string textColor { get; set; }
            }

            public class RewardsConfig
            {
                [JsonProperty("Economics")] public RewardSettings Economics { get; set; }
                [JsonProperty("Reward Points (RP)")] public RewardSettings RP { get; set; }
                [JsonProperty("Skill Tree XP")] public RewardSettings XP { get; set; }
            }

            public class RewardSettings
            {
                [JsonProperty("Enable")] public bool Enable { get; set; }
                [JsonProperty("RewardForLootHackableCrate")] public int RewardForLootHackableCrate { get; set; }
                [JsonProperty("RewardForDestroyBradley")] public int RewardForDestroyBradley { get; set; }

            }

            public class NpcSettings
            {
                [JsonProperty("Name")] public string Name { get; set; }
                [JsonProperty("Health")] public float Health { get; set; }
                [JsonProperty("Roam Range")] public float RoamRange { get; set; }

                [JsonProperty("Kit, remember that this decrease the performance")] public string Kit { get; set; }
                [JsonProperty("Chase Range")] public float ChaseRange { get; set; }
                [JsonProperty("Attack Range Multiplier")] public float AttackRangeMultiplier { get; set; }
                [JsonProperty("Sense Range")] public float SenseRange { get; set; }
                [JsonProperty("Target Memory Duration [sec.]")] public float MemoryDuration { get; set; }
                [JsonProperty("Scale damage")] public float DamageScale { get; set; }
                [JsonProperty("Aim Cone Scale")] public float AimConeScale { get; set; }
                [JsonProperty("Detect the target only in the NPC's viewing vision cone? [true/false]")] public bool CheckVisionCone { get; set; }
                [JsonProperty("Vision Cone")] public float VisionCone { get; set; }
                [JsonProperty("Speed")] public float Speed { get; set; }
                [JsonProperty("Disable radio effects? [true/false]")] public bool DisableRadio { get; set; }
                [JsonProperty("Is this a stationary NPC? [true/false]")] public bool Stationary { get; set; }
                [JsonProperty("Remove a corpse after death? (it is recommended to use the true value to improve performance) [true/false]")] public bool IsRemoveCorpse { get; set; }
                [JsonProperty("Wear items")] public HashSet<NpcClothing> ClothingItems { get; set; }
                [JsonProperty("Belt items")] public HashSet<NpcBelt> BeltItems { get; set; }
            }

            public class NpcBelt
            {
                [JsonProperty("ShortName")] public string ShortName { get; set; }
                [JsonProperty("Amount")] public int Amount { get; set; }
                [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
                [JsonProperty("Mods")] public HashSet<string> Mods { get; set; }
                [JsonProperty("Ammo")] public string Ammo { get; set; }
            }

            public class NpcClothing
            {
                [JsonProperty("ShortName")] public string ShortName { get; set; }
                [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
            }

            public static PluginConfig DefaultConfig()
            {

                return new PluginConfig()
                {
                    // Timing
                    eventInterval = 7200f,
                    eventDuration = 2400f,
                    onlyManualRun = false,
                    eventStartWarningTime = 120f,
                    eventEndWarningTime = 400f,
                    cooldownAfterCompletedEvent = 240f,

                    discordWebhookUrl = "https://hookdeck.com/webhooks/platforms/how-to-get-started-with-discord-webhooks",
                    discord = false,

                    // PVE Configuration
                    pveMode = false,
                    PVEModeSphereRadius = 2f,
                    onlyPlayersInTheSphere = true,

                    // Event Marker 
                    markerAlpha = 0.5f,
                    markerRadius = 2f,
                    markerColor = "255 80 103",


                    // Version
                    version = new VersionConfig
                    {
                        major = 1,
                        minor = 6,
                        patch = 3
                    },

                    //Notifications
                    notificationConfig = new NotificationConfig
                    {
                        sendMessagesToPlayers = true,
                        notificationAdapter = "Chat",
                        messagePrefix = "[The Water Treatment Plant Showdown]",
                        chatConfiguration = new ChatConfiguration
                        {
                            messageIcon = 76561199486270644,
                            messagePrefixColor = "#d06c31",
                            messagePrefixSize = 13,
                            messageColor = "#FFFFFF",
                            messageSize = 13,
                        },
                        guiAnnouncementConfiguration = new GUIAnnouncementConfiguration
                        {
                            bannerTintColor = "0.1 0.1 0.1 0.7",
                            textColor = "1 1 1",
                        }
                    },

                    // Loot 
                    eliteCratesAmount = 3,
                    hackableCratesAmount = 2,

                    // Rewards
                    Rewards = new RewardsConfig
                    {
                        Economics = new RewardSettings
                        {
                            Enable = false,
                            RewardForLootHackableCrate = 200,
                            RewardForDestroyBradley = 2000
                        },
                        XP = new RewardSettings
                        {
                            Enable = false,
                            RewardForLootHackableCrate = 200,
                            RewardForDestroyBradley = 1000
                        },
                        RP = new RewardSettings
                        {
                            Enable = false,
                            RewardForLootHackableCrate = 200,
                            RewardForDestroyBradley = 2000
                        }
                    },

                    //feature Flags
                    spawnBradley = true,
                    spawnSnipers = true,
                    spawnGuards = true,
                    spawnZombies = true,
                    spawnSamSites = true,
                    createExplosionInMainBuilding = true,

                    addRadiationForEndOfEvent = true,

                    debug = false,

                    //Vending Machine Mark configuration
                    activateVendingMachine = true,
                    nameOfTheEventForThePlayers = "WT Showdown",
                    vendingMachineTitleOwner = "Owner: {0}",
                    vendingMachineTitleTimer = "{0} minutes left",

                    // NPCs
                    zombiesAmount = 8,

                    guardsSettings = new NpcSettings
                    {
                        Name = "Guardian",
                        Health = 150f,
                        Kit = "",
                        RoamRange = 8f,
                        ChaseRange = 100f,
                        AttackRangeMultiplier = 2f,
                        SenseRange = 85f,
                        MemoryDuration = 30f,
                        DamageScale = 0.4f,
                        AimConeScale = 1f,
                        CheckVisionCone = false,
                        VisionCone = 135f,
                        Speed = 7.5f,
                        DisableRadio = false,
                        Stationary = false,
                        IsRemoveCorpse = true,
                        ClothingItems = new HashSet<NpcClothing>
                        {
                            new NpcClothing { ShortName = "hoodie", SkinID = 941172099 },
                            new NpcClothing { ShortName = "shoes.boots", SkinID = 869007492 },
                            new NpcClothing { ShortName = "roadsign.jacket", SkinID = 2803024010 },
                            new NpcClothing { ShortName = "coffeecan.helmet", SkinID = 2803024592 },
                            new NpcClothing { ShortName = "pants", SkinID = 1313091292 }
                        },
                        BeltItems = new HashSet<NpcBelt>
                        {
                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight" }, Ammo = string.Empty },
                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                        },
                    },
                    snipersSettings = new NpcSettings
                    {
                        Name = "Silent Death",
                        Health = 150f,
                        RoamRange = 20f,
                        Kit = "",
                        ChaseRange = 100f,
                        AttackRangeMultiplier = 2f,
                        SenseRange = 85f,
                        MemoryDuration = 30f,
                        DamageScale = 0.4f,
                        AimConeScale = 1f,
                        CheckVisionCone = false,
                        VisionCone = 135f,
                        Speed = 7.5f,
                        DisableRadio = false,
                        Stationary = false,
                        IsRemoveCorpse = true,
                        ClothingItems = new HashSet<NpcClothing>
                        {
                            new NpcClothing { ShortName = "hoodie", SkinID = 3031048156 },
                            new NpcClothing { ShortName = "shoes.boots", SkinID = 2511111623 },
                            new NpcClothing { ShortName = "jacket", SkinID = 3023836945 },
                            new NpcClothing { ShortName = "pants", SkinID = 3031050852 },
                            new NpcClothing { ShortName = "metal.facemask", SkinID = 3037689021 }
                        },
                        BeltItems = new HashSet<NpcBelt>
                        {
                            new NpcBelt { ShortName = "rifle.bolt", Amount = 1, SkinID = 562396268, Mods = new HashSet<string> { "weapon.mod.flashlight" }, Ammo = string.Empty },
                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                        },
                    },
                };
            }
        }
        #endregion


        private void OnServerInitialized()
        {
            if (!permission.PermissionExists(AdminPermission, this))
            {
                permission.RegisterPermission(AdminPermission, this);
            }

            if (config == null)
            {
                LoadConfig();
            }

            CheckConfigVersion();
            FillPrefabsToUse();
            log = new Log(config, this);

            // set notification center 
            INotificationAdapter notificationAdapter;

            switch (config.notificationConfig.notificationAdapter)
            {
                case "GUIAnnouncement":
                    if (GUIAnnouncements == null)
                    {
                        log.Error("You set GUI Announcements as default Notification but the plugin is not loaded");
                        return;
                    }
                    notificationAdapter = new GUIAnnouncementNotifcations(config.notificationConfig, log);
                    notificationAdapter.SetReference(GUIAnnouncements);
                    break;
                default:
                    notificationAdapter = new ChatNotifications(config.notificationConfig, log);
                    break;
            }

            notification = new NotificationCenter(config.notificationConfig, notificationAdapter, this);

            try
            {
                missionZone = new MissionZone();
            }
            catch (InvalidOperationException ex)
            {
                Puts("The Map doesn't contain the Monument Water Treatment so the plugin will unload");
                Oxide.Core.Interface.Oxide.UnloadPlugin("WaterTreatmentEvent");
                return;
            }
            rewards = new Rewards(config, this);

            StartTimer();
        }

        private void StartTimer()
        {
            if (config.onlyManualRun)
            {
                log.Debug("Event will only run manually");
                return;
            }
            timer.In(config.eventInterval, StartEvent);
        }

        private void FillPrefabsToUse()
        {
            prefabPaths = new Dictionary<string, string>
            {
                { "GenericRadiusMarker", "assets/prefabs/tools/map/genericradiusmarker.prefab" },
                { "VendingMarker", "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab"},
                { "SamSite", "assets/prefabs/npc/sam_site_turret/sam_static.prefab" },
                { "LockedCrate", "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab"},
                { "EliteCrate", "assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab" },
                { "Bradley", "assets/prefabs/npc/m2bradley/bradleyapc.prefab" },
                { "F15", "assets/scripts/entity/misc/f15/f15e.prefab" },
                { "MLRS", "assets/content/vehicles/mlrs/rocket_mlrs.prefab" },
                { "Zombie", "assets/prefabs/npc/scarecrow/scarecrow.prefab" },
                { "SandBag", "assets/prefabs/deployable/barricades/barricade.sandbags.prefab" },
                { "Sphere", "assets/prefabs/visualization/sphere.prefab"},
                { "Sprinkler", "assets/prefabs/deployable/playerioents/sprinkler/electric.sprinkler.deployed.prefab"},
                { "ComputerStation", "assets/prefabs/deployable/computerstation/computerstation.static.prefab"}
            };
        }

        private void Unload()
        {
            ForceEndEvent();
        }

        #region Commands 
        [ConsoleCommand("wtestop")]
        private void StopEventThroughConsoleCommand(ConsoleSystem.Arg arg)
        {
            log.Info("Stopping the event manually");
            BasePlayer player = arg.Player();
            StopEventThroughCommand(player);
        }

        [ChatCommand("wtestop")]
        private void StopEventThroughChatCommand(BasePlayer player)
        {
            notification.Send(player, "Stopping the event manually");
            StopEventThroughCommand(player);
        }

        private void StopEventThroughCommand(BasePlayer player)
        {

            if (player != null && !permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                notification.Send(player, "You do not have permission to use this command.");
                return;
            }
            ForceEndEvent();
            log.Info("Stopping the event manually");
        }

        [ChatCommand("wtestart")]
        private void StartEventThroughChatCommand(BasePlayer player)
        {
            notification.Send(player, "Starting the event manually");
            log.Info("Starting the event manually");
            StartEventThroughCommand(player);

        }

        [ConsoleCommand("wtestart")]
        private void StartEventThroughConsoleCommand(ConsoleSystem.Arg arg)
        {
            log.Info("Starting the event manually");
            BasePlayer player = arg.Player();
            StartEventThroughCommand(player);
        }

        private void StartEventThroughCommand(BasePlayer player)
        {
            if (player != null && !permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                notification.Send(player, "You do not have permission to use this command.");
                return;
            }
            log.Info("Starting the event manually");
            StartEvent();
        }
        #endregion

        void StartEvent()
        {

            if (started)
            {
                log.Debug("Event already started");
                return;
            }

            log.Info("Starting the event");
            SuscribeHooks();
            InitiateTheEvent();

            timer.In(config.eventDuration, EndEvent);

        }

        private void InitiateTheEvent()
        {
            started = true;
            if (missionZone == null)
            {
                log.Error("Mission Zone is null, we cannot start the event");
                return;

            }


            Interface.Oxide.CallHook("OnWaterTreatmentEventStart", missionZone.EventCenter, missionZone.ZoneRadius);

            log.Debug("Event will start soon");
            notification.SendToAll(BasePlayer.activePlayerList, "StartingEvent");

            timer.Once(config.eventStartWarningTime, () =>
            {

                SpawnBradleyAPC();
                SpawnNPCs();
                CreateEventMarker();

                eventStartTime = DateTime.Now;
                eventEndTime = eventStartTime.AddSeconds(config.eventDuration);

                if (config.activateVendingMachine)
                {
                    vendingMarker = new VendingMarkerFx(missionZone, config, GetMessage("vendingMachineTitle", ""), this);
                    log.Debug("Spawn Vending Machine Marker");
                    vendingMarker.Spawn();
                    vendingMarker.startTime = DateTime.Now;
                    vendingMarker.endTime = vendingMarker.startTime.Value.AddSeconds(config.eventDuration);


                    eventDurationTimer = timer.Every(5.0f, vendingMarker.UpdateVendingMarkerTitle);
                }
                timerCheckingPlayers = timer.Every(2.0f, CheckIfNewPlayersEnteredTheZone);
                timer.Once(3f, PrepareScenario);

                if (config.pveMode)
                {
                    sphere = new EventSphere(this, log, notification, missionZone);
                    pveMode = new PVEMode(sphere, log, notification);
                    pveMode.Activate();
                }

                notification.SendToAll(BasePlayer.activePlayerList, "StartedEvent");
                log.Info("Event started");
            });
        }

        private void PrepareScenario()
        {
            BaseEntities = new Dictionary<string, Vector3[]>()
            {
                {
                    prefabPaths["EliteCrate"],
                    new Vector3[]
                    {
                        new Vector3(-1.33f, 6.27f, -61.66f),
                        new Vector3(-3.64f, 6.27f, -61.86f),
                        new Vector3(-8.15f, 6.27f, -61.45f)
                    }
                },
                {
                    prefabPaths["LockedCrate"],
                    new Vector3[]
                    {
                        new Vector3(-3, 0, -64),
                        new Vector3(-3.81f, 6.27f, -63.93f)
                    }
                },
                {
                    prefabPaths["SandBag"],
                    new Vector3[]
                    {
                         new Vector3(-10.34f, 9.54f, -71.93f),
                         new Vector3(-6.01f, 10.25f, -61.86f),
                         new Vector3(-13.94f, 3.28f, -75.49f),
                         new Vector3(-8.16f, 10.30f, -70.22f)

                    }
                }
            };

            foreach (KeyValuePair<string, Vector3[]> entity in BaseEntities)
            {
                SpawnBasedEntities(entity.Key, entity.Value);
            }


            if (config.spawnSamSites)
            {
                SpawnSamSites();
            }

            SpawnComputerStation();

            log.Debug("Scenario Created");

        }

        private void SpawnComputerStation()
        {
            Vector3 computerLocation = new Vector3(-11.55f, 6.27f, -63.03f);
            var computerStation = GameManager.server.CreateEntity(
                prefabPaths["ComputerStation"],
                missionZone.GetLocationFromPoint(computerLocation),
                Quaternion.identity
            );

            ComputerStation computerStationComponent = computerStation.GetComponent<ComputerStation>();

            computerStationComponent.transform.rotation = Quaternion.Euler(0f, 80f, 0f);

            computerStation.Spawn();
            entities.Add(computerStation);
        }

        private void SpawnSamSites()
        {
            log.Debug("Spawning Sam Sites");
            Vector3[] samSiteLocations =
            {
                    new Vector3(39, 6.8f, -40),
                    new Vector3(-34.25f, 18.15f, 35.79f),
                    new Vector3(-4, 6.5f, -137),
            };
            SpawnBasedEntities(prefabPaths["SamSite"], samSiteLocations);
        }

        private void SpawnBasedEntities(string prefab, Vector3[] vectorlist)
        {

            int amount = vectorlist.Length;
            if (prefab.Contains("crate_elite"))
            {
                amount = config.eliteCratesAmount;
            }

            if (prefab.Contains("codelockedhackablecrate"))
            {
                amount = config.hackableCratesAmount;
            }

            int i = 0;
            foreach (Vector3 vec in vectorlist)
            {
                if (amount == i)
                {
                    return;
                }
                var position = missionZone.GetLocationFromPoint(vec);
                BaseEntity entity = GameManager.server.CreateEntity(
                    prefab,
                    position
                );

                entity.enableSaving = false;
                entity.Spawn();
                entities.Add(entity);

                if (prefab.Contains("codelockedhackablecrate"))
                {
                    hackableCrate.Add(entity.net.ID);
                }

                i++;
            }
        }

        /**
         * Simulate an explosion in the main building to create a fire
         *         
         */
        private void CreateAnExplosionInMainBuilding()
        {

            log.Debug("Creating Explosion in the main building");
            Vector3[] pointsOfExplosions =
            {
                new Vector3(-10.45f, 6.27f, -63.58f),
                new Vector3(-10.73f, 6.27f, -70.60f),
                new Vector3(-3.27f, 6.27f, -62.96f),
                new Vector3(1.12f, 6.22f, -64.85f),
                new Vector3(1.11f, 6.22f, -70.66f)
            };

            foreach (Vector3 point in pointsOfExplosions)
            {
                BaseEntity explosion = GameManager.server.CreateEntity(prefabPaths["MLRS"], missionZone.GetLocationFromPoint(point), Quaternion.identity);
                BaseEntity fire = GameManager.server.CreateEntity("assets/bundled/prefabs/fireball.prefab", missionZone.GetLocationFromPoint(point), Quaternion.identity);
                fire.Spawn();
                explosion.Spawn();
                entities.Add(explosion);
                entities.Add(fire);
            }

            SetSprinklers();
        }

        /**
         * Spawn Sprinklers after explosion to avoid Zombies die by Fire
         */
        private void SetSprinklers()
        {
            log.Debug("Adding Sprinklers");
            Vector3[] Sprinklers = new Vector3[]
            {
                new Vector3(-3.27f, 5.50f, -65.26f),
                new Vector3(0.03f, 6f, -71.06f),
                new Vector3(-8.81f, 6, -65.83f)
            };

            foreach (Vector3 point in Sprinklers)
            {
                BaseEntity sprinkler = GameManager.server.CreateEntity(prefabPaths["Sprinkler"], missionZone.GetLocationFromPoint(point), Quaternion.identity);

                Sprinkler sprinklerComponent = sprinkler.GetComponent<Sprinkler>();
                sprinklerComponent.WaterPerSplash = 40;
                sprinklerComponent.SplashFrequency = 1f;
                sprinklerComponent.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
                sprinklerComponent.SetSprinklerState(true);

                sprinkler.Spawn();
                entities.Add(sprinkler);
            }
        }

        /**
         * Vending Machines markers are use for having the title of the event
         * This title can contain the time remaining for the event and the owner of the event
         */


        internal class VendingMarkerFx
        {

            private PluginConfig config;
            private MissionZone map;
            private VendingMachineMapMarker? marker;
            private string langMessage;

            private string owner = "";

            public DateTime? endTime { get; set; }
            public DateTime? startTime { get; set; }

            private WaterTreatmentEvent plugin;

            public VendingMarkerFx(MissionZone map, PluginConfig config, string langMessage, WaterTreatmentEvent plugin)
            {
                this.config = config;
                this.map = map;
                this.langMessage = langMessage;
                this.plugin = plugin;
            }

            public void Spawn()
            {
                marker = (VendingMachineMapMarker)GameManager.server.CreateEntity(
                    plugin.prefabPaths["VendingMarker"],
                    map.EventCenter,
                    Quaternion.identity
                );
                marker.enableSaving = false;

                marker.markerShopName = config.nameOfTheEventForThePlayers;
                marker.Spawn();
            }

            public void SetEventOwner(string owner)
            {
                this.owner = $" ({owner}) ";
            }


            public void UpdateVendingMarkerTitle()
            {
                if (!endTime.HasValue)
                {
                    return;
                }

                TimeSpan remaining = endTime.Value - DateTime.Now;

                marker.markerShopName = string.Format(
                    langMessage,
                    config.nameOfTheEventForThePlayers,
                    owner,
                    (int)Math.Ceiling(remaining.TotalMinutes)
                );

                marker.SendNetworkUpdate();
            }

            public void Destroy()
            {
                try
                {
                    marker?.Kill();
                }
                catch (NullReferenceException)
                {

                }
            }
        }


        private void CreateEventMarker()
        {

            log.Debug("Trying to create the marker");

            var marker = GameManager.server.CreateEntity(
                prefabPaths["GenericRadiusMarker"],
                missionZone.EventCenter
            ) as MapMarkerGenericRadius;

            if (marker == null || config == null || config.markerAlpha == null || config.markerRadius == null || config.markerColor == null)
            {
                log.Debug($"Marker is null: {marker} Alpha: {config.markerAlpha} Radius: {config.markerRadius} Color: {config.markerColor}");
                log.Debug("Mark couldn't be added");
                return;
            }

            marker.alpha = config.markerAlpha;
            marker.radius = config.markerRadius;
            log.Debug("Get the coors for the marker");
            string[] colorValues = config.markerColor.Split(' ');

            int red = int.Parse(colorValues[0]);
            int green = int.Parse(colorValues[1]);
            int blue = int.Parse(colorValues[2]);

            log.Debug("Extract the right color");
            UnityEngine.Color extractedColor = new UnityEngine.Color(red / 255f, green / 255f, blue / 255f);

            marker.color1 = extractedColor;
            marker.name = "Evento de Water Treatment";
            log.Debug("Spawn Marker");
            marker.Spawn();

            log.Debug("Send Update");
            marker.SendUpdate();

            log.Debug("Add Marker to the entities");
            entities.Add(marker);
            log.Debug("Mark ready");

        }

        private void SpawnBradleyAPC()
        {
            if (config.spawnBradley == false)
            {
                return;
            }

            Vector3 pointA = missionZone.GetLocationFromPoint(new Vector3(17.03f, 0, -16.32f));
            Vector3 pointB = missionZone.GetLocationFromPoint(new Vector3(14.91f, 0, -115.23f));

            var bradley = GameManager.server.CreateEntity(prefabPaths["Bradley"], pointA) as BradleyAPC;

            if (bradley == null)
            {
                return;
            }

            // Bradley setup
            bradley.EnableSaving(false);
            bradley.Spawn();
            entities.Add(bradley);

            // Clear existing path and set new waypoints
            bradley.ClearPath();
            bradley.currentPath.Clear();
            bradley.currentPath.Add(pointA);
            bradley.currentPath.Add(pointB);

            // Start Bradley movement
            bradley.DoAI = true;
            bradley.DoSimpleAI();

            bradleyId = bradley.net.ID;

        }

        /**
         * Spawn an F15 to later drop a nuke
         */
        private void SpawnF15()
        {
            log.Debug("Spawning F15 for the end of the event");

            Vector3 startingPosition = new Vector3(-410.53f, 200, 130.09f);
            Vector3 targetPosition = missionZone.EventCenter;

            F15 jetF15 = (F15)GameManager.server.CreateEntity(prefabPaths["F15"], startingPosition);

            if (jetF15 == null)
            {
                log.Debug("Failed to create F15 entity.");
                return;
            }

            Vector3 direction = targetPosition - jetF15.transform.position;
            jetF15.transform.forward = direction.normalized;

            jetF15.transform.position = startingPosition;
            jetF15.movePosition = targetPosition;
            jetF15.defaultAltitude = 200f;
            jetF15.Spawn();
            timer.Once(1f, () => FireRocketWhenAtPosition(jetF15, targetPosition));

            if (jetF15 != null && !jetF15.IsDestroyed)
            {
                jetF15.Invoke(() => jetF15.Kill(), 15f);
                entities.Add(jetF15);
            }
        }

        private void FireRocketWhenAtPosition(F15 jetF15, Vector3 targetPosition)
        {
            if (jetF15 == null)
            {
                return;
            }

            float distanceToTarget = Vector3.Distance(jetF15.transform.position, targetPosition);
            float tolerance = 150f;

            if (distanceToTarget <= tolerance)
            {
                timer.Once(1.5f, () => FireRocket(targetPosition));
            }
        }

        public void FireRocket(Vector3 targetPosition)
        {
            log.Debug("Firing Rocket");
            Vector3 launchPosition = new Vector3(targetPosition.x, 150, targetPosition.z);
            Vector3 direction = (targetPosition - launchPosition).normalized;

            MLRSRocket rocket = (MLRSRocket)GameManager.server.CreateEntity(prefabPaths["MLRS"], launchPosition);
            if (rocket == null)
            {
                return;
            }

            Rigidbody rigidBody = rocket.GetComponent<Rigidbody>();
            if (rigidBody == null)
            {
                return;
            }

            rocket.Spawn();
            waitingForRocket = true;

            log.Debug("Rocket Launched");

            if (config.addRadiationForEndOfEvent)
            {
                CreateRadiationZone(missionZone.EventCenter);
            }

            entities.Add(rocket);
        }



        /**
         * @todo check if this works
         */
        private void OnCollisionEnter(Collision collision)
        {

            if (waitingForRocket && collision.gameObject.GetComponent<MLRSRocket>() != null)
            {
                Effect.server.Run(prefabPaths["MLRS"], collision.transform.position);
                log.Debug("Rocket Impacted");

                waitingForRocket = false;
            }
        }

        #region RadiationRegion

        private class RadiationZone : MonoBehaviour
        {
            private TriggerRadiation triggerRadiation;
            public float radius;
            public float amount;

            private const int PLAYER_MASK = 131072;

            private void Awake()
            {
                gameObject.layer = (int)Rust.Layer.Reserved1;
                enabled = false;
            }

            private void OnDestroy() => Destroy(gameObject);

            public void InitializeRadiationZone(string type, Vector3 position, float radius, float amount)
            {
                this.radius = radius;
                this.amount = amount;

                gameObject.name = type;
                transform.position = position;
                SphereCollider sphereCollider = gameObject.GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = radius;

                triggerRadiation = gameObject.GetComponent<TriggerRadiation>() ?? gameObject.AddComponent<TriggerRadiation>();
                triggerRadiation.RadiationAmountOverride = amount;
                triggerRadiation.interestLayers = PLAYER_MASK;
                triggerRadiation.enabled = true;
            }

            public void Deactivate() => triggerRadiation?.gameObject.SetActive(false);

            public void Reactivate() => triggerRadiation?.gameObject.SetActive(true);
        }

        private void CreateRadiationZone(Vector3 center)
        {
            RadiationZone newZone = new GameObject().AddComponent<RadiationZone>();
            newZone.InitializeRadiationZone("water treatment plant", center, 50f, 50f);
            radiationZones.Add(newZone);

            log.Debug("Activate Radiation in the event");
        }

        #endregion

        private void SpawnNPCs()
        {

            if (config.spawnZombies)
            {
                if (config.createExplosionInMainBuilding)
                {
                    CreateAnExplosionInMainBuilding();
                }

                timer.Once(20f, SpawnZombies);
            }

            if (config.spawnGuards)
            {
                SpawnGuards();
            }

            if (config.spawnSnipers)
            {
                SpawnSnipers();
            }

        }

        private void SpawnZombies()
        {
            for (int i = 0; i <= config.zombiesAmount; i++)
            {
                SpawnZombie();
            }
        }

        private void SpawnZombie()
        {
            var position = missionZone.GetLocationFromPoint(new Vector3(-3.5f, 0.5f, -64.5f));

            var zombie = GameManager.server.CreateEntity(
                prefabPaths["Zombie"],
                position
            );
            zombie.Spawn();
            entities.Add(zombie);
        }

        private void SpawnSnipers()
        {
            List<Vector3> vectorList = new List<Vector3>
            {
                new Vector3(-34.26f, 18.56f, 35.06f),
                new Vector3(95.28f, 8.63f, -51.79f),
                new Vector3(69.60f, 12.78f, -18.55f),
                new Vector3(-5.86f, 56.76f, 71.55f),
            };

            JObject snipersSettings = GetNPCSettings(config.snipersSettings);

            foreach (Vector3 vec in vectorList)
            {
                var position = missionZone.GetLocationFromPoint(vec);
                ScientistNPC npc = NpcSpawn?.Call("SpawnNpc", position, snipersSettings) as ScientistNPC;
                entities.Add(npc);
            }
        }

        private void SpawnGuards()
        {

            JObject guardSetting = GetNPCSettings(config.guardsSettings);

            /**
             * Locations over the monument where the Guards are going to be located
             */
            List<Vector3> guardsPositions = new List<Vector3>
             {
                new Vector3 (-11.73f, 6.64f, 12.92f),
                new Vector3 (1.75f, 2.86f, 2.79f),
                new Vector3 (-8.03f, 0.24f, -30.79f),
                new Vector3 (-5.81f, 1.32f, -51.94f),
                new Vector3 (-25.67f, 0.12f, -96.65f),
                new Vector3 (-7.01f, 0.24f, -103.07f),
                new Vector3 (-6.86f, 6.65f, -98.09f),
                new Vector3 (-52.44f, 1.27f, -97.14f),
                new Vector3 (-51.11f, 1.28f, -64.23f),
                new Vector3 (-56.60f, 0.24f, 3.77f),
                new Vector3 (-22.38f, 0f, -51.99f),
                new Vector3 (-4.48f, 7.41f, -73.75f),
                new Vector3 (-15.62f, 3.25f, -69.20f),
                new Vector3 (38.65f, 1.15f, -96.82f)
            };

            System.Random rand = new System.Random();
            foreach (Vector3 pos in guardsPositions)
            {
                int guardsToSpawn = rand.Next(1, 3);

                for (int i = 0; i <= guardsToSpawn; i++)
                {
                    var position = missionZone.GetLocationFromPoint(pos);
                    ScientistNPC npc = NpcSpawn?.Call("SpawnNpc", position, guardSetting) as ScientistNPC;
                    entities.Add(npc);
                }

            }
        }

        private static JObject GetNPCSettings(PluginConfig.NpcSettings config)
        {
            HashSet<string> states = config.Stationary
                ? new HashSet<string> { "IdleState", "CombatStationaryState" }
                : new HashSet<string> { "RoamState", "ChaseState", "CombatState" };

            if (config.BeltItems.Any(x => x.ShortName == "rocket.launcher" || x.ShortName == "explosive.timed"))
            {
                states.Add("RaidState");
            }

            return new JObject
            {
                ["Name"] = config.Name,
                ["WearItems"] = new JArray { config.ClothingItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                ["BeltItems"] = new JArray { config.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                ["Kit"] = config.Kit,
                ["Health"] = config.Health,
                ["RoamRange"] = config.RoamRange,
                ["ChaseRange"] = config.ChaseRange,
                ["SenseRange"] = config.SenseRange,
                ["ListenRange"] = config.SenseRange / 2f,
                ["AttackRangeMultiplier"] = config.AttackRangeMultiplier,
                ["CheckVisionCone"] = config.CheckVisionCone,
                ["VisionCone"] = config.VisionCone,
                ["HostileTargetsOnly"] = false,
                ["DamageScale"] = config.DamageScale,
                ["TurretDamageScale"] = 1f,
                ["AimConeScale"] = config.AimConeScale,
                ["DisableRadio"] = config.DisableRadio,
                ["CanRunAwayWater"] = true,
                ["CanSleep"] = false,
                ["SleepDistance"] = 100f,
                ["Speed"] = config.Speed,
                ["AreaMask"] = 1,
                ["AgentTypeID"] = -1372625422,
                ["HomePosition"] = string.Empty,
                ["MemoryDuration"] = config.MemoryDuration,
                ["States"] = new JArray { states }
            };
        }

        private void StartEndingEventDueToUserCompleted()
        {
            if (startEnding)
            {
                log.Debug("Start ending already initiated before");
                return;
            }

            startEnding = true;
            eventEndTime = DateTime.Now.AddSeconds(config.cooldownAfterCompletedEvent);

            notification.SendToAll(playersOnTheEvent, "AlertOfNuke");
            log.Info("Event is finishing due to some players loot the crates already");
            timer.Once(config.cooldownAfterCompletedEvent, () => { EndEvent(); });
        }

        void EndEvent()
        {
            log.Info("Finishing The Event");


            SpawnF15();

            timer.In(config.eventEndWarningTime, () =>
            {
                ForceEndEvent();
                notification.SendToAll(BasePlayer.activePlayerList, "EndEvent");
                Interface.Oxide.CallHook("OnWaterTreatmentEventEnded", missionZone.EventCenter, missionZone.ZoneRadius);
                StartTimer();
            });


        }

        private void ForceEndEvent()
        {
            UnSubscribeHooks();
            foreach (var entity in entities)
            {
                if (entity != null && !entity.IsDestroyed)
                {
                    entity.Kill();
                }
            }

            foreach (var zone in radiationZones)
            {
                if (zone != null)
                {
                    UnityEngine.Object.Destroy(zone.gameObject);
                }
            }

            bradleyId = null;
            hackableCrate?.Clear();
            playersOnTheEvent.Clear();
            radiationZones.Clear();

            if (vendingMarker != null)
            {
                vendingMarker.Destroy();
            }

            if (timerCheckingPlayers != null)
            {
                timerCheckingPlayers.Destroy();
            }

            started = false;
            waitingForRocket = false;

            if (pveMode != null)
            {
                pveMode.ResetOwner();
            }

            if (eventDurationTimer != null)
            {
                eventDurationTimer.Destroy();
            }


        }

        #region Logging 
        private Log log;

        private class Log
        {
            private PluginConfig pluginConfig;
            private string logFilePath;
            private string logDirectory;


            const string ERROR_LEVEL = "ERROR";
            const string WARNING_LEVEL = "WARNING";
            const string INFO_LEVEL = "INFO";
            const string DEBUG_LEVEL = "DEBUG";

            private WaterTreatmentEvent plugin;

            public Log(PluginConfig config, WaterTreatmentEvent plugin)
            {
                pluginConfig = config;

                this.plugin = plugin;

                logDirectory = $"{Interface.Oxide.LogDirectory}/WaterTreatmentEvent";

                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                logFilePath = $"{logDirectory}/Log_{DateTime.Now:yyyy-MM-dd}.txt";

            }

            public void Info(string message)
            {
                LogIntoFile(message, INFO_LEVEL);


                Debug($"Sending Discord Message {pluginConfig.discord}");
                if (pluginConfig.discord)
                {
                    plugin.SendDiscordMessage(message);
                }
            }

            public void Debug(string message)
            {
                LogIntoFile(message, DEBUG_LEVEL);
            }

            public void Error(string message)
            {
                LogIntoFile(message, ERROR_LEVEL);
            }

            public void Warning(string message)
            {
                LogIntoFile(message, WARNING_LEVEL);
            }

            private void LogIntoFile(string message, string type)
            {

                var logMessage = $"{DateTime.Now:HH:mm:ss} [WaterTreatmentEvent] [{type}] {message}";

                try
                {
                    File.AppendAllText(logFilePath, logMessage + Environment.NewLine);

                    if ((type == ERROR_LEVEL || type == WARNING_LEVEL || type == INFO_LEVEL) || pluginConfig.debug == true)
                    {
                        plugin.Puts($"[{type}] {logMessage}");
                    }
                }
                catch (Exception ex)
                {
                    plugin.Puts("[ERROR] We couldn't save the logs into file, so we will show it in the console");
                    plugin.Puts($"[ERROR] {ex.Message}");
                    plugin.Puts(logMessage);
                }

            }
        }
        #endregion


        #region PVE Mode

        private class PVEMode
        {
            BasePlayer owner;
            EventSphere sphere;
            Log log;
            NotificationCenter notification;

            public PVEMode(EventSphere sphereEntity, Log log, NotificationCenter notification)
            {
                this.sphere = sphereEntity;
                this.log = log;
                this.notification = notification;
            }

            public bool HasOwner()
            {
                return owner != null;
            }

            public BasePlayer GetOwner()
            {
                return owner;
            }

            public void ResetOwner()
            {
                owner = null;
            }

            public void AddOwner(BasePlayer player)
            {
                log.Debug($"trying to add {player.displayName} as a owner");
                if (owner)
                {
                    return;
                }
                owner = player;

                notification.Send(player, "YouAreTheNewOwner");

                log.Info($"{owner.displayName} is now the owner of the Event");
            }

            /** 
             * if the player is the owner will be accepted otherwise will be ejected
             */
            public bool CheckIfIsAllowed(BasePlayer player)
            {
                if (owner == null)
                {
                    return true;
                }

                if (player.net.ID == owner.net.ID)
                {
                    return true;
                }

                if (IsPlayerATeamMemberOfTheOwner(player))
                {
                    log.Debug($"{player.displayName} is a team member of the owner {owner.displayName}");
                    return true;
                }

                log.Debug($"{player.displayName} will be ejected from the event");

                sphere.ejectPlayer(player);

                return false;
            }

            public void Activate()
            {
                sphere.Create();

            }

            private bool IsPlayerATeamMemberOfTheOwner(BasePlayer player)
            {
                try
                {
                    if (owner == null || owner.Team == null)
                    {
                        return false;
                    }

                    if (owner.Team.members.Count == 0)
                    {
                        return false;
                    }

                    return owner.Team.members.Contains(player.userID);
                }
                catch (NullReferenceException e)
                {
                    log.Error($"Error trying to determinate if the player belongs to a team {player.displayName}");
                    return false;
                }

            }

            public string GetOwnerName()
            {
                if (owner)
                {
                    return owner.displayName;
                }
                return "";
            }
        }

        private class EventSphere
        {
            private readonly BasePlayer eventOwner;
            private SphereEntity sphereEntity;
            private readonly WaterTreatmentEvent parent;
            private readonly Log log;
            private readonly NotificationCenter notification;
            private readonly MissionZone mapHelper;

            Vector3 rejectionArea = new Vector3(-20.30f, -0.36f, -208.45f);

            public EventSphere(WaterTreatmentEvent parent, Log log, NotificationCenter notification, MissionZone mapHelper)
            {
                this.parent = parent;
                this.log = log;
                this.notification = notification;
                this.mapHelper = mapHelper;
            }
            public void Create()
            {
                const string spherePrefab = "assets/prefabs/visualization/sphere.prefab";
                Vector3 center = mapHelper.EventCenter;

                float radius = mapHelper.ZoneRadius * (parent.config.PVEModeSphereRadius != null ? parent.config.PVEModeSphereRadius : 2f);

                int numberOfSpheres = 5;

                for (int i = 0; i < numberOfSpheres; i++)
                {
                    SphereEntity sphereEntity = (SphereEntity)GameManager.server.CreateEntity(
                        spherePrefab, center, Quaternion.identity
                    );

                    if (sphereEntity == null)
                    {
                        throw new Exception($"Couldn't create the sphere entity for iteration {i + 1} of PVE Mode.");
                    }

                    sphereEntity.EnableSaving(false);
                    sphereEntity.EnableGlobalBroadcast(false);

                    sphereEntity.currentRadius = radius;
                    sphereEntity.lerpRadius = radius;

                    sphereEntity.Spawn();

                    sphereEntity.UpdateScale();
                    sphereEntity.SendNetworkUpdateImmediate();

                    parent.entities.Add(sphereEntity);
                }

                log.Debug($"Created {numberOfSpheres} overlapping spheres to make the event zone appear darker.");
            }

            public void ejectPlayer(BasePlayer player)
            {
                player.EnsureDismounted();
                Vector3 direction = player.transform.position - mapHelper.EventCenter;
                direction.Normalize();
                Vector3 newPosition = player.transform.position + direction * 10f;

                player.MovePosition(newPosition);
                player.Teleport(newPosition);

                player.SendNetworkUpdateImmediate();

                log.Debug($"{player.displayName} has been ejected due to not been part of the owner of the event team");
                notification.Send(player, "NotThePlayerOwnerOrTeam");
            }
        }
        #endregion

        #region Notification Center
        /**
         * Interface to be implemented for each adapter for notifications
         */
        public interface INotificationAdapter
        {
            public void Send(BasePlayer player, string message, params object[] args);
            public void SetReference(Oxide.Core.Plugins.Plugin pluginReference);
        }

        /**
         * Centralize Notification center, compatible with several adapters 
         */
        private class NotificationCenter
        {
            private readonly PluginConfig.NotificationConfig pluginConfig;
            private INotificationAdapter adapter;
            private WaterTreatmentEvent parent;

            public NotificationCenter(PluginConfig.NotificationConfig config, INotificationAdapter notificationAdapter, WaterTreatmentEvent pluginInstance)
            {
                pluginConfig = config;
                adapter = notificationAdapter;
                parent = pluginInstance;
            }


            public void SendToAll(ListHashSet<BasePlayer> players, string message, params object[] args)
            {
                foreach (BasePlayer player in players)
                {
                    Send(player, message, args);
                }
            }

            public void Send(BasePlayer player, string message, params object[] args)
            {
                message = string.Format(message, args);
                adapter.Send(player, parent.GetMessage(message, player.UserIDString), args);
            }

        }

        /** 
         * Adapter for normal Rust chat messages
         */
        private class ChatNotifications : INotificationAdapter
        {
            private readonly PluginConfig.NotificationConfig notificationConfig;
            private readonly Log log;

            public ChatNotifications(PluginConfig.NotificationConfig config, Log logger)
            {
                notificationConfig = config;
                log = logger;
            }

            public void SetReference(Oxide.Core.Plugins.Plugin pluginReference)
            {
            }

            public void Send(BasePlayer player, string message, params object[] args)
            {
                string messageFormatted = FormatMessage(string.Format(message, args));
                player.ChatMessage(messageFormatted);
            }

            private string FormatMessage(string message)
            {
                return string.Format(
                    "<size={0}><color={1}>{2}</color></size> <size={3}><color={4}>{5}</color></size>",
                    notificationConfig.chatConfiguration.messagePrefixSize,
                    notificationConfig.chatConfiguration.messagePrefixColor,
                    notificationConfig.messagePrefix,
                    notificationConfig.chatConfiguration.messageSize,
                    notificationConfig.chatConfiguration.messageColor,
                    message
                );
            }

        }

        /** 
         * Adapter for normal Rust chat messages
         */
        private class GUIAnnouncementNotifcations : INotificationAdapter
        {
            PluginConfig.NotificationConfig notificationConfig;
            Log log;

            Oxide.Core.Plugins.Plugin plugin;

            public GUIAnnouncementNotifcations(PluginConfig.NotificationConfig config, Log logger)
            {
                notificationConfig = config;
                log = logger;
            }


            public void SetReference(Oxide.Core.Plugins.Plugin pluginReference)
            {
                plugin = pluginReference;
            }

            public void Send(BasePlayer player, string message, params object[] args)
            {

                string messageFormatted = string.Format(message, args);
                plugin.Call(
                    "CreateAnnouncement",
                    messageFormatted,
                    notificationConfig.guiAnnouncementConfiguration.bannerTintColor,
                    notificationConfig.guiAnnouncementConfiguration.textColor,
                    player
                );
            }
        }
        #endregion

        public void SendDiscordMessage(string message)
        {
            try
            {
                string jsonPayload = JsonConvert.SerializeObject(new
                {
                    content = message
                });

                using (WebClient client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    client.UploadString(config.discordWebhookUrl, "POST", jsonPayload);
                }
                log.Debug($"Message sent to Discord {message} ");

            }
            catch (Exception ex)
            {
                log.Error("Error sending message to Discord: " + ex.Message);
            }
        }
        private void Loaded()
        {
            if (NpcSpawn == null)
            {
                throw new MissingComponentException(
                    "SpawnNPCs is a required package, you can get it for free here https://codefling.com/extensions/npc-spawn"
                );
            }
        }
    }

}
