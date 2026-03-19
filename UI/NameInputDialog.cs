using System;
using System.Collections.Generic;
using GearSetsMod.Patches;
using UnityEngine;

namespace GearSetsMod.UI
{
    public enum ModalMode { TextInput, Confirm, PointEditor }

    public class NameInputDialog : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public string Result { get; private set; }
        public ModalMode Mode { get; private set; }
        public Action<string> OnConfirm;

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

        private List<PointEntry> _pointEntries;
        private Action<Dictionary<string, float>> _onPointsConfirm;
        private Vector2 _pointScrollPos;

        public class PointEntry
        {
            public string Key;   // internal identifier for matching
            public string Label; // display text shown to the user
            public float OriginalValue;
            public string EditText;
        }

        public void Open()
        {
            OpenTextInput("", OnConfirm);
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

        public void OpenPointEditor(List<PointEntry> entries, Action<Dictionary<string, float>> onConfirm)
        {
            _pointEntries = entries;
            _onPointsConfirm = onConfirm;
            _message = "Edit Point Values";
            Mode = ModalMode.PointEditor;
            IsOpen = true;
            _focusField = false;
            _pointScrollPos = Vector2.zero;
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
                    else if (Mode == ModalMode.Confirm)
                    {
                        _onConfirmAction?.Invoke();
                    }
                    else if (Mode == ModalMode.PointEditor)
                    {
                        CommitPointEdits();
                    }
                    Close();
                    Event.current.Use();
                    return;
                }
            }

            InitStyles();

            float w, h;
            if (Mode == ModalMode.PointEditor)
            {
                w = 520;
                int rowCount = _pointEntries != null ? _pointEntries.Count : 0;
                h = Mathf.Min(80 + rowCount * 30 + 60, 420);
            }
            else
            {
                w = 420;
                h = Mode == ModalMode.TextInput ? 150 : 130;
            }
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
            else if (Mode == ModalMode.PointEditor)
            {
                GUILayout.Label(_message, _labelStyle);
                GUILayout.Space(6);

                _pointScrollPos = GUILayout.BeginScrollView(_pointScrollPos,
                    GUILayout.ExpandHeight(true));

                if (_pointEntries != null)
                {
                    foreach (var entry in _pointEntries)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(entry.Label, _messageStyle,
                            GUILayout.Width(280), GUILayout.Height(26));
                        entry.EditText = GUILayout.TextField(entry.EditText, 10,
                            _fieldStyle, GUILayout.Height(26), GUILayout.Width(80));
                        float orig = entry.OriginalValue;
                        if (float.TryParse(entry.EditText, out float cur) && cur != orig)
                            GUILayout.Label($"(was {orig:F0})", _labelStyle,
                                GUILayout.Width(90), GUILayout.Height(26));
                        else
                            GUILayout.Label("", GUILayout.Width(90), GUILayout.Height(26));
                        GUILayout.EndHorizontal();
                    }
                }

                GUILayout.EndScrollView();

                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply", _buttonStyle, GUILayout.Height(30)))
                {
                    CommitPointEdits();
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

        private void CommitPointEdits()
        {
            if (_pointEntries == null || _onPointsConfirm == null) return;
            var changes = new Dictionary<string, float>();
            foreach (var entry in _pointEntries)
            {
                if (float.TryParse(entry.EditText, out float newVal) && newVal != entry.OriginalValue)
                    changes[entry.Key] = newVal;
            }
            if (changes.Count > 0)
                _onPointsConfirm(changes);
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
}
