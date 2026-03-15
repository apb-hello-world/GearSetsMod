using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Attributes;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.CharacterSheet.Tabs;
using Awaken.TG.Main.Heroes.Development.Talents;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Loadouts;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Heroes.Storage;
using BepInEx.Logging;
using GearSetsMod.Core;
using GearSetsMod.Patches;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace GearSetsMod.UI
{
    public enum ModalMode { TextInput, Confirm }

    public class NameInputDialog : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public string Result { get; private set; }
        public ModalMode Mode { get; private set; }
        public System.Action<string> OnConfirm;

        private string _text = "";
        private string _message = "";
        private bool _focusField;
        private Action _onConfirmAction;
        private GUIStyle _boxStyle;
        private GUIStyle _borderStyle;
        private GUIStyle _fieldStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _messageStyle;

        public void Open()
        {
            _text = "";
            _message = "";
            Result = null;
            Mode = ModalMode.TextInput;
            IsOpen = true;
            _focusField = true;
            _onConfirmAction = null;
            GearSetsTabPatch.SuppressInput = true;
        }

        public void OpenTextInput(string label, Action<string> onConfirm)
        {
            _text = "";
            _message = label;
            Result = null;
            Mode = ModalMode.TextInput;
            OnConfirm = onConfirm;
            _onConfirmAction = null;
            IsOpen = true;
            _focusField = true;
            GearSetsTabPatch.SuppressInput = true;
        }

        public void OpenConfirm(string message, Action onConfirm)
        {
            _text = "";
            _message = message;
            Result = null;
            Mode = ModalMode.Confirm;
            OnConfirm = null;
            _onConfirmAction = onConfirm;
            IsOpen = true;
            _focusField = false;
            GearSetsTabPatch.SuppressInput = true;
        }

        private void Close()
        {
            IsOpen = false;
            GearSetsTabPatch.SuppressInput = false;
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            if (Event.current.isKey || Event.current.isScrollWheel)
            {
                if (Event.current.keyCode == KeyCode.Escape)
                {
                    Close();
                    Event.current.Use();
                    return;
                }
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    if (Mode == ModalMode.TextInput)
                    {
                        if (!string.IsNullOrEmpty(_text.Trim()))
                        {
                            Result = _text.Trim();
                            OnConfirm?.Invoke(Result);
                        }
                    }
                    else
                    {
                        _onConfirmAction?.Invoke();
                    }
                    Close();
                    Event.current.Use();
                    return;
                }
            }

            InitStyles();

            float w = 420, h = Mode == ModalMode.TextInput ? 150 : 130;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

            GUI.Box(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), "", _borderStyle);
            GUI.Box(rect, "", _boxStyle);
            GUILayout.BeginArea(new Rect(rect.x + 20, rect.y + 15, w - 40, h - 30));

            if (Mode == ModalMode.TextInput)
            {
                string label = string.IsNullOrEmpty(_message) ? "Name this gear set:" : _message;
                GUILayout.Label(label, _labelStyle);
                GUILayout.Space(8);

                GUI.SetNextControlName("GearSetNameField");
                _text = GUILayout.TextField(_text, 50, _fieldStyle, GUILayout.Height(30));

                if (_focusField)
                {
                    GUI.FocusControl("GearSetNameField");
                    _focusField = false;
                }

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Save", _buttonStyle, GUILayout.Height(30)))
                {
                    if (!string.IsNullOrEmpty(_text.Trim()))
                    {
                        Result = _text.Trim();
                        OnConfirm?.Invoke(Result);
                    }
                    Close();
                }
                if (GUILayout.Button("Cancel", _buttonStyle, GUILayout.Height(30)))
                {
                    Close();
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Space(10);
                GUILayout.Label(_message, _messageStyle);
                GUILayout.Space(15);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm", _buttonStyle, GUILayout.Height(30)))
                {
                    _onConfirmAction?.Invoke();
                    Close();
                }
                if (GUILayout.Button("Cancel", _buttonStyle, GUILayout.Height(30)))
                {
                    Close();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp)
                Event.current.Use();
        }

        private void InitStyles()
        {
            if (_boxStyle != null) return;

            _borderStyle = new GUIStyle();
            _borderStyle.normal.background = MakeTex(2, 2, new Color(0.4f, 0.35f, 0.2f, 0.8f));

            _boxStyle = new GUIStyle();
            _boxStyle.normal.background = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.1f, 0.97f));

            _fieldStyle = new GUIStyle(GUI.skin.textField);
            _fieldStyle.fontSize = 18;
            _fieldStyle.normal.textColor = Color.white;
            _fieldStyle.focused.textColor = Color.white;
            _fieldStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.18f, 1f));
            _fieldStyle.focused.background = MakeTex(2, 2, new Color(0.18f, 0.18f, 0.22f, 1f));
            _fieldStyle.alignment = TextAnchor.MiddleLeft;
            _fieldStyle.padding = new RectOffset(10, 10, 0, 0);

            _buttonStyle = new GUIStyle();
            _buttonStyle.fontSize = 16;
            _buttonStyle.alignment = TextAnchor.MiddleCenter;
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.25f, 1f));
            _buttonStyle.hover.textColor = Color.white;
            _buttonStyle.hover.background = MakeTex(2, 2, new Color(0.3f, 0.3f, 0.35f, 1f));
            _buttonStyle.active.textColor = Color.white;
            _buttonStyle.active.background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.2f, 1f));
            _buttonStyle.padding = new RectOffset(10, 10, 5, 5);

            _labelStyle = new GUIStyle();
            _labelStyle.fontSize = 18;
            _labelStyle.normal.textColor = new Color(0.85f, 0.75f, 0.45f);
            _labelStyle.alignment = TextAnchor.MiddleLeft;

            _messageStyle = new GUIStyle();
            _messageStyle.fontSize = 17;
            _messageStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            _messageStyle.alignment = TextAnchor.MiddleCenter;
            _messageStyle.wordWrap = true;
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
    }

    [NoPrefab]
    public class VGearSetsUI : View<GearSetsUI>
    {
        private static readonly Color BgDark = new Color(0.08f, 0.08f, 0.10f, 0.95f);
        private static readonly Color PanelBg = new Color(0.12f, 0.12f, 0.15f, 0.90f);
        private static readonly Color BtnNormal = new Color(0.18f, 0.18f, 0.22f, 1f);
        private static readonly Color BtnHover = new Color(0.25f, 0.25f, 0.30f, 1f);
        private static readonly Color BtnSelected = new Color(0.30f, 0.55f, 0.45f, 1f);
        private static readonly Color AccentGold = new Color(0.85f, 0.75f, 0.45f, 1f);
        private static readonly Color TextDim = new Color(0.55f, 0.55f, 0.58f, 1f);
        private static readonly Color TextLight = new Color(0.88f, 0.88f, 0.90f, 1f);
        private static readonly Color DividerColor = new Color(0.25f, 0.25f, 0.30f, 0.6f);

        internal Transform setListContent;
        internal TextMeshProUGUI detailTitle;
        internal Transform detailContent; // parent for dynamically created table rows
        internal Transform detailLeftCol;
        internal Transform detailRightCol;
        internal TextMeshProUGUI detailTimestamp;
        internal TextMeshProUGUI statusText;
        internal NameInputDialog nameDialog;
        internal Button saveBtn;
        internal Button updateBtn;
        internal Button loadBtn;
        internal Button deleteBtn;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            var rt = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            var bg = gameObject.AddComponent<Image>();
            bg.color = BgDark;

            BuildTitle();
            BuildLeftPanel();
            BuildRightPanel();
            BuildStatusBar();
        }

        private void BuildTitle()
        {
            var titleObj = CreateChild("Title", transform,
                new Vector2(0f, 0.92f), new Vector2(1f, 1f));
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "GEAR SETS";
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = AccentGold;

            var divider = CreateChild("Divider", transform,
                new Vector2(0.05f, 0.915f), new Vector2(0.95f, 0.918f));
            var divImg = divider.AddComponent<Image>();
            divImg.color = DividerColor;
        }

        private void BuildLeftPanel()
        {
            var panel = CreateChild("LeftPanel", transform,
                new Vector2(0.02f, 0.08f), new Vector2(0.32f, 0.91f));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = PanelBg;

            var header = CreateChild("ListHeader", panel.transform,
                new Vector2(0f, 0.94f), new Vector2(1f, 1f));
            var headerText = header.AddComponent<TextMeshProUGUI>();
            headerText.text = "  Saved Sets";
            headerText.fontSize = 18;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.MidlineLeft;
            headerText.color = TextLight;

            var scrollArea = CreateChild("ScrollArea", panel.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0.93f));

            var scrollRect = scrollArea.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            var viewport = CreateChild("Viewport", scrollArea.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f));
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();

            var content = CreateChild("Content", viewport.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.sizeDelta = new Vector2(0f, 0f);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;
            setListContent = content.transform;
        }

        private void BuildRightPanel()
        {
            var panel = CreateChild("RightPanel", transform,
                new Vector2(0.34f, 0.08f), new Vector2(0.98f, 0.91f));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = PanelBg;

            detailTitle = CreateChild("DetailTitle", panel.transform,
                new Vector2(0.03f, 0.88f), new Vector2(0.97f, 0.97f))
                .AddComponent<TextMeshProUGUI>();
            detailTitle.text = "Select a set";
            detailTitle.fontSize = 22;
            detailTitle.fontStyle = FontStyles.Bold;
            detailTitle.alignment = TextAlignmentOptions.MidlineLeft;
            detailTitle.color = AccentGold;

            detailTimestamp = CreateChild("DetailTimestamp", panel.transform,
                new Vector2(0.03f, 0.82f), new Vector2(0.97f, 0.88f))
                .AddComponent<TextMeshProUGUI>();
            detailTimestamp.text = "";
            detailTimestamp.fontSize = 14;
            detailTimestamp.alignment = TextAlignmentOptions.MidlineLeft;
            detailTimestamp.color = TextDim;

            var slotDivider = CreateChild("SlotDivider", panel.transform,
                new Vector2(0.03f, 0.81f), new Vector2(0.97f, 0.813f));
            slotDivider.AddComponent<Image>().color = DividerColor;

            var slotsScroll = CreateChild("SlotsScroll", panel.transform,
                new Vector2(0.03f, 0.18f), new Vector2(0.97f, 0.80f));
            var sr = slotsScroll.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 20f;

            var vp = CreateChild("Viewport", slotsScroll.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f));
            vp.AddComponent<RectMask2D>();
            sr.viewport = vp.GetComponent<RectTransform>();

            var content = CreateChild("Content", vp.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.sizeDelta = new Vector2(0f, 0f);
            var hlg2 = content.AddComponent<HorizontalLayoutGroup>();
            hlg2.spacing = 12f;
            hlg2.padding = new RectOffset(8, 8, 4, 4);
            hlg2.childForceExpandWidth = true;
            hlg2.childForceExpandHeight = false;
            hlg2.childControlWidth = true;
            hlg2.childControlHeight = true;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = contentRT;

            detailContent = content.transform;

            // Left column
            var leftCol = new GameObject("LeftCol");
            leftCol.transform.SetParent(content.transform, false);
            var leftLE = leftCol.AddComponent<LayoutElement>();
            leftLE.flexibleWidth = 1f;
            var leftVLG = leftCol.AddComponent<VerticalLayoutGroup>();
            leftVLG.spacing = 2f;
            leftVLG.childForceExpandWidth = true;
            leftVLG.childForceExpandHeight = false;
            leftVLG.childControlWidth = true;
            leftVLG.childControlHeight = true;
            detailLeftCol = leftCol.transform;

            // Right column
            var rightCol = new GameObject("RightCol");
            rightCol.transform.SetParent(content.transform, false);
            var rightLE = rightCol.AddComponent<LayoutElement>();
            rightLE.flexibleWidth = 1f;
            var rightVLG = rightCol.AddComponent<VerticalLayoutGroup>();
            rightVLG.spacing = 2f;
            rightVLG.childForceExpandWidth = true;
            rightVLG.childForceExpandHeight = false;
            rightVLG.childControlWidth = true;
            rightVLG.childControlHeight = true;
            detailRightCol = rightCol.transform;

            BuildActionButtons(panel.transform);

            nameDialog = gameObject.AddComponent<NameInputDialog>();
        }

        private void BuildActionButtons(Transform parent)
        {
            var btnRow = CreateChild("ButtonRow", parent,
                new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.15f));
            var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.padding = new RectOffset(4, 4, 4, 4);
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            saveBtn = CreateButton(btnRow.transform, "SaveBtn", "SAVE CURRENT", AccentGold);
            updateBtn = CreateButton(btnRow.transform, "UpdateBtn", "UPDATE", new Color(0.55f, 0.78f, 0.55f, 1f));
            loadBtn = CreateButton(btnRow.transform, "LoadBtn", "LOAD SET", new Color(0.45f, 0.65f, 0.85f, 1f));
            deleteBtn = CreateButton(btnRow.transform, "DeleteBtn", "DELETE", new Color(0.75f, 0.35f, 0.35f, 1f));
        }

        private void BuildStatusBar()
        {
            var bar = CreateChild("StatusBar", transform,
                new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.06f));
            statusText = bar.AddComponent<TextMeshProUGUI>();
            statusText.text = "";
            statusText.fontSize = 14;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = TextDim;
        }

        private Button CreateButton(Transform parent, string name, string label, Color textColor)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            var le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 36f;
            le.preferredHeight = 40f;

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = BtnNormal;

            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = BtnNormal;
            colors.highlightedColor = BtnHover;
            colors.pressedColor = BtnSelected;
            colors.disabledColor = new Color(0.12f, 0.12f, 0.14f, 0.5f);
            btn.colors = colors;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;

            return btn;
        }

        internal void ClearDetailContent()
        {
            ClearChildren(detailLeftCol);
            ClearChildren(detailRightCol);
        }

        private void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            var children = new List<GameObject>();
            foreach (Transform child in parent)
                children.Add(child.gameObject);
            foreach (var child in children)
                UnityEngine.Object.DestroyImmediate(child);
        }

        internal void AddSectionHeader(string text, Transform target)
        {
            var row = new GameObject("Header");
            row.transform.SetParent(target, false);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 28f;
            le.preferredHeight = 28f;
            var txt = row.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.fontSize = 15;
            txt.color = AccentGold;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.richText = true;
        }

        internal void AddTableRow(string label, string value, Transform target)
        {
            var row = new GameObject("Row");
            row.transform.SetParent(target, false);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 22f;
            le.preferredHeight = 22f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 8f;

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            var labelLE = labelObj.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 130;
            labelLE.flexibleWidth = 0;
            var labelTxt = labelObj.AddComponent<TextMeshProUGUI>();
            labelTxt.text = label;
            labelTxt.fontSize = 14;
            labelTxt.color = new Color(0.78f, 0.7f, 0.45f);
            labelTxt.alignment = TextAlignmentOptions.MidlineLeft;

            var valueObj = new GameObject("Value");
            valueObj.transform.SetParent(row.transform, false);
            var valueLE = valueObj.AddComponent<LayoutElement>();
            valueLE.flexibleWidth = 1f;
            var valueTxt = valueObj.AddComponent<TextMeshProUGUI>();
            valueTxt.text = value;
            valueTxt.fontSize = 14;
            valueTxt.color = TextLight;
            valueTxt.alignment = TextAlignmentOptions.MidlineLeft;
        }

        internal void AddSubRow(string label, string value, Transform target)
        {
            var row = new GameObject("SubRow");
            row.transform.SetParent(target, false);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 20f;
            le.preferredHeight = 20f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 8f;
            hlg.padding = new RectOffset(16, 0, 0, 0);

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            var labelLE = labelObj.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 114;
            labelLE.flexibleWidth = 0;
            var labelTxt = labelObj.AddComponent<TextMeshProUGUI>();
            labelTxt.text = label;
            labelTxt.fontSize = 13;
            labelTxt.color = TextDim;
            labelTxt.alignment = TextAlignmentOptions.MidlineLeft;

            var valueObj = new GameObject("Value");
            valueObj.transform.SetParent(row.transform, false);
            var valueLE = valueObj.AddComponent<LayoutElement>();
            valueLE.flexibleWidth = 1f;
            var valueTxt = valueObj.AddComponent<TextMeshProUGUI>();
            valueTxt.text = value;
            valueTxt.fontSize = 13;
            valueTxt.color = TextDim;
            valueTxt.alignment = TextAlignmentOptions.MidlineLeft;
        }

        internal void AddSpacer(Transform target, float height = 8f)
        {
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(target, false);
            var le = spacer.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }

        internal Button CreateSetListEntry(string setName)
        {
            var entryObj = new GameObject("SetEntry_" + setName);
            entryObj.transform.SetParent(setListContent, false);
            var le = entryObj.AddComponent<LayoutElement>();
            le.minHeight = 34f;
            le.preferredHeight = 36f;

            var entryImg = entryObj.AddComponent<Image>();
            entryImg.color = BtnNormal;

            var btn = entryObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = BtnNormal;
            colors.highlightedColor = BtnHover;
            colors.pressedColor = BtnSelected;
            btn.colors = colors;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(entryObj.transform, false);
            var textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.05f, 0f);
            textRT.anchorMax = new Vector2(0.95f, 1f);
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = setName;
            tmp.fontSize = 15;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = TextLight;

            return btn;
        }

        internal void SetSelectedEntryHighlight(string selectedName)
        {
            if (setListContent == null) return;
            foreach (Transform child in setListContent)
            {
                var img = child.GetComponent<Image>();
                if (img == null) continue;
                bool isSelected = child.gameObject.name == "SetEntry_" + selectedName;
                img.color = isSelected ? BtnSelected : BtnNormal;
            }
        }

        private static GameObject CreateChild(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2? pivot = null)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return obj;
        }
    }

    public class GearSetsUI : CharacterSheetTab<VGearSetsUI>
    {
        private static readonly BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("GearSetsMod");

        private VGearSetsUI _view;
        private GearSet _selectedSet;
        private List<GearSet> _allSets = new List<GearSet>();

        /// <summary>
        /// Tracks item GUIDs that were pulled from the stash during the last set load.
        /// When loading a new set, items in this list that aren't needed by the new set
        /// are returned to the stash.
        /// </summary>
        private static List<string> _lastStashPulledGuids = new List<string>();

        protected override void AfterViewSpawned(VGearSetsUI view)
        {
            _view = view;
            _selectedSet = null;

            view.saveBtn.onClick.AddListener(OnSaveClicked);
            view.updateBtn.onClick.AddListener(OnUpdateClicked);
            view.loadBtn.onClick.AddListener(OnLoadClicked);
            view.deleteBtn.onClick.AddListener(OnDeleteClicked);

            RefreshSetList();
            UpdateDetailPanel();
        }

        private void RefreshSetList()
        {
            Log.LogInfo($"[RefreshSetList] called. _view={_view != null}, setListContent={_view?.setListContent != null}");
            if (_view == null || _view.setListContent == null) return;

            var children = new List<GameObject>();
            foreach (Transform child in _view.setListContent)
                children.Add(child.gameObject);
            Log.LogInfo($"[RefreshSetList] destroying {children.Count} old entries");
            foreach (var child in children)
                UnityEngine.Object.DestroyImmediate(child);

            Log.LogInfo($"[RefreshSetList] ConfigPath={SetManager.ConfigPath}");
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

            Log.LogInfo($"[RefreshSetList] found {_allSets.Count} sets");
            foreach (var set in _allSets)
            {
                Log.LogInfo($"[RefreshSetList] creating entry for: {set.Name}");
                var btn = _view.CreateSetListEntry(set.Name);
                var captured = set;
                btn.onClick.AddListener(() => OnSetSelected(captured));
            }

            // Force layout rebuild after adding entries
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

        private static readonly Dictionary<string, string> TreeNameMap = new Dictionary<string, string>
        {
            {"TalentTree_Str", "Strength"}, {"TalentTree_Dex", "Dexterity"},
            {"TalentTree_End", "Endurance"}, {"TalentTree_Per", "Perception"},
            {"TalentTree_Pra", "Practicality"}, {"TalentTree_Spi", "Spirituality"},
            {"TalentTree_WyrdArthur", "King's Soul"}, {"TalentTree_RedDeath", "Red Death"},
            {"WyrdArthur", "King's Soul"}, {"Wyrd Arthur", "King's Soul"},
        };

        private static readonly string[] AttributeTreeKeys =
            { "TalentTree_Str", "TalentTree_Dex", "TalentTree_End", "TalentTree_Per", "TalentTree_Pra", "TalentTree_Spi" };

        private static readonly string[] AttributeNames =
            { "Strength", "Dexterity", "Endurance", "Perception", "Practicality", "Spirituality" };

        private static string FriendlyTreeName(string raw)
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

        private void UpdateDetailPanel()
        {
            if (_view == null) return;

            if (_selectedSet == null)
            {
                _view.detailTitle.text = "Select a set";
                _view.detailTimestamp.text = "";
                _view.ClearDetailContent();
                _view.AddSectionHeader("Save your current equipment to create a gear set,\nor select an existing set from the list.", _view.detailLeftCol);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_view.detailLeftCol.GetComponent<RectTransform>());
                LayoutRebuilder.ForceRebuildLayoutImmediate(_view.detailRightCol.GetComponent<RectTransform>());
                LayoutRebuilder.ForceRebuildLayoutImmediate(_view.detailContent.GetComponent<RectTransform>());
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
                LayoutRebuilder.ForceRebuildLayoutImmediate(_view.detailLeftCol.GetComponent<RectTransform>());
                LayoutRebuilder.ForceRebuildLayoutImmediate(_view.detailRightCol.GetComponent<RectTransform>());
                LayoutRebuilder.ForceRebuildLayoutImmediate(_view.detailContent.GetComponent<RectTransform>());
                return;
            }

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
                {
                    armorEntries.Add(kvp);
                }
                else if (AccessorySlotNames.Contains(kvp.Key))
                {
                    accessoryEntries.Add(kvp);
                }
                else if (QuickSlotNames.Contains(kvp.Key))
                {
                    quickSlotEntries.Add(kvp);
                }
                else
                {
                    armorEntries.Add(kvp);
                }
            }

            var left = _view.detailLeftCol;
            var right = _view.detailRightCol;

            foreach (var group in loadoutGroups)
            {
                _view.AddSectionHeader($"── Weapon {group.Key + 1} ──", left);
                foreach (var entry in group.Value)
                {
                    if (string.IsNullOrEmpty(entry.Value)) continue;
                    _view.AddTableRow(PrettySlotName(entry.Key), ResolveItemName(entry.Value), left);
                }
                _view.AddSpacer(left);
            }

            if (armorEntries.Count > 0)
            {
                _view.AddSectionHeader("── Armor ──", right);
                foreach (var kvp in armorEntries)
                {
                    if (string.IsNullOrEmpty(kvp.Value)) continue;
                    _view.AddTableRow(PrettySlotName(kvp.Key), ResolveItemName(kvp.Value), right);
                }
                _view.AddSpacer(right);
            }

            if (accessoryEntries.Count > 0)
            {
                _view.AddSectionHeader("── Accessories ──", right);
                foreach (var kvp in accessoryEntries)
                {
                    if (string.IsNullOrEmpty(kvp.Value)) continue;
                    _view.AddTableRow(PrettySlotName(kvp.Key), ResolveItemName(kvp.Value), right);
                }
                _view.AddSpacer(right);
            }

            if (quickSlotEntries.Count > 0)
            {
                _view.AddSectionHeader("── Quick Slots ──", right);
                foreach (var kvp in quickSlotEntries)
                {
                    if (string.IsNullOrEmpty(kvp.Value)) continue;
                    _view.AddTableRow(PrettySlotName(kvp.Key), ResolveItemName(kvp.Value), right);
                }
                _view.AddSpacer(right);
            }

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
                    string displayName = FriendlyTreeName(tree.Key);
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

                var set = CaptureCurrentState(name);
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
                string statusMsg = ApplySet(_selectedSet);
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
                var set = CaptureCurrentState(name);
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

        private static GearSet CaptureCurrentState(string name)
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

                        string slotName = GetRichEnumName(slot);
                        string key = $"Loadout{loadoutIdx}_{slotName}";
                        set.SlotToItemGuid[key] = guid;
                    }

                    foreach (var loadoutSlotType in EquipmentSlotType.Loadouts)
                    {
                        string slotName = GetRichEnumName(loadoutSlotType);
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
                    string slotName = GetRichEnumName(slotType);
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

        private static readonly string[] AttributePrefixes =
            { "Dexterity", "Endurance", "Perception", "Practicality", "Spirituality", "Strength", "KingPower", "Wyrdskill" };

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
                        string subtreeName = GetRichEnumName(talent.TalentTreeBranchType);

                        foreach (var prefix in AttributePrefixes)
                        {
                            if (subtreeName.StartsWith(prefix))
                            {
                                subtreeName = subtreeName.Substring(prefix.Length);
                                break;
                            }
                        }
                        subtreeName = PrettySlotName(subtreeName);

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

                Log.LogInfo($"[CaptureTalents] Captured {set.TalentLevels.Count} talent levels across {set.TalentTrees.Count} trees");
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
                    Log.LogInfo($"[CaptureRpgStats] Hero level: {set.HeroLevel}");
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
                                if (val is float f) { set.RpgStats[name] = f; Log.LogInfo($"[CaptureRpgStats] {name} = {f} (via {valProp})"); break; }
                                if (val is int i) { set.RpgStats[name] = i; Log.LogInfo($"[CaptureRpgStats] {name} = {i} (via {valProp})"); break; }
                                if (val is double d) { set.RpgStats[name] = (float)d; Log.LogInfo($"[CaptureRpgStats] {name} = {d} (via {valProp})"); break; }
                                Log.LogInfo($"[CaptureRpgStats] {name}.{valProp} type={val?.GetType().Name}, value={val}");
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
                Log.LogInfo($"[CaptureRpgStats] Captured {set.RpgStats.Count} RPG stats (BaseValue): {string.Join(", ", set.RpgStats.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[CaptureRpgStats] Failed: {ex.Message}");
            }
        }

        private static string GetRichEnumName(object richEnum)
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

        private static string PrettySlotName(string raw)
        {
            var map = new Dictionary<string, string>
            {
                {"MainHand", "Main Hand"}, {"OffHand", "Off Hand"},
                {"AdditionalMainHand", "Main Hand"}, {"AdditionalOffHand", "Off Hand"},
                {"Ring1", "Ring 1"}, {"Ring2", "Ring 2"},
                {"FoodQuickSlot", "Food Slot"}, {"QuickSlot2", "Quick Slot 2"}, {"QuickSlot3", "Quick Slot 3"},
                {"HorseArmor", "Horse Armor"},
            };
            if (map.TryGetValue(raw, out var pretty)) return pretty;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                    sb.Append(' ');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        private static string ApplySet(GearSet set)
        {
            var hero = Hero.Current;
            if (hero == null) return "No hero available.";

            var heroItems = hero.HeroItems;
            if (heroItems == null) return "No hero inventory available.";

            int stashPulledCount = 0;
            int stashReturnedCount = 0;
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
            if (_lastStashPulledGuids.Count > 0)
            {
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
                                Log.LogInfo($"[ApplySet] Keeping stash-pulled item {guid} — needed by new set");
                                continue;
                            }

                            // Find the item in inventory and return it to stash
                            var itemToReturn = heroItems.Items.FirstOrDefault(i => i.Template?.GUID == guid);
                            if (itemToReturn != null)
                            {
                                try
                                {
                                    var returned = itemToReturn.MoveTo(storage);
                                    if (returned != null)
                                    {
                                        stashReturnedCount++;
                                        Log.LogInfo($"[ApplySet] Returned '{itemToReturn.DisplayName}' to stash");
                                    }
                                    else
                                    {
                                        Log.LogWarning($"[ApplySet] MoveTo(stash) returned null for '{itemToReturn.DisplayName}'");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.LogWarning($"[ApplySet] Failed to return item {guid} to stash: {ex.Message}");
                                }
                            }
                            else
                            {
                                Log.LogInfo($"[ApplySet] Previously pulled item {guid} no longer in inventory (sold/consumed?)");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[ApplySet] Failed to return items to stash: {ex.Message}");
                }

                _lastStashPulledGuids.Clear();
            }

            var newPulledGuids = new List<string>();

            try
            {
                foreach (var kvp in set.SlotToItemGuid)
                {
                    try
                    {
                        if (kvp.Key.StartsWith("Loadout") && kvp.Key.Contains("_"))
                        {
                            var underscoreIdx = kvp.Key.IndexOf('_');
                            var idxStr = kvp.Key.Substring(7, underscoreIdx - 7);
                            var slotName = kvp.Key.Substring(underscoreIdx + 1);
                            if (!int.TryParse(idxStr, out int loadoutIdx)) continue;

                            var slotType = FindSlotByName(slotName);
                            if (slotType == null) continue;

                            var loadout = heroItems.LoadoutAt(loadoutIdx) as HeroLoadout;
                            if (loadout == null) continue;

                            if (string.IsNullOrEmpty(kvp.Value))
                            {
                                loadout.EquipItem(slotType, null);
                                continue;
                            }

                            var item = FindItemInInventoryOrStash(heroItems, ref storage, ref storageRequested, hero, kvp.Value, ref stashPulledCount, newPulledGuids);
                            if (item == null)
                            {
                                missingCount++;
                                missingItems.Add(kvp.Key);
                                continue;
                            }

                            loadout.EquipItem(slotType, item);
                        }
                        else
                        {
                            var slotType = FindSlotByName(kvp.Key);
                            if (slotType == null) continue;

                            if (string.IsNullOrEmpty(kvp.Value))
                            {
                                CharacterInventoryExtension.Unequip(heroItems, slotType);
                                continue;
                            }

                            var item = FindItemInInventoryOrStash(heroItems, ref storage, ref storageRequested, hero, kvp.Value, ref stashPulledCount, newPulledGuids);
                            if (item == null)
                            {
                                missingCount++;
                                missingItems.Add(kvp.Key);
                                continue;
                            }

                            var currentLoadout = heroItems.CurrentLoadout as HeroLoadout;
                            if (currentLoadout != null)
                                currentLoadout.EquipItem(slotType, item);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"[ApplySet] Failed to equip {kvp.Key}: {ex.Message}");
                    }
                }
            }
            finally
            {
                // Always release stash items if we requested them
                if (storageRequested && storage != null)
                {
                    try
                    {
                        storage.ReleaseItems();
                        Log.LogInfo("[ApplySet] Released stash items");
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"[ApplySet] Failed to release stash items: {ex.Message}");
                    }
                }
            }

            ApplyTalents(hero, set);
            ApplyRpgStats(hero, set);
            RecalculateStats(hero);

            // Track which items were pulled from stash for this load
            _lastStashPulledGuids = newPulledGuids;

            // Build status message
            var statusParts = new List<string>();
            statusParts.Add("Loaded: " + set.Name);

            if (stashReturnedCount > 0)
                statusParts.Add($"{stashReturnedCount} item(s) returned to stash");
            if (stashPulledCount > 0)
                statusParts.Add($"{stashPulledCount} item(s) pulled from stash");
            if (missingCount > 0)
                statusParts.Add($"{missingCount} item(s) not found");

            // Level-up point surplus info
            if (set.HeroLevel > 0)
            {
                try
                {
                    int currentLevel = (int)hero.CharacterStats.Level.BaseValue;
                    if (currentLevel > set.HeroLevel)
                    {
                        int levelDiff = currentLevel - set.HeroLevel;
                        statusParts.Add($"{levelDiff} level(s) gained since save — extra points unspent");
                    }
                }
                catch { }
            }

            if (set.Version < 2)
                statusParts.Add("(v1 set — re-save for accuracy)");

            return string.Join(". ", statusParts) + ".";
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
                        Log.LogInfo("[FindItem] HeroStorage not available");
                        return null;
                    }
                }

                if (!storageRequested)
                {
                    storage.RequestItems();
                    storageRequested = true;
                    Log.LogInfo("[FindItem] Materialized stash items for search");
                }

                var stashItem = storage.Items?.FirstOrDefault(i => i.Template?.GUID == itemGuid);
                if (stashItem == null) return null;

                // Move from stash to hero inventory
                Log.LogInfo($"[FindItem] Found item '{stashItem.DisplayName}' in stash, moving to inventory");
                var movedItem = stashItem.MoveTo(heroItems);
                if (movedItem != null)
                {
                    stashPulledCount++;
                    stashPulledGuids.Add(itemGuid);
                    Log.LogInfo($"[FindItem] Successfully moved '{movedItem.DisplayName}' from stash to inventory");
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
                Log.LogInfo("[ApplyTalents] No talent levels to restore");
                return;
            }

            var heroTalents = hero.Talents;
            if (heroTalents == null)
            {
                Log.LogWarning("[ApplyTalents] hero.Talents is null");
                return;
            }

            Log.LogInfo($"[ApplyTalents] Restoring {set.TalentLevels.Count} talent levels");

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
                Log.LogInfo($"[ApplyTalents] Reset {resetCount} talents");

                // Phase 2: Apply saved levels (ordered by tree level requirement)
                int appliedCount = 0;
                int failedCount = 0;
                foreach (TalentTable table in heroTalents.Elements<TalentTable>())
                {
                    // Collect talents that need levels, sorted by required tree level
                    var toApply = new List<(Talent talent, string name, int target)>();
                    foreach (Talent talent in table.talents)
                    {
                        string talentName = talent.Template?.name ?? "";
                        if (string.IsNullOrEmpty(talentName)) continue;
                        if (!set.TalentLevels.TryGetValue(talentName, out int targetLevel)) continue;
                        if (targetLevel <= 0) continue;
                        toApply.Add((talent, talentName, targetLevel));
                    }

                    // Sort by RequiredTreeLevelToUnlock (parents first)
                    toApply.Sort((a, b) => a.talent.RequiredTreeLevelToUnlock.CompareTo(b.talent.RequiredTreeLevelToUnlock));

                    Log.LogInfo($"[ApplyTalents] Tree '{table.TreeTemplate?.name}': {toApply.Count} talents to restore");

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
                            Log.LogInfo($"[ApplyTalents] Applied {targetLevel} levels to '{talentName}'");
                        }
                    }
                }

                Log.LogInfo($"[ApplyTalents] Complete: {appliedCount} talents applied, {failedCount} failed");
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
                Log.LogInfo("[ApplyRpgStats] No RPG stats to restore");
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
                Log.LogInfo($"[ApplyRpgStats] Restoring {set.RpgStats.Count} RPG stats (v{set.Version}). HeroRPGStats type: {rpgType.Name}");

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

                        float targetBaseValue = kvp.Value;

                        // v1 migration: saved value was Modified (includes gear/talent bonuses)
                        // Approximate BaseValue by subtracting the modifier delta
                        if (isV1)
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

                                // modifiers = currentMod - currentBase (gear, talents, buffs)
                                // savedModified = savedBase + modifiers (approximately)
                                // so savedBase ≈ savedModified - modifiers = savedModified - (currentMod - currentBase)
                                float modifiers = currentMod - currentBase;
                                targetBaseValue = kvp.Value - modifiers;
                                if (targetBaseValue < 0) targetBaseValue = 0;

                                Log.LogInfo($"[ApplyRpgStats] v1 migration for {kvp.Key}: savedModified={kvp.Value}, currentBase={currentBase}, currentMod={currentMod}, modifiers={modifiers}, approxBase={targetBaseValue}");
                            }
                            catch (Exception ex)
                            {
                                Log.LogWarning($"[ApplyRpgStats] v1 migration failed for {kvp.Key}, using raw value: {ex.Message}");
                            }
                        }

                        // Use Stat.SetTo() to directly set the base value
                        // This is the key fix from KitsuneRhin's discovery - IncreaseBy/DecreaseBy
                        // modified the wrong value and RecalculateAllStats undid everything
                        if (statObj is Stat stat)
                        {
                            float oldBase = stat.BaseValue;
                            stat.SetTo(targetBaseValue, false, null);
                            Log.LogInfo($"[ApplyRpgStats] {kvp.Key}: SetTo({targetBaseValue}) [was {oldBase}]");
                        }
                        else
                        {
                            Log.LogWarning($"[ApplyRpgStats] {kvp.Key}: stat object is not a Stat (type={statObj.GetType().Name}), falling back to reflection");
                            // Fallback: try SetTo via reflection
                            var setToMethod = statObj.GetType().GetMethod("SetTo", new[] { typeof(float), typeof(bool), typeof(object) });
                            if (setToMethod != null)
                            {
                                setToMethod.Invoke(statObj, new object[] { targetBaseValue, false, null });
                                Log.LogInfo($"[ApplyRpgStats] {kvp.Key}: SetTo({targetBaseValue}) via reflection");
                            }
                            else
                            {
                                Log.LogWarning($"[ApplyRpgStats] {kvp.Key}: SetTo method not found. Available methods: {string.Join(", ", statObj.GetType().GetMethods().Select(m => m.Name))}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"[ApplyRpgStats] Failed for {kvp.Key}: {ex.Message}");
                    }
                }

                if (isV1)
                    Log.LogInfo("[ApplyRpgStats] v1 set loaded with approximate migration. Re-save the set for accurate BaseValue storage.");

                Log.LogInfo("[ApplyRpgStats] RPG stat restoration complete");
            }
            catch (Exception ex)
            {
                Log.LogError($"[ApplyRpgStats] Failed: {ex}");
            }
        }

        private static void RecalculateStats(Hero hero)
        {
            try
            {
                Log.LogInfo("[RecalculateStats] Starting stat recalculation");

                // IMPORTANT: Do NOT call HeroRPGStats.RecalculateAllStats() here.
                // As discovered by KitsuneRhin, it resets stats from an internal wrapper,
                // undoing any changes made by SetTo() in ApplyRpgStats.
                // Instead, recalculate the downstream stat systems that derive from RPG stats.

                try
                {
                    hero.HeroStats.RecalculateAllStats(false);
                    Log.LogInfo("[RecalculateStats] HeroStats.RecalculateAllStats done");
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[RecalculateStats] HeroStats.RecalculateAllStats failed: {ex.Message}");
                }

                try
                {
                    int level = (int)hero.CharacterStats.Level.BaseValue;
                    hero.CharacterStats.RecalculateAllStats(level, level, false);
                    Log.LogInfo($"[RecalculateStats] CharacterStats.RecalculateAllStats done (level={level})");
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[RecalculateStats] CharacterStats.RecalculateAllStats failed: {ex.Message}");
                }

                // HeroMultStats if available
                try
                {
                    var heroType = hero.GetType();
                    var multProp = heroType.GetProperty("HeroMultStats", BindingFlags.Instance | BindingFlags.Public);
                    if (multProp != null)
                    {
                        var multStats = multProp.GetValue(hero);
                        if (multStats != null)
                        {
                            var recalcMethod = multStats.GetType().GetMethods()
                                .FirstOrDefault(m => m.Name == "RecalculateAllStats");
                            if (recalcMethod != null)
                            {
                                var parms = recalcMethod.GetParameters();
                                if (parms.Length == 1 && parms[0].ParameterType == typeof(bool))
                                    recalcMethod.Invoke(multStats, new object[] { false });
                                else if (parms.Length == 0)
                                    recalcMethod.Invoke(multStats, null);
                                Log.LogInfo("[RecalculateStats] HeroMultStats.RecalculateAllStats done");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[RecalculateStats] HeroMultStats recalc failed: {ex.Message}");
                }

                Log.LogInfo("[RecalculateStats] All stat recalculation complete");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[RecalculateStats] Failed: {ex.Message}");
            }
        }

        private static EquipmentSlotType FindSlotByName(string name)
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

        private static string ResolveItemName(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "(empty)";

            try
            {
                var hero = Hero.Current;
                if (hero == null) return guid;

                var item = hero.HeroItems?.Items
                    .FirstOrDefault(i => i.Template?.GUID == guid);

                if (item != null) return item.DisplayName;
            }
            catch { }

            return guid;
        }
    }
}
