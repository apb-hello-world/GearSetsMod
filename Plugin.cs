using System.IO;
using BepInEx;
using HarmonyLib;
using GearSetsMod.Core;
using GearSetsMod.Patches;

namespace GearSetsMod
{
    [BepInPlugin(PluginId, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        internal const string PluginId = "com.gearsets.taintedgrail";
        internal const string PluginName = "Gear & Skill Sets";
        internal const string PluginVersion = "2.2.0";

        private void Awake()
        {
            SetManager.ConfigPath = Path.Combine(Paths.ConfigPath, "GearSets");

            var harmony = new Harmony(PluginId);
            GearSetsTabPatch.Initialize(harmony, Logger);
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded!");
        }
    }
}
