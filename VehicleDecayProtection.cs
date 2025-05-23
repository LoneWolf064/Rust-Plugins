﻿// #define DEBUG_LOG
// #define DEBUG_SHOW

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Rust;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Vehicle Decay Protection", "WhiteThunder", "2.7.2")]
    [Description("Protects vehicles from decay based on ownership and other factors.")]
    internal class VehicleDecayProtection : CovalencePlugin
    {
        #region Fields

        private Configuration _pluginConfig;

        private const string Permission_NoDecay_AllVehicles = "vehicledecayprotection.nodecay.allvehicles";

        private const float VanillaDecaySeconds = 60f;
        #if DEBUG_SHOW
        private const float MaxDrawSeconds = 30f;
        private const float MaxDrawDistanceSquared = 10000f;
        #endif

        private readonly VehicleInfoManager _vehicleInfoManager;

        public VehicleDecayProtection()
        {
            _vehicleInfoManager = new VehicleInfoManager(this);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(Permission_NoDecay_AllVehicles, this);
        }

        private void OnServerInitialized()
        {
            _vehicleInfoManager.OnServerInitialized(_pluginConfig);

            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                var entity = networkable as BaseEntity;
                if (entity is BaseVehicle or HotAirBalloon)
                {
                    HandleEntitySpawned(entity);
                }
            }
        }

        private void Unload()
        {
            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                var entity = networkable as BaseEntity;
                if (entity is BaseVehicle or HotAirBalloon)
                {
                    var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(entity);
                    if (vehicleInfo == null)
                        continue;

                    VehicleDecayReplacer.RemoveFromEntity(entity);
                }
            }
        }

        // Using separate hooks to improve performance by reducing hook calls.
        private void OnEntitySpawned(BaseVehicle entity) => HandleEntitySpawned(entity);
        private void OnEntitySpawned(HotAirBalloon entity) => HandleEntitySpawned(entity);

        #endregion

        #region Exposed Hooks

        private static class ExposedHooks
        {
            public static object OnVehicleDecayReplace(BaseEntity entity)
            {
                return Interface.CallHook("OnVehicleDecayReplace", entity);
            }
        }

        #endregion

        #region Helper Methods

        private void ScheduleReplaceDecay(BaseEntity entity, IVehicleInfo vehicleInfo)
        {
            NextTick(() =>
            {
                if (entity == null || entity.IsDestroyed)
                    return;

                if (ExposedHooks.OnVehicleDecayReplace(entity) is false)
                    return;

                VehicleDecayReplacer.AddToEntity(entity, vehicleInfo);
            });
        }

        private void HandleEntitySpawned(BaseEntity entity)
        {
            // Null check because some people reported seeing GetVehicleInfo throw NRE, though this shouldn't happen.
            if (entity == null)
                return;

            var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(entity);
            if (vehicleInfo == null || !vehicleInfo.VehicleConfig.Enabled)
                return;

            ScheduleReplaceDecay(entity, vehicleInfo);
        }

        private bool UserHasPermission(UserData userData, string perm)
        {
            return userData.Perms.Contains(perm)
                || permission.GroupsHavePermission(userData.Groups, perm);
        }

        private bool UserHasVehiclePermission(ulong ownerId, string vehicleSpecificNoDecayPerm)
        {
            if (ownerId == 0)
                return false;

            var userData = permission.GetUserData(ownerId.ToString());

            return UserHasPermission(userData, Permission_NoDecay_AllVehicles)
                || UserHasPermission(userData, vehicleSpecificNoDecayPerm);
        }

        private bool VehiclePrivilegeHasPermission(BaseEntity vehicle, string vehicleSpecificNoDecayPerm)
        {
            var vehiclePrivilege = GetChildOfType<VehiclePrivilege>(vehicle);
            if (vehiclePrivilege == null)
                return false;

            foreach (var entry in vehiclePrivilege.authorizedPlayers)
            {
                if (UserHasVehiclePermission(entry.userid, vehicleSpecificNoDecayPerm))
                    return true;
            }

            return false;
        }

        private bool LockOwnerHasPermission(BaseEntity vehicle, string vehicleSpecificNoDecayPerm, out ulong lockOwnerId)
        {
            lockOwnerId = 0;

            var baseLock = vehicle.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
            if (baseLock == null || !baseLock.IsLocked() || baseLock.OwnerID == vehicle.OwnerID)
                return false;

            lockOwnerId = baseLock.OwnerID;
            return UserHasVehiclePermission(baseLock.OwnerID, vehicleSpecificNoDecayPerm);
        }

        public static void LogInfo(string message) => Interface.Oxide.LogInfo($"[Vehicle Decay Protection] {message}");
        public static void LogError(string message) => Interface.Oxide.LogError($"[Vehicle Decay Protection] {message}");
        public static void LogWarning(string message) => Interface.Oxide.LogWarning($"[Vehicle Decay Protection] {message}");

        private static T GetChildOfType<T>(BaseEntity entity) where T : BaseEntity
        {
            foreach (var child in entity.children)
            {
                var childOfType = child as T;
                if (childOfType != null)
                    return childOfType;
            }

            return null;
        }

        private static string[] FindPrefabsOfType<T>() where T : BaseEntity
        {
            var prefabList = new List<string>();

            foreach (var assetPath in GameManifest.Current.entities)
            {
                var entity = GameManager.server.FindPrefab(assetPath)?.GetComponent<T>();
                if (entity == null)
                    continue;

                prefabList.Add(entity.PrefabName);
            }

            return prefabList.ToArray();
        }

        #if DEBUG_SHOW
        private static bool IsPlayerDrawEligible(BasePlayer player, BaseEntity entity)
        {
            return player.IsAdmin
                && (player.transform.position - entity.transform.position).sqrMagnitude < MaxDrawDistanceSquared;
        }

        private static void DrawVehicleText(BasePlayer player, BaseEntity entity, IVehicleInfo vehicleInfo, Color color, string text)
        {
            player.SendConsoleCommand(
                "ddraw.text",
                Mathf.Min(MaxDrawSeconds, vehicleInfo.VehicleConfig.DecayIntervalSeconds - 5f),
                color,
                entity.transform.position + new Vector3(0, entity.WorldSpaceBounds().extents.y * 2, 0),
                $"<size=20>VDP ({vehicleInfo.VehicleConfig.DecayIntervalSeconds}s)\n{text}</size>"
            );
        }
        #endif

        private static void SetupDecayTick(FacepunchBehaviour component, Action action, float time)
        {
            component.InvokeRandomized(action, UnityEngine.Random.Range(time / 2f, time), time, time / 10f);
        }

        private static bool WasRecentlyUsed(BaseEntity entity, IVehicleInfo vehicleInfo, float protectionMinutesAfterUse = -1)
        {
            var timeSinceLastUsed = vehicleInfo.GetTimeSinceLastUsed(entity);
            var vehicleConfig = vehicleInfo.VehicleConfig;
            if (vehicleConfig.ProtectionMinutesAfterUse != -1)
            {
                protectionMinutesAfterUse = vehicleConfig.ProtectionMinutesAfterUse;
            }
            if (protectionMinutesAfterUse != -1 && timeSinceLastUsed >= 60 * protectionMinutesAfterUse)
                return false;

            #if DEBUG_SHOW
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (IsPlayerDrawEligible(player, entity))
                {
                    DrawVehicleText(player, entity, vehicleInfo, Color.green, $"{(int)timeSinceLastUsed}s < {60 * protectionMinutesAfterUse}s");
                }
            }
            #endif

            #if DEBUG_LOG
            LogWarning($"{entity.ShortPrefabName} :: Recently used :: {(int)timeSinceLastUsed}s < {60 * protectionMinutesAfterUse}s");
            #endif

            return true;
        }

        private static bool VehicleHasPermission(VehicleDecayProtection pluginInstance, BaseEntity entity, IVehicleInfo vehicleInfo)
        {
            if (!pluginInstance._pluginConfig.EnablePermission)
                return false;

            pluginInstance.TrackStart();
            var ownerHasPermission = pluginInstance.UserHasVehiclePermission(entity.OwnerID, vehicleInfo.Permission);
            pluginInstance.TrackEnd();

            if (ownerHasPermission)
            {
                #if DEBUG_SHOW
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (IsPlayerDrawEligible(player, entity))
                    {
                        DrawVehicleText(player, entity, vehicleInfo, Color.green, "Owner permission");
                    }
                }
                #endif

                #if DEBUG_LOG
                LogWarning($"{entity.ShortPrefabName} :: Owner has permission :: {entity.OwnerID}");
                #endif

                return true;
            }

            ulong lockOwnerId;
            pluginInstance.TrackStart();
            var lockOwnerHasPermission = pluginInstance.LockOwnerHasPermission(entity, vehicleInfo.Permission, out lockOwnerId);
            pluginInstance.TrackEnd();

            if (lockOwnerHasPermission)
            {
                #if DEBUG_SHOW
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (IsPlayerDrawEligible(player, entity))
                    {
                        DrawVehicleText(player, entity, vehicleInfo, Color.green, "Lock owner permission");
                    }
                }
                #endif

                #if DEBUG_LOG
                LogWarning($"{entity.ShortPrefabName} :: Lock owner has permission");
                #endif

                return true;
            }

            pluginInstance.TrackStart();
            var privilegeHasPermission = entity is Tugboat && pluginInstance.VehiclePrivilegeHasPermission(entity, vehicleInfo.Permission);
            pluginInstance.TrackEnd();

            if (privilegeHasPermission)
            {
                #if DEBUG_SHOW
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (IsPlayerDrawEligible(player, entity))
                    {
                        DrawVehicleText(player, entity, vehicleInfo, Color.green, "Vehicle privilege permission");
                    }
                }
                #endif

                return true;
            }

            return false;
        }

        private static float GetInsideMultiplier(BaseEntity entity, IVehicleInfo vehicleInfo, out bool isOutside, bool forceOutsideCheck)
        {
            isOutside = true;

            var vehicleConfig = vehicleInfo.VehicleConfig;
            if (forceOutsideCheck || vehicleConfig.DecayMultiplierInside != 1f)
            {
                isOutside = entity.IsOutside();
            }

            if (vehicleConfig.DecayMultiplierInside == 1f || isOutside)
                return 1f;

            #if DEBUG_SHOW
            if (vehicleConfig.DecayMultiplierInside == 0f)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (IsPlayerDrawEligible(player, entity))
                    {
                        DrawVehicleText(player, entity, vehicleInfo, Color.green, $"Inside x{vehicleConfig.DecayMultiplierInside}");
                    }
                }
            }
            #endif

            #if DEBUG_LOG
            LogWarning($"{entity.ShortPrefabName} :: Inside :: x{vehicleConfig.DecayMultiplierInside}");
            #endif

            return vehicleConfig.DecayMultiplierInside;
        }

        private static float GetNearTCMultiplier(VehicleDecayProtection pluginInstance, BaseEntity entity, IVehicleInfo vehicleInfo)
        {
            var vehicleConfig = vehicleInfo.VehicleConfig;
            if (vehicleConfig.DecayMultiplierNearTC == 1f)
                return 1f;

            pluginInstance.TrackStart();
            var hasBuildingPrivilege = entity.GetBuildingPrivilege() != null;
            pluginInstance.TrackEnd();

            if (!hasBuildingPrivilege)
                return 1f;

            #if DEBUG_SHOW
            if (vehicleConfig.DecayMultiplierNearTC == 0f)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (IsPlayerDrawEligible(player, entity))
                    {
                        DrawVehicleText(player, entity, vehicleInfo, Color.green, $"Near TC x{vehicleConfig.DecayMultiplierNearTC}");
                    }
                }
            }
            #endif

            #if DEBUG_LOG
            LogWarning($"{entity.ShortPrefabName} :: Near TC :: x{vehicleConfig.DecayMultiplierNearTC}");
            #endif

            return vehicleConfig.DecayMultiplierNearTC;
        }

        private static float GetLocationMultiplier(VehicleDecayProtection pluginInstance, BaseEntity entity, IVehicleInfo vehicleInfo, out bool isOutside, bool forceOutsideCheck = false)
        {
            var multiplier = GetInsideMultiplier(entity, vehicleInfo, out isOutside, forceOutsideCheck);
            if (multiplier == 0f)
                return 0f;

            multiplier *= GetNearTCMultiplier(pluginInstance, entity, vehicleInfo);
            return multiplier == 0f ? 0f : multiplier;
        }

        private static float GetLocationMultiplier(VehicleDecayProtection pluginInstance, BaseEntity entity, IVehicleInfo vehicleInfo)
        {
            return GetLocationMultiplier(pluginInstance, entity, vehicleInfo, out _);
        }

        private static void DoDecayDamage(BaseCombatEntity entity, IVehicleInfo vehicleInfo, float fraction, DamageType damageType = DamageType.Decay, bool useProtection = false)
        {
            var amount = entity.MaxHealth() * fraction * vehicleInfo.GetTimeMultiplier();

            if (useProtection && entity.baseProtection != null)
            {
                // Manually scale damage so that we can show the correct amount.
                amount *= (1 - entity.baseProtection.amounts[(int)damageType]);
            }

            #if DEBUG_SHOW
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (IsPlayerDrawEligible(player, entity))
                {
                    DrawVehicleText(player, entity, vehicleInfo, Color.red, $"-{amount:f2}");
                }
            }
            #endif

            if (amount == 0)
                return;

            entity.Hurt(amount, damageType, entity, useProtection: false);
        }

        private static void DoCarDecayDamage(ModularCar car, IVehicleInfo vehicleInfo, float amount)
        {
            #if DEBUG_SHOW
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (IsPlayerDrawEligible(player, car))
                {
                    DrawVehicleText(player, car, vehicleInfo, Color.red, $"-{amount:f2}");
                }
            }
            #endif

            car.DoDecayDamage(amount);
        }

        private static void HelicopterDecay(VehicleDecayProtection pluginInstance, PlayerHelicopter heli, IVehicleInfo vehicleInfo)
        {
            if (heli.healthFraction == 0f
                || heli.IsOn()
                || WasRecentlyUsed(heli, vehicleInfo)
                || VehicleHasPermission(pluginInstance, heli, vehicleInfo))
                return;

            var multiplier = GetLocationMultiplier(pluginInstance, heli, vehicleInfo, out var isOutside, forceOutsideCheck: true);
            if (multiplier == 0f)
                return;

            var decayMinutes = isOutside ? PlayerHelicopter.outsidedecayminutes : PlayerHelicopter.insidedecayminutes;
            DoDecayDamage(heli, vehicleInfo, multiplier / decayMinutes);
        }

        private static void SnowmobileDecay(VehicleDecayProtection pluginInstance, Snowmobile snowmobile, IVehicleInfo vehicleInfo)
        {
            if (snowmobile.IsDead()
                || WasRecentlyUsed(snowmobile, vehicleInfo)
                || VehicleHasPermission(pluginInstance, snowmobile, vehicleInfo))
                return;

            var multiplier = GetLocationMultiplier(pluginInstance, snowmobile, vehicleInfo);
            if (multiplier == 0f)
                return;

            DoDecayDamage(snowmobile, vehicleInfo, multiplier / Snowmobile.outsideDecayMinutes);
        }

        private static void WaterVehicleDecay(VehicleDecayProtection pluginInstance, BaseCombatEntity waterVehicle, IVehicleInfo vehicleInfo, float outsideDecayMinutes, float deepWaterDecayMinutes, float protectionMinutesAfterUse = -1)
        {
            if (waterVehicle.healthFraction == 0f
                || WasRecentlyUsed(waterVehicle, vehicleInfo, protectionMinutesAfterUse)
                || VehicleHasPermission(pluginInstance, waterVehicle, vehicleInfo))
                return;

            var multiplier = GetLocationMultiplier(pluginInstance, waterVehicle, vehicleInfo);
            if (multiplier == 0f)
                return;

            var decayMinutes = outsideDecayMinutes;
            var overallWaterDepth = WaterLevel.GetOverallWaterDepth(waterVehicle.transform.position, waves: true, volumes: false);
            if (overallWaterDepth > 12f)
            {
                var divisor = Mathf.Lerp(0.1f, 1f, Mathf.InverseLerp(12f, 16f, overallWaterDepth));
                decayMinutes = Mathf.Min(decayMinutes, deepWaterDecayMinutes / divisor);
            }

            DoDecayDamage(waterVehicle, vehicleInfo, multiplier / decayMinutes);
        }

        private static void SledDecay(VehicleDecayProtection pluginInstance, Sled sled, IVehicleInfo vehicleInfo)
        {
            if (sled.DecayAmount == 0f
                || sled.AnyMounted()
                || VehicleHasPermission(pluginInstance, sled, vehicleInfo))
                return;

            var multiplier = GetLocationMultiplier(pluginInstance, sled, vehicleInfo);
            if (multiplier == 0f)
                return;

            DoDecayDamage(sled, vehicleInfo, multiplier * sled.DecayAmount / sled.MaxHealth(), DamageType.Generic, useProtection: true);
        }

        private static void BikeDecay(VehicleDecayProtection pluginInstance, Bike bike, IVehicleInfo vehicleInfo)
        {
            if (bike.IsDead()
                || WasRecentlyUsed(bike, vehicleInfo)
                || VehicleHasPermission(pluginInstance, bike, vehicleInfo))
                return;

            var multiplier = GetLocationMultiplier(pluginInstance, bike, vehicleInfo, out _, forceOutsideCheck: true);
            if (multiplier == 0f)
                return;

            DoDecayDamage(bike, vehicleInfo, multiplier / Bike.outsideDecayMinutes);
        }

        private static void SiegeWeaponDecay(VehicleDecayProtection pluginInstance, BaseSiegeWeapon siegeWeapon, IVehicleInfo vehicleInfo)
        {
            if (siegeWeapon.IsDestroyed
                || WasRecentlyUsed(siegeWeapon, vehicleInfo)
                || VehicleHasPermission(pluginInstance, siegeWeapon, vehicleInfo))
                return;

            var multiplier = GetLocationMultiplier(pluginInstance, siegeWeapon, vehicleInfo);
            if (multiplier == 0f)
                return;

            DoDecayDamage(siegeWeapon, vehicleInfo, multiplier / BaseSiegeWeapon.outsideDecayMinutes, useProtection: true);
        }

        #endregion

        #region Vehicle Decay Component

        private class VehicleDecayReplacer : FacepunchBehaviour
        {
            public static void AddToEntity(BaseEntity entity, IVehicleInfo vehicleInfo)
            {
                var component = entity.gameObject.AddComponent<VehicleDecayReplacer>();
                component._entity = entity;
                component._vehicleInfo = vehicleInfo;

                // Cancel vanilla decay.
                var vanillaDecayMethod = vehicleInfo.GetVanillaDecayMethod(entity);
                if (vanillaDecayMethod != null)
                {
                    entity.CancelInvoke(vanillaDecayMethod);
                }

                // Enable custom decay.
                SetupDecayTick(component, component.DecayTick, vehicleInfo.VehicleConfig.DecayIntervalSeconds);
            }

            public static void RemoveFromEntity(BaseEntity entity)
            {
                var component = entity.gameObject.GetComponent<VehicleDecayReplacer>();
                if (component == null)
                    return;

                // Enable vanilla decay.
                var vanillaDecayMethod = component._vehicleInfo.GetVanillaDecayMethod(entity);
                if (vanillaDecayMethod != null)
                {
                    SetupDecayTick(entity, vanillaDecayMethod, VanillaDecaySeconds);
                }

                // Cancel custom decay.
                DestroyImmediate(component);
            }

            private BaseEntity _entity;
            private IVehicleInfo _vehicleInfo;

            private void DecayTick()
            {
                _vehicleInfo.DecayTick(_entity);
            }
        }

        #endregion

        #region Vehicle Info

        private interface IVehicleInfo
        {
            uint[] PrefabIds { get; }
            VehicleConfig VehicleConfig { get; }
            string Permission { get; }

            void OnServerInitialized(VehicleDecayProtection plugin);
            bool IsCorrectType(BaseEntity entity);
            float GetTimeMultiplier();
            float GetTimeSinceLastUsed(BaseEntity entity);
            Action GetVanillaDecayMethod(BaseEntity entity);
            void DecayTick(BaseEntity entity);
        }

        private class VehicleInfo<T> : IVehicleInfo where T : BaseEntity
        {
            public uint[] PrefabIds { get; private set; }
            public VehicleConfig VehicleConfig { get; set; }
            public string Permission { get; private set; }

            public string VehicleType;
            public string[] PrefabPaths;

            public Func<T, float> TimeSinceLastUsed = (entity) =>
            {
                throw new NotImplementedException($"TimeSinceLastUsed: {entity.ShortPrefabName}");
            };

            public Func<T, Action> VanillaDecayMethod = (entity) =>
            {
                throw new NotImplementedException($"VanillaDecayMethod: {entity.ShortPrefabName}");
            };

            public Action<T, IVehicleInfo> Decay = (entity, vehicleInfo) =>
            {
                throw new NotImplementedException($"Decay: {entity.ShortPrefabName}");
            };

            public void OnServerInitialized(VehicleDecayProtection pluginInstance)
            {
                Permission = $"{nameof(VehicleDecayProtection)}.nodecay.{VehicleType}".ToLower();
                pluginInstance.permission.RegisterPermission(Permission, pluginInstance);

                var prefabIds = new List<uint>(PrefabPaths.Length);

                foreach (var prefabName in PrefabPaths)
                {
                    var prefabId = StringPool.Get(prefabName);
                    if (prefabId != 0)
                    {
                        prefabIds.Add(prefabId);
                    }
                    else
                    {
                        LogError($"Invalid prefab. Please alert the plugin maintainer -- {prefabName}");
                    }
                }

                PrefabIds = prefabIds.ToArray();
            }

            public bool IsCorrectType(BaseEntity entity)
            {
                return entity is T;
            }

            public float GetTimeMultiplier()
            {
                return VehicleConfig.DecayIntervalSeconds / VanillaDecaySeconds;
            }

            public float GetTimeSinceLastUsed(BaseEntity entity)
            {
                return TimeSinceLastUsed(entity as T);
            }

            public Action GetVanillaDecayMethod(BaseEntity entity)
            {
                return VanillaDecayMethod(entity as T);
            }

            public void DecayTick(BaseEntity entity)
            {
                Decay(entity as T, this);
            }
        }

        private class VehicleInfoManager
        {
            private static readonly FieldInfo BikeTimeSinceLastUsedField = typeof(Bike).GetField("timeSinceLastUsed",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            private VehicleDecayProtection _pluginInstance;

            private readonly Dictionary<uint, IVehicleInfo> _prefabIdToVehicleInfo = new();

            public VehicleInfoManager(VehicleDecayProtection pluginInstance)
            {
                _pluginInstance = pluginInstance;
            }

            public void OnServerInitialized(Configuration pluginConfig)
            {
                var allVehicles = new IVehicleInfo[]
                {
                    new VehicleInfo<AttackHelicopter>
                    {
                        VehicleType = "attackhelicopter",
                        PrefabPaths = new[] { "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.AttackHelicopter,
                        TimeSinceLastUsed = heli => UnityEngine.Time.time - heli.lastEngineOnTime,
                        VanillaDecayMethod = heli => heli.DecayTick,
                        Decay = (heli, vehicleInfo) => HelicopterDecay(_pluginInstance, heli, vehicleInfo),
                    },
                    new VehicleInfo<Ballista>
                    {
                        VehicleType = "ballista",
                        PrefabPaths = new[] { "assets/content/vehicles/siegeweapons/ballista/ballista.entity.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.Ballista,
                        TimeSinceLastUsed = siegeWeapon => UnityEngine.Time.time - siegeWeapon.lastUseTime,
                        VanillaDecayMethod = siegeWeapon => siegeWeapon.DecayTick,
                        Decay = (siegeWeapon, vehicleInfo) => SiegeWeaponDecay(_pluginInstance, siegeWeapon, vehicleInfo),
                    },
                    new VehicleInfo<BatteringRam>
                    {
                        VehicleType = "batteringram",
                        PrefabPaths = new[] { "assets/content/vehicles/siegeweapons/batteringram/batteringram.entity.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.BatteringRam,
                        TimeSinceLastUsed = siegeWeapon => UnityEngine.Time.time - siegeWeapon.lastUseTime,
                        VanillaDecayMethod = siegeWeapon => siegeWeapon.DecayTick,
                        Decay = (siegeWeapon, vehicleInfo) => SiegeWeaponDecay(_pluginInstance, siegeWeapon, vehicleInfo),
                    },
                    new VehicleInfo<Catapult>
                    {
                        VehicleType = "catapult",
                        PrefabPaths = new[] { "assets/content/vehicles/siegeweapons/catapult/catapult.entity.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.Catapult,
                        TimeSinceLastUsed = siegeWeapon => UnityEngine.Time.time - siegeWeapon.lastUseTime,
                        VanillaDecayMethod = siegeWeapon => siegeWeapon.DecayTick,
                        Decay = (siegeWeapon, vehicleInfo) => SiegeWeaponDecay(_pluginInstance, siegeWeapon, vehicleInfo),
                    },
                    new VehicleInfo<SiegeTower>
                    {
                        VehicleType = "siegetower",
                        PrefabPaths = new[] { "assets/content/vehicles/siegeweapons/siegetower/siegetower.entity.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.SiegeTower,
                        TimeSinceLastUsed = siegeWeapon => UnityEngine.Time.time - siegeWeapon.lastUseTime,
                        VanillaDecayMethod = siegeWeapon => siegeWeapon.DecayTick,
                        Decay = (siegeWeapon, vehicleInfo) => SiegeWeaponDecay(_pluginInstance, siegeWeapon, vehicleInfo),
                    },
                    new VehicleInfo<Bike>
                    {
                        VehicleType = "motorbike.sidecar",
                        PrefabPaths = new[] { "assets/content/vehicles/bikes/motorbike_sidecar.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.MotorBikeSidecar,
                        TimeSinceLastUsed = bike => (TimeSince)BikeTimeSinceLastUsedField.GetValue(bike),
                        VanillaDecayMethod = bike => bike.BikeDecay,
                        Decay = (bike, vehicleInfo) => BikeDecay(_pluginInstance, bike, vehicleInfo),
                    },
                    new VehicleInfo<Bike>
                    {
                        VehicleType = "motorbike",
                        PrefabPaths = new[] { "assets/content/vehicles/bikes/motorbike.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.MotorBike,
                        TimeSinceLastUsed = bike => (TimeSince)BikeTimeSinceLastUsedField.GetValue(bike),
                        VanillaDecayMethod = bike => bike.BikeDecay,
                        Decay = (bike, vehicleInfo) => BikeDecay(_pluginInstance, bike, vehicleInfo),
                    },
                    new VehicleInfo<Bike>
                    {
                        VehicleType = "pedalbike",
                        PrefabPaths = new[] { "assets/content/vehicles/bikes/pedalbike.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.PedalBike,
                        TimeSinceLastUsed = bike => (TimeSince)BikeTimeSinceLastUsedField.GetValue(bike),
                        VanillaDecayMethod = bike => bike.BikeDecay,
                        Decay = (bike, vehicleInfo) => BikeDecay(_pluginInstance, bike, vehicleInfo),
                    },
                    new VehicleInfo<Bike>
                    {
                        VehicleType = "pedaltrike",
                        PrefabPaths = new[] { "assets/content/vehicles/bikes/pedaltrike.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.PedalTrike,
                        TimeSinceLastUsed = bike => (TimeSince)BikeTimeSinceLastUsedField.GetValue(bike),
                        VanillaDecayMethod = bike => bike.BikeDecay,
                        Decay = (bike, vehicleInfo) => BikeDecay(_pluginInstance, bike, vehicleInfo),
                    },
                    new VehicleInfo<SubmarineDuo>
                    {
                        VehicleType = "duosubmarine",
                        PrefabPaths = new[] { "assets/content/vehicles/submarine/submarineduo.entity.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.DuoSubmarine,
                        TimeSinceLastUsed = submarine => submarine.timeSinceLastUsed,
                        VanillaDecayMethod = submarine => submarine.SubmarineDecay,
                        Decay = (submarine, vehicleInfo) => WaterVehicleDecay(
                            _pluginInstance,
                            submarine,
                            vehicleInfo,
                            BaseSubmarine.outsidedecayminutes,
                            BaseSubmarine.deepwaterdecayminutes
                        )
                    },
                    new VehicleInfo<HotAirBalloon>
                    {
                        VehicleType = "hotairballoon",
                        PrefabPaths = new[] { "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.HotAirBalloon,
                        TimeSinceLastUsed = hab => UnityEngine.Time.time - hab.sinceLastBlast,
                        VanillaDecayMethod = hab => hab.DecayTick,
                        Decay = (hab, vehicleInfo) =>
                        {
                            if (hab.healthFraction == 0f
                                || hab.IsFullyInflated
                                || WasRecentlyUsed(hab, vehicleInfo)
                                || VehicleHasPermission(_pluginInstance, hab, vehicleInfo))
                                return;

                            var multiplier = GetLocationMultiplier(_pluginInstance, hab, vehicleInfo);
                            if (multiplier == 0f)
                                return;

                            DoDecayDamage(hab, vehicleInfo, multiplier / HotAirBalloon.outsidedecayminutes);
                        }
                    },
                    new VehicleInfo<Kayak>
                    {
                        VehicleType = "kayak",
                        PrefabPaths = new[] { "assets/content/vehicles/boats/kayak/kayak.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.Kayak,
                        TimeSinceLastUsed = kayak => kayak.timeSinceLastUsed,
                        VanillaDecayMethod = kayak => kayak.BoatDecay,
                        Decay = (kayak, vehicleInfo) => WaterVehicleDecay(
                            _pluginInstance,
                            kayak,
                            vehicleInfo,
                            MotorRowboat.outsidedecayminutes,
                            MotorRowboat.deepwaterdecayminutes
                        ),
                    },
                    new VehicleInfo<Minicopter>
                    {
                        VehicleType = "minicopter",
                        PrefabPaths = new[] { "assets/content/vehicles/minicopter/minicopter.entity.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.Minicopter,
                        TimeSinceLastUsed = mini => UnityEngine.Time.time - mini.lastEngineOnTime,
                        VanillaDecayMethod = mini => mini.DecayTick,
                        Decay = (mini, vehicleInfo) => HelicopterDecay(_pluginInstance, mini, vehicleInfo),
                    },
                    new VehicleInfo<ModularCar>
                    {
                        VehicleType = "modularcar",
                        // There are at least 37 valid Modular Car prefabs.
                        PrefabPaths = FindPrefabsOfType<ModularCar>(),
                        VehicleConfig = pluginConfig.Vehicles.ModularCar,
                        TimeSinceLastUsed = car => UnityEngine.Time.time - car.lastEngineOnTime,
                        VanillaDecayMethod = car => car.DecayTick,
                        Decay = (car, vehicleInfo) =>
                        {
                            if (car.IsDestroyed
                                || car.IsOn()
                                || car.immuneToDecay
                                || WasRecentlyUsed(car, vehicleInfo)
                                || VehicleHasPermission(_pluginInstance, car, vehicleInfo))
                                return;

                            if (car.IsDead())
                            {
                                var numModules = Mathf.Max(1, car.AttachedModuleEntities.Count);
                                DoCarDecayDamage(car, vehicleInfo, 120f / numModules * vehicleInfo.GetTimeMultiplier());
                                return;
                            }

                            var multiplier = GetLocationMultiplier(_pluginInstance, car, vehicleInfo);
                            if (multiplier == 0f)
                                return;

                            var health = car.HasAnyModules
                                ? car.AttachedModuleEntities.Max(module => module.MaxHealth())
                                : car.MaxHealth();

                            DoCarDecayDamage(car, vehicleInfo, health * vehicleInfo.GetTimeMultiplier() * multiplier / ModularCar.outsidedecayminutes);
                        }
                    },
                    new VehicleInfo<RHIB>
                    {
                        VehicleType = "rhib",
                        PrefabPaths = new[] { "assets/content/vehicles/boats/rhib/rhib.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.RHIB,
                        TimeSinceLastUsed = rhib => rhib.timeSinceLastUsedFuel,
                        VanillaDecayMethod = rhib => rhib.BoatDecay,
                        Decay = (rhib, vehicleInfo) =>
                        {
                            if (rhib.IsDying)
                                return;

                            WaterVehicleDecay(
                                _pluginInstance,
                                rhib,
                                vehicleInfo,
                                MotorRowboat.outsidedecayminutes,
                                MotorRowboat.deepwaterdecayminutes,
                                MotorRowboat.decaystartdelayminutes
                            );
                        }
                    },
                    new VehicleInfo<RidableHorse>
                    {
                        VehicleType = "ridablehorse",
                        PrefabPaths = new[] { "assets/content/vehicles/horse/ridablehorse.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.RidableHorse,
                        TimeSinceLastUsed = horse => UnityEngine.Time.time - horse.lastRiddenTime,
                        VanillaDecayMethod = horse => horse.HorseDecay,
                        Decay = (horse, vehicleInfo) =>
                        {
                            if (horse.healthFraction == 0f
                                || horse.IsDestroyed
                                || WasRecentlyUsed(horse, vehicleInfo))
                                return;

                            if (UnityEngine.Time.time < horse.lastEatTime + 600f
                                || horse.IsForSale
                                || UnityEngine.Time.time < horse.nextDecayTime
                                || VehicleHasPermission(_pluginInstance, horse, vehicleInfo))
                                return;

                            var multiplier = GetLocationMultiplier(_pluginInstance, horse, vehicleInfo);
                            if (multiplier == 0f)
                                return;

                            DoDecayDamage(horse, vehicleInfo, multiplier / RidableHorse.decayMinutes);
                        },
                    },
                    new VehicleInfo<MotorRowboat>
                    {
                        VehicleType = "rowboat",
                        PrefabPaths = new[] { "assets/content/vehicles/boats/rowboat/rowboat.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.Rowboat,
                        TimeSinceLastUsed = rowBoat => rowBoat.timeSinceLastUsedFuel,
                        VanillaDecayMethod = rowBoat => rowBoat.BoatDecay,
                        Decay = (rowBoat, vehicleInfo) =>
                        {
                            if (rowBoat.IsDying)
                                return;

                            WaterVehicleDecay(
                                _pluginInstance,
                                rowBoat,
                                vehicleInfo,
                                MotorRowboat.outsidedecayminutes,
                                MotorRowboat.deepwaterdecayminutes,
                                MotorRowboat.decaystartdelayminutes
                            );
                        }
                    },
                    new VehicleInfo<ScrapTransportHelicopter>
                    {
                        VehicleType = "scraptransporthelicopter",
                        PrefabPaths = new[] { "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.ScrapTransportHelicopter,
                        TimeSinceLastUsed = heli => UnityEngine.Time.time - heli.lastEngineOnTime,
                        VanillaDecayMethod = heli => heli.DecayTick,
                        Decay = (heli, vehicleInfo) => HelicopterDecay(_pluginInstance, heli, vehicleInfo),
                    },
                    new VehicleInfo<Sled>
                    {
                        VehicleType = "sled",
                        PrefabPaths = new[] { "assets/prefabs/misc/xmas/sled/sled.deployed.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.Sled,
                        TimeSinceLastUsed = sled => float.MaxValue,
                        VanillaDecayMethod = sled => sled.DecayOverTime,
                        Decay = (sled, vehicleInfo) => SledDecay(_pluginInstance, sled, vehicleInfo)
                    },
                    new VehicleInfo<Sled>
                    {
                        VehicleType = "sled.xmas",
                        PrefabPaths = new[] { "assets/prefabs/misc/xmas/sled/skins/sled.deployed.xmas.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.SledXmas,
                        TimeSinceLastUsed = sled => float.MaxValue,
                        VanillaDecayMethod = sled => sled.DecayOverTime,
                        Decay = (sled, vehicleInfo) => SledDecay(_pluginInstance, sled, vehicleInfo)
                    },
                    new VehicleInfo<Snowmobile>
                    {
                        VehicleType = "snowmobile",
                        PrefabPaths = new[] { "assets/content/vehicles/snowmobiles/snowmobile.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.Snowmobile,
                        TimeSinceLastUsed = snowmobile => snowmobile.timeSinceLastUsed,
                        VanillaDecayMethod = snowmobile => snowmobile.SnowmobileDecay,
                        Decay = (snowmobile, vehicleInfo) => SnowmobileDecay(_pluginInstance, snowmobile, vehicleInfo),
                    },
                    new VehicleInfo<BaseSubmarine>
                    {
                        VehicleType = "solosubmarine",
                        PrefabPaths = new[] { "assets/content/vehicles/submarine/submarinesolo.entity.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.SoloSubmarine,
                        TimeSinceLastUsed = submarine => submarine.timeSinceLastUsed,
                        VanillaDecayMethod = submarine => submarine.SubmarineDecay,
                        Decay = (submarine, vehicleInfo) => WaterVehicleDecay(
                            _pluginInstance,
                            submarine,
                            vehicleInfo,
                            BaseSubmarine.outsidedecayminutes,
                            BaseSubmarine.deepwaterdecayminutes
                        ),
                    },
                    new VehicleInfo<Snowmobile>
                    {
                        VehicleType = "tomaha",
                        PrefabPaths = new[] { "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.Tomaha,
                        TimeSinceLastUsed = tomaha => tomaha.timeSinceLastUsed,
                        VanillaDecayMethod = tomaha => tomaha.SnowmobileDecay,
                        Decay = (tomaha, vehicleInfo) => SnowmobileDecay(_pluginInstance, tomaha, vehicleInfo),
                    },
                    new VehicleInfo<Tugboat>
                    {
                        VehicleType = "tugboat",
                        PrefabPaths = new[] { "assets/content/vehicles/boats/tugboat/tugboat.prefab" },
                        VehicleConfig = pluginConfig.Vehicles.Tugboat,
                        TimeSinceLastUsed = tugboat => tugboat.timeSinceLastUsedFuel,
                        VanillaDecayMethod = tugboat => tugboat.BoatDecay,
                        Decay = (tugboat, vehicleInfo) =>
                        {
                            if (tugboat.IsDying)
                                return;

                            WaterVehicleDecay(
                                _pluginInstance,
                                tugboat,
                                vehicleInfo,
                                Tugboat.tugdecayminutes,
                                Tugboat.tugdecayminutes,
                                Tugboat.tugdecaystartdelayminutes
                            );
                        },
                    },
                };

                foreach (var vehicleInfo in allVehicles)
                {
                    vehicleInfo.OnServerInitialized(_pluginInstance);

                    foreach (var prefabId in vehicleInfo.PrefabIds)
                    {
                        _prefabIdToVehicleInfo[prefabId] = vehicleInfo;
                    }
                }
            }

            public IVehicleInfo GetVehicleInfo(BaseEntity entity)
            {
                return _prefabIdToVehicleInfo.TryGetValue(entity.prefabID, out var vehicleInfo) && vehicleInfo.IsCorrectType(entity)
                    ? vehicleInfo
                    : null;
            }
        }

        #endregion

        #region Configuration

        private class VehicleConfig
        {
            [JsonProperty("Allow the plugin to influence decay")]
            public bool Enabled = true;

            [JsonProperty("Decay multiplier while inside")]
            public float DecayMultiplierInside = 1;

            [JsonProperty("DecayMultiplierInside")]
            private float DeprecatedDecayMultiplierInside { set => DecayMultiplierInside = value; }

            [JsonProperty("Decay multiplier near tool cupboard")]
            public float DecayMultiplierNearTC = 1;

            [JsonProperty("DecayMultiplierNearTC")]
            private float DeprecatedDecayMultiplierNearTC { set => DecayMultiplierNearTC = value; }

            [JsonProperty("Protect from decay after recent use (minutes)", DefaultValueHandling = DefaultValueHandling.Ignore)]
            [DefaultValue(-1f)]
            public float ProtectionMinutesAfterUse = 10;

            [JsonProperty("ProtectionMinutesAfterUse")]
            private float DeprecatedProtectionMinutesAfterUse { set => ProtectionMinutesAfterUse = value; }

            [JsonProperty("Decay interval (seconds)")]
            public float DecayIntervalSeconds = 60;

            [JsonProperty("DecayIntervalSeconds")]
            private float DeprecatedDecayIntervalSeconds { set => DecayIntervalSeconds = value; }
        }

        private class VehicleConfigMap
        {
            [JsonProperty("Ridable Horse")]
            public VehicleConfig RidableHorse = new()
            {
                DecayMultiplierInside = 2,
                ProtectionMinutesAfterUse = 10,
            };

            [JsonProperty("RidableHorse")]
            private VehicleConfig DeprecatedRidableHorse { set => RidableHorse = value; }

            [JsonProperty("Ballista")]
            public VehicleConfig Ballista = new()
            {
                DecayMultiplierInside = 0.1f,
                ProtectionMinutesAfterUse = 5,
            };

            [JsonProperty("Battering Ram")]
            public VehicleConfig BatteringRam = new()
            {
                DecayMultiplierInside = 0.1f,
                ProtectionMinutesAfterUse = 5,
            };

            [JsonProperty("Catapult")]
            public VehicleConfig Catapult = new()
            {
                DecayMultiplierInside = 0.1f,
                ProtectionMinutesAfterUse = 5,
            };

            [JsonProperty("Siege Tower")]
            public VehicleConfig SiegeTower = new()
            {
                DecayMultiplierInside = 0.1f,
                ProtectionMinutesAfterUse = 5,
            };

            [JsonProperty("Modular Car")]
            public VehicleConfig ModularCar = new()
            {
                DecayMultiplierInside = 0.1f,
                ProtectionMinutesAfterUse = 10,
            };

            [JsonProperty("ModularCar")]
            private VehicleConfig DeprecatedModularCar { set => ModularCar = value; }

            [JsonProperty("Motor Bike")]
            public VehicleConfig MotorBike = new()
            {
                DecayMultiplierInside = 0f,
                ProtectionMinutesAfterUse = 45,
            };

            [JsonProperty("Motor Bike Sidecar")]
            public VehicleConfig MotorBikeSidecar = new()
            {
                DecayMultiplierInside = 0f,
                ProtectionMinutesAfterUse = 45,
            };

            [JsonProperty("Pedal Bike")]
            public VehicleConfig PedalBike = new()
            {
                DecayMultiplierInside = 0f,
                ProtectionMinutesAfterUse = 45,
            };

            [JsonProperty("Pedal Trike")]
            public VehicleConfig PedalTrike = new()
            {
                DecayMultiplierInside = 0f,
                ProtectionMinutesAfterUse = 45,
            };

            [JsonProperty("Snowmobile")]
            public VehicleConfig Snowmobile = new()
            {
                DecayMultiplierInside = 0f,
                ProtectionMinutesAfterUse = 45,
            };

            [JsonProperty("Tomaha")]
            public VehicleConfig Tomaha = new()
            {
                DecayMultiplierInside = 0f,
                ProtectionMinutesAfterUse = 45,
            };

            [JsonProperty("Sled")]
            public VehicleConfig Sled = new()
            {
                DecayMultiplierInside = 1f,
                ProtectionMinutesAfterUse = -1,
            };

            [JsonProperty("Sled Xmas")]
            public VehicleConfig SledXmas = new()
            {
                DecayMultiplierInside = 1f,
                ProtectionMinutesAfterUse = -1,
            };

            [JsonProperty("Sled.Xmas")]
            private VehicleConfig DeprecatedSledXmas { set => SledXmas = value; }

            [JsonProperty("Kayak")]
            public VehicleConfig Kayak = new()
            {
                DecayMultiplierInside = 0f,
                ProtectionMinutesAfterUse = -1,
            };

            [JsonProperty("Rowboat")]
            public VehicleConfig Rowboat = new()
            {
                DecayMultiplierInside = 0f,
                ProtectionMinutesAfterUse = -1,
            };

            [JsonProperty("RHIB")]
            public VehicleConfig RHIB = new()
            {
                DecayMultiplierInside = 0f,
                ProtectionMinutesAfterUse = -1,
            };

            [JsonProperty("Tugboat")]
            public VehicleConfig Tugboat = new()
            {
                ProtectionMinutesAfterUse = -1,
            };

            [JsonProperty("Solo Submarine")]
            public VehicleConfig SoloSubmarine = new()
            {
                DecayMultiplierInside = 0f,
                ProtectionMinutesAfterUse = -1,
            };

            [JsonProperty("SoloSubmarine")]
            private VehicleConfig DeprecatedSoloSubmarine { set => SoloSubmarine = value; }

            [JsonProperty("Duo Submarine")]
            public VehicleConfig DuoSubmarine = new()
            {
                DecayMultiplierInside = 0f,
                ProtectionMinutesAfterUse = -1,
            };

            [JsonProperty("DuoSubmarine")]
            private VehicleConfig DeprecatedDuoSubmarine { set => DuoSubmarine = value; }

            [JsonProperty("Hot Air Balloon")]
            public VehicleConfig HotAirBalloon = new()
            {
                DecayMultiplierInside = 0f,
                ProtectionMinutesAfterUse = 10,
            };

            [JsonProperty("HotAirBalloon")]
            private VehicleConfig DeprecatedHotAirBalloon { set => HotAirBalloon = value; }

            [JsonProperty("Minicopter")]
            public VehicleConfig Minicopter = new()
            {
                DecayMultiplierInside = 1f,
                ProtectionMinutesAfterUse = 10,
            };

            [JsonProperty("Scrap Transport Helicopter")]
            public VehicleConfig ScrapTransportHelicopter = new()
            {
                DecayMultiplierInside = 1f,
                ProtectionMinutesAfterUse = 10,
            };

            [JsonProperty("ScrapTransportHelicopter")]
            private VehicleConfig DeprecatedScrapTransportHelicopter { set => ScrapTransportHelicopter = value; }

            [JsonProperty("Attack Helicopter")]
            public VehicleConfig AttackHelicopter = new()
            {
                DecayMultiplierInside = 1f,
                ProtectionMinutesAfterUse = 10,
            };
        }

        private class Configuration : BaseConfiguration
        {
            [JsonProperty("Enable permission")]
            public bool EnablePermission = true;

            [JsonProperty("EnablePermission")]
            private bool DeprecatedEnablePermission { set => EnablePermission = value; }

            [JsonProperty("Vehicles")]
            public VehicleConfigMap Vehicles = new();
        }

        private Configuration GetDefaultConfig() => new();

        #endregion

        #region Configuration Helpers

        private class BaseConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(BaseConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            var changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                if (currentRaw.TryGetValue(key, out var currentRawValue))
                {
                    var currentDictValue = currentRawValue as Dictionary<string, object>;
                    if (currentWithDefaults[key] is Dictionary<string, object> defaultDictValue)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                        {
                            changed = true;
                        }
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _pluginConfig = Config.ReadObject<Configuration>();
                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_pluginConfig))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_pluginConfig, true);
        }

        #endregion
    }
}
