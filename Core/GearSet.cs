using System;
using System.Collections.Generic;

namespace GearSetsMod.Core
{
    [Serializable]
    public class GearSet
    {
        // Version 1: RpgStats stored Modified values (broken)
        // Version 2: RpgStats stores BaseValue (correct)
        public int Version { get; set; } = 2;
        public string Name { get; set; }
        public Dictionary<string, string> SlotToItemGuid { get; set; } = new Dictionary<string, string>();
        // Key = slot name (e.g., "MainHand"), Value = Item Template GUID
        public int LoadoutIndex { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        // Hero level at time of capture (for point budget validation)
        public int HeroLevel { get; set; }
        // Nested: tree name → { subtree name → points }
        public Dictionary<string, Dictionary<string, int>> TalentTrees { get; set; } = new Dictionary<string, Dictionary<string, int>>();
        // Per-talent levels: talent template name → level
        public Dictionary<string, int> TalentLevels { get; set; } = new Dictionary<string, int>();
        // RPG attributes: stat name → base value (Strength, Dexterity, etc.)
        public Dictionary<string, float> RpgStats { get; set; } = new Dictionary<string, float>();
    }
}
