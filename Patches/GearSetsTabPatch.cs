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

        // Harmony prefix: return false = skip ToggleCharacterSheet while dialog is open
        // Prevents keys like 'i' from closing the inventory while typing a set name
        public static bool ToggleCharacterSheet_Prefix()
        {
            return !SuppressInput;
        }

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

                // Step 9: Patch ToggleCharacterSheet to prevent 'i' key from closing the sheet while typing
                var toggleMethod = typeof(CharacterSheetUI).GetMethod("ToggleCharacterSheet", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (toggleMethod != null)
                    _harmony.Patch(toggleMethod, prefix: new HarmonyMethod(typeof(GearSetsTabPatch).GetMethod(nameof(ToggleCharacterSheet_Prefix), BindingFlags.Static | BindingFlags.Public)));
                var toggleOverload = typeof(CharacterSheetUI).GetMethod("ToggleCharacterSheet", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(CharacterSheetTabType), typeof(bool), typeof(CharacterSheetTabType[]) }, null);
                if (toggleOverload != null)
                    _harmony.Patch(toggleOverload, prefix: new HarmonyMethod(typeof(GearSetsTabPatch).GetMethod(nameof(ToggleCharacterSheet_Prefix), BindingFlags.Static | BindingFlags.Public)));

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
                var sheetTabs = __instance.TabsController as CharacterSheetTabs;
                if (sheetTabs == null) { _log.LogError("[GearSets] Could not get CharacterSheetTabs!"); return; }

                var buttonsField = FindButtonsField();
                if (buttonsField == null) { _log.LogError("[GearSets] Could not find _buttons field!"); return; }

                var buttons = buttonsField.GetValue(sheetTabs) as Array;
                if (buttons == null || buttons.Length == 0) { _log.LogError("[GearSets] _buttons null or empty!"); return; }

                if (TryReuseExistingButton(buttons, buttonsField, sheetTabs))
                    return;

                var tabBtnComp = CloneTemplateButton(buttons);
                if (tabBtnComp == null) return;

                ConfigureTabType(tabBtnComp);
                _setsButtonInstance.transform.SetAsLastSibling();
                InitializeButtonLabel(tabBtnComp);
                WireClickHandler(tabBtnComp, sheetTabs);
                AppendToButtonsArray(tabBtnComp, buttons, buttonsField, sheetTabs);

                _log.LogInfo("[GearSets] Tab button injected successfully!");
            }
            catch (Exception ex) { _log.LogError($"[GearSets] Failed to inject button: {ex}"); }
        }

        private static FieldInfo FindButtonsField()
        {
            var tabsBaseType = typeof(CharacterSheetTabs).BaseType;
            return tabsBaseType?.GetField("_buttons", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        /// <summary>
        /// If the button already exists in the array (re-open) or was cached, reuse it.
        /// Returns true if reuse succeeded and no further work is needed.
        /// </summary>
        private static bool TryReuseExistingButton(Array buttons, FieldInfo buttonsField, CharacterSheetTabs sheetTabs)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons.GetValue(i);
                if (btn != null && ((Component)btn).gameObject.name == "SetsTabButton")
                {
                    ((Component)btn).gameObject.SetActive(true);
                    return true;
                }
            }

            if (_setsButtonInstance != null)
            {
                _setsButtonInstance.SetActive(true);
                var comp = _setsButtonInstance.GetComponent<VCCharacterSheetTabButton>();
                if (comp != null)
                {
                    AppendToButtonsArray(comp, buttons, buttonsField, sheetTabs);
                    return true;
                }
                _setsButtonInstance = null;
            }

            return false;
        }

        /// <summary>
        /// Clones the last tab button as a template for the Sets tab.
        /// Returns the <see cref="VCCharacterSheetTabButton"/> component, or null on failure.
        /// </summary>
        private static VCCharacterSheetTabButton CloneTemplateButton(Array buttons)
        {
            var template = buttons.GetValue(buttons.Length - 1);
            if (template == null) { _log.LogError("[GearSets] Template button null!"); return null; }

            _setsButtonInstance = UnityEngine.Object.Instantiate(((Component)template).gameObject, ((Component)template).transform.parent);
            _setsButtonInstance.name = "SetsTabButton";

            var tabBtnComp = _setsButtonInstance.GetComponent<VCCharacterSheetTabButton>();
            if (tabBtnComp == null)
            {
                _log.LogError("[GearSets] Clone missing VCCharacterSheetTabButton!");
                UnityEngine.Object.Destroy(_setsButtonInstance);
                _setsButtonInstance = null;
            }

            return tabBtnComp;
        }

        /// <summary>
        /// Sets the tabType field on the cloned button to point to our custom <see cref="_setsTabType"/>.
        /// </summary>
        private static void ConfigureTabType(VCCharacterSheetTabButton tabBtnComp)
        {
            var tabTypeField = typeof(VCCharacterSheetTabButton).GetField("tabType", BindingFlags.Instance | BindingFlags.NonPublic);
            if (tabTypeField == null) return;

            try
            {
                var richEnumRef = tabTypeField.GetValue(tabBtnComp);
                var enumProp = richEnumRef.GetType().GetProperty("Enum", BindingFlags.Instance | BindingFlags.Public);
                if (enumProp != null && enumProp.CanWrite)
                    enumProp.SetValue(richEnumRef, _setsTabType);
                else
                {
                    var ef = richEnumRef.GetType().GetField("_enum", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (ef != null) ef.SetValue(richEnumRef, _setsTabType);
                }
            }
            catch (Exception ex) { _log.LogWarning($"[GearSets] Could not set tabType field: {ex.Message}"); }
        }

        /// <summary>
        /// Initializes the button label text to "Sets" via ButtonConfig.InitializeButton.
        /// </summary>
        private static void InitializeButtonLabel(VCCharacterSheetTabButton tabBtnComp)
        {
            var headerTabBtnBase = typeof(VCCharacterSheetTabButton).BaseType;
            var btnConfigField = headerTabBtnBase?.GetField("buttonConfig", BindingFlags.Instance | BindingFlags.NonPublic);
            if (btnConfigField == null) return;

            var config = btnConfigField.GetValue(tabBtnComp);
            if (config == null) return;

            var initBtn = config.GetType().GetMethod("InitializeButton", BindingFlags.Instance | BindingFlags.Public);
            if (initBtn == null) return;

            try
            {
                initBtn.Invoke(config, new object[] { null, "Sets", false });
                _setsButtonConfig = config;
            }
            catch (Exception ex) { _log.LogWarning($"[GearSets] InitializeButton failed: {ex.Message}"); }
        }

        /// <summary>
        /// Wires the ARButton.OnClick event to our <see cref="OnSetsButtonClicked"/> handler.
        /// </summary>
        private static void WireClickHandler(VCCharacterSheetTabButton tabBtnComp, CharacterSheetTabs sheetTabs)
        {
            _currentTabs = sheetTabs;
            var headerTabBtnBase = typeof(VCCharacterSheetTabButton).BaseType;
            var vcTabBtnBase = headerTabBtnBase?.BaseType;
            var buttonField = vcTabBtnBase?.GetField("button", BindingFlags.Instance | BindingFlags.Public);
            if (buttonField == null) return;

            var arButton = buttonField.GetValue(tabBtnComp);
            if (arButton == null) return;

            var onClick = arButton.GetType().GetEvent("OnClick");
            if (onClick == null) return;

            try
            {
                var handler = typeof(GearSetsTabPatch).GetMethod(nameof(OnSetsButtonClicked), BindingFlags.Static | BindingFlags.Public);
                onClick.AddEventHandler(arButton, Delegate.CreateDelegate(onClick.EventHandlerType, handler));
            }
            catch (Exception ex) { _log.LogWarning($"[GearSets] Could not wire OnClick: {ex.Message}"); }
        }

        private static void AppendToButtonsArray(VCCharacterSheetTabButton tabBtnComp, Array buttons, FieldInfo buttonsField, CharacterSheetTabs sheetTabs)
        {
            var arr = Array.CreateInstance(buttonsField.FieldType.GetElementType(), buttons.Length + 1);
            Array.Copy(buttons, arr, buttons.Length);
            arr.SetValue(tabBtnComp, buttons.Length);
            buttonsField.SetValue(sheetTabs, arr);
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
