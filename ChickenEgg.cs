using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{ 
    [Info("ChickenEgg", "LAGZYA", "2.2.0")]
    public class ChickenEgg : RustPlugin
    {
        private const bool En = false;
        #region Cfg

        public static ChickenEgg ins;
        private ConfigData cfg { get; set; } 

        private class ConfigData 
        {      
            [JsonProperty(En ? "Time it takes for an egg to grow" : "Время через которое вырастит яйцо")]
            public double timerRost = 900;

            [JsonProperty(En ? "Time it takes a hen to lay an egg" : "Время которое нужно потратить курице, чтобы снести яйцо")]
            public double time = 1800;
  
            [JsonProperty(En ? "How many eggs can a hen lay at most (When the hen overcomes this number will be:1. If the lifetime of the chicken is -1, then the chicken will die. 2.If the time of the chicken is not equal to -1, then the chicken will die after the time specified in the lifetime." : "Сколько яиц максимум может снести курица(Когда курица преодалеет данное кол-во будет:1.Если время жизни курицы равно -1,то курица умрет. 2.Если время курицы не равно -1, то курица умрет через время указаное во времени жизни.")]
            public int eggmax = 30;

            [JsonProperty(En ? "How long will the chicken live (Leave -1 if you want off)" : "Сколько по времени будет жить курица(Оставить -1, если хотите откл)")]
            public double lifetime = 1800;

            [JsonProperty(En ? "Turning on the food system? (If yes, then the settings will be completely different; everything that you set up above will not work, except for the growth of the egg)" :
                "Включаем систему еды? (Если да то настройки будут полностью другие все что вы настраивали выше работать не будет, кроме роста яйца)")]
            public bool eatsystem = true;

            [JsonProperty(En ? "[FOOD SYSTEM SETTINGS] How long will the chicken live (Do not rise above this limit)" :
                "[НАСТРОЙКИ СИСТЕМЫ ЕДЫ] Сколько по времени будет жить курица(Выше этой границы не поднимиться)")]
            public double eatsystemlifetime = 1800;

            [JsonProperty(En ? "[FOOD SYSTEM SETTINGS] How often will the chicken eat?":"[НАСТРОЙКИ СИСТЕМЫ ЕДЫ] Как часто курица будет есть?")]
            public float eatsystemeatRemove = 25;

            [JsonProperty(En ? "[FOOD SYSTEM SETTINGS] How long does it take a chicken to lay an egg" : "[НАСТРОЙКИ СИСТЕМЫ ЕДЫ] Время которое нужно потратить курице, чтобы снести яйцо")]
            public double eatsystemtime = 60;
            [JsonProperty(En ? "[FOOD SYSTEM SETTINGS] Enable adding time from food not listed?" : "[НАСТРОЙКИ СИСТЕМЫ ЕДЫ] Включить добавление времени от еды которой нету в списке?")]
            public bool tryeatsystemTimeAdd = false;
            [JsonProperty(En ? "[FOOD SYSTEM SETTINGS] If food is not on the list, how much time should I add?" : "[НАСТРОЙКИ СИСТЕМЫ ЕДЫ] Если еды нет в списке сколько времени прибавлять?")]
            public double eatsystemTimeAdd = 5;

            [JsonProperty(En ? "[FOOD SYSTEM SETTINGS] How much life will food add?" : "[НАСТРОЙКИ СИСТЕМЫ ЕДЫ] Сколько времени к жизнь будет прибавлять еда?")]
            public Dictionary<string, double> _eatTime = new Dictionary<string, double>();
            [JsonProperty(En ? "Time after which the bag will disappear after death" : "Время через которое пропадет сумка после смерти")]
            public float TimerRemove = 300;
            [JsonProperty(En ? "Skin ID Eggs (Which Grows)" : "Скин айди яйца(Которое ростет)")]
            public ulong skinIdRost = 2053576055;

            [JsonProperty(En ? "Skin ID Eggs (Which breaks)" : "Скин айди яйца(Которое разбивается)")]
            public ulong skinIdEgg = 2053584503;

            [JsonProperty(En ? "What is the chance that a chicken will lay an egg from which another chicken can be raised?" : "Каков шанс того что курица снесет яйцо из которого можно вырастить еще курицу?")]
            public float chickenshansstart = 25f;

            [JsonProperty(En ? "What is the chance that the chicken will lay an egg that can drop items?" : "Каков шанс того что курица снесет яйцо из которого могут выпасть предметы?")]
            public float chickenshans = 100f;

            [JsonProperty(En ? "What is the chance that a chicken egg will drop when harvesting a chicken carcass?" : "Каков шанс что выпадет яйцо курицы при добыче туши курицы?")]
            public float shans = 2f;
            [JsonProperty(En ? "Enable chat notification when you get an egg?" : "Включить оповещение в чат когда добыл яйцо?")] 
            public bool annonce = false;
            [JsonProperty(En ? "The ability to damage chickens is only available to build owners?" : "Возможность наносить урон курицам, есть только у владельцев билды?")] 
            public bool damageTrue = false;
            
            [JsonProperty(En ? "The ability to loot the inventory of a chicken, is there only for the owners of the build?" : "Возможность залутать инвентарь курицы, есть только у владельцев билды?")] 
            public bool lootonlyauth = false;

            [JsonProperty(En ? "Items that drop after breaking an egg" : "Предметы которые выпадают, после разбития яйца")]
            public List<ItemDrop> _itemList = new List<ItemDrop>();

            internal class ItemDrop
            { 
                [JsonProperty(En ? "Enabled?" : "Включить?")] public bool Enabled = true; 
                [JsonProperty(En ? "Shortname" : "Шортнейм")] public string shortname = "rifle.ak";
                [JsonProperty(En ?  "Display name(Leave blank if default)" : "Название(Оставить пустым,если стандартное)")] public string DisplayName = "";
                [JsonProperty(En ? "SkinId" : "СкинАйди")] public ulong SkinId = 0;
                [JsonProperty(En ? "Min" : "Мин. кол-во")] public int Min = 1;
                [JsonProperty(En ? "Max" : "Макс. кол-во")] public int Max = 1;
            } 

            public static ConfigData GetNewConf()
            {
                var newConfig = new ConfigData
                {
                    _itemList = new List<ItemDrop>
                    {
                        new ItemDrop(),
                        new ItemDrop()
                        {
                            Enabled = true,
                            shortname = "wood",
                            Min = 1000,
                            Max = 5000
                        }
                    },
                    _eatTime = new Dictionary<string, double>()
                    {
                        ["chicken.cooked"] = 10f,
                        ["corn"] = 2f
                    }
                };
                return newConfig;
            }
        }

        protected override void LoadDefaultConfig()
        {
            cfg = ConfigData.GetNewConf();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(cfg);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultMessages()
        {
            var ru = new Dictionary<string, string>();
            foreach (var rus in new Dictionary<string, string>()
            {
                ["CHICKEN_NAME"] = "<size=20>Домашняя курица</size>",
                ["CHICKEN_TIMETOEGG"] = "Курица снесет яйцо через: {0}",
                ["CHICKEN_TIMETODEATH"] = "Курица умрет через: {0}",
                ["CHICKEN_EGGCOUNT"] = "Курица снесет еще: {0} яиц",
                ["CHICKEN_EGGCOUNTDEATH"] = "Курица умрет когда снесет еще: {0} яиц",
                ["CHICKEN_EGGMAX"] = "Курица снесла максимальное кол-во яиц",
                ["CHICKEN_FOODNEED"] = "Положите еды, чтобы предотвратить смерть курицы",
                ["CHICKEN_EGGTOCHICKEN"] = "Яйцо вылупится через: {0}",
                ["CHICKEN_BACKPACK"] = "Рюкзак от курочки", 
                ["CHICKEN_PLACE"] = "Поставьте курочку около кормушки.", 
                ["ANNONCE"] = "Вам удалось добыть яйцо курицы."
            }) ru.Add(rus.Key, rus.Value);
            lang.RegisterMessages(ru, this, "ru");
            ru = new Dictionary<string, string>
            {
                ["CHICKEN_NAME"] = "<size=20>Homemade Chicken</size>",
                ["CHICKEN_TIMETOEGG"] = "Chicken will lay an egg in: {0}",
                ["CHICKEN_TIMETODEATH"] = "Chicken will die in: {0} ",
                ["CHICKEN_EGGCOUNT"] = "Chicken will lay: {0} more eggs",
                ["CHICKEN_EGGCOUNTDEATH"] = "Chicken will die when it lays: {0} more eggs",
                ["CHICKEN_EGGMAX"] = "Chicken will lay the maximum number of in eggs",
                ["CHICKEN_FOODNEED"] = "Put food to prevent chicken from dying",
                ["CHICKEN_EGGTOCHICKEN"] = "Egg will hatch in: {0}", 
                ["CHICKEN_BACKPACK"] = "Chicken Backpack",
                ["CHICKEN_PLACE"] = "Place the chicken near the feeder.",
                ["ANNONCE"] = "You have obtained a chicken egg."
            };
            lang.RegisterMessages(ru, this, "en");
        }

        #endregion

        #region ClasOrComp

        public class Chickens
        {
            public double TimeNextEgg;
            public double LifeTime;
            public int EggComplete = 0;
            public ulong OwnerId;
            public Vector3 position;
            public List<StorageChicken> EggList = new List<StorageChicken>();
            internal double IsLive => Math.Max(LifeTime - CurrentTime(), 0);
            internal double IsCoolDown => Math.Max(TimeNextEgg - CurrentTime(), 0);
        }

        public class StorageChicken
        {
            public string shortname;
            public int amount;
            public ulong skin;
        }

        private Dictionary<ulong, Chickens> _itemList = new Dictionary<ulong, Chickens>();

        private Dictionary<ulong, Egg> _eggList = new Dictionary<ulong, Egg>();

        private class Egg
        {
            public ulong ownerId;
            public double TimeSpawnChicken;
            public Vector3 position;
            internal double IsCoolDown => Math.Max(TimeSpawnChicken - CurrentTime(), 0);
        }

        private class StartEgg : MonoBehaviour
        {
            public Dictionary<BasePlayer, bool> PlayersList = new Dictionary<BasePlayer, bool>();
            private BaseEntity entity;
            private Egg f;
            private Vector3 pos;
            private ulong Id;
            private BaseEntity ent;

            private void Awake()
            {
                entity = GetComponent<BaseEntity>();
                pos = entity.transform.position;
                Id = entity.net.ID.Value;
                InvokeRepeating("EggRost", 1f, 1f);
                var entities = Physics.OverlapSphereNonAlloc(pos, 1.5f, colBuffer, dLayer,
                    QueryTriggerInteraction.Collide);
                if (entities != 0)
                    for (var i = 0; i < entities; i++)
                    {
                        var entity2 = colBuffer[i].GetComponentInParent<BaseEntity>();
                        if (entity2.ShortPrefabName == "hitchtrough.deployed")
                        {
                            ent = entity2;
                            return;
                        }
                    }
            }

            public void OnDestroy()
            {
                if (!entity.IsDestroyed) entity.Kill();
                Destroy(this);
            }

            public void CheckTrigger()
            {
                var entities = Physics.OverlapSphereNonAlloc(entity.transform.position, 3, colBuffer, playerLayer,
                    QueryTriggerInteraction.Collide);
                if (entities != 0)
                    for (var i = 0; i < entities; i++)
                    {
                        var player = colBuffer[i].GetComponentInParent<BasePlayer>();

                        if (player != null && !player.IsSleeping() && !player.IsDead() && !player.IsNpc &&
                            player.userID.IsSteamId() && !PlayersList.ContainsKey(player))
                            PlayersList.Add(player, player.IsAdmin);
                    }
            }

            private void EggRost()
            {
                if (ent == null)
                {
                    ins._eggList.Remove(Id);
                    Destroy(this);
                    return;
                }

                if (!ins._eggList.TryGetValue(Id, out f)) return;
                if (f.IsCoolDown > 0)
                {
                    Teg();
                }
                else
                {
                    ins.SpawnChicken(pos, f.ownerId, new List<StorageChicken>(),
                        ins.cfg.eatsystem ? CurrentTime() + ins.cfg.eatsystemtime : CurrentTime() + ins.cfg.time,
                        ins.cfg.eatsystem
                            ? CurrentTime() + ins.cfg.eatsystemlifetime
                            : CurrentTime() + ins.cfg.lifetime, 0);
                    ins._eggList.Remove(Id);
                    Destroy(this);
                    return;
                }
            }

            private void Teg()
            {
                CheckTrigger();
                foreach (var player in PlayersList)
                    if (player.Key != null)
                    {
                        if (Vector3.Distance(player.Key.transform.position, pos) <= 3)
                        {
                            Vector3 vectors;
                            vectors = entity.CenterPoint() + new Vector3(0, 0.5f, 0);
                            string text;
                            text =
                                $"{ins.lang.GetMessage("CHICKEN_NAME", ins, player.Key.UserIDString)}\n{string.Format(ins.lang.GetMessage("CHICKEN_EGGTOCHICKEN", ins, player.Key.UserIDString), FormatTime(TimeSpan.FromSeconds(f.IsCoolDown), "ru"))}";
                            if (!player.Value) SetPlayerFlag(player.Key, BasePlayer.PlayerFlags.IsAdmin, true);
                            player.Key.SendConsoleCommand("ddraw.text", 1.01f + Time.deltaTime, Color.white, vectors,
                                $"{text}");
                            if (!player.Value) SetPlayerFlag(player.Key, BasePlayer.PlayerFlags.IsAdmin, false);
                        }
                        else
                        { 
                            PlayersList.Remove(player.Key);
                            return;
                        }
                    }
                    else
                    {
                        PlayersList.Remove(player.Key);
                        return;
                    }
            }

            private void SetPlayerFlag(BasePlayer player, BasePlayer.PlayerFlags f, bool b)
            {
                if (b)
                {
                    if (player.HasPlayerFlag(f)) return;
                    player.playerFlags |= f;
                }
                else
                {
                    if (!player.HasPlayerFlag(f)) return;
                    player.playerFlags &= ~f;
                }

                player.SendNetworkUpdateImmediate(false);
            }
        }

        private static readonly int playerLayer = LayerMask.GetMask("Player (Server)");
        private static readonly Collider[] colBuffer = Vis.colBuffer;

        private class ChickenStartEgg : MonoBehaviour
        { 
            public Dictionary<BasePlayer, bool> PlayersList = new Dictionary<BasePlayer, bool>();
            private Chicken entity;
            private Chickens f;
            private Vector3 pos;
            private HitchTrough ent;
            public double nextEat = 0;
            internal double IsNextEat => Math.Max(nextEat - CurrentTime(), 0);

            private void Awake()
            {
                entity = GetComponent<Chicken>();
                pos = entity.transform.position;
                var entities = Physics.OverlapSphereNonAlloc(pos, 1.5f, colBuffer, dLayer, QueryTriggerInteraction.Collide);
                if (entities != 0)
                    for (var i = 0; i < entities; i++)
                    {
                        var entity2 = colBuffer[i].GetComponentInParent<HitchTrough>();
                        if (entity2 != null && entity2.ShortPrefabName == "hitchtrough.deployed")
                        {
                            ent = entity2;
                            break;
                        }
                    }

                InvokeRepeating("AddEgg", 1f, 1f);
            }

            private void TryAddEgg()
            {
                if (!ins.cfg.eatsystem)
                    if (ins.cfg.eggmax != -1 && f.EggComplete >= ins.cfg.eggmax) return;

                if (f.IsCoolDown > 0) return;

                if (ins.cfg.eatsystem)
                    f.TimeNextEgg = CurrentTime() + ins.cfg.eatsystemtime;
                else f.TimeNextEgg = CurrentTime() + ins.cfg.time;

                if (f.EggList.Count < 6)
                {
                    if (Core.Random.Range(0f, 100f) <= ins.cfg.chickenshansstart)
                    {
                        var find = f.EggList.Find(p => p.skin == ins.cfg.skinIdRost);
                        if (find != null)
                            find.amount += 1;
                        else
                            f.EggList.Add(new StorageChicken()
                            {
                                shortname = "sticks",
                                skin = ins.cfg.skinIdRost,
                                amount = 1
                            });

                        f.EggComplete++; 
                    }

                    if (!ins.cfg.eatsystem)
                        if (ins.cfg.eggmax != -1 && f.EggComplete >= ins.cfg.eggmax) return;

                    if (Core.Random.Range(0f, 100f) <= ins.cfg.chickenshans)
                    {
                        var find = f.EggList.Find(p => p.skin == ins.cfg.skinIdEgg);
                        if (find != null)
                            find.amount += 1;
                        else
                            f.EggList.Add(new StorageChicken()
                            { 
                                shortname = "easter.goldegg",
                                skin = ins.cfg.skinIdEgg,
                                amount = 1
                            });

                        f.EggComplete++;
                    }
                }
            }

            private void TryEat(Item item)
            {
                double ss;
                if (!ins.cfg._eatTime.TryGetValue(item.info.shortname, out ss)) ss = ins.cfg.eatsystemTimeAdd;

                if (f.LifeTime + ss < ins.cfg.eatsystemlifetime + CurrentTime())
                {
                    item.UseItem(1);
                    if (item.amount <= 0) item.Remove();

                    f.LifeTime += ss;
                }
 
                nextEat = ins.cfg.eatsystemeatRemove + CurrentTime();
            }

            private Item GetFoodItem()
            {
                foreach (var obj in ent.inventory.itemList.Where(obj => obj.info.category == ItemCategory.Food))
                {
                    if (ins.cfg.tryeatsystemTimeAdd) return obj;
                    if (ins.cfg._eatTime.ContainsKey(obj.info.shortname))
                        return obj; 
                }
 
                return (Item) null;
            }

            private void AddEgg()
            {
                if (!ins._itemList.TryGetValue(entity.net.ID.Value, out f)) return;
                if (ent == null)
                {
                    var iContainer = new ItemContainer();
                    iContainer.ServerInitialize(null, 42);
                    iContainer.GiveUID();
                    iContainer.entityOwner = entity;
                    iContainer.SetFlag(ItemContainer.Flag.NoItemInput, true);
                    if (f.EggList.Count > 0)
                    {
                        foreach (var storageChicken in f.EggList)
                        {
                            if(storageChicken.amount < 1) continue;
                            var item = ItemManager.CreateByName(storageChicken.shortname, storageChicken.amount,
                                storageChicken.skin);
                            item.MoveToContainer(iContainer); 
                        } 
 
                        var t = iContainer.Drop("assets/prefabs/misc/item drop/item_drop.prefab", entity.transform.position + Vector3.up, Quaternion.identity, 1f);
                        t.playerName = ins.lang.GetMessage("CHICKEN_BACKPACK", ins);
                        t.CancelInvoke(t.RemoveMe);
                        t.ResetRemovalTime(ins.cfg.TimerRemove);
                    }

                    ins._itemList.Remove(entity.net.ID.Value);
                    entity.Kill();
                    Destroy(this);
                    return;
                }
             
                
                Teg();
                TryAddEgg();
                if (ins.cfg.eatsystem) 
                {
                    var items = GetFoodItem();
                    if (items != null)
                        if (IsNextEat <= 0) TryEat(items);

                    if (ins.cfg.eatsystemlifetime > 0 && f.IsLive <= 0)
                    {
                       
                        entity.Kill();
                        Destroy(this);
                    }
                    return;
                }

                if (ins.cfg.lifetime > 0 && f.IsLive <= 0)
                {
                    entity.Kill();
                    Destroy(this);
                }

                if (ins.cfg.lifetime == -1 && f.EggComplete >= ins.cfg.eggmax)
                { 
                    entity.Kill();
                    Destroy(this);
                } 
            }
 
            public void OnDestroy()
            {
                Destroy(this);
                if (!entity.IsDestroyed) entity.Kill();
            }

            public void CheckTrigger()
            {
                var entities = Physics.OverlapSphereNonAlloc(pos, 3, colBuffer, playerLayer,
                    QueryTriggerInteraction.Collide);
                if (entities != 0)
                    for (var i = 0; i < entities; i++)
                    {
                        var player = colBuffer[i].GetComponentInParent<BasePlayer>();

                        if (player != null && !player.IsSleeping() && !player.IsDead() && !player.IsNpc &&
                            player.userID.IsSteamId() && !PlayersList.ContainsKey(player))
                            PlayersList.Add(player, player.IsAdmin);
                    }
            }

            private void Teg()
            {
                CheckTrigger();
                foreach (var player in PlayersList)
                    if (player.Key != null)
                    {
                        if (Vector3.Distance(player.Key.transform.position, pos) <= 3)
                        {
                            Vector3 vectors;
                            vectors = pos + new Vector3(0, 0.5f, 0);

                            var text = "";
                            if (!ins.cfg.eatsystem)
                            {
                                text =
                                    $"{ins.lang.GetMessage("CHICKEN_NAME", ins, player.Key.UserIDString)}\n{string.Format(ins.lang.GetMessage("CHICKEN_TIMETOEGG", ins, player.Key.UserIDString), FormatTime(TimeSpan.FromSeconds(f.IsCoolDown), "ru"))}";
                                if (ins.cfg.lifetime > 0 && ins.cfg.lifetime != -1)
                                {
                                    text +=
                                        $"\n{string.Format(ins.lang.GetMessage("CHICKEN_TIMETODEATH", ins, player.Key.UserIDString), FormatTime(TimeSpan.FromSeconds(f.IsLive), "ru"))}";
                                    if (ins.cfg.eggmax != -1 && f.EggComplete < ins.cfg.eggmax)
                                        text +=
                                            $"\n{string.Format(ins.lang.GetMessage("CHICKEN_EGGCOUNT", ins, player.Key.UserIDString), ins.cfg.eggmax - f.EggComplete)}";
                                    else if (ins.cfg.eggmax != -1 && f.EggComplete >= ins.cfg.eggmax)
                                        text =
                                            $"{ins.lang.GetMessage("CHICKEN_NAME", ins, player.Key.UserIDString)}\n{string.Format(ins.lang.GetMessage("CHICKEN_TIMETODEATH", ins, player.Key.UserIDString), FormatTime(TimeSpan.FromSeconds(f.IsLive), "ru"))}\n{string.Format(ins.lang.GetMessage("CHICKEN_EGGMAX", ins, player.Key.UserIDString), FormatTime(TimeSpan.FromSeconds(f.IsLive), "ru"))}";
                                }
                                else if (ins.cfg.eggmax != -1)
                                {
                                    text +=
                                        $"\n{string.Format(ins.lang.GetMessage("CHICKEN_EGGCOUNTDEATH", ins, player.Key.UserIDString), ins.cfg.eggmax - f.EggComplete)}";
                                }
                            }
                            else
                            {
                                if (GetFoodItem() == null)
                                    text =
                                        $"{ins.lang.GetMessage("CHICKEN_NAME", ins, player.Key.UserIDString)}\n{string.Format(ins.lang.GetMessage("CHICKEN_TIMETOEGG", ins, player.Key.UserIDString), FormatTime(TimeSpan.FromSeconds(f.IsCoolDown), "ru"))}\n{string.Format(ins.lang.GetMessage("CHICKEN_TIMETODEATH", ins, player.Key.UserIDString), FormatTime(TimeSpan.FromSeconds(f.IsLive), "ru"))}\n{ins.lang.GetMessage("CHICKEN_FOODNEED", ins, player.Key.UserIDString)}";
                                else if (f.LifeTime + CurrentTime() != ins.cfg.eatsystemlifetime + CurrentTime())
                                    text =
                                        $"{ins.lang.GetMessage("CHICKEN_NAME", ins, player.Key.UserIDString)}\n{string.Format(ins.lang.GetMessage("CHICKEN_TIMETOEGG", ins, player.Key.UserIDString), FormatTime(TimeSpan.FromSeconds(f.IsCoolDown), "ru"))}\n{string.Format(ins.lang.GetMessage("CHICKEN_TIMETODEATH", ins, player.Key.UserIDString), FormatTime(TimeSpan.FromSeconds(f.IsLive), "ru"))}";
                                else
                                    text =
                                        $"{ins.lang.GetMessage("CHICKEN_NAME", ins, player.Key.UserIDString)}\n{string.Format(ins.lang.GetMessage("CHICKEN_TIMETOEGG", ins, player.Key.UserIDString), FormatTime(TimeSpan.FromSeconds(f.IsCoolDown), "ru"))}";
                            }

                            if (!player.Value) SetPlayerFlag(player.Key, BasePlayer.PlayerFlags.IsAdmin, true);
                            player.Key.SendConsoleCommand("ddraw.text", 1.01f + Time.deltaTime, Color.white, vectors,
                                $"{text}");
                            if (!player.Value) SetPlayerFlag(player.Key, BasePlayer.PlayerFlags.IsAdmin, false);
                        }
                        else
                        {
                            PlayersList.Remove(player.Key);
                            return;
                        }
                    }
                    else
                    {
                        PlayersList.Remove(player.Key);
                        return;
                    }
            }

            private void SetPlayerFlag(BasePlayer player, BasePlayer.PlayerFlags f, bool b)
            {
                if (b)
                {
                    if (player.HasPlayerFlag(f)) return;
                    player.playerFlags |= f;
                }
                else
                {
                    if (!player.HasPlayerFlag(f)) return;
                    player.playerFlags &= ~f;
                }

                player.SendNetworkUpdateImmediate(false);
            }
        }

        private static readonly int dLayer = LayerMask.GetMask("Deployed");
        private static readonly int  cLayer = LayerMask.GetMask("Construction");

        private class ItemPlacement : MonoBehaviour
        {
            private BasePlayer player;
            private BaseEntity hitEntity = null;
            private DroppedItem goldEgg;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                enabled = false;
                SpawnDroppedItem(player.GetActiveItem()?.name);
            }


            private void Update()
            {
                var activeItem = player.GetActiveItem();
                if (activeItem == null || activeItem.skin != ins.cfg.skinIdRost)
                    CancelPlacement();

                var input = player.serverInput;
                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, 3f, cLayer) && hit.GetEntity())
                {
                    hitEntity = hit.GetEntity();
                    goldEgg.transform.position = player.eyes.HeadRay().GetPoint(1f);
                    goldEgg.transform.position = new Vector3(goldEgg.transform.position.x,
                        hit.transform.position.y + 0.25f, goldEgg.transform.position.z);
                }
                else
                {
                    goldEgg.transform.position = player.eyes.HeadRay().GetPoint(1f);
                    hitEntity = null;
                    if (goldEgg.transform.position.y <
                        TerrainMeta.HeightMap.GetHeight(goldEgg.transform.position) + 0.25f)
                        goldEgg.transform.position = new Vector3(goldEgg.transform.position.x,
                            TerrainMeta.HeightMap.GetHeight(goldEgg.transform.position) + 0.5f,
                            goldEgg.transform.position.z);
                    else
                        goldEgg.transform.position = new Vector3(goldEgg.transform.position.x,
                            TerrainMeta.HeightMap.GetHeight(goldEgg.transform.position) + 0.5f,
                            goldEgg.transform.position.z);
                }

                if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    
                    var entities = Physics.OverlapSphereNonAlloc(goldEgg.transform.position, 1.5f, colBuffer, dLayer,
                        QueryTriggerInteraction.Collide);
                    if (entities != 0)
                        for (var i = 0; i < entities; i++)
                        { 
                            var entity = colBuffer[i].GetComponentInParent<BaseEntity>();
                            if (entity.ShortPrefabName == "hitchtrough.deployed")
                            {
                                if (activeItem != null) PlaceEgg(activeItem);
                                return;
                            }
                        }
                }
            }

            private void SpawnDroppedItem(string name)
            {
                goldEgg = ins.CreateEggItem(player, name, player.transform.position, false);
                goldEgg.Unstick();
                goldEgg.item.GetWorldEntity().gameObject.SetLayerRecursive(28);
                enabled = true;
            }

            public void CancelPlacement(bool notify = true)
            {
                enabled = false;
                goldEgg.DestroyItem();
                goldEgg.Kill();
                Destroy(this);
            }

            private void PlaceEgg(Item activeItem)
            {
                activeItem.MarkDirty();
                if (activeItem.amount <= 1) activeItem.RemoveFromContainer();
                else activeItem.amount -= 1;
                goldEgg.name = "Яичко";
                goldEgg.DestroyItem();
                goldEgg.Kill();
                Vector3 pos;
                if (hitEntity != null)
                    pos = new Vector3(goldEgg.transform.position.x, hitEntity.transform.position.y + 0.25f,
                        goldEgg.transform.position.z);
                else
                    pos = new Vector3(goldEgg.transform.position.x,
                        TerrainMeta.HeightMap.GetHeight(goldEgg.transform.position) + 0.25f,
                        goldEgg.transform.position.z);
                var item = ins.CreateEggItem(player, name, pos, false, "egg");
                item.StickIn();
                Destroy(this);
            }

            public void OnPlayerDeath()
            {
                CancelPlacement();
                Destroy(this);
            }
        }

        #endregion

        #region Hooks
        [PluginReference] private Plugin TruePVE;
       private object CanCombineDroppedItem(WorldItem first, WorldItem second)
        {
            return CanStackItem(first.item, second.item);
        }
        private object CanEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (!cfg.damageTrue) return null;
            if (entity == null || info == null || !(entity is Chicken)) return null;
            if (entity.GetComponent<ChickenStartEgg>() == null || info.InitiatorPlayer == null) return null;
            if (info.InitiatorPlayer.IsBuildingBlocked()) return false; 
            return null;
        } 

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (TruePVE) return null;
            return CanEntityTakeDamage(entity, info);
        }

        private object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return null;
            if (dispenser.gameObject.GetComponent<BaseEntity>()?.ShortPrefabName != "chicken.corpse") return null;
            if (!(Core.Random.Range(0f, 100f) <= cfg.shans)) return null;
            var giveItem = ItemManager.CreateByName("sticks", 1, cfg.skinIdRost);
            giveItem.name = En ? "Chicken Egg" : "Яичко";
            player.SendConsoleCommand($"note.inv {ItemManager.FindItemDefinition("sticks").itemid} 1 \"{giveItem.name}\"");
            if(cfg.annonce) SendReply(player, lang.GetMessage("ANNONCE", this,player.UserIDString ));
            if (!player.inventory.GiveItem(giveItem)) giveItem.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
            return null;
        }

        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item == null || action == null || player == null) return null;
            if (action != "unwrap") return null;
            if (item.skin != cfg.skinIdEgg) return null; 
            if (!GiveNagrada(player))
            {   
                SendReply(player, En ? "[ChickenEgg] Notify an administrator that an error has occurred." : "[ChickenEgg] Сообщите администратору, что произошла ошибка.");
                return false;
            }
            item.UseItem(1);
            player.SendNetworkUpdate();
            var x = new Effect("assets/prefabs/misc/easter/painted eggs/effects/gold_open.prefab", player, 0,
                new Vector3(), new Vector3());
            EffectNetwork.Send(x, player.Connection);
            return false;
        }

        
        private object CanStackItem(Item item, Item triger)
        {
            if (item.skin == cfg.skinIdEgg && triger.skin == cfg.skinIdEgg) return true;
            if (item.skin == cfg.skinIdRost && triger.skin == cfg.skinIdRost) return true;
            return null;
        }

        [PluginReference] private Plugin CustomSkinsStacksFix, Loottable, StackModifier, Stacks;
        private Item OnItemSplit(Item item, int amount)
        {
            if (Loottable != null) return null; 
            if (CustomSkinsStacksFix != null) return null;
            if (StackModifier != null) return null;
            if (Stacks != null) return null;
            if (item.skin != cfg.skinIdRost && item.skin != cfg.skinIdEgg) return null;
            item.amount -= amount;
            var newItem = ItemManager.Create(item.info, amount, item.skin);
            newItem.name = item.name;
            newItem.skin = item.skin;
            newItem.amount = amount;
            return newItem;
        }

        private void OnActiveItemChanged(BasePlayer player, Item olditem, Item item)
        {
            if (player == null || item == null)
                return;
            if (item.skin != cfg.skinIdRost || player.GetComponent<ItemPlacement>()) return;
            SendReply(player, lang.GetMessage("CHICKEN_PLACE", this, player.UserIDString));
            player.gameObject.AddComponent<ItemPlacement>();
        }

        [PluginReference] private Plugin Vanish;

        private object OnNpcTarget(BaseNpc entity, BaseEntity target)
        {
            if (Vanish && target is BasePlayer) return null;
            if (entity != null && entity is Chicken && entity.skinID == 234)
                return true;
            if (target == null)
                return null;
            if (target.ShortPrefabName == "chicken" && target.skinID == 234)
                return true;
            return null;
        }

        private DroppedItem CreateEggItem(BasePlayer player, string name, Vector3 position, bool canPickup = true,
            string type = "", uint index = 0)
        {
            var createEgg = CreateWorldObject(player, -1002156085, name, position, canPickup, type);
            UnityEngine.Object.Destroy(createEgg.GetComponent<EntityCollisionMessage>());
            UnityEngine.Object.Destroy(createEgg.GetComponent<PhysicsEffects>());
            
            return createEgg as DroppedItem;
        }

        private void CreateEggStart(string name, Vector3 position, bool canPickup = true, string type = "",
            ulong index = 0)
        {
            var item = ItemManager.CreateByItemID(-1002156085);
            if (!string.IsNullOrEmpty(name))
                item.name = name;
            var worldEntity =
                GameManager.server.CreateEntity("assets/prefabs/misc/burlap sack/generic_world.prefab", position);
            var worldItem = worldEntity as WorldItem;
            if (worldItem != null)
                worldItem.InitializeItem(item);
            worldEntity.Invoke(
                () => { (worldEntity as DroppedItem)?.CancelInvoke(((DroppedItem) worldEntity).IdleDestroy); }, 1f);

            if (!(worldItem == null))
            {
                worldItem.enableSaving = false;
                worldItem.allowPickup = canPickup;
            }

            worldEntity.Spawn();
            item.SetWorldEntity(worldEntity);
            if (type != "restart") return;
            var time = startEggList[index].TimeSpawnChicken;
            var owner = startEggList[index].ownerId;
            _eggList.Add(worldEntity.net.ID.Value, new Egg()
            {
                ownerId = owner,
                TimeSpawnChicken = time,
                position = worldEntity.transform.position
            });
            worldEntity.gameObject.AddComponent<StartEgg>();
            (worldEntity as DroppedItem)?.StickIn();
            UnityEngine.Object.Destroy(worldEntity.GetComponent<EntityCollisionMessage>());
            UnityEngine.Object.Destroy(worldEntity.GetComponent<PhysicsEffects>());
        }

        private BaseEntity CreateWorldObject(BasePlayer player, int itemId, string name, Vector3 pos, bool canPickup, string type)
        {
            var item = ItemManager.CreateByItemID(itemId);

            if (!string.IsNullOrEmpty(name))
                item.name = name;

            var worldEntity =
                GameManager.server.CreateEntity("assets/prefabs/misc/burlap sack/generic_world.prefab", pos);

            var worldItem = worldEntity as WorldItem;
            if (worldItem != null)
                worldItem.InitializeItem(item);

            worldEntity.Invoke(
                () => { (worldEntity as DroppedItem)?.CancelInvoke(((DroppedItem) worldEntity).IdleDestroy); }, 1f);

            if (!(worldItem == null))
            {
                worldItem.enableSaving = false;
                worldItem.allowPickup = canPickup;
            }

            worldEntity.Spawn();
            item.SetWorldEntity(worldEntity);
            if (type != "egg") return worldEntity;
            _eggList.Add(worldEntity.net.ID.Value, new Egg()
            {
                ownerId = player.userID,
                TimeSpawnChicken = CurrentTime() + cfg.timerRost,
                position = worldEntity.transform.position
            });
            worldEntity.gameObject.AddComponent<StartEgg>();

            return worldEntity;
        }

        private object CanNpcEat(BaseNpc entity, BaseEntity target)
        {
            var player = target?.ToPlayer();
            if (entity == null || player == null)
                return null;
            if (entity.ShortPrefabName == "chicken" && entity.skinID == 234)
                return false;
            return null;
        }

        private void OnEntityKill(Chicken entity)
        {
            if (entity == null) return;
            if (_itemList.ContainsKey(entity.net.ID.Value))
            {
                var iContainer = new ItemContainer();
                iContainer.ServerInitialize(null, 6);
                iContainer.GiveUID();
                iContainer.entityOwner = entity;
                iContainer.SetFlag(ItemContainer.Flag.NoItemInput, true);
                if (_itemList[entity.net.ID.Value].EggList.Count > 0)
                {
                    foreach (var storageChicken in _itemList[entity.net.ID.Value].EggList)
                    { 
                        if(storageChicken.amount < 1) continue;
                        var item = ItemManager.CreateByName(storageChicken.shortname, storageChicken.amount,
                            storageChicken.skin);
                        item.MoveToContainer(iContainer);
                    }
                    if(iContainer.itemList.Count >= 1)
                    {
                        var t = iContainer.Drop("assets/prefabs/misc/item drop/item_drop.prefab",
                            entity.transform.position + Vector3.up, Quaternion.identity, 1f);
                        t.playerName = ins.lang.GetMessage("CHICKEN_BACKPACK", ins);
                        t.CancelInvoke(t.RemoveMe);
                        t.ResetRemovalTime(cfg.TimerRemove);
                    }
                } 

                _itemList.Remove(entity.net.ID.Value);
                entity.GetComponent<ChickenStartEgg>()?.OnDestroy();
            }
        }

        private void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Chicken/Chickens", _itemList);
            Interface.Oxide.DataFileSystem.WriteObject("Chicken/Eggs", _eggList);
            if (Start != null) Rust.Global.Runner.StopCoroutine(Start);
            var ent2 = UnityEngine.Object.FindObjectsOfType<ChickenStartEgg>();
            foreach (var t in ent2) t?.OnDestroy();

            var ent1 = UnityEngine.Object.FindObjectsOfType<StartEgg>();
            foreach (var t in ent1) t?.OnDestroy();

            var ent3 = UnityEngine.Object.FindObjectsOfType<ItemPlacement>();
            foreach (var t in ent3) t?.CancelPlacement();
        }

        private IEnumerator StartServer()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("Chicken/Chickens"))
            {
                _itemList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Chickens>>("Chicken/Chickens");
                foreach (var keyAndValue in _itemList)
                {
                    startChickenList.Add(keyAndValue.Key, keyAndValue.Value);
                }
                _itemList.Clear();
                foreach (var list in startChickenList)
                { 
                    SpawnChicken(list.Value.position, list.Value.OwnerId, list.Value.EggList, list.Value.TimeNextEgg,
                        list.Value.LifeTime, list.Value.EggComplete);
                    yield return new WaitForSeconds(0.5f);
                }

                Puts(En ? "Load chicken: " + startChickenList.Count.ToString() :"Загружено куриц: " + startChickenList.Count.ToString());
            }

            if (Interface.Oxide.DataFileSystem.ExistsDatafile("Chicken/Eggs"))
            {
                _eggList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Egg>>("Chicken/Eggs");
                foreach (var keyAndValue in _eggList)
                {
                    startEggList.Add(keyAndValue.Key, keyAndValue.Value);
                }

                _eggList.Clear();  
                foreach (var list in startEggList)
                {
                    CreateEggStart(En ? "Chicken Egg" : "Яичко", list.Value.position, false, "restart", list.Key);
                    yield return new WaitForSeconds(0.5f);
                }

                Puts(En ? "Load eggs: " + startChickenList.Count.ToString() :"Загружено куриц: " + _eggList.Count.ToString());
            }
 
            yield return 0;
        }

        private Coroutine Start;

        private void Init()
        {
            ins = this;
        }
        private void OnServerInitialized()
        {
            Start = Rust.Global.Runner.StartCoroutine(StartServer());
  
        }

        private Dictionary<ulong, Egg> startEggList = new Dictionary<ulong, Egg>();
        private Dictionary<ulong, Chickens> startChickenList = new Dictionary<ulong, Chickens>();

        private object CanLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity.ShortPrefabName == "waterstorage" && entity.skinID == 78329804321)
            {
                if (cfg.lootonlyauth && player.IsBuildingBlocked()) return false;
                player.EndLooting();
                NextTick(() => StartLoot(player, entity));
                return false;
            }

            return null;
        }

        private void OnNewSave()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("Chicken/Chickens"))
            {
                _itemList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Chickens>>("Chicken/Chickens");
                _itemList.Clear();
            }

            if (Interface.Oxide.DataFileSystem.ExistsDatafile("Chicken/Eggs"))
            {
                _eggList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Egg>>("Chicken/Eggs");
                _eggList.Clear();
            }

            Interface.Oxide.DataFileSystem.WriteObject("Chicken/Chickens", _itemList);
            Interface.Oxide.DataFileSystem.WriteObject("Chicken/Eggs", _eggList);
            Interface.Oxide.ReloadPlugin("ChickenEgg");
        }

        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            if (inventory == null) return;
            var player = inventory.GetComponent<BasePlayer>();
            if (player == null) return;
            if (!_activeOpenBears.ContainsKey(player)) return;
            var cont = _activeOpenBears[player];
            if (cont == null) return;
            _activeOpenBears.Remove(player);
            Chickens f;
            if (!_itemList.TryGetValue((uint) cont.entityOwner.OwnerID, out f)) return;
            var _dropList = new List<Item>();
            foreach (var item in cont.itemList)
            {
                if (item.skin != cfg.skinIdRost && item.skin != cfg.skinIdEgg)
                {
                    _dropList.Add(item);
                    continue;
                }

                var find = f.EggList.Find(p => p.skin == item.skin);
                if (find != null)
                    find.amount += item.amount;
                else
                    f.EggList.Add(new StorageChicken()
                    {
                        shortname = item.info.shortname,
                        amount = item.amount,
                        skin = item.skin
                    });
            }
            if(_dropList.Count >= 1)
                for (var i = 1; i <= _dropList.Count; i++)
                    _dropList[i - 1].Drop(player.inventory.containerMain.dropPosition,
                        player.inventory.containerMain.dropVelocity);
            cont.Clear();
            _activeOpenBears.Remove(player);
            cont.Kill();
        }

        #endregion

        #region Met

        [ConsoleCommand("giveegg")]
        private void GiveEgg(ConsoleSystem.Arg a)
        {
            var player = a.Player();
            if (player != null && !player.IsAdmin) return;
            if (a.Args[1].ToInt() < 1) return;
            var items = ItemManager.CreateByName("sticks", a.Args[1].ToInt(),cfg.skinIdRost);
            items.name = En ? "Chicken egg" : "Яичко";
            var findPlayer = BasePlayer.Find(a.Args[0]);
            if (findPlayer == null)
            {
                Puts(En ? "Player not found" : "Игрок не найден");
                if (player != null)  
                    PrintToConsole(player, En ? "Player not found" : "Игрок не найден");
                return;
            }
 
            findPlayer.SendConsoleCommand(
                $"note.inv {items.info.itemid} {items.amount} \"{items.name}\"");
            if (!findPlayer.inventory.GiveItem(items))
                items.Drop(findPlayer.inventory.containerMain.dropPosition,
                    findPlayer.inventory.containerMain.dropVelocity);
            
            
        }

        private void SpawnChicken(Vector3 pos, ulong id, List<StorageChicken> itemses, double TimeNextEgg, double LifeTime, int EggComplete)
        {
            var chicken =GameManager.server.CreateEntity("assets/rust.ai/agents/chicken/chicken.prefab", pos) as Chicken;
            if (chicken == null) return;
            chicken.skinID = 234;
            UnityEngine.Object.Destroy(chicken.GetComponent<AnimalBrain>());
            chicken.Spawn();
            var chickenstorage = GameManager.server.CreateEntity("assets/prefabs/deployable/waterpurifier/waterstorage.prefab", pos);
            chickenstorage.skinID = 78329804321;
            chickenstorage.OwnerID = chicken.net.ID.Value;
            chickenstorage.Spawn();
            chickenstorage.SetParent(chicken, "spine1", true, true);
            if (chicken == null) return;
            _itemList.Add(chicken.net.ID.Value, new Chickens()
            {
                LifeTime = LifeTime,
                TimeNextEgg = TimeNextEgg,
                OwnerId = id,
                EggComplete = EggComplete,
                position = chicken.transform.position,
                EggList = itemses
            });
            LogToFile("SpawnChicken", $"ID: {chicken.net.ID} chicken spawning complete, ownerid {id}!", this);
            chicken.gameObject.AddComponent<ChickenStartEgg>();
            chicken.CancelInvoke(chicken.TickAi);
        }

        private bool GiveNagrada(BasePlayer player)
        {
            var itemGet = cfg._itemList.ToList().FindAll(p => p.Enabled).GetRandom();
            if(itemGet == null)
            {
                PrintError("Item not found");
                LogToFile("err", "Nagrada error 1",this );
                return false;
            }
            var item = ItemManager.CreateByName(itemGet.shortname, Core.Random.Range(itemGet.Min, itemGet.Max), itemGet.SkinId);
            if(item == null)
            {
                PrintError($"The item has the wrong shortname: {itemGet.shortname}");
                LogToFile("err", $"The item has the wrong shortname: {itemGet.shortname}",this );
                return false;
            }
            if (!string.IsNullOrEmpty(itemGet.DisplayName))
                item.name = itemGet.DisplayName;
            if (!player.inventory.GiveItem(item))
                item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
            return true;
        }

        private static void PlayerLootContainer(BasePlayer player, ItemContainer container, StorageContainer storage)
        {
            player.inventory.loot.Clear();
            player.inventory.loot.PositionChecks = false;
            player.inventory.loot.entitySource = container.entityOwner ?? player;
            player.inventory.loot.itemSource = null;
            player.inventory.loot.MarkDirty();
            player.inventory.loot.AddContainer(container);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", storage.panelName);
        }

        private Dictionary<BasePlayer, ItemContainer> _activeOpenBears = new Dictionary<BasePlayer, ItemContainer>();

        private object CanLootPlayer(BasePlayer looted, BasePlayer looter)
        {
            if (looter == null) return null;
            if (_activeOpenBears.ContainsKey(looter)) return true;

            return null;
        }

        private void StartLoot(BasePlayer player, BaseEntity entity)
        { 
            if(_activeOpenBears.Any(p => p.Value.entityOwner.net.ID == entity.net.ID)) return; 
            ItemContainer cont;
            Chickens f;
            if (!_itemList.TryGetValue((uint) entity.OwnerID, out f)) return;
            if (_activeOpenBears.TryGetValue(player, out cont))
            {
                if(cont != null)
                {
                    var dropList = new List<Item>();
                    foreach (var item in cont.itemList)
                    {
                        if (item.skin != cfg.skinIdRost && item.skin != cfg.skinIdEgg)
                        {
                            dropList.Add(item);
                            continue;
                        }

                        var find = f.EggList.Find(p => p.skin == item.skin);
                        if (find != null)
                            find.amount += item.amount;
                        else
                            f.EggList.Add(new StorageChicken()
                            {
                                shortname = item.info.shortname,
                                amount = item.amount,
                                skin = item.skin
                            });
                    }

                    for (var i = 1; i <= dropList.Count; i++)
                        dropList[i - 1].Drop(player.inventory.containerMain.dropPosition,
                            player.inventory.containerMain.dropVelocity);
                }
                _activeOpenBears.Remove(player);
            }
            var container = new ItemContainer();
            var storage =
                GameManager.server.CreateEntity("assets/prefabs/deployable/small stash/small_stash_deployed.prefab") as
                    StorageContainer;
            if (storage == null) return;
            container.entityOwner = entity;
            container.isServer = true;
            container.allowedContents = ItemContainer.ContentsType.Generic;
            container.GiveUID();
            _activeOpenBears.Add(player, container);
            container.capacity = 6;
            if(f.EggList.Count >= 1)
                foreach (var item in f.EggList)
                {
                    if (item.amount < 1) continue;
                    var items = ItemManager.CreateByName(item.shortname, item.amount, item.skin);
                    items.name = En ? "Chicken egg": "Яичко";
                    var findItem = container.itemList.Find(p => p.skin == item.skin);
                    if (findItem != null)
                    {
                        findItem.amount += 1;
                    }
                    else
                    {
                        container.itemList.Add(items);
                        items.parent = container;
                        items.MarkDirty();
                    } 
                }

            f.EggList.Clear();
            container.playerOwner = player;
            PlayerLootContainer(player, container, storage);
        }

        #endregion

        #region Help

        private static string Format(int units, string form1, string form2, string form3)
        {
            var tmp = units % 10;
            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9) return $"{units} {form1}";
            if (tmp >= 2 && tmp <= 4) return $"{units} {form2}";
            return $"{units} {form3}";
        }

        public static string FormatTime(TimeSpan time, string language, int maxSubstr = 5)
        {
            var result = string.Empty;
            switch (language)
            {
                
                default:
                    var i = 0;
                    if (time.Days != 0 && i < maxSubstr)
                    {
                        if (!string.IsNullOrEmpty(result)) result += " ";
                        result += $"{Format(time.Days, En ? "DAY": "ДНЕЙ", En ? "DAY":"ДНЯ", En ? "DAY":"ДЕНЬ")}";
                        i++;
                    }

                    if (time.Hours != 0 && i < maxSubstr)
                    {
                        if (!string.IsNullOrEmpty(result)) result += " ";
                        result += $"{Format(time.Hours, En ? "HOUR": "ЧАСОВ", En ? "HOUR":"ЧАСА", En ? "HOUR":"ЧАС")}";
                        i++;
                    }

                    if (time.Minutes != 0 && i < maxSubstr)
                    {
                        if (!string.IsNullOrEmpty(result)) result += " ";
                        result += $"{Format(time.Minutes, En ? "MINUTE": "МИНУТ", En ? "MINUTE":"МИНУТЫ", En ? "MINUTE":"МИНУТА")}";
                        i++;
                    }

                    if (time.Seconds != 0 && i < maxSubstr)
                    {
                        if (!string.IsNullOrEmpty(result)) result += " ";
                        result += $"{Format(time.Seconds, En ? "SEC": "СЕК", En ? "SEC":"СЕК", En ? "SEC":"СЕК")}";
                        i++;
                    }

                    break;
            }

            return result;
        }

        //33971100
        private static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        private static double CurrentTime()
        {
            return DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        }

        #endregion
    }
}
