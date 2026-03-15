using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Development.Talents;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Loadouts;
using BepInEx.Logging;

namespace GearSetsMod.Core
{
    /// <summary>
    /// Captures the hero's current gear, talents, and RPG stats into a <see cref="GearSet"/>.
    /// </summary>
    public static class GearSetCapture
    {
        private static readonly BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("GearSetsMod.Capture");

        private static readonly string[] AttributePrefixes =
            { "Dexterity", "Endurance", "Perception", "Practicality", "Spirituality", "Strength", "KingPower", "Wyrdskill" };

        public static GearSet CaptureCurrentState(string name)
        {
            var hero = Hero.Current;
            if (hero == null) return null;

            var heroItems = hero.HeroItems;
            if (heroItems == null) return null;

            var set = new GearSet
            {
                Name = name,
                LoadoutIndex = heroItems.CurrentLoadoutIndex,
                CreatedAt = DateTime.Now
            };

            CaptureLoadoutWeapons(heroItems, set);
            CaptureArmorAndAccessories(heroItems, set);
            CaptureTalents(hero, set);
            CaptureRpgStats(hero, set);

            return set;
        }

        private static void CaptureLoadoutWeapons(HeroItems heroItems, GearSet set)
        {
            for (int loadoutIdx = 0; loadoutIdx < 4; loadoutIdx++)
            {
                try
                {
                    var loadout = heroItems.LoadoutAt(loadoutIdx);
                    if (loadout == null) continue;

                    var cacheField = typeof(HeroLoadout).GetField("_cache", NonPublicInstance);
                    var cache = cacheField?.GetValue(loadout) as System.Collections.IList;
                    if (cache == null) continue;

                    foreach (var entry in cache)
                    {
                        var slotField = entry.GetType().GetField("slot");
                        var itemField = entry.GetType().GetField("item");
                        var slot = slotField?.GetValue(entry);
                        var item = itemField?.GetValue(entry) as Item;
                        if (slot == null || item == null) continue;

                        string guid = item.Template?.GUID;
                        if (string.IsNullOrEmpty(guid)) continue;

                        string slotName = SlotHelpers.GetRichEnumName(slot);
                        string key = $"Loadout{loadoutIdx}_{slotName}";
                        set.SlotToItemGuid[key] = guid;
                    }

                    foreach (var loadoutSlotType in EquipmentSlotType.Loadouts)
                    {
                        string slotName = SlotHelpers.GetRichEnumName(loadoutSlotType);
                        string key = $"Loadout{loadoutIdx}_{slotName}";
                        if (!set.SlotToItemGuid.ContainsKey(key))
                            set.SlotToItemGuid[key] = "";
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[CaptureLoadoutWeapons] Loadout {loadoutIdx} failed: {ex.Message}");
                }
            }
        }

        private static void CaptureArmorAndAccessories(HeroItems heroItems, GearSet set)
        {
            var armorSlotTypes = new[]
            {
                EquipmentSlotType.Helmet, EquipmentSlotType.Cuirass, EquipmentSlotType.Gauntlets,
                EquipmentSlotType.Greaves, EquipmentSlotType.Boots, EquipmentSlotType.Back,
                EquipmentSlotType.Amulet, EquipmentSlotType.Ring1, EquipmentSlotType.Ring2,
                EquipmentSlotType.HorseArmor,
                EquipmentSlotType.FoodQuickSlot, EquipmentSlotType.QuickSlot2, EquipmentSlotType.QuickSlot3
            };

            foreach (var slotType in armorSlotTypes)
            {
                try
                {
                    string slotName = SlotHelpers.GetRichEnumName(slotType);
                    var item = CharacterInventoryExtension.EquippedItem(heroItems, slotType);
                    if (item == null)
                    {
                        set.SlotToItemGuid[slotName] = "";
                        continue;
                    }

                    string guid = item.Template?.GUID;
                    if (string.IsNullOrEmpty(guid))
                    {
                        set.SlotToItemGuid[slotName] = "";
                        continue;
                    }

                    set.SlotToItemGuid[slotName] = guid;
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[CaptureArmorAndAccessories] Slot {slotType} failed: {ex.Message}");
                }
            }
        }

        private static void CaptureTalents(Hero hero, GearSet set)
        {
            try
            {
                var heroTalents = hero.Talents;
                if (heroTalents == null) return;

                foreach (TalentTable table in heroTalents.Elements<TalentTable>())
                {
                    string treeName = table.TreeTemplate?.name ?? "Unknown";
                    var subtrees = new Dictionary<string, int>();

                    foreach (Talent talent in table.talents)
                    {
                        string subtreeName = SlotHelpers.GetRichEnumName(talent.TalentTreeBranchType);

                        foreach (var prefix in AttributePrefixes)
                        {
                            if (subtreeName.StartsWith(prefix))
                            {
                                subtreeName = subtreeName.Substring(prefix.Length);
                                break;
                            }
                        }
                        subtreeName = SlotHelpers.PrettySlotName(subtreeName);

                        int level = talent.Level;

                        if (level > 0)
                        {
                            if (subtrees.ContainsKey(subtreeName))
                                subtrees[subtreeName] += level;
                            else
                                subtrees[subtreeName] = level;

                            string talentName = talent.Template?.name ?? "";
                            if (!string.IsNullOrEmpty(talentName))
                                set.TalentLevels[talentName] = level;
                        }
                    }

                    if (subtrees.Count > 0)
                        set.TalentTrees[treeName] = subtrees;
                }

                Log.LogDebug($"[CaptureTalents] Captured {set.TalentLevels.Count} talent levels across {set.TalentTrees.Count} trees");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[CaptureTalents] Failed: {ex.Message}");
            }
        }

        private static void CaptureRpgStats(Hero hero, GearSet set)
        {
            try
            {
                var rpgStats = hero.HeroRPGStats;
                if (rpgStats == null)
                {
                    Log.LogWarning("[CaptureRpgStats] hero.HeroRPGStats is null");
                    return;
                }

                // Capture hero level for point budget tracking
                try
                {
                    set.HeroLevel = (int)hero.CharacterStats.Level.BaseValue;
                    Log.LogDebug($"[CaptureRpgStats] Hero level: {set.HeroLevel}");
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[CaptureRpgStats] Could not read hero level: {ex.Message}");
                }

                var statNames = new[] { "Strength", "Dexterity", "Perception", "Endurance", "Practicality", "Spirituality" };
                var rpgType = rpgStats.GetType();

                foreach (var name in statNames)
                {
                    try
                    {
                        var prop = rpgType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                        if (prop == null)
                        {
                            Log.LogWarning($"[CaptureRpgStats] Property '{name}' not found on {rpgType.Name}");
                            continue;
                        }
                        var stat = prop.GetValue(rpgStats);
                        if (stat == null)
                        {
                            Log.LogWarning($"[CaptureRpgStats] Stat object for '{name}' is null");
                            continue;
                        }

                        var statType = stat.GetType();

                        // Read BaseValue first (raw attribute points, not including gear/talent modifiers)
                        // v1 bug: read Modified first, which included gear bonuses and drifted on every load
                        foreach (var valProp in new[] { "BaseValue", "Value", "Modified" })
                        {
                            var vp = statType.GetProperty(valProp, BindingFlags.Instance | BindingFlags.Public);
                            if (vp != null)
                            {
                                var val = vp.GetValue(stat);
                                if (val is float f) { set.RpgStats[name] = f; Log.LogDebug($"[CaptureRpgStats] {name} = {f} (via {valProp})"); break; }
                                if (val is int i) { set.RpgStats[name] = i; Log.LogDebug($"[CaptureRpgStats] {name} = {i} (via {valProp})"); break; }
                                if (val is double d) { set.RpgStats[name] = (float)d; Log.LogDebug($"[CaptureRpgStats] {name} = {d} (via {valProp})"); break; }
                                Log.LogDebug($"[CaptureRpgStats] {name}.{valProp} type={val?.GetType().Name}, value={val}");
                            }
                        }

                        if (!set.RpgStats.ContainsKey(name))
                            Log.LogWarning($"[CaptureRpgStats] Could not read value for '{name}'. Available props: {string.Join(", ", Array.ConvertAll(statType.GetProperties(BindingFlags.Instance | BindingFlags.Public), p => p.Name + ":" + p.PropertyType.Name))}");
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"[CaptureRpgStats] Failed for '{name}': {ex.Message}");
                    }
                }

                set.Version = 2;
                Log.LogDebug($"[CaptureRpgStats] Captured {set.RpgStats.Count} RPG stats (BaseValue): {string.Join(", ", set.RpgStats.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[CaptureRpgStats] Failed: {ex.Message}");
            }
        }
    }
}
