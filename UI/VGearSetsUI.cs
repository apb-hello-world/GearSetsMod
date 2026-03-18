using System.Collections.Generic;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Attributes;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GearSetsMod.UI
{
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
        internal Button resetBtn;

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
            resetBtn = CreateButton(btnRow.transform, "ResetBtn", "RESET BUILD", new Color(0.85f, 0.55f, 0.25f, 1f));
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
                Object.DestroyImmediate(child);
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
}
