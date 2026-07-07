using OxenteGames.OxOptimizer.Tabs;
using UnityEditor;
using UnityEngine;

namespace OxenteGames.OxOptimizer
{
    public class OxOptimizerWindow : EditorWindow
    {
        public const string Version = "1.0.0";
        public const string RepositoryUrl = "https://github.com/OxenteGames/OxOptimizer";

        private int _activeTab;

        // tab names stay in English on purpose — they mirror Unity's own terminology
        private static readonly string[] TabNames = { "Export", "Memory", "Textures", "Models", "Audio", "Fonts", "Build logs" };

        public static EditorWindow EditorWindowInstance;

        [MenuItem("Tools/OxOptimizer (WebGL)")]
        public static void ShowWindow()
        {
            EditorWindowInstance = GetWindow(typeof(OxOptimizerWindow), false, "OxOptimizer");
            EditorWindowInstance.minSize = new Vector2(800, 600);
        }

        void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);

            OxGui.Header(Version);

            _activeTab = GUILayout.Toolbar(_activeTab, TabNames, GUILayout.Height(24));
            GUILayout.Space(4);

            switch (_activeTab)
            {
                case 0:
                    ExportTab.RenderGUI();
                    break;
                case 1:
                    MemoryTab.RenderGUI();
                    break;
                case 2:
                    TexturesTab.RenderGUI();
                    break;
                case 3:
                    ModelsTab.RenderGUI();
                    break;
                case 4:
                    AudioTab.RenderGUI();
                    break;
                case 5:
                    FontsTab.RenderGUI();
                    break;
                case 6:
                    BuildLogsTab.RenderGUI();
                    break;
            }

            GUILayout.FlexibleSpace();
            RenderFooter();
            EditorGUILayout.EndVertical();
        }

        void RenderFooter()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("OxenteGames  •  v" + Version, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("GitHub", EditorStyles.miniButton, GUILayout.Width(70)))
                Application.OpenURL(RepositoryUrl);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void OnDestroy()
        {
            EditorWindowInstance = null;
        }
    }
}
