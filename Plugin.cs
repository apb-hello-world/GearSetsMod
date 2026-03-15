using System.IO;
using BepInEx;
using HarmonyLib;
using GearSetsMod.Core;
using GearSetsMod.Patches;

namespace GearSetsMod
{
    [BepInPlugin("com.gearsets.taintedgrail", "Gear & Skill Sets", "2.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;

        private void Awake()
        {
            Instance = this;

            SetManager.ConfigPath = Path.Combine(Paths.ConfigPath, "GearSets");

            var harmony = new Harmony("com.gearsets.taintedgrail");
            GearSetsTabPatch.Initialize(harmony, Logger);
            Logger.LogInfo("Gear & Skill Sets v2.0.0 loaded!");
        }
    }
}