using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Awaken.TG.Main.Heroes.CharacterSheet;
using Awaken.TG.Main.Heroes.CharacterSheet.Tabs;
using Awaken.TG.Main.UI.Components.Tabs;
using BepInEx.Logging;
using TMPro;
using UnityEngine.UI;

namespace GearSetsMod.Patches
{
    public static class GearSetsTabPatch
    {
        private static CharacterSheetTabType _setsTabType;
        private static bool _initialized;
        private static Harmony _harmony;
        private static GameObject _setsButtonInstance;
        private static GameObject _fallbackButtonInstance;
        private static Image _fallbackButtonImage;
        private static TextMeshProUGUI _fallbackButtonLabel;
        private static CharacterSheetTabs _currentTabs;
        private static object _setsButtonConfig;
        private static ManualLogSource _log;

        private static readonly Color FallbackClear = new Color(0f, 0f, 0f, 0f);
        private static readonly Color FallbackNormalText = new Color(0.70f, 0.67f, 0.64f, 1f);
        private static readonly Color FallbackSelectedText = new Color(0.90f, 0.82f, 0.55f, 1f);

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
                foreach (var toggleOverload in typeof(CharacterSheetUI).GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    if (toggleOverload.Name != "ToggleCharacterSheet" || toggleOverload.GetParameters().Length == 0)
                        continue;

                    _harmony.Patch(toggleOverload, prefix: new HarmonyMethod(typeof(GearSetsTabPatch).GetMethod(nameof(ToggleCharacterSheet_Prefix), BindingFlags.Static | BindingFlags.Public)));
                }

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
                PlaceButtonInHeader(tabBtnComp, buttons);
                InitializeButtonLabel(tabBtnComp);
                WireClickHandler(tabBtnComp, sheetTabs);
                AppendToButtonsArray(tabBtnComp, buttons, buttonsField, sheetTabs);
                RebuildHeaderLayout(tabBtnComp);
                HideFallbackButton();

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
                    var comp = ((Component)btn).GetComponent<VCCharacterSheetTabButton>();
                    ((Component)btn).gameObject.SetActive(true);
                    if (comp != null)
                    {
                        PlaceButtonInHeader(comp, buttons);
                        RebuildHeaderLayout(comp);
                    }
                    HideFallbackButton();
                    return true;
                }
            }

            if (_setsButtonInstance != null)
            {
                _setsButtonInstance.SetActive(true);
                var comp = _setsButtonInstance.GetComponent<VCCharacterSheetTabButton>();
                if (comp != null)
                {
                    PlaceButtonInHeader(comp, buttons);
                    AppendToButtonsArray(comp, buttons, buttonsField, sheetTabs);
                    RebuildHeaderLayout(comp);
                    HideFallbackButton();
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
            var template = FindButtonComponent(buttons, CharacterSheetTabType.Journal)
                ?? FindButtonComponent(buttons, CharacterSheetTabType.Quests)
                ?? FindButtonComponent(buttons, CharacterSheetTabType.Map)
                ?? FindLastButtonComponent(buttons);
            if (template == null) { _log.LogError("[GearSets] Template button null!"); return null; }

            _setsButtonInstance = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            _setsButtonInstance.name = "SetsTabButton";
            _log.LogInfo($"[GearSets] Cloned tab button template from {template.gameObject.name}.");

            var tabBtnComp = _setsButtonInstance.GetComponent<VCCharacterSheetTabButton>();
            if (tabBtnComp == null)
            {
                _log.LogError("[GearSets] Clone missing VCCharacterSheetTabButton!");
                UnityEngine.Object.Destroy(_setsButtonInstance);
                _setsButtonInstance = null;
            }

            return tabBtnComp;
        }

        private static void EnsureFallbackButton(CharacterSheetUI sheet, Array buttons)
        {
            if (sheet?.TabButtonsHost == null)
                return;

            _currentTabs = sheet.TabsController as CharacterSheetTabs ?? _currentTabs;

            if (_fallbackButtonInstance != null)
            {
                _fallbackButtonInstance.SetActive(true);
                StyleAndPlaceFallbackButton(sheet, buttons);
                RebuildFallbackLayout();
                return;
            }

            var buttonObj = new GameObject("SetsFallbackButton", typeof(RectTransform));
            buttonObj.transform.SetParent(sheet.TabButtonsHost, false);
            _fallbackButtonInstance = buttonObj;

            var rect = buttonObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(132f, 54f);

            var layout = buttonObj.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            _fallbackButtonImage = buttonObj.AddComponent<Image>();
            _fallbackButtonImage.color = FallbackClear;

            var button = buttonObj.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = FallbackClear;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.12f);
            colors.selectedColor = FallbackClear;
            colors.disabledColor = FallbackClear;
            button.colors = colors;
            button.onClick.AddListener(OnSetsButtonClicked);

            var labelObj = new GameObject("Label", typeof(RectTransform));
            labelObj.transform.SetParent(buttonObj.transform, false);
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            _fallbackButtonLabel = labelObj.AddComponent<TextMeshProUGUI>();
            _fallbackButtonLabel.text = "SETS";
            _fallbackButtonLabel.fontSize = 28f;
            _fallbackButtonLabel.fontStyle = FontStyles.Bold;
            _fallbackButtonLabel.alignment = TextAlignmentOptions.Center;
            _fallbackButtonLabel.color = FallbackSelectedText;
            _fallbackButtonLabel.raycastTarget = false;

            StyleAndPlaceFallbackButton(sheet, buttons);
            RebuildFallbackLayout();
            _log.LogInfo($"[GearSets] Fallback button created under {GetTransformPath(sheet.TabButtonsHost)} with {sheet.TabButtonsHost.childCount} tab host children.");
        }

        private static void StyleAndPlaceFallbackButton(CharacterSheetUI sheet, Array buttons)
        {
            if (_fallbackButtonInstance == null || sheet?.TabButtonsHost == null)
                return;

            var source = FindButtonComponent(buttons, CharacterSheetTabType.Journal)
                ?? FindButtonComponent(buttons, CharacterSheetTabType.Quests)
                ?? FindLastButtonComponent(buttons);
            if (source == null)
                return;

            if (_fallbackButtonInstance.transform.parent != source.transform.parent)
                _fallbackButtonInstance.transform.SetParent(source.transform.parent, false);

            CopyNativeTextStyle(source);
            PlaceFallbackAfterSource(buttons, source);
            _fallbackButtonInstance.transform.SetAsLastSibling();
        }

        private static void RebuildFallbackLayout()
        {
            var parent = _fallbackButtonInstance?.transform.parent as RectTransform;
            while (parent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
                parent = parent.parent as RectTransform;
            }
        }

        private static void HideFallbackButton()
        {
            if (_fallbackButtonInstance != null)
                _fallbackButtonInstance.SetActive(false);
        }

        private static Component FindButtonComponent(Array buttons, CharacterSheetTabType tabType)
        {
            foreach (var button in buttons)
            {
                if (button is not Component component)
                    continue;

                try
                {
                    var typeProp = button.GetType().GetProperty("Type", BindingFlags.Instance | BindingFlags.Public);
                    if (typeProp?.GetValue(button) == tabType)
                        return component;
                }
                catch
                {
                }
            }

            return null;
        }

        private static Component FindLastButtonComponent(Array buttons)
        {
            for (var i = buttons.Length - 1; i >= 0; i--)
            {
                if (buttons.GetValue(i) is Component component)
                    return component;
            }

            return null;
        }

        private static void CopyNativeTextStyle(Component source)
        {
            if (_fallbackButtonLabel == null)
                return;

            var sourceLabel = source.GetComponentInChildren<TextMeshProUGUI>(true);
            if (sourceLabel == null)
                return;

            _fallbackButtonLabel.font = sourceLabel.font;
            _fallbackButtonLabel.fontSharedMaterial = sourceLabel.fontSharedMaterial;
            _fallbackButtonLabel.fontSize = sourceLabel.fontSize;
            _fallbackButtonLabel.fontStyle = sourceLabel.fontStyle;
            _fallbackButtonLabel.characterSpacing = sourceLabel.characterSpacing;
            _fallbackButtonLabel.wordSpacing = sourceLabel.wordSpacing;
            _fallbackButtonLabel.lineSpacing = sourceLabel.lineSpacing;
            _fallbackButtonLabel.enableAutoSizing = sourceLabel.enableAutoSizing;
            _fallbackButtonLabel.fontSizeMin = sourceLabel.fontSizeMin;
            _fallbackButtonLabel.fontSizeMax = sourceLabel.fontSizeMax;
            _fallbackButtonLabel.color = FallbackSelectedText;
        }

        private static void PlaceFallbackAfterSource(Array buttons, Component source)
        {
            var sourceLabel = source.GetComponentInChildren<TextMeshProUGUI>(true);
            var sourceRect = source.transform as RectTransform;
            var fallbackRect = _fallbackButtonInstance?.transform as RectTransform;
            if (sourceRect == null || fallbackRect == null)
                return;

            fallbackRect.anchorMin = sourceRect.anchorMin;
            fallbackRect.anchorMax = sourceRect.anchorMax;
            fallbackRect.pivot = sourceRect.pivot;
            fallbackRect.sizeDelta = sourceRect.sizeDelta;
            fallbackRect.localScale = sourceRect.localScale;
            fallbackRect.localRotation = sourceRect.localRotation;

            var spacing = GetNativeTabSpacing(buttons);
            fallbackRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(spacing, 0f);

            if (_fallbackButtonLabel != null && sourceLabel != null)
                CopyNativeLabelRect(sourceLabel.rectTransform, _fallbackButtonLabel.rectTransform);
        }

        private static void CopyNativeLabelRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
        }

        private static float GetNativeTabSpacing(Array buttons)
        {
            var quests = FindButtonComponent(buttons, CharacterSheetTabType.Quests);
            var journal = FindButtonComponent(buttons, CharacterSheetTabType.Journal);
            if (TryGetLabelCenterX(quests, out var questX) && TryGetLabelCenterX(journal, out var journalX))
            {
                var spacing = journalX - questX;
                if (Mathf.Abs(spacing) > 10f)
                    return spacing;
            }

            var map = FindButtonComponent(buttons, CharacterSheetTabType.Map);
            if (TryGetLabelCenterX(map, out var mapX) && TryGetLabelCenterX(quests, out questX))
            {
                var spacing = questX - mapX;
                if (Mathf.Abs(spacing) > 10f)
                    return spacing;
            }

            return 170f;
        }

        private static bool TryGetLabelCenterX(Component button, out float centerX)
        {
            centerX = 0f;
            if (button == null || _fallbackButtonInstance == null)
                return false;

            var hostRect = _fallbackButtonInstance.transform.parent as RectTransform;
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (hostRect == null || label == null)
                return false;

            var corners = new Vector3[4];
            label.rectTransform.GetWorldCorners(corners);
            var left = hostRect.InverseTransformPoint(corners[0]);
            var right = hostRect.InverseTransformPoint(corners[2]);
            centerX = (left.x + right.x) * 0.5f;
            return true;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static void PlaceButtonInHeader(VCCharacterSheetTabButton tabBtnComp, Array buttons)
        {
            var buttonTransform = ((Component)tabBtnComp).transform;
            var buttonObject = ((Component)tabBtnComp).gameObject;
            var layoutElement = buttonObject.GetComponent<LayoutElement>() ?? buttonObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            var source = FindButtonComponent(buttons, CharacterSheetTabType.Journal)
                ?? FindButtonComponent(buttons, CharacterSheetTabType.Quests)
                ?? FindLastButtonComponent(buttons);
            if (source == null)
            {
                buttonTransform.SetAsLastSibling();
                return;
            }

            if (buttonTransform.parent != source.transform.parent)
                buttonTransform.SetParent(source.transform.parent, false);

            var buttonRect = buttonTransform as RectTransform;
            var sourceRect = source.transform as RectTransform;
            if (buttonRect != null && sourceRect != null)
            {
                buttonRect.anchorMin = sourceRect.anchorMin;
                buttonRect.anchorMax = sourceRect.anchorMax;
                buttonRect.pivot = sourceRect.pivot;
                buttonRect.sizeDelta = sourceRect.sizeDelta;
                buttonRect.localScale = sourceRect.localScale;
                buttonRect.localRotation = sourceRect.localRotation;
            }

            var previous = FindButtonComponent(buttons, CharacterSheetTabType.Quests)
                ?? FindButtonComponent(buttons, CharacterSheetTabType.Map);
            var positioner = buttonObject.GetComponent<NativeTabPositioner>() ?? buttonObject.AddComponent<NativeTabPositioner>();
            positioner.Initialize(buttonRect, source.transform as RectTransform, previous?.transform as RectTransform);
            buttonTransform.SetAsLastSibling();
        }

        private static int FindSiblingIndexAfterTab(Array buttons, CharacterSheetTabType tabType)
        {
            foreach (var button in buttons)
            {
                if (button is not Component component)
                    continue;

                try
                {
                    var typeProp = button.GetType().GetProperty("Type", BindingFlags.Instance | BindingFlags.Public);
                    if (typeProp?.GetValue(button) == tabType)
                        return component.transform.GetSiblingIndex() + 1;
                }
                catch
                {
                    // Some cloned third-party buttons can be partially initialized during sheet construction.
                }
            }

            return -1;
        }

        private class NativeTabPositioner : MonoBehaviour
        {
            private RectTransform _target;
            private RectTransform _source;
            private RectTransform _previous;
            private int _framesRemaining;

            public void Initialize(RectTransform target, RectTransform source, RectTransform previous)
            {
                _target = target;
                _source = source;
                _previous = previous;
                _framesRemaining = 90;
                Apply();
            }

            private void LateUpdate()
            {
                if (_framesRemaining <= 0)
                {
                    enabled = false;
                    return;
                }

                _framesRemaining--;
                Apply();
            }

            private void Apply()
            {
                if (_target == null || _source == null)
                    return;

                if (_target.parent != _source.parent)
                    _target.SetParent(_source.parent, false);

                _target.anchorMin = _source.anchorMin;
                _target.anchorMax = _source.anchorMax;
                _target.pivot = _source.pivot;
                _target.sizeDelta = _source.sizeDelta;
                _target.localScale = _source.localScale;
                _target.localRotation = _source.localRotation;

                var spacing = GetWorldSpacing();
                _target.position = _source.position + spacing;
                _target.SetAsLastSibling();
            }

            private Vector3 GetWorldSpacing()
            {
                if (_previous != null)
                {
                    var spacing = _source.position - _previous.position;
                    if (spacing.sqrMagnitude > 0.0001f)
                        return spacing * 0.9f;
                }

                var width = _source.rect.width * Mathf.Abs(_source.lossyScale.x);
                return _source.right * Mathf.Max(width * 1.30f, 126f);
            }
        }

        private static void RebuildHeaderLayout(VCCharacterSheetTabButton tabBtnComp)
        {
            var parent = ((Component)tabBtnComp).transform.parent as RectTransform;
            while (parent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
                parent = parent.parent as RectTransform;
            }
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
            if (_fallbackButtonImage != null)
                _fallbackButtonImage.color = FallbackClear;
            if (_fallbackButtonLabel != null)
                _fallbackButtonLabel.color = selected ? FallbackSelectedText : FallbackNormalText;

            if (_setsButtonConfig == null) return;
            try { _setsButtonConfig.GetType().GetMethod("SetSelection", BindingFlags.Instance | BindingFlags.Public)?.Invoke(_setsButtonConfig, new object[] { selected }); }
            catch (Exception ex) { _log?.LogWarning($"[GearSets] Button selection error: {ex.Message}"); }
        }
    }
}
