using System;
using UnityEditor;
using UnityEngine;

namespace OxenteGames.OxOptimizer
{
    /// <summary>
    /// Shared UI kit for all OxOptimizer tabs. Styles are cached so OnGUI doesn't
    /// allocate a new GUIStyle on every repaint.
    /// </summary>
    public static class OxGui
    {

        public static readonly Color Accent = new Color32(7, 236, 137, 255);
        public static readonly Color Dark = new Color32(15, 36, 58, 255);
        public static readonly Color Pass = new Color32(93, 201, 103, 255);
        public static readonly Color Fail = new Color32(229, 77, 66, 255);
        // Warning (orange): a value that isn't ideal but isn't critical either.
        public static readonly Color Warning = new Color32(240, 173, 78, 255);
        // Pending: checks that can't be evaluated yet (they need the Build Logs analysis).
        public static readonly Color Pending = new Color32(120, 128, 138, 255);

        /// <summary>
        /// How far a value is from the recommended setting. Drives the cell text color in the
        /// analysis tables: Ok = white (ideal), Warning = orange, Bad = red (far from ideal).
        /// </summary>
        public enum Grade { Ok, Warning, Bad }

        public static Color GradeColor(Grade grade)
        {
            switch (grade)
            {
                case Grade.Bad:
                    return Fail;
                case Grade.Warning:
                    return Warning;
                default:
                    return Color.white;
            }
        }

        private static GUIStyle _cellStyle;

        /// <summary>Draws a table cell whose text color reflects how close the value is to the ideal.</summary>
        public static void GradedLabel(Rect rect, string text, Grade grade)
        {
            if (_cellStyle == null)
                _cellStyle = new GUIStyle(EditorStyles.label);
            _cellStyle.normal.textColor = GradeColor(grade);
            GUI.Label(rect, text, _cellStyle);
        }

        private static GUIStyle _legendStyle;

        /// <summary>Explains what the cell colors in the analysis tables mean.</summary>
        public static void GradeLegend()
        {
            if (_legendStyle == null)
                _legendStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(OxLoc.T("Colors:", "Cores:"), EditorStyles.miniLabel, GUILayout.Width(42));
            DrawLegendItem(OxLoc.T("ideal", "ideal"), Color.white);
            DrawLegendItem(OxLoc.T("attention", "atenção"), Warning);
            DrawLegendItem(OxLoc.T("far from ideal", "longe do ideal"), Fail);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawLegendItem(string label, Color color)
        {
            var hex = ColorUtility.ToHtmlStringRGB(color);
            GUILayout.Label($"<color=#{hex}>■</color> {label}", _legendStyle, GUILayout.Width(110));
        }

        private static GUIStyle _titleStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _badgeStyle;
        private static GUIStyle _rowLabelStyle;
        private static GUIStyle _hintStyle;
        private static GUIStyle _sectionStyle;
        private static Texture _logo;

        private static void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
            _subtitleStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 11 };
            _badgeStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            _rowLabelStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            _hintStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, wordWrap = true };
            _sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _hintStyle.normal.textColor = new Color(0.62f, 0.62f, 0.62f);
        }

        private static readonly string[] LanguageNames = { "English", "Português (BR)" };

        /// <summary>Window header: logo, product name, tagline and language selector, with an accent separator.</summary>
        public static void Header(string version)
        {
            EnsureStyles();

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            if (_logo == null)
                _logo = Resources.Load<Texture>("ox_logo");
            if (_logo != null)
                GUILayout.Label(_logo, GUILayout.Height(42), GUILayout.Width(42f * _logo.width / Mathf.Max(1, _logo.height)));

            EditorGUILayout.BeginVertical();
            GUILayout.Space(2);
            GUILayout.Label("OxOptimizer", _titleStyle);
            GUILayout.Label(OxLoc.T("WebGL toolkit by OxenteGames", "Toolkit WebGL da OxenteGames") + "  •  v" + version, _subtitleStyle);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical();
            GUILayout.Space(12);
            var selectedLanguage = (OxLanguage)EditorGUILayout.Popup((int)OxLoc.Language, LanguageNames, GUILayout.Width(110));
            if (selectedLanguage != OxLoc.Language)
                OxLoc.Language = selectedLanguage; // only write EditorPrefs on change
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);

            var line = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(line, Accent);
            GUILayout.Space(4);
        }

        public static void Section(string title)
        {
            EnsureStyles();
            GUILayout.Space(10);
            GUILayout.Label(title, _sectionStyle);
            GUILayout.Space(2);
        }

        /// <summary>
        /// One audit row: ✔/✖ badge, description, and a "Fix" button when the check fails.
        /// </summary>
        public static void StatusRow(string label, bool ok, Action fix, string hint = null)
        {
            EnsureStyles();

            EditorGUILayout.BeginVertical();
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            var previousColor = _badgeStyle.normal.textColor;
            _badgeStyle.normal.textColor = ok ? Pass : Fail;
            GUILayout.Label(ok ? "✔" : "✖", _badgeStyle, GUILayout.Width(22));
            _badgeStyle.normal.textColor = previousColor;

            GUILayout.Label(label, _rowLabelStyle);
            GUILayout.FlexibleSpace();

            if (!ok && fix != null && GUILayout.Button(OxLoc.T("Fix", "Corrigir"), GUILayout.Width(60)))
                fix();

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(hint))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(26);
                GUILayout.Label(hint, _hintStyle);
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            EditorGUILayout.EndVertical();
        }

        /// <summary>Informational row with an accent-colored marker, no fix button.</summary>
        public static void InfoRow(string info)
        {
            EnsureStyles();

            EditorGUILayout.BeginVertical();
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            var previousColor = _badgeStyle.normal.textColor;
            _badgeStyle.normal.textColor = Accent;
            GUILayout.Label("●", _badgeStyle, GUILayout.Width(22));
            _badgeStyle.normal.textColor = previousColor;

            GUILayout.Label(info, _rowLabelStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Informational row with an action button — for optimizations that shouldn't be
        /// presented as pass/fail because they depend on how the project uses the feature.
        /// </summary>
        public static void ActionRow(string info, string buttonLabel, Action onClick)
        {
            EnsureStyles();

            EditorGUILayout.BeginVertical();
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            var previousColor = _badgeStyle.normal.textColor;
            _badgeStyle.normal.textColor = Accent;
            GUILayout.Label("●", _badgeStyle, GUILayout.Width(22));
            _badgeStyle.normal.textColor = previousColor;

            GUILayout.Label(info, _rowLabelStyle);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(buttonLabel, GUILayout.Width(80)))
                onClick();

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
            EditorGUILayout.EndVertical();
        }

        /// <summary>Centered link-style button, used for external documentation.</summary>
        public static void LinkButton(string label, string url)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(label))
                Application.OpenURL(url);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }
}
