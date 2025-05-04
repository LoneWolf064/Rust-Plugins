using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Back Pump Jack", "Arainrr/Lorenzo", "1.4.28")]
    [Description("Allows players to use survey charges to create an oil crater")]
    public class BackPumpJack : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Friends, Clans;

        private static BackPumpJack _instance;

        private const string PREFAB_CRATER_OIL = "assets/prefabs/tools/surveycharge/survey_crater_oil.prefab";

        private readonly HashSet<ulong> _checkedCraters = new HashSet<ulong>();
        private readonly List<QuarryData> _activeCraters = new List<QuarryData>();
        private readonly List<MiningQuarry> _miningQuarries = new List<MiningQuarry>();
        private readonly Dictionary<ulong, PermissionSettings> _activeSurveyCharges = new Dictionary<ulong, PermissionSettings>();
        private readonly object _true = true;
        private readonly object _false = false;

        private int QuarryDefaultDieselStack = 0;
        private int QuarryDefaultFuelCapacity = 6;
        private int QuarryDefaultHopperSlots = 18;

        private int PumpDefaultDieselStack = 0;
        private int PumpDefaultFuelCapacity = 6;
        private int PumpDefaultHopperSlots = 18;

        private float workPerFuelRestore = -1;
        private float workToAddRestore = -1;
        private float processRateRestore = -1;

        public Timer refilltimer = null;
        private readonly float PlayerRadius = 12f;
        private readonly float MatchRadius = 0.5f;

        struct QuarryRates
        {
            public float processRate;
            public float workToAdd;
            public float workPerFuel;
        }

        QuarryRates miningQuarryBackup;
        QuarryRates pumpjackbackup;

        #endregion Fields

        #region Oxide Hooks

        private void Init()
        {
            MiningQuarry staticQuarry;
            MiningQuarry miningQuarry;
            MiningQuarry staticpumpjack;
            MiningQuarry pumpjack;

            StorageContainer fuelstorage;
            StorageContainer hopper;

            LoadData();
            foreach (var permissionSetting in _configData.Permissions)
            {
                if (!permission.PermissionExists(permissionSetting.Permission, this))
                {
                    permission.RegisterPermission(permissionSetting.Permission, this);
                }
            }

            staticQuarry = GameManager.server.FindPrefab("assets/bundled/prefabs/static/miningquarry_static.prefab")?.GetComponent<MiningQuarry>();
            miningQuarry = GameManager.server.FindPrefab("assets/prefabs/deployable/quarry/mining_quarry.prefab")?.GetComponent<MiningQuarry>();
            staticpumpjack = GameManager.server.FindPrefab("assets/bundled/prefabs/static/pumpjack-static.prefab")?.GetComponent<MiningQuarry>();
            pumpjack = GameManager.server.FindPrefab("assets/prefabs/deployable/oil jack/mining.pumpjack.prefab")?.GetComponent<MiningQuarry>();

            if (_configData.ApplyPatchForMiningRates)
            {
                miningQuarryBackup.processRate = miningQuarry.processRate;
                miningQuarryBackup.workToAdd = miningQuarry.workToAdd;
                miningQuarryBackup.workPerFuel = miningQuarry.workPerFuel;

                miningQuarry.processRate = staticQuarry.processRate;
                miningQuarry.workToAdd = staticQuarry.workToAdd;
                miningQuarry.workPerFuel = staticQuarry.workPerFuel;

                pumpjackbackup.processRate = pumpjack.processRate;
                pumpjackbackup.workToAdd = pumpjack.workToAdd;
                pumpjackbackup.workPerFuel = pumpjack.workPerFuel;

                pumpjack.processRate = staticpumpjack.processRate;
                pumpjack.workToAdd = staticpumpjack.workToAdd;
                pumpjack.workPerFuel = staticpumpjack.workPerFuel;
            }

            if (staticQuarry != null)
            {
                fuelstorage = staticQuarry?.fuelStoragePrefab.prefabToSpawn.GetEntity() as StorageContainer;
                QuarryDefaultDieselStack = fuelstorage?.maxStackSize ?? 0;
                QuarryDefaultFuelCapacity = fuelstorage?.inventorySlots ?? 6;

                hopper = staticQuarry.hopperPrefab.prefabToSpawn.GetEntity() as StorageContainer;
                QuarryDefaultHopperSlots = hopper?.inventorySlots ?? 18;
            }

            if (staticpumpjack != null)
            {
                fuelstorage = staticpumpjack.fuelStoragePrefab.prefabToSpawn.GetEntity() as StorageContainer;
                PumpDefaultDieselStack = fuelstorage?.maxStackSize ?? 0;
                PumpDefaultFuelCapacity = fuelstorage?.inventorySlots ?? 6;

                hopper = staticpumpjack.hopperPrefab.prefabToSpawn.GetEntity() as StorageContainer;
                PumpDefaultHopperSlots = hopper?.inventorySlots ?? 18;
            }


            Unsubscribe(nameof(CanBuild));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityKill));
        }

        private void Unload()
        {
            foreach (var serverEntity in BaseNetworkable.serverEntities)
            {
                if (serverEntity is MiningQuarry miningQuarry)
                {
                    if (_configData.ApplyPatchtoUserQuarry && miningQuarry.ShortPrefabName == "mining_quarry" && workToAddRestore != -1)
                    {
                        miningQuarry.workPerFuel = workPerFuelRestore;
                        miningQuarry.workToAdd = workToAddRestore;
                        miningQuarry.processRate = processRateRestore;
                    }

                    if (miningQuarry.ShortPrefabName == "mining_quarry")
                    {
                        // restore default value
                        try
                        {
                            var fuelstorage = miningQuarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>();
                            if (_configData.DieselFuelMaxStackSize >= 0) fuelstorage.inventory.maxStackSize = QuarryDefaultDieselStack;
                            if (_configData.FuelSlots > 0) fuelstorage.inventory.capacity = QuarryDefaultFuelCapacity;
                        }
                        catch (Exception ex)
                        {
                            PrintError(ex.Message);
                            PrintError($"Quarry at location {miningQuarry.transform.position} is missing fuel storage container.");
                            miningQuarry.fuelStoragePrefab?.DoSpawn(miningQuarry);
                        }

                        // restore default value
                        try {
                            var hopper = miningQuarry.hopperPrefab.instance.GetComponent<StorageContainer>();
                            if (_configData.HopperSlots > 0) hopper.inventory.capacity = QuarryDefaultHopperSlots;
                        }
                        catch (Exception ex)
                        {
                            PrintError(ex.Message);
                            PrintError($"Quarry at location {miningQuarry.transform.position} is missing hopper storage container.");
                            miningQuarry.hopperPrefab?.DoSpawn(miningQuarry);
                        }
                    }

                    if (miningQuarry.ShortPrefabName == "mining.pumpjack")
                    {
                        try {
                            // restore default value
                            var fuelstorage = miningQuarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>();
                            if (_configData.DieselFuelMaxStackSize >= 0) fuelstorage.inventory.maxStackSize = PumpDefaultDieselStack;
                            if (_configData.FuelSlots > 0) fuelstorage.inventory.capacity = PumpDefaultFuelCapacity;
                        }
                        catch (Exception ex)
                        {
                            PrintError(ex.Message);
                            PrintError($"Quarry at location {miningQuarry.transform.position} is missing fuel storage container.");
                            miningQuarry.fuelStoragePrefab?.DoSpawn(miningQuarry);
                        }

                        // restore default value
                        try {
                            var hopper = miningQuarry.hopperPrefab.instance.GetComponent<StorageContainer>();
                            if (_configData.HopperSlots > 0) hopper.inventory.capacity = PumpDefaultHopperSlots;
                        }
                        catch (Exception ex)
                        {
                            PrintError(ex.Message);
                            PrintError($"Quarry at location {miningQuarry.transform.position} is missing hopper storage container.");
                            miningQuarry.hopperPrefab?.DoSpawn(miningQuarry);
                        }
                    }

                    if (miningQuarry.ShortPrefabName == "mining_quarry" || miningQuarry.ShortPrefabName == "mining.pumpjack")
                    {
                        if (_configData.TimePerBarrel > 0) miningQuarry.workPerFuel = 1000f;
                        if (miningQuarry.pendingWork > miningQuarry.workPerFuel) miningQuarry.pendingWork = miningQuarry.workPerFuel;
                    }

                    if (miningQuarry.isStatic)
                    {
                        miningQuarry.UpdateStaticDeposit();
                    }

                    QuarryStateDetector quarrydetector = miningQuarry.gameObject.GetComponent<QuarryStateDetector>();
                    // When server shutdown, onDestroy member never execute with Destroy(obj) 
                    // use DestroyImmediate instead to avoid execution delay
                    if (quarrydetector != null) UnityEngine.Object.DestroyImmediate(quarrydetector);

                }
            }

            if (_configData.ApplyPatchForMiningRates)
            {
                var miningQuarry = GameManager.server.FindPrefab("assets/prefabs/deployable/quarry/mining_quarry.prefab")?.GetComponent<MiningQuarry>();
                if (miningQuarry != null)
                {
                    miningQuarry.processRate = miningQuarryBackup.processRate;
                    miningQuarry.workToAdd = miningQuarryBackup.workToAdd;
                    miningQuarry.workPerFuel = miningQuarryBackup.workPerFuel;
                }
                var pumpjack = GameManager.server.FindPrefab("assets/prefabs/deployable/oil jack/mining.pumpjack.prefab")?.GetComponent<MiningQuarry>();
                if (pumpjack != null)
                {
                    pumpjack.processRate = pumpjackbackup.processRate;
                    pumpjack.workToAdd = pumpjackbackup.workToAdd;
                    pumpjack.workPerFuel = pumpjackbackup.workPerFuel;
                }
            }

            if (refilltimer != null) refilltimer.Destroy();

            SaveData();
            _instance = null;
        }

        private void OnServerInitialized(bool initial)
        {
            _instance = this;
            if (_configData.Global.CantDeploy)
            {
                Subscribe(nameof(CanBuild));
            }
            if (_configData.Global.CantDamage)
            {
                Subscribe(nameof(OnEntityTakeDamage));
            }

            AddCovalenceCommand(_configData.CommandRefill, nameof(CCmdRefresh));
            AddCovalenceCommand(_configData.CommandInfo, nameof(CCmdInfo));
            AddCovalenceCommand(_configData.CommandResetDeposits, nameof(CCmdResetDeposit));            

            var quarry = GameManager.server.FindPrefab("assets/prefabs/deployable/quarry/mining_quarry.prefab")?.GetComponent<MiningQuarry>();
            if (quarry != null)
            {
                QuarrySettings.WorkPerMinute = 60f / quarry.processRate * quarry.workToAdd;
            }
            var pumpjack = GameManager.server.FindPrefab("assets/prefabs/deployable/oil jack/mining.pumpjack.prefab")?.GetComponent<MiningQuarry>();
            if (pumpjack != null)
            {
                PumpJackSettings.WorkPerMinute = 60f / pumpjack.processRate * pumpjack.workToAdd;
            }

            int CraterCount = 0;
            int QuaryCount = 0;
            timer.Once(2f, () =>
            {
                Subscribe(nameof(OnEntitySpawned));
                Subscribe(nameof(OnEntityKill));

                foreach (var serverEntity in BaseNetworkable.serverEntities)
                {
                    if (serverEntity is SurveyCrater surveyCrater)
                    {
                        CraterCount++;

                        var deposit = ResourceDepositManager.GetOrCreate(surveyCrater.transform.position);
                        if (deposit?._resources == null || deposit._resources.Count <= 0)
                        {
                            continue;
                        }
                        var mineralItems = deposit._resources.Select(depositEntry => new MineralItemData
                        {
                            amount = depositEntry.amount,
                            shortname = depositEntry.type.shortname,
                            workNeeded = depositEntry.workNeeded
                        }).ToList();
                        _activeCraters.Add(new QuarryData
                        {
                            position = surveyCrater.transform.position,
                            isLiquid = surveyCrater.ShortPrefabName == "survey_crater_oil",
                            mineralItems = mineralItems
                        });
                        continue;
                    }

                    if ((serverEntity is MiningQuarry miningQuarry) && !miningQuarry.IsDestroyed)
                    {

                        QuaryCount++;
                        OnEntitySpawned(miningQuarry);
                    }
                }

                CheckValidData();
            });

            timer.Once(10f, () => RefillMiningQuarries(true));
        }

        private void OnServerSave()
        {
            refilltimer = timer.Once(Random.Range(0f, 10f), () => RefillMiningQuarries());
        }

        public readonly string[] QuarryTypeLookup = { "Oil", "Stone", "Sulfur", "HQM" };

        private void OnEntitySpawned(MiningQuarry miningQuarry)
        {
            if (miningQuarry == null) return;
            
            if ((miningQuarry.ShortPrefabName == "miningquarry_static" || miningQuarry.ShortPrefabName == "pumpjack-static") && !miningQuarry.OwnerID.IsSteamId())
            {
                if (_configData.StaticQuarryModifier)
                {
                    var type = QuarryTypeLookup[(int)miningQuarry.staticType];
                    StaticQuarrySettings QuarrySetting;
                    bool isLiquid = (miningQuarry.staticType == MiningQuarry.QuarryType.None);

                    float WorkPerMinute = miningQuarry.workToAdd * (60.0f / miningQuarry.processRate);
                    if (_configData.StaticQuarryType.TryGetValue(type, out QuarrySetting))
                    {
                        miningQuarry._linkedDeposit._resources.Clear();

                        QuarrySetting.RefillResourceDeposit(miningQuarry._linkedDeposit, WorkPerMinute, isLiquid);
                    }
                    else PrintWarning($"Missing resource definition from config file ({type})");
                }
                return;
            }


            // PATCH to make quarry and pumpjack work the same as static-quarry and static-pumpjack
            if (_configData.ApplyPatchtoUserQuarry && miningQuarry.ShortPrefabName == "mining_quarry")
            {
                if (workToAddRestore == -1)
                {
                    workPerFuelRestore = miningQuarry.workPerFuel;
                    workToAddRestore = miningQuarry.workToAdd;
                    processRateRestore = miningQuarry.processRate;
                }

                // use same parameter as static quarry
                miningQuarry.workPerFuel = 1000;
                miningQuarry.workToAdd = 40;
                miningQuarry.processRate = 5;
            }

            _miningQuarries.Add(miningQuarry);
            UpdateAndRefill(miningQuarry);

            if (_configData.PatchLadder || _configData.PatchLightSignal)
            {
                QuarryStateDetector quarrydetector = miningQuarry.gameObject.GetComponent<QuarryStateDetector>();
                if (quarrydetector == null) miningQuarry.gameObject.AddComponent<QuarryStateDetector>();
            }

            try
            {
                var fuelstorage = miningQuarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>();
                if (_configData.DieselFuelMaxStackSize >= 0) fuelstorage.inventory.maxStackSize = _configData.DieselFuelMaxStackSize;
                if (_configData.FuelSlots > 0) fuelstorage.inventory.capacity = _configData.FuelSlots;
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
                PrintError($"Quarry at location {miningQuarry.transform.position} is missing fuel storage container.");
                miningQuarry.fuelStoragePrefab?.DoSpawn(miningQuarry);
            }

            try
            {
                var hopper = miningQuarry.hopperPrefab.instance.GetComponent<StorageContainer>();
                if (_configData.HopperSlots > 0) hopper.inventory.capacity = _configData.HopperSlots;
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
                PrintError($"Quarry at location {miningQuarry.transform.position} is missing hopper storage container.");
                miningQuarry.hopperPrefab?.DoSpawn(miningQuarry);
            }

            if (_configData.TimePerBarrel > 0.0f)
            {
                miningQuarry.workPerFuel = miningQuarry.workToAdd / miningQuarry.processRate * (float)_configData.TimePerBarrel;
            }
            if (miningQuarry.pendingWork > miningQuarry.workPerFuel) miningQuarry.pendingWork = miningQuarry.workPerFuel;
        }

        private bool UpdateAndRefill(MiningQuarry miningQuarry)
        {
            bool FoundEntry = false;
            // scan stored data if entry exist
            foreach (var quarryData in _storedData.quarryDataList)
            {
                if (quarryData == null)
                {
                    continue;
                }
                if (Vector3Ex.Distance2D(quarryData.position, miningQuarry.transform.position) < MatchRadius)
                {
                    FoundEntry = true;
                    CreateResourceDeposit(miningQuarry, quarryData);
                    break;
                }
            }
            if (FoundEntry == false)
            {
                // scan activecraters if entry exist
                for (var i = _activeCraters.Count - 1; i >= 0; i--)
                {
                    var quarryData = _activeCraters[i];
                    var Distance = Vector3Ex.Distance2D(quarryData.position, miningQuarry.transform.position);
                    if (Distance < MatchRadius)
                    {
                        FoundEntry = true;
                        _storedData.quarryDataList.Add(quarryData);
                        CreateResourceDeposit(miningQuarry, quarryData);
                        break;
                    }
                }
            }

            if (FoundEntry == false)
            {
                var permissionSetting = GetPermissionSetting(miningQuarry.OwnerID.ToString());
                if (permissionSetting == null) permissionSetting = _configData.Permissions[0];

                List<MineralItemData> mineralItems;
                miningQuarry._linkedDeposit._resources.Clear();
                if (miningQuarry.ShortPrefabName == "mining_quarry") mineralItems = permissionSetting.Quarry.RefillResourceDeposit(miningQuarry._linkedDeposit);
                else mineralItems = permissionSetting.PumpJack.RefillResourceDeposit(miningQuarry._linkedDeposit);

                if (mineralItems.Count == 0) PrintWarning("Deposit contain no mineral items !");
                else
                {
                    _storedData.quarryDataList.Add(new QuarryData
                    {
                        position = miningQuarry.transform.position,
                        isLiquid = miningQuarry._linkedDeposit._resources[0].isLiquid,
                        mineralItems = mineralItems
                    });
                }

                string log = string.Format("Deposit info for quarry at ({0}), will be added to datafile\n", miningQuarry.transform.position);
                var WorkPerMinute = 60f / miningQuarry.processRate * miningQuarry.workToAdd;
                foreach (var res in miningQuarry._linkedDeposit._resources)
                {
                    log += string.Format("Resource: {0,-14}, Amount: {1,5:F0} Rate: {2,5:F0} pM\n", res.type.shortname, res.amount, WorkPerMinute / res.workNeeded);
                }
                PrintWarning(log);

                //PrintWarning($"No active crater found in data when building quarry ! closest entry: {MinDistance}");
            }
            return FoundEntry;
        }

        private void OnEntityKill(MiningQuarry miningQuarry)
        {
            if (miningQuarry == null) return;

            if (miningQuarry.ShortPrefabName == "mining_quarry" || miningQuarry.ShortPrefabName == "mining.pumpjack")
            {
                _miningQuarries.Remove(miningQuarry);

                QuarryStateDetector quarrydetector = miningQuarry.gameObject.GetComponent<QuarryStateDetector>();
                if (quarrydetector != null) UnityEngine.Object.Destroy(quarrydetector);

                // cleanup datafile when quarry are removed
                CheckValidData();
            }
        }

        private void OnExplosiveThrown(BasePlayer player, SurveyCharge surveyCharge)
        {
            if (surveyCharge == null || surveyCharge.net == null)
            {
                return;
            }
            var permissionSetting = GetPermissionSetting(player.UserIDString);
            if (permissionSetting == null)
            {
                return;
            }
            surveyCharge.OwnerID = (ulong)player.userID;
            _activeSurveyCharges.Add(surveyCharge.net.ID.Value, permissionSetting);
        }

        private void OnEntityKill(SurveyCharge surveyCharge)
        {
            if (surveyCharge == null || surveyCharge.net == null) return;

            PermissionSettings permissionSettings;
            if (_activeSurveyCharges.TryGetValue(surveyCharge.net.ID.Value, out permissionSettings))
            {
                _activeSurveyCharges.Remove(surveyCharge.net.ID.Value);

                QuarryData craterinfo = findinactivecraters(surveyCharge);

                if (craterinfo == null) ModifyResourceDeposit(permissionSettings, surveyCharge.transform.position, surveyCharge.OwnerID);
                else CopyResourceDepositInfo(permissionSettings, surveyCharge.transform.position, surveyCharge.OwnerID, craterinfo);
            }
        }

        private object OnEntityTakeDamage(SurveyCrater surveyCrater, HitInfo info)
        {
            if (surveyCrater == null || !surveyCrater.OwnerID.IsSteamId())
            {
                return null;
            }
            var player = info?.InitiatorPlayer;
            if (player == null || !player.userID.IsSteamId())
            {
                return _true;
            }
            if (!AreFriends((ulong)player.userID, surveyCrater.OwnerID))
            {
                SendChatMessage(player, "NoDamage", player.UserIDString);
                return _true;
            }
            return null;
        }

        private object OnEntityTakeDamage(StorageContainer storage, HitInfo info)
        {
            if (storage?.GetParentEntity() is MiningQuarry)
            {
                var ShortPrefabName = (storage?.GetParentEntity() as MiningQuarry).ShortPrefabName;
                if (ShortPrefabName == "mining_quarry" || ShortPrefabName == "mining.pumpjack")
                    return true; // ignore damage on storage from mining quarry
            }
            return null;
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null)
            {
                return null;
            }
            var surveyCrater = target.entity as SurveyCrater;
            if (surveyCrater == null || !surveyCrater.OwnerID.IsSteamId())
            {
                return null;
            }
            var player = planner.GetOwnerPlayer();
            if (player == null)
            {
                return null;
            }
            if (!AreFriends((ulong)player.userID, surveyCrater.OwnerID))
            {
                SendChatMessage(player, "NoDeploy", player.UserIDString);
                return true;
            }
            return null;
        }

        #endregion Oxide Hooks

        #region Methods

        private int RefillMiningQuarries(bool AddMissing = false)
        {
            var count = 0;
            foreach (var miningQuarry in _miningQuarries)
            {
                if ((miningQuarry == null || miningQuarry.IsDestroyed)) continue;


                if (UpdateAndRefill(miningQuarry)) count++;
            }
            SaveData();
            return count;
        }

        private void CheckValidData()
        {
            int removals = 0;
            var QuarryList = Pool.Get<List<MiningQuarry>>();

            for (int i = _storedData.quarryDataList.Count - 1; i >= 0; i--)
            {
                bool validData = false;
                QuarryList.Clear();
                Vis.Entities(_storedData.quarryDataList[i].position, 5f, QuarryList, Layers.Mask.Default);
                foreach (MiningQuarry miningQuarry in QuarryList)
                {
                    if (miningQuarry == null || miningQuarry.IsDestroyed) continue;

                    if (Vector3Ex.Distance2D(_storedData.quarryDataList[i].position, miningQuarry.transform.position) < MatchRadius)
                    {
                        validData = true;
                        break;
                    }
                }
                if (!validData)
                {
                    _storedData.quarryDataList.RemoveAt(i);
                    removals++;
                }
            }
            if (removals != 0) PrintWarning($"CheckValidData: removing {removals} entrys,  quarrycount:{_miningQuarries.Count}");
            Pool.FreeUnmanaged(ref QuarryList);
            SaveData();
        }

        private static void CreateResourceDeposit(MiningQuarry miningQuarry, QuarryData quarryData)
        {
            if (quarryData.isLiquid)
            {
                miningQuarry.canExtractLiquid = true;
            }
            else
            {
                miningQuarry.canExtractSolid = true;
            }

            miningQuarry._linkedDeposit._resources.Clear();
            foreach (var mineralItem in quarryData.mineralItems)
            {
                var itemDefinition = ItemManager.FindItemDefinition(mineralItem.shortname);
                if (itemDefinition == null)
                {
                    continue;
                }
                miningQuarry._linkedDeposit.Add(itemDefinition, 1f, mineralItem.amount, mineralItem.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, quarryData.isLiquid);
            }
            miningQuarry.SendNetworkUpdateImmediate();
        }

        private static void CreateResourceDeposit(ResourceDepositManager.ResourceDeposit deposit, QuarryData quarryData)
        {
            deposit._resources.Clear();
            foreach (var mineralItem in quarryData.mineralItems)
            {
                var itemDefinition = ItemManager.FindItemDefinition(mineralItem.shortname);
                if (itemDefinition == null) continue;               
                deposit.Add(itemDefinition, 1f, mineralItem.amount, mineralItem.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, quarryData.isLiquid);
            }
        }

        private void ModifyResourceDeposit(PermissionSettings permissionSettings, Vector3 checkPosition, ulong playerID)
        {
            NextTick(() =>
            {
                var surveyCraterList = Pool.Get<List<SurveyCrater>>();
                Vis.Entities(checkPosition, 1f, surveyCraterList, Layers.Mask.Default);
                if (surveyCraterList.Count > 1) PrintWarning("More then one survey crater found at one spot ");

                foreach (var surveyCrater in surveyCraterList)
                {
                    if (surveyCrater == null || surveyCrater.IsDestroyed) continue;
                    if (_checkedCraters.Contains(surveyCrater.net?.ID.Value ?? 0)) continue;

                    if (Random.Range(0f, 100f) < permissionSettings.OilCraterChance)
                    {
                        var oilCrater = GameManager.server.CreateEntity(PREFAB_CRATER_OIL, surveyCrater.transform.position) as SurveyCrater;
                        if (oilCrater == null)
                        {
                            continue;
                        }
                        surveyCrater.Kill();
                        oilCrater.Spawn();
                        _checkedCraters.Add(oilCrater.net?.ID.Value ?? 0);
                        {
                            var deposit = ResourceDepositManager.GetOrCreate(oilCrater.transform.position);
                            if (deposit != null)
                            {
                                oilCrater.OwnerID = playerID;
                                deposit._resources.Clear();

                                var mineralItems = permissionSettings.PumpJack.RefillResourceDeposit(deposit);
                                _activeCraters.Add(new QuarryData
                                {
                                    position = oilCrater.transform.position,
                                    isLiquid = permissionSettings.PumpJack.IsLiquid,
                                    mineralItems = mineralItems
                                });
                            }
                        }
                    }
                    else if (Random.Range(0f, 100f) < permissionSettings.Quarry.ModifyChance)
                    {
                        _checkedCraters.Add(surveyCrater.net?.ID.Value ?? 0);
                        var deposit = ResourceDepositManager.GetOrCreate(surveyCrater.transform.position);
                        if (deposit != null)
                        {
                            surveyCrater.OwnerID = playerID;
                            deposit._resources.Clear();

                            var mineralItems = permissionSettings.Quarry.RefillResourceDeposit(deposit);
                            _activeCraters.Add(new QuarryData
                            {
                                position = surveyCrater.transform.position,
                                isLiquid = permissionSettings.Quarry.IsLiquid,
                                mineralItems = mineralItems
                            });
                        }

                    }
                    else
                    {
                        // Default FP surveycrater
                        _checkedCraters.Add(surveyCrater.net?.ID.Value ?? 0);
                        var deposit = ResourceDepositManager.GetOrCreate(surveyCrater.transform.position);
                        if (deposit != null)
                        {
                            surveyCrater.OwnerID = playerID;

                            var mineralItems = deposit._resources.Select(depositEntry => new MineralItemData
                            {
                                amount = Random.Range(50000, 100000),
                                shortname = depositEntry.type.shortname,
                                workNeeded = depositEntry.workNeeded
                            }).ToList();

                            _activeCraters.Add(new QuarryData
                            {
                                position = surveyCrater.transform.position,
                                isLiquid = deposit._resources[0].isLiquid,
                                mineralItems = mineralItems
                            });
                        }
                    }
                    
                }
                Pool.FreeUnmanaged(ref surveyCraterList);
            });
        }

      
        private void CopyResourceDepositInfo(PermissionSettings permissionSettings, Vector3 checkPosition, ulong playerID, QuarryData crater)
        {
            NextTick(() =>
            {
                var surveyCraterList = Pool.Get<List<SurveyCrater>>();
                Vis.Entities(checkPosition, 1f, surveyCraterList, Layers.Mask.Default);
                if (surveyCraterList.Count > 1) PrintWarning("More then one survey crater found at one spot ");

                foreach (var surveyCrater in surveyCraterList)
                {
                    if (surveyCrater == null || surveyCrater.IsDestroyed) continue;
                    if (_checkedCraters.Contains(surveyCrater.net?.ID.Value ?? 0)) continue;

                    SurveyCrater newsurveyCrater;
                    if (crater.isLiquid)
                    {
                        surveyCrater.Kill();
                        newsurveyCrater = GameManager.server.CreateEntity(PREFAB_CRATER_OIL, surveyCrater.transform.position) as SurveyCrater;
                        if (newsurveyCrater == null) continue;
                        newsurveyCrater.Spawn();
                    }
                    else newsurveyCrater = surveyCrater;

                    _checkedCraters.Add(newsurveyCrater.net?.ID.Value ?? 0);
                    
                    var deposit = ResourceDepositManager.GetOrCreate(newsurveyCrater.transform.position);
                    if (deposit != null)
                    {
                        newsurveyCrater.OwnerID = playerID;
                        deposit._resources.Clear();
                        CreateResourceDeposit(deposit, crater);
                    }
                    newsurveyCrater.SendNetworkUpdateImmediate();
                }
                Pool.FreeUnmanaged(ref surveyCraterList);
            });
        }


        QuarryData findinactivecraters(SurveyCharge surveycharge)
        {
            float mindistance = _configData.ResourceDepositCheckRadius;
            QuarryData targetcrater = null;
            foreach (var crater in _activeCraters)
            {
                var distance = Vector3Ex.Distance2D(crater.position, surveycharge.transform.position);
                if (distance < mindistance)
                {
                    mindistance = distance;
                    targetcrater = crater;                    
                }
            }

            if (targetcrater == null)
            {
                foreach (var crater in _storedData.quarryDataList)
                {
                    var distance = Vector3Ex.Distance2D(crater.position, surveycharge.transform.position);
                    if (distance < mindistance)
                    {
                        mindistance = distance;
                        targetcrater = crater;
                    }
                }
            }
            return targetcrater;
        }

        private PermissionSettings GetPermissionSetting(string UserIDString)
        {
            PermissionSettings permissionSettings = null;
            var priority = 0;
            foreach (var perm in _configData.Permissions)
            {
                if (perm.Priority >= priority && permission.UserHasPermission(UserIDString, perm.Permission))
                {
                    priority = perm.Priority;
                    permissionSettings = perm;
                }
            }
            return permissionSettings;
        }

        #region AreFriends

        private bool AreFriends(ulong playerID, ulong friendID)
        {
            if (playerID == friendID)
            {
                return true;
            }
            if (_configData.Global.UseTeams && SameTeam(playerID, friendID))
            {
                return true;
            }
            if (_configData.Global.UseFriends && HasFriend(playerID, friendID))
            {
                return true;
            }
            if (_configData.Global.UseClans && SameClan(playerID, friendID))
            {
                return true;
            }
            return false;
        }

        private bool HasFriend(ulong playerID, ulong friendID)
        {
            if (!IspluginLoaded(Friends))
            {
                return false;
            }
            return (bool)Friends.Call("HasFriend", playerID, friendID);
        }

        private bool SameTeam(ulong playerID, ulong friendID)
        {
            if (!RelationshipManager.TeamsEnabled())
            {
                return false;
            }
            var playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerID);
            if (playerTeam == null) return false;
            
            if (playerTeam.members.Contains(friendID)) return true;
            return false;
        }

        private bool SameClan(ulong playerID, ulong friendID)
        {
            if (IspluginLoaded(Clans))
            {
                //Clans
                var isMember = Clans.Call("IsClanMember", playerID.ToString(), friendID.ToString());
                if (isMember != null && (bool)isMember) return true;

                //Rust:IO Clans
                var playerClan = Clans.Call("GetClanOf", playerID);
                var friendClan = Clans.Call("GetClanOf", friendID);
                if (playerClan != null && friendClan != null && (string)playerClan == (string)friendClan) return true;
            }

            if (_configData.Global.UseClanTable == true)
            {
                long playerclan = BasePlayer.FindAwakeOrSleeping(playerID.ToString())?.clanId ?? 0;
                IClan clan = null;
                if (playerclan!=0 && (ClanManager.ServerInstance.Backend?.TryGet(playerclan, out clan) ?? false))
                {
                    foreach (ClanMember member in clan.Members)
                    {
                        if (member.SteamId == friendID) return true;
                    }
                }
            }

            return false;
        }

        #endregion AreFriends

        private bool IsAdmin(ulong id) => permission.UserHasPermission(id.ToString(), _configData.PermissionAdmin);

        bool IspluginLoaded(Plugin a) => (a?.IsLoaded ?? false);

        #endregion Methods

        private string PositionToString(Vector3 position) => MapHelper.PositionToString(position);
        #region Commands

        private void CCmdRefresh(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;

            var count = RefillMiningQuarries(true);

            if (player != null) SendChatMessage(player, "Refreshed {0} of {1} quarry resources.", count, _miningQuarries.Count);
            else Puts($"Refreshed {count} of {_miningQuarries.Count} quarry resources.");
        }
        
        private void CCmdResetDeposit(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null) return;
            if (!player.IsAdmin &&  !IsAdmin((ulong)player.userID)) return;

            _storedData.quarryDataList.Clear();
            var count = RefillMiningQuarries(true);

            if (player != null) SendChatMessage(player, "Reset resource deposit of {0} quarry.", _miningQuarries.Count);
            else Puts($"Reset resource deposit of {_miningQuarries.Count} quarry.");
        }

        private void CCmdInfo(IPlayer iplayer, string command, string[] args)
        {
            if (iplayer.Id == "server_console") return;

            var player = iplayer.Object as BasePlayer;
            if (player == null) return;

            int count = 0;
            int loopcount = 0;
            string log = "";

            foreach (var miningQuarry in _miningQuarries)
            {
                if (miningQuarry == null || miningQuarry.IsDestroyed)
                {
                    continue;
                }
                var WorkPerMinute = 60f / miningQuarry.processRate * miningQuarry.workToAdd;
                if (Vector3Ex.Distance2D(player.transform.position, miningQuarry.transform.position) < PlayerRadius)
                {
                    int index = 0;
                    foreach (var quarryData in _storedData.quarryDataList)
                    {
                        if (quarryData == null)
                        {
                            continue;
                        }
                        if (Vector3Ex.Distance2D(quarryData.position, miningQuarry.transform.position) < MatchRadius)
                        {
                            count++;

                            log = string.Format("Datafile contain {0} entry,  Quarry count {1} \n", _storedData.quarryDataList.Count, _miningQuarries.Count);
                            log += string.Format("Datafile for Quarry at : {0}\n", PositionToString(quarryData.position));
                            foreach (var mineralitem in quarryData.mineralItems)
                            {
                                log += string.Format("Resource: {0,-14}, Rate: {1,5:F0} pM, amount: {2,-5}\n", mineralitem.shortname, WorkPerMinute / mineralitem.workNeeded, mineralitem.amount);
                            }
                        }
                        index++;
                    }

                    log += string.Format("Deposit info\n");
                    foreach (var res in miningQuarry._linkedDeposit._resources)
                    {
                        log += string.Format("Resource: {0,-14}, Rate: {1,5:F0} pM, amount: {2,-5}\n", res.type.shortname, WorkPerMinute / res.workNeeded, res.amount);
                    }
                }
                loopcount++;
            }
            SendChatMessage(player, log);
            SendChatMessage(player, "Tested {0} entry, Found {1} quarry resources in data file.", loopcount, count);
            SaveData();
        }

        #endregion Commands

        #region ConfigurationFile

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public GlobalSettings Global { get; set; } = new GlobalSettings();

            [JsonProperty(PropertyName = "Chat Settings")]
            public ChatSettings Chat { get; set; } = new ChatSettings();

            [JsonProperty(PropertyName = "Permission List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<PermissionSettings> Permissions { get; set; } = new List<PermissionSettings>
            {
                new PermissionSettings
                {
                    Permission = "backpumpjack.use",
                    Priority = 0,
                    OilCraterChance = 20f,
                    PumpJack = new PumpJackSettings
                    {
                        AmountMin = 1,
                        AmountMax = 1,
                        AllowDuplication = false,
                        MineralItems = new List<MineralItem>
                        {
                            new MineralItem
                            {
                                ShortName = "crude.oil",
                                Chance = 50f,
                                PmMin = 28.8f,
                                PmMax = 28.8f
                            },
                            new MineralItem
                            {
                                ShortName = "lowgradefuel",
                                Chance = 50f,
                                PmMin = 81.6f,
                                PmMax = 81.6f,
                            }
                        }
                    },
                    Quarry = new QuarrySettings
                    {
                        AmountMin = 1,
                        AmountMax = 2,
                        ModifyChance = 10,
                        AllowDuplication = false,
                        MineralItems = new List<MineralItem>
                        {
                            new MineralItem
                            {
                                ShortName = "stones",
                                Chance = 60f,
                                PmMin = 100f,
                                PmMax = 150f
                            },
                            new MineralItem
                            {
                                ShortName = "metal.ore",
                                Chance = 50f,
                                PmMin = 12f,
                                PmMax = 20f
                            },
                            new MineralItem
                            {
                                ShortName = "sulfur.ore",
                                Chance = 50f,
                                PmMin = 12f,
                                PmMax = 12f
                            },
                            new MineralItem
                            {
                                ShortName = "hq.metal.ore",
                                Chance = 50f,
                                PmMin = 1.0f,
                                PmMax = 1.5f
                            }
                        }
                    }
                },
                new PermissionSettings
                {
                    Permission = "backpumpjack.vip",
                    Priority = 1,
                    OilCraterChance = 40f,
                    PumpJack = new PumpJackSettings
                    {
                        AmountMin = 2,
                        AmountMax = 2,
                        AllowDuplication = false,
                        MineralItems = new List<MineralItem>
                        {
                            new MineralItem
                            {
                                ShortName = "crude.oil",
                                Chance = 50f,
                                PmMin = 38f,
                                PmMax = 38f
                            },
                            new MineralItem
                            {
                                ShortName = "lowgradefuel",
                                Chance = 50f,
                                PmMin = 100f,
                                PmMax = 100f,
                            }
                        }
                    },
                    Quarry = new QuarrySettings
                    {
                        AmountMin = 1,
                        AmountMax = 3,
                        ModifyChance = 50,
                        AllowDuplication = false,
                        MineralItems = new List<MineralItem>
                        {
                            new MineralItem
                            {
                                ShortName = "stones",
                                Chance = 60f,
                                PmMin = 120f,
                                PmMax = 180f
                            },
                            new MineralItem
                            {
                                ShortName = "metal.ore",
                                Chance = 50f,
                                PmMin = 15f,
                                PmMax = 25f
                            },
                            new MineralItem
                            {
                                ShortName = "sulfur.ore",
                                Chance = 50f,
                                PmMin = 15f,
                                PmMax = 15f
                            },
                            new MineralItem
                            {
                                ShortName = "hq.metal.ore",
                                Chance = 50f,
                                PmMin = 1.5f,
                                PmMax = 2f
                            }
                        }
                    }
                }
            };

            // Static quarry override
            [JsonProperty(PropertyName = "Static quarry settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, StaticQuarrySettings> StaticQuarryType { get; set; } = new Dictionary<string, StaticQuarrySettings>
            {
                {"Stone",
                    new StaticQuarrySettings
                    {
                        MineralItems = new List<StaticMineralItem>
                        {
                            new StaticMineralItem
                            {
                                ShortName = "stones",
                                pM = 2500f
                            },
                            new StaticMineralItem
                            {
                                ShortName = "metal.ore",
                                pM = 500f
                            }
                        }
                    }
                },
                {"HQM",
                    new StaticQuarrySettings
                    {
                        MineralItems = new List<StaticMineralItem>
                        {
                            new StaticMineralItem
                            {
                                ShortName = "hq.metal.ore",
                                pM = 25f
                            }
                        }
                    }
                },
                { "Sulfur",
                    new StaticQuarrySettings
                    {
                        MineralItems = new List<StaticMineralItem>
                        {
                            new StaticMineralItem
                            {
                                ShortName = "sulfur.ore",
                                pM = 500f
                            }
                        }
                    }
                },
                { "Oil",
                    new StaticQuarrySettings
                    {
                        MineralItems = new List<StaticMineralItem>
                        {
                            new StaticMineralItem
                            {
                                ShortName = "crude.oil",
                                pM = 30f
                            },
                            new StaticMineralItem
                            {
                                ShortName = "lowgradefuel",
                                pM = 85f,
                            }
                        }
                    }
                }
            };

            [JsonProperty(PropertyName = "Apply patch to user quarry to fix long run time")]
            public bool ApplyPatchtoUserQuarry = true;

            [JsonProperty(PropertyName = "Apply patch for mining rates for more precise pM config params")]
            public bool ApplyPatchForMiningRates = false;

            [JsonProperty(PropertyName = "Patch for ladder flyhack")]
            public bool PatchLadder = true;

            [JsonProperty(PropertyName = "Patch for light signal when quarry is running")]
            public bool PatchLightSignal = true;

            [JsonProperty(PropertyName = "Maximum stack size for diesel engine (-1 to disable function)")]
            public int DieselFuelMaxStackSize = -1;

            [JsonProperty(PropertyName = "Number of slots for diesel storage (-1 to disable function)")]
            public int FuelSlots = -1;

            [JsonProperty(PropertyName = "Number of slots for output storage (-1 to disable function)")]
            public int HopperSlots = -1;

            [JsonProperty(PropertyName = "Time per barrel of diesel in second (-1 to disable function, default time 125 sec)")]
            public int TimePerBarrel = -1;

            [JsonProperty(PropertyName = "Enable static quarry resource modifier")]
            public bool StaticQuarryModifier = false;

            [JsonProperty(PropertyName = "refill command name")]
            public string CommandRefill = "backpumpjack.refill";

            [JsonProperty(PropertyName = "Info command name")]
            public string CommandInfo = "backpumpjack.info";

            [JsonProperty(PropertyName = "Reset resource deposit command name")]
            public string CommandResetDeposits = "backpumpjack.reset";

            [JsonProperty(PropertyName = "Search radius for past resource deposit allocation (use 0.0 to disable)")]
            public float ResourceDepositCheckRadius = 0f;

            [JsonProperty(PropertyName = "Permission Admin")]
            public string PermissionAdmin { get; set; } = "backpumpjack.admin";

            [JsonProperty(PropertyName = "Version")]
            public VersionNumber Version { get; set; }
        }

        private class GlobalSettings
        {
            [JsonProperty(PropertyName = "Use Teams")]
            public bool UseTeams { get; set; } = true;

            [JsonProperty(PropertyName = "Use Friends")]
            public bool UseFriends { get; set; } = true;

            [JsonProperty(PropertyName = "Use Clans")]
            public bool UseClans { get; set; } = false;

            [JsonProperty(PropertyName = "Use clan table")]
            public bool UseClanTable { get; set; } = false;

            [JsonProperty(PropertyName = "Block damage another player's survey crater")]
            public bool CantDamage { get; set; } = true;

            [JsonProperty(PropertyName = "Block deploy a quarry on another player's survey crater")]
            public bool CantDeploy { get; set; } = true;
        }

        private class ChatSettings
        {
            [JsonProperty(PropertyName = "Chat Prefix")]
            public string Prefix { get; set; } = "<color=#00FFFF>[BackPumpJack]</color>: ";

            [JsonProperty(PropertyName = "Chat SteamID Icon")]
            public ulong SteamIdIcon { get; set; } = 0;
        }

        private class PermissionSettings
        {
            [JsonProperty(PropertyName = "Permission")]
            public string Permission { get; set; }

            [JsonProperty(PropertyName = "Priority")]
            public int Priority { get; set; }

            [JsonProperty(PropertyName = "Oil Crater Chance")]
            public float OilCraterChance { get; set; }

            [JsonProperty(PropertyName = "Oil Crater Settings")]
            public PumpJackSettings PumpJack { get; set; } = new PumpJackSettings();

            [JsonProperty(PropertyName = "Normal Crater Settings")]
            public QuarrySettings Quarry { get; set; } = new QuarrySettings();
        }

        private abstract class MiningSettings
        {
            [JsonProperty(PropertyName = "Minimum Mineral Amount")]
            public int AmountMin { get; set; }

            [JsonProperty(PropertyName = "Maximum Mineral Amount")]
            public int AmountMax { get; set; }

            [JsonProperty(PropertyName = "Allow Duplication Of Mineral Item")]
            public bool AllowDuplication { get; set; }

            [JsonProperty(PropertyName = "Mineral Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MineralItem> MineralItems { get; set; } = new List<MineralItem>();

            [JsonIgnore]
            public abstract bool IsLiquid { get; }

            public abstract float GetWorkPerMinute();

            public List<MineralItemData> RefillResourceDeposit(ResourceDepositManager.ResourceDeposit deposit)
            {
                var amountsRemaining = Random.Range(AmountMin, AmountMax + 1);
                var mineralItems = new List<MineralItemData>();
                if (deposit == null) return mineralItems;
                if (MineralItems.Count == 0) return mineralItems;               

                
                for (var i = 0; i < 200; i++)
                {
                    if (amountsRemaining <= 0)
                    {
                        break;
                    }
                    var mineralItem = MineralItems.GetRandom();
                    if (mineralItem != null)
                    {
                        if (!AllowDuplication && deposit._resources.Any(x => x.type.shortname == mineralItem.ShortName))
                        {
                            continue;
                        }
                        if (Random.Range(0f, 100f) < mineralItem.Chance)
                        {
                            var itemDef = ItemManager.FindItemDefinition(mineralItem.ShortName);
                            if (itemDef != null)
                            {
                                var amount = Random.Range(5000000, 10000000);
                                var workNeeded = GetWorkPerMinute() / Random.Range(mineralItem.PmMin, mineralItem.PmMax);
                                deposit.Add(itemDef, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, IsLiquid);
                                mineralItems.Add(new MineralItemData
                                {
                                    amount = amount,
                                    shortname = itemDef.shortname,
                                    workNeeded = workNeeded
                                });
                            }
                            amountsRemaining--;
                        }
                    }
                }
                return mineralItems;
            }
        }

        private class QuarrySettings : MiningSettings
        {
            [JsonProperty(PropertyName = "Modify Chance (If not modified, use default mineral)", Order = -1)]
            public float ModifyChance { get; set; }

            public static float WorkPerMinute { get; set; }
            public override bool IsLiquid => false;
            public override float GetWorkPerMinute() => WorkPerMinute;
        }

        private class PumpJackSettings : MiningSettings
        {
            public static float WorkPerMinute { get; set; }
            public override bool IsLiquid => true;
            public override float GetWorkPerMinute() => WorkPerMinute;
        }

        private class MineralItem
        {
            [JsonProperty(PropertyName = "Mineral Item Short Name")]
            public string ShortName { get; set; }

            [JsonProperty(PropertyName = "Chance")]
            public float Chance { get; set; }

            [JsonProperty(PropertyName = "Minimum pM")]
            public float PmMin { get; set; }

            [JsonProperty(PropertyName = "Maximum pM")]
            public float PmMax { get; set; }
        }

        private class StaticQuarrySettings
        {
            [JsonProperty(PropertyName = "Mineral Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<StaticMineralItem> MineralItems { get; set; } = new List<StaticMineralItem>();

            public void RefillResourceDeposit(ResourceDepositManager.ResourceDeposit deposit, float WorkPerMinute, bool IsLiquid = false)
            {
                if (deposit == null)
                {
                    _instance.PrintError("RefillResourceDeposit  Null deposit");
                    return;
                }
                for (var i = 0; i < MineralItems.Count; i++)
                {
                    var mineralItem = MineralItems[i];

                    var itemDef = ItemManager.FindItemDefinition(mineralItem.ShortName);
                    if (itemDef != null)
                    {
                        var amount = 1000;
                        var workNeeded = WorkPerMinute / Math.Max(mineralItem.pM, 10f);
                        deposit.Add(itemDef, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, IsLiquid);
                    }
                }
                return;
            }
        }


        private class StaticMineralItem
        {
            [JsonProperty(PropertyName = "Mineral Item Short Name")]
            public string ShortName { get; set; }

            [JsonProperty(PropertyName = "Resource per minutes (pM)")]
            public float pM { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                {
                    LoadDefaultConfig();
                }
                else
                {
                    UpdateConfigValues();
                }
            }
            catch (Exception ex)
            {
                PrintError($"The configuration file is corrupted or missing. \n{ex}");
                LoadDefaultConfig();
            }

            foreach (var RessourcePermission in _configData.Permissions)
            {
                if (RessourcePermission.OilCraterChance != 0 && RessourcePermission.PumpJack.AmountMin == 0)
                    PrintWarning($"Warning: {RessourcePermission.Permission} Oil Crater Settings, AmountMin is zero");

                if (RessourcePermission.OilCraterChance != 0 && RessourcePermission.PumpJack.AmountMax > RessourcePermission.PumpJack.MineralItems.Count)
                    PrintWarning($"Warning: {RessourcePermission.Permission} Oil Crater Settings, AmountMax ({RessourcePermission.PumpJack.AmountMax}) larger then possible resource ({RessourcePermission.PumpJack.MineralItems.Count})");

                if (RessourcePermission.Quarry.ModifyChance != 0 && RessourcePermission.Quarry.AmountMin == 0)
                    PrintWarning($"Warning: {RessourcePermission.Permission} Normal Crater Settings, Chance AmountMin is zero");

                if (RessourcePermission.Quarry.ModifyChance != 0 && RessourcePermission.Quarry.AmountMax > RessourcePermission.Quarry.MineralItems.Count)
                    PrintWarning($"Warning: {RessourcePermission.Permission} Normal Crater Settings, AmountMax ({RessourcePermission.Quarry.AmountMax}) larger then possible resource  ({RessourcePermission.Quarry.MineralItems.Count})");

                if (RessourcePermission.Quarry.MineralItems.Count == 0)
                    PrintWarning($"Warning: {RessourcePermission.Permission} Mineral Item list is empty");

                if (RessourcePermission.Quarry.AmountMin > RessourcePermission.Quarry.AmountMax)
                    PrintWarning($"Warning: {RessourcePermission.Permission} AmountMin can not be larger ther AmountMax for Quarry");

                if (RessourcePermission.PumpJack.AmountMin > RessourcePermission.PumpJack.AmountMax)
                    PrintWarning($"Warning: {RessourcePermission.Permission} AmountMin can not be larger ther AmountMax for Pumpjack");

            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
            _configData.Version = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_configData);
        }

        private void UpdateConfigValues()
        {
            if (_configData.Version < Version)
            {
                if (_configData.Version <= default(VersionNumber))
                {
                    string prefix, prefixColor;
                    if (GetConfigValue(out prefix, "Chat Settings", "Chat Prefix") && GetConfigValue(out prefixColor, "Chat Settings", "Chat Prefix Color"))
                    {
                        _configData.Chat.Prefix = $"<color={prefixColor}>{prefix}</color>: ";
                    }
                }
                _configData.Version = Version;
            }
        }

        private bool GetConfigValue<T>(out T value, params string[] path)
        {
            var configValue = Config.Get(path);
            if (configValue == null)
            {
                value = default(T);
                return false;
            }
            value = Config.ConvertValue<T>(configValue);
            return true;
        }

        #endregion ConfigurationFile

        #region DataFile

        private StoredData _storedData;

        private class StoredData
        {
            public readonly List<QuarryData> quarryDataList = new List<QuarryData>();
        }

        private class QuarryData
        {
            public Vector3 position;
            public bool isLiquid;
            public List<MineralItemData> mineralItems = new List<MineralItemData>();
        }

        private class MineralItemData
        {
            public string shortname;
            public int amount;
            public float workNeeded;
        }

        private void LoadData()
        {
            try
            {
                _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                PrintWarning("Data file did not load properly or is missing.");
                _storedData = null;
            }
            if (_storedData == null)
            {
                ClearData();
            }
        }

        private void ClearData()
        {
            _storedData = new StoredData();
            SaveData();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
        }


        #endregion DataFile

        #region LanguageFile

        private void BroadcastMessage(string msg, params object[] args)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                SendChatMessage(player, msg, args);
            }
        }

        public void SendChatMessage(BasePlayer player, string msg, params object[] args)
        {
            player.ChatMessage(Lang(msg, player.UserIDString, args));
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, id), args);
            }
            catch (Exception)
            {
                PrintError($"Error in the language formatting of '{key}'. (userid: {id}. lang: {lang.GetLanguage(id)}. args: {string.Join(" ,", args)})");
                throw;
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoDamage"] = "You can't damage another player's survey crater.",
                ["NoDeploy"] = "You can't deploy a quarry on another player's survey crater."
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoDamage"] = "您不能伤害别人的矿坑",
                ["NoDeploy"] = "您不能放置挖矿机到别人的矿坑上"
            }, this, "zh-CN");
        }

        #endregion LanguageFile


        #region statedetector

        //const string _sirenPrefab = "assets/prefabs/deployable/playerioents/lights/sirenlight/electric.sirenlight.deployed.prefab";
        //const string _sirenPrefab = "assets/prefabs/misc/permstore/industriallight/industrial.wall.lamp.red.deployed.prefab";
        //const string _lampPrefab = "assets/prefabs/misc/permstore/industriallight/industrial.wall.lamp.deployed.prefab";
        const string _lampPrefab = "assets/prefabs/misc/permstore/industriallight/industrial.wall.lamp.green.deployed.prefab";
        //const string _lampPrefab = "assets/prefabs/misc/permstore/industriallight/industrial.wall.lamp.red.deployed.prefab";
        const string _ladderprefab = "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab";


        // This component adds a method to evaluate if quarry is stopped from fuel or full output 
        public class QuarryStateDetector : MonoBehaviour
        {
            public MiningQuarry quarry;
            bool state;

            SimpleLight lamp1 = null;
            SimpleLight lamp2 = null;
            BaseLadder ladder1 = null;
            BaseLadder ladder2 = null;


            Vector3 QuarryLampPos1 = new Vector3(2.8f, 9.4f, 2.2f);
            Vector3 QuarryLampPos2 = new Vector3(2.8f, 9.4f, -2.0f);
            Vector3 PumpLampPos1 = new Vector3(-3.8f, 8.5f, 1.37f);
            Vector3 PumpLampPos2 = new Vector3(-3.8f, 8.5f, -1.37f);

            Vector3 LampRotation1 = new Vector3(0.0f, 0.0f, 0.0f);
            Vector3 LampRotation2 = new Vector3(0.0f, 180.0f, 0.0f);


            Vector3 LadderPosition1 = new Vector3(1.6f, 8.0f, -2.3f);
            Vector3 LadderPosition2 = new Vector3(1.6f, 4.8f, -2.6f);

            Vector3 LadderRotation = new Vector3(-5.0f, 180.0f, 0.0f);

            void Awake()
            {
                quarry = GetComponent<MiningQuarry>();
                state = quarry.IsEngineOn();

                if (quarry.ShortPrefabName == "mining_quarry")
                {
                    if (_instance._configData.PatchLadder)
                    {
                        ladder1 = GameManager.server.CreateEntity(_ladderprefab, quarry.transform.position, new Quaternion(), true) as BaseLadder;
                        if (ladder1 != null)
                        {
                            ladder1.Spawn();
                            ladder1.pickup.enabled = false;
                            ladder1.SetParent(quarry);
                            ladder1.transform.localPosition = LadderPosition1;
                            ladder1.transform.localEulerAngles = LadderRotation;
                            ladder1.InitializeHealth(10000, 10000);
                            ladder1.EnableSaving(false);
                            RemoveProblemComponents(ladder1);
                            ladder1.SendNetworkUpdateImmediate();
                        }

                        ladder2 = GameManager.server.CreateEntity(_ladderprefab, quarry.transform.position, new Quaternion(), true) as BaseLadder;
                        if (ladder2 != null)
                        {
                            ladder2.Spawn();
                            ladder2.pickup.enabled = false;
                            ladder2.SetParent(quarry);
                            ladder2.transform.localPosition = LadderPosition2;
                            ladder2.transform.localEulerAngles = LadderRotation;
                            ladder2.InitializeHealth(10000, 10000);
                            ladder2.EnableSaving(false);
                            RemoveProblemComponents(ladder2);
                            ladder2.SendNetworkUpdateImmediate();
                        }
                    }
                }

                if (_instance._configData.PatchLightSignal)
                {
                    lamp1 = GameManager.server.CreateEntity(_lampPrefab, quarry.transform.position, new Quaternion(), true) as SimpleLight;
                    if (lamp1 != null)
                    {
                        lamp1.Spawn();
                        lamp1.pickup.enabled = false;
                        lamp1.SetParent(quarry);
                        lamp1.transform.localPosition = (quarry.ShortPrefabName == "mining_quarry") ? QuarryLampPos1 : PumpLampPos1;
                        lamp1.transform.localEulerAngles = LampRotation1;
                        lamp1.InitializeHealth(10000, 10000);
                        HideInputsAndOutputs(lamp1);
                        RemoveProblemComponents(lamp1);
                        lamp1.CancelInvoke(lamp1.DecayTick);
                        lamp1.EnableSaving(false);
                        lamp1.SetFlag(IOEntity.Flags.On, state);
                        lamp1.SendNetworkUpdateImmediate();
                    }

                    lamp2 = GameManager.server.CreateEntity(_lampPrefab, quarry.transform.position, new Quaternion(), true) as SimpleLight;
                    if (lamp2 != null)
                    {
                        lamp2.Spawn();
                        lamp2.pickup.enabled = false;
                        lamp2.SetParent(quarry);
                        lamp2.transform.localPosition = (quarry.ShortPrefabName == "mining_quarry") ? QuarryLampPos2 : PumpLampPos2;
                        lamp2.InitializeHealth(10000, 10000);
                        lamp2.transform.localEulerAngles = LampRotation2;
                        HideInputsAndOutputs(lamp2);
                        RemoveProblemComponents(lamp2);
                        lamp2.CancelInvoke(lamp2.DecayTick);
                        lamp2.EnableSaving(false);
                        lamp2.SetFlag(IOEntity.Flags.On, state);
                        lamp2.SendNetworkUpdateImmediate();
                    }
                }

                // randomize the start of the invoke to avoid server lag if all quarry evaluate at the same time
                var delay = Oxide.Core.Random.Range(0f, 1.0f);
                InvokeRepeating("CheckMiningQuarry", delay, 1.0f);

            }

            void OnDestroy()
            {
                CancelInvoke("CheckMiningQuarry");
            }

            void CheckMiningQuarry()
            {                
                if (!quarry.IsEngineOn() && state)    // switch off
                {
                    lamp1?.SetFlag(IOEntity.Flags.On, false);
                    lamp2?.SetFlag(IOEntity.Flags.On, false);
                    state = false;
                }
                //else if (quarry.engineSwitchPrefab.instance.HasFlag(MiningQuarry.Flags.On) && !state) // switch on
                else if (quarry.engineSwitchPrefab.instance.HasFlag(MiningQuarry.Flags.On) && !state) // switch on
                {
                    lamp1?.SetFlag(IOEntity.Flags.On, true);
                    lamp2?.SetFlag(IOEntity.Flags.On, true);
                    state = true;
                }
            }


            private static void HideInputsAndOutputs(IOEntity ioEntity)
            {
                // Hide the inputs and outputs on the client.
                foreach (var input in ioEntity.inputs)
                    input.type = IOEntity.IOType.Generic;

                foreach (var output in ioEntity.outputs)
                    output.type = IOEntity.IOType.Generic;
            }

            private static void RemoveProblemComponents(BaseEntity entity)
            {
                UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());

                foreach (var collider in entity.GetComponentsInChildren<Collider>())
                {
                    if (!collider.isTrigger) UnityEngine.Object.DestroyImmediate(collider);
                }
            }
        }

    #endregion
    }

}