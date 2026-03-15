using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Awaken.TG.Main.Heroes.CharacterSheet;
using Awaken.TG.Main.Heroes.CharacterSheet.Tabs;
using Awaken.TG.Main.UI.Components.Tabs;
using BepInEx.Logging;

namespace GearSetsMod.Patches
{
    public static class GearSetsTabPatch
    {
        private static CharacterSheetTabType _setsTabType;
        private static bool _initialized;
        private static Harmony _harmony;
        private static GameObject _setsButtonInstance;
        private static CharacterSheetTabs _currentTabs;
        private static object _setsButtonConfig;
        private static ManualLogSource _log;

        public static bool SuppressInput { get; set; }

        public static CharacterSheetTabType SetsTabType => _setsTabType;

        // Harmony prefix: return false = skip original OnHandle (suppresses tab-switching keys while dialog is open)
        public static bool OnHandle_Prefix()
        {
            return !SuppressInput;
        }

        public static void Initialize(Harmony harmony, ManualLogSource log)
        {
            if (_initialized) return;
            _harmony = harmony;
            _log = log;

            try
            {
                // Step 1: Create CharacterSheetTabType via reflection (private constructor)
                var ctors = typeof(CharacterSheetTabType).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
                ConstructorInfo ctor = null;
                Type spawnDelegateType = null;
                foreach (var c in ctors)
                {
                    var p = c.GetParameters();
                    if (p.Length == 3 && p[0].ParameterType == typeof(string) && p[2].ParameterType == typeof(string))
                    {
                        ctor = c;
                        spawnDelegateType = p[1].ParameterType;
                        break;
                    }
                }
                if (ctor == null) { _log.LogError("[GearSets] Could not find CharacterSheetTabType constructor!"); return; }

                Func<CharacterSheetUI, ICharacterSheetTab> spawnFunc = (_) => (ICharacterSheetTab)(object)new UI.GearSetsUI();
                var spawnDelegate = Delegate.CreateDelegate(spawnDelegateType, spawnFunc.Target, spawnFunc.Method);
                _setsTabType = (CharacterSheetTabType)ctor.Invoke(new object[] { "Sets", spawnDelegate, "Sets" });

                // Step 2: Set _spawn field
                var baseType = typeof(CharacterSheetTabType).BaseType;
                var spawnProp = baseType.GetProperty("_spawn", BindingFlags.Instance | BindingFlags.NonPublic);
                if (spawnProp != null && spawnProp.CanWrite) spawnProp.SetValue(_setsTabType, spawnDelegate);
                else { var f = baseType.GetField("<_spawn>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) f.SetValue(_setsTabType, spawnDelegate); }

                // Step 3: Set _visible to Always
                var alwaysField = baseType.GetField("Always", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                if (alwaysField != null)
                {
                    var alwaysVal = alwaysField.GetValue(null);
                    var visProp = baseType.GetProperty("_visible", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (visProp != null && visProp.CanWrite) visProp.SetValue(_setsTabType, alwaysVal);
                    else { var vf = baseType.GetField("<_visible>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic); if (vf != null) vf.SetValue(_setsTabType, alwaysVal); }
                }

                // Step 4: Patch Title getter
                var tabTypeEnumBase = baseType.BaseType;
                var titleProp = tabTypeEnumBase != null ? tabTypeEnumBase.GetProperty("Title", BindingFlags.Instance | BindingFlags.Public) : null;
                if (titleProp?.GetGetMethod() != null)
                    _harmony.Patch(titleProp.GetGetMethod(), postfix: new HarmonyMethod(typeof(GearSetsTabPatch).GetMethod(nameof(Title_Postfix), BindingFlags.Static | BindingFlags.Public)));

                // Step 5: Patch IsVisible
                var isVisMethod = typeof(CharacterSheetTabType).GetMethod("IsVisible", BindingFlags.Instance | BindingFlags.Public);
                if (isVisMethod != null)
                    _harmony.Patch(isVisMethod, postfix: new HarmonyMethod(typeof(GearSetsTabPatch).GetMethod(nameof(IsVisible_Postfix), BindingFlags.Static | BindingFlags.Public)));

                // Step 6: Patch OnFullyInitialized
                var onInitMethod = typeof(CharacterSheetUI).GetMethod("OnFullyInitialized", BindingFlags.Instance | BindingFlags.NonPublic);
                if (onInitMethod != null)
                    _harmony.Patch(onInitMethod, postfix: new HarmonyMethod(typeof(GearSetsTabPatch).GetMethod(nameof(OnFullyInitialized_Postfix), BindingFlags.Static | BindingFlags.Public)));

                // Step 7: Patch ChangeTab
                var tabsBaseType = typeof(CharacterSheetTabs).BaseType;
                var changeTabMethod = typeof(CharacterSheetTabs).GetMethod("ChangeTab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? (tabsBaseType != null ? tabsBaseType.GetMethod("ChangeTab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null);
                if (changeTabMethod != null)
                    _harmony.Patch(changeTabMethod, postfix: new HarmonyMethod(typeof(GearSetsTabPatch).GetMethod(nameof(ChangeTab_Postfix), BindingFlags.Static | BindingFlags.Public)));

                // Step 8: Patch OnHandle to suppress keyboard input while name dialog is open
                var onHandleMethod = typeof(CharacterSheetTabs).GetMethod("OnHandle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (onHandleMethod != null)
                    _harmony.Patch(onHandleMethod, prefix: new HarmonyMethod(typeof(GearSetsTabPatch).GetMethod(nameof(OnHandle_Prefix), BindingFlags.Static | BindingFlags.Public)));

                _initialized = true;
                _log.LogInfo("[GearSets] Tab type registered successfully!");
            }
            catch (Exception ex) { _log.LogError($"[GearSets] Failed to initialize: {ex}"); }
        }

        public static void Title_Postfix(object __instance, ref string __result)
        {
            if (_setsTabType != null && __instance == _setsTabType) __result = "Sets";
        }

        public static void IsVisible_Postfix(CharacterSheetTabType __instance, ref bool __result, CharacterSheetUI target)
        {
            if (_setsTabType != null && __instance == _setsTabType) __result = true;
        }

        public static void ChangeTab_Postfix(CharacterSheetTabType type)
        {
            UpdateButtonSelection(_setsTabType != null && type == _setsTabType);
        }

        public static void OnFullyInitialized_Postfix(CharacterSheetUI __instance)
        {
            if (_setsTabType == null) return;
            try
            {
                var tabsController = __instance.TabsController;
                var sheetTabs = tabsController as CharacterSheetTabs;
                if (sheetTabs == null) { _log.LogError("[GearSets] Could not get CharacterSheetTabs!"); return; }

                var tabsBaseType = typeof(CharacterSheetTabs).BaseType;
                var buttonsField = tabsBaseType != null ? tabsBaseType.GetField("_buttons", BindingFlags.Instance | BindingFlags.NonPublic) : null;
                if (buttonsField == null) { _log.LogError("[GearSets] Could not find _buttons field!"); return; }

                var buttonsRaw = buttonsField.GetValue(sheetTabs);
                var buttons = buttonsRaw as Array;
                if (buttons == null || buttons.Length == 0) { _log.LogError("[GearSets] _buttons null or empty!"); return; }

                // Check if already exists
                for (int i = 0; i < buttons.Length; i++)
                {
                    var btn = buttons.GetValue(i);
                    if (btn != null && ((Component)btn).gameObject.name == "SetsTabButton") { ((Component)btn).gameObject.SetActive(true); return; }
                }

                if (_setsButtonInstance != null)
                {
                    _setsButtonInstance.SetActive(true);
                    var comp = _setsButtonInstance.GetComponent<VCCharacterSheetTabButton>();
                    if (comp != null)
                    {
                        var arr = Array.CreateInstance(buttonsField.FieldType.GetElementType(), buttons.Length + 1);
                        Array.Copy(buttons, arr, buttons.Length);
                        arr.SetValue(comp, buttons.Length);
                        buttonsField.SetValue(sheetTabs, arr);
                        return;
                    }
                    _setsButtonInstance = null;
                }

                // Clone last button as template
                var template = buttons.GetValue(buttons.Length - 1);
                if (template == null) { _log.LogError("[GearSets] Template button null!"); return; }

                _setsButtonInstance = UnityEngine.Object.Instantiate(((Component)template).gameObject, ((Component)template).transform.parent);
                _setsButtonInstance.name = "SetsTabButton";

                var tabBtnComp = _setsButtonInstance.GetComponent<VCCharacterSheetTabButton>();
                if (tabBtnComp == null) { _log.LogError("[GearSets] Clone missing VCCharacterSheetTabButton!"); UnityEngine.Object.Destroy(_setsButtonInstance); _setsButtonInstance = null; return; }

                // Set tabType via reflection on the RichEnumReference field
                var tabTypeField = typeof(VCCharacterSheetTabButton).GetField("tabType", BindingFlags.Instance | BindingFlags.NonPublic);
                if (tabTypeField != null)
                {
                    try
                    {
                        var richEnumRef = tabTypeField.GetValue(tabBtnComp);
                        var enumProp = richEnumRef.GetType().GetProperty("Enum", BindingFlags.Instance | BindingFlags.Public);
                        if (enumProp != null && enumProp.CanWrite) enumProp.SetValue(richEnumRef, _setsTabType);
                        else
                        {
                            var ef = richEnumRef.GetType().GetField("_enum", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (ef != null) ef.SetValue(richEnumRef, _setsTabType);
                        }
                    }
                    catch (Exception ex) { _log.LogWarning($"[GearSets] Could not set tabType field: {ex.Message}"); }
                }

                _setsButtonInstance.transform.SetAsLastSibling();

                // Initialize button label via ButtonConfig
                var headerTabBtnBase = typeof(VCCharacterSheetTabButton).BaseType;
                var btnConfigField = headerTabBtnBase != null ? headerTabBtnBase.GetField("buttonConfig", BindingFlags.Instance | BindingFlags.NonPublic) : null;
                if (btnConfigField != null)
                {
                    var config = btnConfigField.GetValue(tabBtnComp);
                    if (config != null)
                    {
                        var initBtn = config.GetType().GetMethod("InitializeButton", BindingFlags.Instance | BindingFlags.Public);
                        if (initBtn != null)
                        {
                            try { initBtn.Invoke(config, new object[] { null, "Sets", false }); _setsButtonConfig = config; }
                            catch (Exception ex) { _log.LogWarning($"[GearSets] InitializeButton failed: {ex.Message}"); }
                        }
                    }
                }

                // Wire click handler via ARButton.OnClick event
                _currentTabs = sheetTabs;
                var vcTabBtnBase = headerTabBtnBase != null ? headerTabBtnBase.BaseType : null;
                var buttonField = vcTabBtnBase != null ? vcTabBtnBase.GetField("button", BindingFlags.Instance | BindingFlags.Public) : null;
                if (buttonField != null)
                {
                    var arButton = buttonField.GetValue(tabBtnComp);
                    if (arButton != null)
                    {
                        var onClick = arButton.GetType().GetEvent("OnClick");
                        if (onClick != null)
                        {
                            try
                            {
                                var handler = typeof(GearSetsTabPatch).GetMethod(nameof(OnSetsButtonClicked), BindingFlags.Static | BindingFlags.Public);
                                onClick.AddEventHandler(arButton, Delegate.CreateDelegate(onClick.EventHandlerType, handler));
                            }
                            catch (Exception ex) { _log.LogWarning($"[GearSets] Could not wire OnClick: {ex.Message}"); }
                        }
                    }
                }

                // Add to buttons array
                var finalArr = Array.CreateInstance(buttonsField.FieldType.GetElementType(), buttons.Length + 1);
                Array.Copy(buttons, finalArr, buttons.Length);
                finalArr.SetValue(tabBtnComp, buttons.Length);
                buttonsField.SetValue(sheetTabs, finalArr);

                _log.LogInfo("[GearSets] Tab button injected successfully!");
            }
            catch (Exception ex) { _log.LogError($"[GearSets] Failed to inject button: {ex}"); }
        }

        public static void OnSetsButtonClicked()
        {
            if (_currentTabs != null && _setsTabType != null)
            {
                _currentTabs.SelectTab(_setsTabType);
                UpdateButtonSelection(true);
            }
        }

        private static void UpdateButtonSelection(bool selected)
        {
            if (_setsButtonConfig == null) return;
            try { _setsButtonConfig.GetType().GetMethod("SetSelection", BindingFlags.Instance | BindingFlags.Public)?.Invoke(_setsButtonConfig, new object[] { selected }); }
            catch (Exception ex) { _log?.LogWarning($"[GearSets] Button selection error: {ex.Message}"); }
        }
    }
}
