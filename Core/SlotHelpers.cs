using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Templates;

namespace GearSetsMod.Core
{
    /// <summary>
    /// Utility methods for slot/enum name resolution and item name lookup.
    /// </summary>
    public static class SlotHelpers
    {
        private static readonly Dictionary<string, string> SlotNameMap = new Dictionary<string, string>
        {
            {"MainHand", "Main Hand"}, {"OffHand", "Off Hand"},
            {"AdditionalMainHand", "Main Hand"}, {"AdditionalOffHand", "Off Hand"},
            {"Ring1", "Ring 1"}, {"Ring2", "Ring 2"},
            {"FoodQuickSlot", "Food Slot"}, {"QuickSlot2", "Quick Slot 2"}, {"QuickSlot3", "Quick Slot 3"},
            {"HorseArmor", "Horse Armor"},
        };

        private static readonly Dictionary<string, string> TreeNameMap = new Dictionary<string, string>
        {
            {"TalentTree_Str", "Strength"}, {"TalentTree_Dex", "Dexterity"},
            {"TalentTree_End", "Endurance"}, {"TalentTree_Per", "Perception"},
            {"TalentTree_Pra", "Practicality"}, {"TalentTree_Spi", "Spirituality"},
            {"TalentTree_WyrdArthur", "King's Soul"}, {"TalentTree_RedDeath", "Red Death"},
            {"WyrdArthur", "King's Soul"}, {"Wyrd Arthur", "King's Soul"},
        };

        public static string GetRichEnumName(object richEnum)
        {
            if (richEnum == null) return "Unknown";
            var type = richEnum.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

            foreach (var propName in new[] { "EnumName", "Name" })
            {
                var prop = type.GetProperty(propName, flags);
                var val = prop?.GetValue(richEnum)?.ToString();
                if (!string.IsNullOrEmpty(val)) return val;
            }

            foreach (var fieldName in new[] { "_enumName", "_name" })
            {
                var field = type.GetField(fieldName, flags);
                var val = field?.GetValue(richEnum)?.ToString();
                if (!string.IsNullOrEmpty(val)) return val;
            }

            return richEnum.ToString();
        }

        public static string PrettySlotName(string raw)
        {
            if (SlotNameMap.TryGetValue(raw, out var pretty)) return pretty;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                    sb.Append(' ');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        public static string FriendlyTreeName(string raw)
        {
            if (TreeNameMap.TryGetValue(raw, out var mapped)) return mapped;
            if (raw.Contains("WyrdArthur")) return "King's Soul";
            foreach (var kvp in TreeNameMap)
            {
                if (raw.Contains(kvp.Key.Replace("TalentTree_", "")))
                    return kvp.Value;
            }
            return PrettySlotName(raw.Replace("TalentTree_", ""));
        }

        public static EquipmentSlotType FindSlotByName(string name)
        {
            var slotTypeType = typeof(EquipmentSlotType);

            var field = slotTypeType.GetField(name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (field != null)
            {
                var val = field.GetValue(null) as EquipmentSlotType;
                if (val != null) return val;
            }

            var prop = slotTypeType.GetProperty(name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (prop != null)
            {
                var val = prop.GetValue(null) as EquipmentSlotType;
                if (val != null) return val;
            }

            var loadoutsField = slotTypeType.GetField("Loadouts",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (loadoutsField != null)
            {
                var loadouts = loadoutsField.GetValue(null) as EquipmentSlotType[];
                if (loadouts != null)
                {
                    foreach (var slot in loadouts)
                    {
                        if (slot != null && slot.ToString() == name)
                            return slot;
                    }
                }
            }

            var allField = slotTypeType.GetField("All",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (allField != null)
            {
                var all = allField.GetValue(null) as EquipmentSlotType[];
                if (all != null)
                {
                    foreach (var slot in all)
                    {
                        if (slot != null && slot.ToString() == name)
                            return slot;
                    }
                }
            }

            return null;
        }

        public static string ResolveItemName(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "(empty)";

            try
            {
                // First try: look up item in hero's inventory (includes equipped items)
                var hero = Hero.Current;
                if (hero != null)
                {
                    var item = hero.HeroItems?.Items
                        .FirstOrDefault(i => i.Template?.GUID == guid);

                    if (item != null) return item.DisplayName;
                }

                // Second try: resolve via template registry (works for any valid GUID,
                // even if the item isn't in inventory — e.g. it's in stash or unowned)
                var template = TemplatesUtil.Load<ItemTemplate>(guid);
                if (template != null)
                {
                    string name = template.ItemName;
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
            catch (System.Exception)
            {
                // Best-effort lookup — fall through to return the raw GUID
            }

            return guid;
        }

        /// <summary>
        /// Returns true if the item template for the given GUID is a two-handed weapon.
        /// Used to distinguish 2H weapons (same item in both hand slots) from
        /// dual-wielding identical 1H weapons.
        /// </summary>
        public static bool IsItemTwoHanded(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return false;
            try
            {
                var template = TemplatesUtil.Load<ItemTemplate>(guid);
                return template != null && template.IsTwoHanded;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
