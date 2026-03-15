using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Development.Talents;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Loadouts;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Heroes.Storage;
using BepInEx.Logging;

namespace GearSetsMod.Core
{
    /// <summary>
    /// Applies a saved <see cref="GearSet"/> to the current hero — gear, talents, RPG stats,
    /// and stash pull/return logic.
    /// </summary>
    public static class GearSetApply
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("GearSetsMod.Apply");

        /// <summary>
        /// Tracks item GUIDs that were pulled from the stash during the last set load.
        /// When loading a new set, items in this list that aren't needed by the new set
        /// are returned to the stash.
        /// </summary>
        private static List<string> _lastStashPulledGuids = new List<string>();

        public static string ApplySet(GearSet set)
        {
            var hero = Hero.Current;
            if (hero == null) return "No hero available.";

            var heroItems = hero.HeroItems;
            if (heroItems == null) return "No hero inventory available.";

            int stashPulledCount = 0;
            int missingCount = 0;
            var missingItems = new List<string>();
            HeroStorage storage = null;
            bool storageRequested = false;

            // Collect the set of GUIDs needed by the new set
            var newSetGuids = new HashSet<string>();
            foreach (var kvp in set.SlotToItemGuid)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                    newSetGuids.Add(kvp.Value);
            }

            // Return previously stash-pulled items that aren't needed by the new set
            int stashReturnedCount = ReturnStashItems(hero, heroItems, newSetGuids, ref storage, ref storageRequested);

            // Equip all slots from the set
            var newPulledGuids = new List<string>();
            try
            {
                EquipAllSlots(set, heroItems, hero, ref storage, ref storageRequested,
                    ref stashPulledCount, ref missingCount, missingItems, newPulledGuids);
            }
            finally
            {
                ReleaseStorage(storage, storageRequested);
            }

            ApplyTalents(hero, set);
            ApplyRpgStats(hero, set);
            RecalculateStats(hero);

            _lastStashPulledGuids = newPulledGuids;

            return BuildStatusMessage(set, hero, stashReturnedCount, stashPulledCount, missingCount);
        }

        /// <summary>
        /// Returns previously stash-pulled items that aren't needed by the new set.
        /// </summary>
        private static int ReturnStashItems(
            Hero hero, HeroItems heroItems, HashSet<string> newSetGuids,
            ref HeroStorage storage, ref bool storageRequested)
        {
            int returned = 0;
            if (_lastStashPulledGuids.Count == 0)
                return returned;

            try
            {
                storage = hero.Element<HeroStorage>();
                if (storage != null)
                {
                    storage.RequestItems();
                    storageRequested = true;

                    foreach (var guid in _lastStashPulledGuids)
                    {
                        if (newSetGuids.Contains(guid))
                        {
                            Log.LogDebug($"[ReturnStash] Keeping stash-pulled item {guid} — needed by new set");
                            continue;
                        }

                        var itemToReturn = heroItems.Items.FirstOrDefault(i => i.Template?.GUID == guid);
                        if (itemToReturn != null)
                        {
                            try
                            {
                                var result = itemToReturn.MoveTo(storage);
                                if (result != null)
                                {
                                    returned++;
                                    Log.LogDebug($"[ReturnStash] Returned '{itemToReturn.DisplayName}' to stash");
                                }
                                else
                                {
                                    Log.LogWarning($"[ReturnStash] MoveTo(stash) returned null for '{itemToReturn.DisplayName}'");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.LogWarning($"[ReturnStash] Failed to return item {guid}: {ex.Message}");
                            }
                        }
                        else
                        {
                            Log.LogDebug($"[ReturnStash] Previously pulled item {guid} no longer in inventory (sold/consumed?)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[ReturnStash] Failed: {ex.Message}");
            }

            _lastStashPulledGuids.Clear();
            return returned;
        }

        /// <summary>
        /// Iterates over every slot in the gear set and equips the corresponding item.
        /// </summary>
        private static void EquipAllSlots(
            GearSet set, HeroItems heroItems, Hero hero,
            ref HeroStorage storage, ref bool storageRequested,
            ref int stashPulledCount, ref int missingCount,
            List<string> missingItems, List<string> newPulledGuids)
        {
            foreach (var kvp in set.SlotToItemGuid)
            {
                try
                {
                    if (kvp.Key.StartsWith("Loadout") && kvp.Key.Contains("_"))
                        EquipLoadoutSlot(kvp, heroItems, hero, ref storage, ref storageRequested,
                            ref stashPulledCount, ref missingCount, missingItems, newPulledGuids);
                    else
                        EquipNonLoadoutSlot(kvp, heroItems, hero, ref storage, ref storageRequested,
                            ref stashPulledCount, ref missingCount, missingItems, newPulledGuids);
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[EquipAllSlots] Failed to equip {kvp.Key}: {ex.Message}");
                }
            }
        }

        private static void EquipLoadoutSlot(
            KeyValuePair<string, string> kvp, HeroItems heroItems, Hero hero,
            ref HeroStorage storage, ref bool storageRequested,
            ref int stashPulledCount, ref int missingCount,
            List<string> missingItems, List<string> newPulledGuids)
        {
            var underscoreIdx = kvp.Key.IndexOf('_');
            var idxStr = kvp.Key.Substring(7, underscoreIdx - 7);
            var slotName = kvp.Key.Substring(underscoreIdx + 1);
            if (!int.TryParse(idxStr, out int loadoutIdx)) return;

            var slotType = SlotHelpers.FindSlotByName(slotName);
            if (slotType == null) return;

            var loadout = heroItems.LoadoutAt(loadoutIdx) as HeroLoadout;
            if (loadout == null) return;

            if (string.IsNullOrEmpty(kvp.Value))
            {
                loadout.EquipItem(slotType, null);
                return;
            }

            var item = FindItemInInventoryOrStash(heroItems, ref storage, ref storageRequested, hero, kvp.Value, ref stashPulledCount, newPulledGuids);
            if (item == null)
            {
                missingCount++;
                missingItems.Add(kvp.Key);
                return;
            }

            loadout.EquipItem(slotType, item);
        }

        private static void EquipNonLoadoutSlot(
            KeyValuePair<string, string> kvp, HeroItems heroItems, Hero hero,
            ref HeroStorage storage, ref bool storageRequested,
            ref int stashPulledCount, ref int missingCount,
            List<string> missingItems, List<string> newPulledGuids)
        {
            var slotType = SlotHelpers.FindSlotByName(kvp.Key);
            if (slotType == null) return;

            if (string.IsNullOrEmpty(kvp.Value))
            {
                CharacterInventoryExtension.Unequip(heroItems, slotType);
                return;
            }

            var item = FindItemInInventoryOrStash(heroItems, ref storage, ref storageRequested, hero, kvp.Value, ref stashPulledCount, newPulledGuids);
            if (item == null)
            {
                missingCount++;
                missingItems.Add(kvp.Key);
                return;
            }

            var currentLoadout = heroItems.CurrentLoadout as HeroLoadout;
            if (currentLoadout != null)
                currentLoadout.EquipItem(slotType, item);
        }

        private static void ReleaseStorage(HeroStorage storage, bool storageRequested)
        {
            if (!storageRequested || storage == null) return;
            try
            {
                storage.ReleaseItems();
                Log.LogDebug("[ApplySet] Released stash items");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[ApplySet] Failed to release stash items: {ex.Message}");
            }
        }

        private static string BuildStatusMessage(GearSet set, Hero hero, int stashReturnedCount, int stashPulledCount, int missingCount)
        {
            var parts = new List<string>();
            parts.Add("Loaded: " + set.Name);

            if (stashReturnedCount > 0)
                parts.Add($"{stashReturnedCount} item(s) returned to stash");
            if (stashPulledCount > 0)
                parts.Add($"{stashPulledCount} item(s) pulled from stash");
            if (missingCount > 0)
                parts.Add($"{missingCount} item(s) not found");

            if (set.HeroLevel > 0)
            {
                try
                {
                    int currentLevel = (int)hero.CharacterStats.Level.BaseValue;
                    if (currentLevel > set.HeroLevel)
                    {
                        int levelDiff = currentLevel - set.HeroLevel;
                        parts.Add($"{levelDiff} level(s) gained since save — extra points unspent");
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[BuildStatusMessage] Could not read level: {ex.Message}");
                }
            }

            if (set.Version < 2)
                parts.Add("(v1 set — re-save for accuracy)");

            return string.Join(". ", parts) + ".";
        }

        /// <summary>
        /// Search hero inventory first, then stash. If found in stash, move to inventory.
        /// </summary>
        private static Item FindItemInInventoryOrStash(
            HeroItems heroItems,
            ref HeroStorage storage,
            ref bool storageRequested,
            Hero hero,
            string itemGuid,
            ref int stashPulledCount,
            List<string> stashPulledGuids)
        {
            // First: search hero inventory
            var item = heroItems.Items.FirstOrDefault(i => i.Template?.GUID == itemGuid);
            if (item != null) return item;

            // Second: search stash
            try
            {
                if (storage == null)
                {
                    storage = hero.Element<HeroStorage>();
                    if (storage == null)
                    {
                        Log.LogDebug("[FindItem] HeroStorage not available");
                        return null;
                    }
                }

                if (!storageRequested)
                {
                    storage.RequestItems();
                    storageRequested = true;
                    Log.LogDebug("[FindItem] Materialized stash items for search");
                }

                var stashItem = storage.Items?.FirstOrDefault(i => i.Template?.GUID == itemGuid);
                if (stashItem == null) return null;

                Log.LogDebug($"[FindItem] Found item '{stashItem.DisplayName}' in stash, moving to inventory");
                var movedItem = stashItem.MoveTo(heroItems);
                if (movedItem != null)
                {
                    stashPulledCount++;
                    stashPulledGuids.Add(itemGuid);
                    Log.LogDebug($"[FindItem] Successfully moved '{movedItem.DisplayName}' from stash to inventory");
                    return movedItem;
                }
                else
                {
                    Log.LogWarning($"[FindItem] MoveTo returned null for item '{stashItem.DisplayName}' — inventory may be full");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[FindItem] Stash search failed for GUID {itemGuid}: {ex.Message}");
                return null;
            }
        }

        private static void ApplyTalents(Hero hero, GearSet set)
        {
            if (set.TalentLevels == null || set.TalentLevels.Count == 0)
            {
                Log.LogDebug("[ApplyTalents] No talent levels to restore");
                return;
            }

            var heroTalents = hero.Talents;
            if (heroTalents == null)
            {
                Log.LogWarning("[ApplyTalents] hero.Talents is null");
                return;
            }

            Log.LogDebug($"[ApplyTalents] Restoring {set.TalentLevels.Count} talent levels");

            try
            {
                // Phase 1: Reset all talents
                int resetCount = 0;
                foreach (TalentTable table in heroTalents.Elements<TalentTable>())
                {
                    foreach (Talent talent in table.talents)
                    {
                        if (talent.Level > 0)
                        {
                            talent.Reset(withRefund: true);
                            resetCount++;
                        }
                    }
                }
                Log.LogDebug($"[ApplyTalents] Reset {resetCount} talents");

                // Phase 2: Apply saved levels (ordered by tree level requirement)
                int appliedCount = 0;
                int failedCount = 0;
                foreach (TalentTable table in heroTalents.Elements<TalentTable>())
                {
                    var toApply = new List<(Talent talent, string name, int target)>();
                    foreach (Talent talent in table.talents)
                    {
                        string talentName = talent.Template?.name ?? "";
                        if (string.IsNullOrEmpty(talentName)) continue;
                        if (!set.TalentLevels.TryGetValue(talentName, out int targetLevel)) continue;
                        if (targetLevel <= 0) continue;
                        toApply.Add((talent, talentName, targetLevel));
                    }

                    toApply.Sort((a, b) => a.talent.RequiredTreeLevelToUnlock.CompareTo(b.talent.RequiredTreeLevelToUnlock));
                    Log.LogDebug($"[ApplyTalents] Tree '{table.TreeTemplate?.name}': {toApply.Count} talents to restore");

                    foreach (var (talent, talentName, targetLevel) in toApply)
                    {
                        bool anyFailed = false;
                        for (int i = 0; i < targetLevel; i++)
                        {
                            bool acquired = talent.AcquireNextTemporaryLevel();
                            if (!acquired)
                            {
                                Log.LogWarning($"[ApplyTalents] FAILED level {i+1}/{targetLevel} for '{talentName}' (EstLevel={talent.EstimatedLevel}, TreeLevel={table.CurrentTreeLevel}, Required={talent.RequiredTreeLevelToUnlock})");
                                anyFailed = true;
                                failedCount++;
                                break;
                            }
                        }
                        talent.ApplyTemporaryLevels();
                        if (!anyFailed)
                        {
                            appliedCount++;
                            Log.LogDebug($"[ApplyTalents] Applied {targetLevel} levels to '{talentName}'");
                        }
                    }
                }

                Log.LogDebug($"[ApplyTalents] Complete: {appliedCount} talents applied, {failedCount} failed");
            }
            catch (Exception ex)
            {
                Log.LogError($"[ApplyTalents] Exception: {ex}");
            }
        }

        private static void ApplyRpgStats(Hero hero, GearSet set)
        {
            if (set.RpgStats == null || set.RpgStats.Count == 0)
            {
                Log.LogDebug("[ApplyRpgStats] No RPG stats to restore");
                return;
            }

            try
            {
                var rpgStats = hero.HeroRPGStats;
                if (rpgStats == null)
                {
                    Log.LogWarning("[ApplyRpgStats] hero.HeroRPGStats is null");
                    return;
                }

                var rpgType = rpgStats.GetType();
                bool isV1 = set.Version < 2;
                Log.LogDebug($"[ApplyRpgStats] Restoring {set.RpgStats.Count} RPG stats (v{set.Version}). HeroRPGStats type: {rpgType.Name}");

                foreach (var kvp in set.RpgStats)
                {
                    try
                    {
                        var prop = rpgType.GetProperty(kvp.Key, BindingFlags.Instance | BindingFlags.Public);
                        if (prop == null)
                        {
                            Log.LogWarning($"[ApplyRpgStats] Property '{kvp.Key}' not found on {rpgType.Name}");
                            continue;
                        }
                        var statObj = prop.GetValue(rpgStats);
                        if (statObj == null)
                        {
                            Log.LogWarning($"[ApplyRpgStats] Stat object for '{kvp.Key}' is null");
                            continue;
                        }

                        float targetBaseValue = isV1
                            ? MigrateV1StatValue(kvp.Key, kvp.Value, statObj)
                            : kvp.Value;

                        ApplySingleStat(kvp.Key, targetBaseValue, statObj);
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"[ApplyRpgStats] Failed for {kvp.Key}: {ex.Message}");
                    }
                }

                if (isV1)
                    Log.LogInfo("[ApplyRpgStats] v1 set loaded with approximate migration. Re-save the set for accurate BaseValue storage.");

                Log.LogDebug("[ApplyRpgStats] RPG stat restoration complete");
            }
            catch (Exception ex)
            {
                Log.LogError($"[ApplyRpgStats] Failed: {ex}");
            }
        }

        /// <summary>
        /// v1 migration: saved value was Modified (includes gear/talent bonuses).
        /// Approximate BaseValue by subtracting the modifier delta.
        /// </summary>
        private static float MigrateV1StatValue(string statName, float savedModified, object statObj)
        {
            try
            {
                var statType = statObj.GetType();
                float currentBase = 0f, currentMod = 0f;
                var baseProp = statType.GetProperty("BaseValue", BindingFlags.Instance | BindingFlags.Public);
                var modProp = statType.GetProperty("Modified", BindingFlags.Instance | BindingFlags.Public)
                           ?? statType.GetProperty("ModifiedValue", BindingFlags.Instance | BindingFlags.Public);

                if (baseProp != null)
                {
                    var bv = baseProp.GetValue(statObj);
                    if (bv is float bf) currentBase = bf;
                    else if (bv is int bi) currentBase = bi;
                }
                if (modProp != null)
                {
                    var mv = modProp.GetValue(statObj);
                    if (mv is float mf) currentMod = mf;
                    else if (mv is int mi) currentMod = mi;
                }

                float modifiers = currentMod - currentBase;
                float approxBase = savedModified - modifiers;
                if (approxBase < 0) approxBase = 0;

                Log.LogDebug($"[MigrateV1] {statName}: savedModified={savedModified}, currentBase={currentBase}, currentMod={currentMod}, modifiers={modifiers}, approxBase={approxBase}");
                return approxBase;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[MigrateV1] Failed for {statName}, using raw value: {ex.Message}");
                return savedModified;
            }
        }

        /// <summary>
        /// Sets a single RPG stat using Stat.SetTo() or reflection fallback.
        /// </summary>
        private static void ApplySingleStat(string statName, float targetBaseValue, object statObj)
        {
            if (statObj is Stat stat)
            {
                float oldBase = stat.BaseValue;
                stat.SetTo(targetBaseValue, false, null);
                Log.LogDebug($"[ApplyRpgStats] {statName}: SetTo({targetBaseValue}) [was {oldBase}]");
            }
            else
            {
                Log.LogWarning($"[ApplyRpgStats] {statName}: stat object is not a Stat (type={statObj.GetType().Name}), falling back to reflection");
                var setToMethod = statObj.GetType().GetMethod("SetTo", new[] { typeof(float), typeof(bool), typeof(object) });
                if (setToMethod != null)
                {
                    setToMethod.Invoke(statObj, new object[] { targetBaseValue, false, null });
                    Log.LogDebug($"[ApplyRpgStats] {statName}: SetTo({targetBaseValue}) via reflection");
                }
                else
                {
                    Log.LogWarning($"[ApplyRpgStats] {statName}: SetTo method not found. Available methods: {string.Join(", ", statObj.GetType().GetMethods().Select(m => m.Name))}");
                }
            }
        }

        private static void RecalculateStats(Hero hero)
        {
            try
            {
                Log.LogDebug("[RecalculateStats] Starting stat recalculation");

                // IMPORTANT: Do NOT call HeroRPGStats.RecalculateAllStats() here.
                // As discovered by KitsuneRhin, it resets stats from an internal wrapper,
                // undoing any changes made by SetTo() in ApplyRpgStats.

                try
                {
                    hero.HeroStats.RecalculateAllStats(false);
                    Log.LogDebug("[RecalculateStats] HeroStats.RecalculateAllStats done");
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[RecalculateStats] HeroStats.RecalculateAllStats failed: {ex.Message}");
                }

                try
                {
                    int level = (int)hero.CharacterStats.Level.BaseValue;
                    hero.CharacterStats.RecalculateAllStats(level, level, false);
                    Log.LogDebug($"[RecalculateStats] CharacterStats.RecalculateAllStats done (level={level})");
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[RecalculateStats] CharacterStats.RecalculateAllStats failed: {ex.Message}");
                }

                RecalculateMultStats(hero);

                Log.LogDebug("[RecalculateStats] All stat recalculation complete");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[RecalculateStats] Failed: {ex.Message}");
            }
        }

        private static void RecalculateMultStats(Hero hero)
        {
            try
            {
                var multProp = hero.GetType().GetProperty("HeroMultStats", BindingFlags.Instance | BindingFlags.Public);
                if (multProp == null) return;

                var multStats = multProp.GetValue(hero);
                if (multStats == null) return;

                var recalcMethod = multStats.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "RecalculateAllStats");
                if (recalcMethod == null) return;

                var parms = recalcMethod.GetParameters();
                if (parms.Length == 1 && parms[0].ParameterType == typeof(bool))
                    recalcMethod.Invoke(multStats, new object[] { false });
                else if (parms.Length == 0)
                    recalcMethod.Invoke(multStats, null);

                Log.LogDebug("[RecalculateStats] HeroMultStats.RecalculateAllStats done");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[RecalculateStats] HeroMultStats recalc failed: {ex.Message}");
            }
        }
    }
}
