using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.CharacterSheet.Tabs;
using Awaken.TG.Main.Heroes.Development.Talents;
using Awaken.TG.Main.Heroes.Stats;
using BepInEx.Logging;
using GearSetsMod.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GearSetsMod.UI
{
    public class GearSetsUI : CharacterSheetTab<VGearSetsUI>
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("GearSetsMod");

        private VGearSetsUI _view;
        private GearSet _selectedSet;
        private List<GearSet> _allSets = new List<GearSet>();

        private static readonly HashSet<string> ArmorSlotNames = new HashSet<string>
        {
            "Helmet", "Cuirass", "Gauntlets", "Greaves", "Boots", "Back", "HorseArmor"
        };

        private static readonly HashSet<string> AccessorySlotNames = new HashSet<string>
        {
            "Amulet", "Ring1", "Ring2"
        };

        private static readonly HashSet<string> QuickSlotNames = new HashSet<string>
        {
            "FoodQuickSlot", "QuickSlot2", "QuickSlot3"
        };

        protected override void AfterViewSpawned(VGearSetsUI view)
        {
            _view = view;
            _selectedSet = null;

            view.saveBtn.onClick.AddListener(OnSaveClicked);
            view.updateBtn.onClick.AddListener(OnUpdateClicked);
            view.loadBtn.onClick.AddListener(OnLoadClicked);
            view.deleteBtn.onClick.AddListener(OnDeleteClicked);
            view.resetBtn.onClick.AddListener(OnResetBuildClicked);
            view.fixPointsBtn.onClick.AddListener(OnFixPointsClicked);

            RefreshSetList();
            UpdateDetailPanel();
        }

        private void RefreshSetList()
        {
            if (_view == null || _view.setListContent == null) return;

            var children = new List<GameObject>();
            foreach (Transform child in _view.setListContent)
                children.Add(child.gameObject);
            foreach (var child in children)
                UnityEngine.Object.DestroyImmediate(child);

            try
            {
                _allSets = SetManager.GetAllSets();
            }
            catch (Exception ex)
            {
                Log.LogError($"[RefreshSetList] GetAllSets failed: {ex}");
                ShowStatus("Error loading sets: " + ex.Message);
                _allSets = new List<GearSet>();
            }

            foreach (var set in _allSets)
            {
                var btn = _view.CreateSetListEntry(set.Name);
                var captured = set;
                btn.onClick.AddListener(() => OnSetSelected(captured));
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_view.setListContent.GetComponent<RectTransform>());

            if (_selectedSet != null)
            {
                _selectedSet = _allSets.FirstOrDefault(s => s.Name == _selectedSet.Name);
                _view.SetSelectedEntryHighlight(_selectedSet?.Name ?? "");
            }
        }

        private void OnSetSelected(GearSet set)
        {
            _selectedSet = set;
            _view.SetSelectedEntryHighlight(set.Name);
            UpdateDetailPanel();
        }

        private void UpdateDetailPanel()
        {
            if (_view == null) return;

            if (_selectedSet == null)
            {
                _view.detailTitle.text = "Select a set";
                _view.detailTimestamp.text = "";
                _view.ClearDetailContent();
                _view.AddSectionHeader("Save your current equipment to create a gear set,\nor select an existing set from the list.", _view.detailLeftCol);
                RebuildDetailLayout();
                return;
            }

            _view.detailTitle.text = _selectedSet.Name;
            _view.detailTimestamp.text = "Created: " + _selectedSet.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            _view.ClearDetailContent();

            if (_selectedSet.SlotToItemGuid.Count == 0
                && (_selectedSet.TalentTrees == null || _selectedSet.TalentTrees.Count == 0)
                && (_selectedSet.RpgStats == null || _selectedSet.RpgStats.Count == 0))
            {
                _view.AddSectionHeader("(empty set)", _view.detailLeftCol);
                RebuildDetailLayout();
                return;
            }

            CategorizeAndRenderSlots();
            RenderStatAndTalentSections();
            RebuildDetailLayout();
        }

        private void CategorizeAndRenderSlots()
        {
            var loadoutGroups = new SortedDictionary<int, List<KeyValuePair<string, string>>>();
            var armorEntries = new List<KeyValuePair<string, string>>();
            var accessoryEntries = new List<KeyValuePair<string, string>>();
            var quickSlotEntries = new List<KeyValuePair<string, string>>();

            foreach (var kvp in _selectedSet.SlotToItemGuid)
            {
                if (kvp.Key.StartsWith("Loadout") && kvp.Key.Contains("_"))
                {
                    var underscoreIdx = kvp.Key.IndexOf('_');
                    var idxStr = kvp.Key.Substring(7, underscoreIdx - 7);
                    var slotName = kvp.Key.Substring(underscoreIdx + 1);
                    if (int.TryParse(idxStr, out int loadoutIdx))
                    {
                        if (!loadoutGroups.ContainsKey(loadoutIdx))
                            loadoutGroups[loadoutIdx] = new List<KeyValuePair<string, string>>();
                        loadoutGroups[loadoutIdx].Add(new KeyValuePair<string, string>(slotName, kvp.Value));
                    }
                }
                else if (ArmorSlotNames.Contains(kvp.Key))
                    armorEntries.Add(kvp);
                else if (AccessorySlotNames.Contains(kvp.Key))
                    accessoryEntries.Add(kvp);
                else if (QuickSlotNames.Contains(kvp.Key))
                    quickSlotEntries.Add(kvp);
                else
                    armorEntries.Add(kvp);
            }

            var left = _view.detailLeftCol;
            var right = _view.detailRightCol;

            RenderLoadoutGroups(loadoutGroups, left);
            RenderItemSection("── Armor ──", armorEntries, right);
            RenderItemSection("── Accessories ──", accessoryEntries, right);
            RenderItemSection("── Quick Slots ──", quickSlotEntries, right);
        }

        private void RenderLoadoutGroups(SortedDictionary<int, List<KeyValuePair<string, string>>> loadoutGroups, Transform parent)
        {
            foreach (var group in loadoutGroups)
            {
                _view.AddSectionHeader($"── Weapon {group.Key + 1} ──", parent);

                // Build slot→GUID lookup for 2H detection
                var slotGuids = new Dictionary<string, string>();
                foreach (var entry in group.Value)
                    slotGuids[entry.Key] = entry.Value;

                foreach (var entry in group.Value)
                {
                    if (string.IsNullOrEmpty(entry.Value)) continue;

                    bool is2H = false;
                    if (entry.Key == "MainHand" || entry.Key == "AdditionalMainHand")
                    {
                        string offKey = entry.Key == "MainHand" ? "OffHand" : "AdditionalOffHand";
                        if (slotGuids.TryGetValue(offKey, out var offGuid) && offGuid == entry.Value)
                            is2H = SlotHelpers.IsItemTwoHanded(entry.Value);
                    }

                    // Skip redundant OffHand row for 2H weapons
                    if (entry.Key == "OffHand" || entry.Key == "AdditionalOffHand")
                    {
                        string mainKey = entry.Key == "OffHand" ? "MainHand" : "AdditionalMainHand";
                        if (slotGuids.TryGetValue(mainKey, out var mainGuid) && mainGuid == entry.Value
                            && SlotHelpers.IsItemTwoHanded(entry.Value))
                            continue;
                    }

                    string slotLabel = SlotHelpers.PrettySlotName(entry.Key);
                    if (is2H) slotLabel += " (2H)";

                    _view.AddTableRow(slotLabel, SlotHelpers.ResolveItemName(entry.Value), parent);
                }
                _view.AddSpacer(parent);
            }
        }

        private void RenderItemSection(string header, List<KeyValuePair<string, string>> entries, Transform parent)
        {
            if (entries.Count == 0) return;
            _view.AddSectionHeader(header, parent);
            foreach (var kvp in entries)
            {
                if (string.IsNullOrEmpty(kvp.Value)) continue;
                _view.AddTableRow(SlotHelpers.PrettySlotName(kvp.Key), SlotHelpers.ResolveItemName(kvp.Value), parent);
            }
            _view.AddSpacer(parent);
        }

        private void RenderStatAndTalentSections()
        {
            var left = _view.detailLeftCol;
            var right = _view.detailRightCol;

            if (_selectedSet.RpgStats != null && _selectedSet.RpgStats.Count > 0)
            {
                _view.AddSectionHeader("── Attributes ──", right);
                foreach (var kvp in _selectedSet.RpgStats)
                    _view.AddTableRow(kvp.Key, kvp.Value.ToString("F0"), right);
                _view.AddSpacer(right);
            }

            if (_selectedSet.TalentTrees != null && _selectedSet.TalentTrees.Count > 0)
            {
                _view.AddSectionHeader("── Talent Trees ──", left);
                foreach (var tree in _selectedSet.TalentTrees)
                {
                    string displayName = SlotHelpers.FriendlyTreeName(tree.Key);
                    int treeTotal = 0;
                    foreach (var sub in tree.Value)
                        treeTotal += sub.Value;
                    _view.AddTableRow(displayName, $"{treeTotal} pts", left);
                    foreach (var sub in tree.Value)
                    {
                        if (sub.Value > 0)
                            _view.AddSubRow(sub.Key, sub.Value.ToString(), left);
                    }
                }
            }
        }

        private void RebuildDetailLayout()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_view.detailLeftCol.GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(_view.detailRightCol.GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(_view.detailContent.GetComponent<RectTransform>());
        }

        private void OnSaveClicked()
        {
            if (_view.nameDialog == null) return;
            _view.nameDialog.OnConfirm = DoSave;
            _view.nameDialog.Open();
        }

        private void DoSave(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                    name = "Set_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

                var set = GearSetCapture.CaptureCurrentState(name);
                if (set == null)
                {
                    ShowStatus("Cannot capture loadout — no hero available.");
                    return;
                }

                SetManager.Save(set);
                _selectedSet = set;
                RefreshSetList();
                UpdateDetailPanel();
                ShowStatus("Saved: " + set.Name);
            }
            catch (Exception ex)
            {
                ShowStatus("Save failed: " + ex.Message);
            }
        }

        private void OnLoadClicked()
        {
            if (_selectedSet == null)
            {
                ShowStatus("No set selected.");
                return;
            }

            try
            {
                string statusMsg = GearSetApply.ApplySet(_selectedSet);
                ShowStatus(statusMsg);
            }
            catch (Exception ex)
            {
                ShowStatus("Load failed: " + ex.Message);
            }
        }

        private void OnDeleteClicked()
        {
            if (_selectedSet == null)
            {
                ShowStatus("No set selected.");
                return;
            }

            if (_view.nameDialog == null) return;
            var setName = _selectedSet.Name;
            _view.nameDialog.OpenConfirm($"Delete set \"{setName}\"?", () => DoDelete(setName));
        }

        private void DoDelete(string name)
        {
            try
            {
                SetManager.Delete(name);
                _selectedSet = null;
                RefreshSetList();
                UpdateDetailPanel();
                ShowStatus("Deleted: " + name);
            }
            catch (Exception ex)
            {
                ShowStatus("Delete failed: " + ex.Message);
            }
        }

        private void OnResetBuildClicked()
        {
            if (_view.nameDialog == null) return;
            _view.nameDialog.OpenConfirm(
                "Reset ALL skills, talents, and attributes?\nThis replicates the Origin Potion effect (no potion consumed).",
                DoResetBuild);
        }

        private void DoResetBuild()
        {
            try
            {
                var hero = Hero.Current;
                if (hero == null)
                {
                    ShowStatus("Reset failed — no hero available.");
                    return;
                }

                hero.Talents?.Reset();

                var rpgStats = hero.HeroRPGStats?.GetHeroRPGStats();
                if (rpgStats != null)
                {
                    foreach (var stat in rpgStats)
                    {
                        int refund = stat.BaseInt - 1;
                        if (refund > 0)
                            hero.Development.BaseStatPoints.IncreaseBy(refund);
                        stat.SetTo(1f);
                    }
                }

                // Do NOT call RecalculateStats — it reconstructs stats from stale wrapper
                // diffs, overwriting the refunded points. The Origin Potion doesn't
                // recalculate either; SetTo/IncreaseBy update live objects directly.

                ShowStatus("Build reset — all talents and attributes refunded.");
            }
            catch (Exception ex)
            {
                Log.LogError($"[ResetBuild] Failed: {ex}");
                ShowStatus("Reset failed: " + ex.Message);
            }
        }

        // Currency stats on hero.CharacterStats
        private static readonly string[] CharacterPointStatNames = new[]
        {
            "TalentPoints",
            "CatalystTalentPoints",
            "SarrasMageTalentPoints",
            "SarrasRogueTalentPoints",
            "SarrasWarriorTalentPoints"
        };

        // Currency stats on hero.HeroStats (different stat container)
        private static readonly string[] HeroPointStatNames = new[]
        {
            "WyrdMemoryShards"
        };

        private static readonly Dictionary<string, string> PointStatLabels = new Dictionary<string, string>
        {
            { "BaseStatPoints", "Attribute Points" },
            { "TalentPoints", "Talent Points" },
            { "CatalystTalentPoints", "Sarras Catalyst" },
            { "SarrasMageTalentPoints", "Sarras Mage" },
            { "SarrasRogueTalentPoints", "Sarras Rogue" },
            { "SarrasWarriorTalentPoints", "Sarras Warrior" },
            { "WyrdMemoryShards", "King's Soul" },
        };

        private void OnFixPointsClicked()
        {
            if (_view.nameDialog == null) return;

            var hero = Hero.Current;
            if (hero == null)
            {
                ShowStatus("No hero available.");
                return;
            }

            var entries = new List<NameInputDialog.PointEntry>();

            // BaseStatPoints lives on hero.Development
            try
            {
                var bsp = hero.Development?.BaseStatPoints;
                if (bsp != null)
                {
                    // Total attribute budget = free points + points invested in RPG stats
                    float invested = 0f;
                    var rpgStats = hero.HeroRPGStats?.GetHeroRPGStats();
                    if (rpgStats != null)
                    {
                        foreach (var stat in rpgStats)
                            invested += stat.BaseInt - 1; // each starts at 1
                    }
                    float total = bsp.BaseValue + invested;
                    entries.Add(new NameInputDialog.PointEntry
                    {
                        Key = "BaseStatPoints",
                        Label = $"Attribute Points (free:{bsp.BaseValue:F0} used:{invested:F0})",
                        OriginalValue = total,
                        EditText = total.ToString("F0")
                    });
                }
            }
            catch (Exception ex)
            {
                Log.LogDebug($"[FixPoints] BaseStatPoints read failed: {ex}");
            }

            // Talent currency stats — compute total budget (available + invested) per currency.
            // Stats live on two different containers:
            //   hero.CharacterStats  — TalentPoints, CatalystTalentPoints, Sarras*
            //   hero.HeroStats       — WyrdMemoryShards (King's Soul)
            var heroTalents = hero.Talents;
            if (heroTalents != null)
            {
                var currencyMap = new Dictionary<string, (Stat stat, int invested)>();
                var typeNameToStatName = new Dictionary<string, string>();

                void TryRegisterStat(string statName, object container)
                {
                    try
                    {
                        var prop = container.GetType().GetProperty(statName, BindingFlags.Instance | BindingFlags.Public);
                        if (prop == null) return;
                        var statObj = prop.GetValue(container);
                        if (statObj is Stat stat)
                        {
                            currencyMap[statName] = (stat, 0);
                            string typeName = stat.Type?.ToString();
                            Log.LogDebug($"[FixPoints] Registered {statName}: type='{typeName}', base={stat.BaseValue}");
                            if (typeName != null)
                                typeNameToStatName[typeName] = statName;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogDebug($"[FixPoints] Failed reading {statName}: {ex.Message}");
                    }
                }

                var charStats = hero.CharacterStats;
                if (charStats != null)
                {
                    foreach (var statName in CharacterPointStatNames)
                        TryRegisterStat(statName, charStats);
                }

                var heroStats = hero.HeroStats;
                if (heroStats != null)
                {
                    foreach (var statName in HeroPointStatNames)
                        TryRegisterStat(statName, heroStats);
                }

                // Count invested points per currency by matching StatType name
                foreach (TalentTable table in heroTalents.Elements<TalentTable>())
                {
                    foreach (Talent talent in table.talents)
                    {
                        if (talent.Level <= 0) continue;
                        try
                        {
                            var cs = talent.CurrencyStat;
                            string csTypeName = cs?.Type?.ToString();
                            if (cs != null && csTypeName != null && typeNameToStatName.TryGetValue(csTypeName, out string name))
                            {
                                var entry = currencyMap[name];
                                currencyMap[name] = (entry.stat, entry.invested + talent.Level);
                            }
                        }
                        catch { }
                    }
                }

                // Build combined ordered list for display
                var allStatNames = new List<string>();
                allStatNames.AddRange(CharacterPointStatNames);
                allStatNames.AddRange(HeroPointStatNames);

                foreach (var statName in allStatNames)
                {
                    if (!currencyMap.ContainsKey(statName)) continue;
                    var (stat, invested) = currencyMap[statName];
                    float available = stat.BaseValue;
                    float total = available + invested;
                    string label = PointStatLabels.ContainsKey(statName) ? PointStatLabels[statName] : statName;
                    entries.Add(new NameInputDialog.PointEntry
                    {
                        Key = statName,
                        Label = $"{label} (free:{available:F0} used:{invested})",
                        OriginalValue = total,
                        EditText = total.ToString("F0")
                    });
                }
            }

            if (entries.Count == 0)
            {
                ShowStatus("Could not read any point stats.");
                return;
            }

            _view.nameDialog.OpenPointEditor(entries, DoFixPoints);
        }

        private void DoFixPoints(Dictionary<string, float> changes)
        {
            try
            {
                var hero = Hero.Current;
                if (hero == null)
                {
                    ShowStatus("No hero available.");
                    return;
                }

                int applied = 0;

                // BaseStatPoints — newTotal is the desired total budget.
                // Compute what free points should be: newTotal - currentlyInvested
                if (changes.TryGetValue("BaseStatPoints", out float newAttrTotal))
                {
                    var bsp = hero.Development?.BaseStatPoints;
                    if (bsp != null)
                    {
                        float invested = 0f;
                        var rpgStats = hero.HeroRPGStats?.GetHeroRPGStats();
                        if (rpgStats != null)
                        {
                            foreach (var stat in rpgStats)
                                invested += stat.BaseInt - 1;
                        }
                        float newFree = newAttrTotal - invested;
                        if (newFree < 0f) newFree = 0f;
                        Log.LogDebug($"[FixPoints] BaseStatPoints: {bsp.BaseValue} -> {newFree} (total {newAttrTotal}, invested {invested})");
                        bsp.SetTo(newFree, false, null);
                        applied++;
                    }
                }

                // Talent currency stats — newTotal is the desired total budget.
                // Set BaseValue to newTotal - currentlyInvested.
                // Stats live on two containers: hero.CharacterStats and hero.HeroStats.
                var heroTalents = hero.Talents;
                {
                    var statObjects = new Dictionary<string, Stat>();

                    void TryResolveStat(string statName, object container)
                    {
                        if (!changes.ContainsKey(statName)) return;
                        try
                        {
                            var prop = container.GetType().GetProperty(statName, BindingFlags.Instance | BindingFlags.Public);
                            if (prop == null) return;
                            var statObj = prop.GetValue(container);
                            if (statObj is Stat stat)
                                statObjects[statName] = stat;
                        }
                        catch { }
                    }

                    var charStats = hero.CharacterStats;
                    if (charStats != null)
                    {
                        foreach (var statName in CharacterPointStatNames)
                            TryResolveStat(statName, charStats);
                    }

                    var heroStats = hero.HeroStats;
                    if (heroStats != null)
                    {
                        foreach (var statName in HeroPointStatNames)
                            TryResolveStat(statName, heroStats);
                    }

                    // Count currently invested per stat using StatType name matching
                    var invested = new Dictionary<string, int>();
                    foreach (var kv in statObjects)
                        invested[kv.Key] = 0;

                    if (heroTalents != null)
                    {
                        var typeNameToStatName = new Dictionary<string, string>();
                        foreach (var kv in statObjects)
                        {
                            string typeName = kv.Value.Type?.ToString();
                            if (typeName != null)
                                typeNameToStatName[typeName] = kv.Key;
                        }

                        foreach (TalentTable table in heroTalents.Elements<TalentTable>())
                        {
                            foreach (Talent talent in table.talents)
                            {
                                if (talent.Level <= 0) continue;
                                try
                                {
                                    var cs = talent.CurrencyStat;
                                    if (cs == null) continue;
                                    string csTypeName = cs.Type?.ToString();
                                    if (csTypeName != null && typeNameToStatName.TryGetValue(csTypeName, out string name))
                                        invested[name] += talent.Level;
                                }
                                catch { }
                            }
                        }
                    }

                    foreach (var kv in statObjects)
                    {
                        float newTotal = changes[kv.Key];
                        int inv = invested.ContainsKey(kv.Key) ? invested[kv.Key] : 0;
                        float newFree = newTotal - inv;
                        if (newFree < 0f) newFree = 0f;
                        Log.LogDebug($"[FixPoints] {kv.Key}: {kv.Value.BaseValue} -> {newFree} (total {newTotal}, invested {inv})");
                        kv.Value.SetTo(newFree, false, null);
                        applied++;
                    }
                }

                ShowStatus(applied > 0
                    ? $"Fixed {applied} stat(s). Save your game to persist."
                    : "No changes applied.");
            }
            catch (Exception ex)
            {
                Log.LogError($"[FixPoints] Failed: {ex}");
                ShowStatus("Fix failed: " + ex.Message);
            }
        }

        private void OnUpdateClicked()
        {
            if (_selectedSet == null)
            {
                ShowStatus("No set selected.");
                return;
            }

            if (_view.nameDialog == null) return;
            var setName = _selectedSet.Name;
            _view.nameDialog.OpenConfirm($"Update set \"{setName}\" with current equipment?", () => DoUpdate(setName));
        }

        private void DoUpdate(string name)
        {
            try
            {
                var set = GearSetCapture.CaptureCurrentState(name);
                if (set == null)
                {
                    ShowStatus("Cannot capture loadout — no hero available.");
                    return;
                }

                SetManager.Save(set);
                _selectedSet = set;
                RefreshSetList();
                UpdateDetailPanel();
                ShowStatus("Updated: " + set.Name);
            }
            catch (Exception ex)
            {
                ShowStatus("Update failed: " + ex.Message);
            }
        }

        private void ShowStatus(string message)
        {
            if (_view != null && _view.statusText != null)
                _view.statusText.text = message;
        }
    }
}
