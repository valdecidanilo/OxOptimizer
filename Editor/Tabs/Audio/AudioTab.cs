using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OxenteGames.OxOptimizer.TreeLib;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace OxenteGames.OxOptimizer.Tabs
{
    public class AudioTab : EditorWindow
    {
        private static MultiColumnHeaderState _multiColumnHeaderState;
        private static AudioTree _audioCompressionTree;

        private static bool _isAnalyzing;
        private static bool _includeFilesFromPackages;
        private static List<AudioTreeItem> _treeItems;
        private static List<string> _pendingAudioPaths;
        private static List<AudioTreeItem> _pendingTreeElements;
        private static int _pendingPathIndex;
        private const int BatchSize = 25;

        public static void RenderGUI()
        {
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            //GUILayout.Label(OxLoc.T("Press \"Analyze audio\" button to load the table.", "Clique em \"Analisar áudio\" para carregar a tabela."));
            //GUILayout.Label(OxLoc.T("Press it again when you need to refresh the data.", "Clique de novo quando precisar atualizar os dados."));
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _audioCompressionTree?.OnGUI(rect);
            if (_isAnalyzing)
            {
                DrawLoadingOverlay(rect);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_isAnalyzing ? OxLoc.T("Analyzing...", "Analisando...") : OxLoc.T("Analyze audio", "Analisar áudio"), GUILayout.Width(200)))
            {
                AnalyzeAudio();
            }

            var fixableCount = _isAnalyzing || _treeItems == null ? 0 : _treeItems.Count(item => item.NeedsQualityFix);
            using (new EditorGUI.DisabledScope(fixableCount == 0))
            {
                if (GUILayout.Button(OxLoc.T($"Fix all qualities ({fixableCount})", $"Corrigir todas as qualidades ({fixableCount})"), GUILayout.Width(220)))
                {
                    FixAllQualities();
                }
            }

            var originalValue = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 160;
            _includeFilesFromPackages = EditorGUILayout.Toggle(OxLoc.T("Include files from Packages", "Incluir arquivos de Packages"), _includeFilesFromPackages);
            EditorGUIUtility.labelWidth = originalValue;
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            GUILayout.Label(
                OxLoc.T(
                    "This utility gives you an overview of the audio clips used in your project. By optimizing various settings, you will be able to considerably decrease your final build size and runtime memory usage. You can click on an audio clip to select it in the Project view.",
                    "Este utilitário dá uma visão geral dos áudios usados no projeto. Otimizando as configurações, você consegue reduzir consideravelmente o tamanho final do build e o uso de memória em runtime. Clique em um áudio para selecioná-lo na aba Project."),
                EditorStyles.wordWrappedLabel);

            OxGui.GradeLegend();

            BuildExplanation("Force to mono",
                OxLoc.T(
                    "Mixes multi-channel audio down to a single channel, halving the imported clip size. Most WebGL games don't need stereo, so keep it enabled unless a clip really depends on stereo separation.",
                    "Mistura áudios multicanal em um único canal, reduzindo o tamanho do clipe importado pela metade. A maioria dos jogos WebGL não precisa de estéreo, então mantenha habilitado, a menos que o clipe realmente dependa da separação estéreo."),
                OxLoc.T("ideal: enabled", "ideal: habilitado"));
            BuildExplanation("Load type",
                OxLoc.T(
                    "The default option, Decompress On Load, is good for audio clips that require precision when played, for example, audio effects or dialogues. For background audio clips Compressed In Memory is recommended, since it reduces the runtime memory, though audio playback is less precise and may introduce latency.",
                    "A opção padrão, Decompress On Load, é boa para áudios que exigem precisão ao tocar, como efeitos e diálogos. Para áudios de fundo, Compressed In Memory é recomendado por reduzir a memória em runtime, embora a reprodução seja menos precisa e possa ter latência."),
                OxLoc.T("ideal: not Decompress on load", "ideal: não Decompress on load"));
            BuildExplanation("Quality",
                OxLoc.T(
                    "Lowering the quality will reduce the build size. Unity re-encodes the source on import, so a quality above the source's own bitrate only inflates the build with zero quality gain. The Fix button targets the quality that matches each clip's source bitrate (music 35-50, short effects 50-70).",
                    "Reduzir a qualidade diminui o tamanho do build. A Unity re-encoda a fonte no import, então uma qualidade acima do bitrate do arquivo fonte só infla o build sem nenhum ganho de qualidade. O botão Corrigir mira a qualidade que casa com o bitrate da fonte de cada clipe (música 35-50, efeitos curtos 50-70)."),
                OxLoc.T("ideal: match the source bitrate", "ideal: casar com o bitrate da fonte"));
        }

        static void BuildExplanation(string label, string explanation, string ideal = null)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(130));
            GUILayout.Label(
                string.IsNullOrEmpty(ideal) ? explanation : $"{explanation}  ({ideal})",
                EditorStyles.wordWrappedLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }


        /**
         * Find recursively the audio clips on which this scene depends.
         */
        static List<string> GetSceneAudioDependencies(string scenePath)
        {
            var audioDependencies = new List<string>();
            var assetDependencies = AssetDatabase.GetDependencies(scenePath, true);
            foreach (var assetDependency in assetDependencies)
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(assetDependency) == typeof(AudioClip))
                {
                    audioDependencies.Add(assetDependency);
                }
            }

            return audioDependencies;
        }

        static List<string> GetUsedAudioInBuildScenes()
        {
            var usedAudioPaths = new HashSet<string>();

            var scenesInBuild = OxAudit.GetScenesInBuildPath();
            foreach (var scenePath in scenesInBuild)
            {
                var audioUsedInScene = GetSceneAudioDependencies(scenePath);
                foreach (var audioPath in audioUsedInScene)
                {
                    usedAudioPaths.Add(audioPath);
                }
            }

            return usedAudioPaths.ToList();
        }

        /**
         * Get the list of audio clips in the Resources folders, or on which assets from the Resources folder depend.
         */
        static List<string> GetUsedAudioInResources()
        {
            var usedAudioPaths = new HashSet<string>();
            var allAssetPaths = AssetDatabase.FindAssets("", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToList();

            // keep only the assets inside a Resources folder, that is not inside an Editor folder
            var rx = new Regex(@"\w*(?<!Editor\/)Resources\/", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            allAssetPaths = allAssetPaths.Where(assetPath => (rx.IsMatch(assetPath))).ToList();

            // find all the audio clips on which the assets from the Resources folder depend
            foreach (var assetPath in allAssetPaths)
            {
                var assetDependencies = AssetDatabase.GetDependencies(assetPath, true);
                foreach (var assetDependency in assetDependencies)
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(assetDependency) == typeof(AudioClip))
                    {
                        usedAudioPaths.Add(assetDependency);
                    }
                }
            }

            return usedAudioPaths.ToList();
        }

        private static void FixAllQualities()
        {
            if (_treeItems == null)
                return;

            var fixableItems = _treeItems.Where(item => item.NeedsQualityFix).ToList();
            try
            {
                AssetDatabase.StartAssetEditing();
                for (var i = 0; i < fixableItems.Count; i++)
                {
                    var item = fixableItems[i];
                    EditorUtility.DisplayProgressBar(
                        OxLoc.T("Fixing audio quality", "Corrigindo qualidade dos áudios"),
                        item.AudioName,
                        (float)i / fixableItems.Count);
                    item.ApplyRecommendedQuality();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            if (OxOptimizerWindow.EditorWindowInstance != null)
            {
                OxOptimizerWindow.EditorWindowInstance.Repaint();
            }
        }

        public static void AnalyzeAudio()
        {
            if (_isAnalyzing)
            {
                return;
            }

            _isAnalyzing = true;
            _pendingAudioPaths = null;
            _pendingTreeElements = null;
            _pendingPathIndex = 0;
            if (OxOptimizerWindow.EditorWindowInstance != null)
            {
                OxOptimizerWindow.EditorWindowInstance.Repaint();
            }

            EditorApplication.delayCall += StartAnalyzeAudio;
        }

        private static void StartAnalyzeAudio()
        {
            if (!_isAnalyzing)
            {
                return;
            }

            var usedAudioPaths = new HashSet<string>();

            GetUsedAudioInBuildScenes().ForEach(path => usedAudioPaths.Add(path));
            GetUsedAudioInResources().ForEach(path => usedAudioPaths.Add(path));

            _pendingAudioPaths = usedAudioPaths.ToList();
            _pendingTreeElements = new List<AudioTreeItem>();
            _pendingTreeElements.Add(new AudioTreeItem("Root", -1, 0, null, null));
            EditorApplication.update += UpdateAnalyzeAudio;
        }

        private static void UpdateAnalyzeAudio()
        {
            try
            {
                var processed = 0;
                while (_pendingAudioPaths != null && _pendingPathIndex < _pendingAudioPaths.Count && processed < BatchSize)
                {
                    var audioPath = _pendingAudioPaths[_pendingPathIndex];
                    _pendingPathIndex++;
                    processed++;

                    if (audioPath.StartsWith("Packages/") && !_includeFilesFromPackages)
                    {
                        continue;
                    }

                    try
                    {
                        var audioImporter = (AudioImporter)AssetImporter.GetAtPath(audioPath);
                        _pendingTreeElements.Add(new AudioTreeItem("AudioClip", 0, _pendingTreeElements.Count, audioPath, audioImporter));
                    }
                    catch (Exception)
                    {
                        Debug.LogWarning("Failed to analyze audio clip at path: " + audioPath);
                    }
                }

                if (_pendingAudioPaths == null || _pendingPathIndex >= _pendingAudioPaths.Count)
                {
                    CompleteAnalyzeAudio();
                    return;
                }

                if (OxOptimizerWindow.EditorWindowInstance != null)
                {
                    OxOptimizerWindow.EditorWindowInstance.Repaint();
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                CompleteAnalyzeAudio();
            }
        }

        private static void CompleteAnalyzeAudio()
        {
            EditorApplication.update -= UpdateAnalyzeAudio;

            if (_pendingTreeElements == null)
            {
                _isAnalyzing = false;
                return;
            }

            _treeItems = _pendingTreeElements.Where(item => item.depth == 0).ToList();
            var treeModel = new TreeModel<AudioTreeItem>(_pendingTreeElements);
            var treeViewState = new TreeViewState();
            if (_multiColumnHeaderState == null)
                _multiColumnHeaderState = new MultiColumnHeaderState(new[]
                {
                    // when adding a new column don't forget to check the sorting method, and the CellGUI method
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Audio clip" }, width = 150, minWidth = 150, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Force to mono" }, width = 90, minWidth = 90, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Load type" }, width = 150, minWidth = 150, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent() { text = "Quality" }, width = 60, minWidth = 60, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent() { text = "" }, width = 110, minWidth = 100, canSort = false },
                });
            _audioCompressionTree = new AudioTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);
            _isAnalyzing = false;
            _pendingAudioPaths = null;
            _pendingTreeElements = null;
            if (OxOptimizerWindow.EditorWindowInstance != null)
            {
                OxOptimizerWindow.EditorWindowInstance.Repaint();
            }
        }

        private static void DrawLoadingOverlay(Rect rect)
        {
            const float overlayWidth = 180f;
            const float overlayHeight = 64f;
            var overlayRect = new Rect(
                rect.x + (rect.width - overlayWidth) * 0.5f,
                rect.y + (rect.height - overlayHeight) * 0.5f,
                overlayWidth,
                overlayHeight);

            GUI.Box(overlayRect, GUIContent.none, EditorStyles.helpBox);

            var spinnerIndex = (int)(EditorApplication.timeSinceStartup * 12.0) % 12;
            var spinnerIcon = EditorGUIUtility.IconContent($"WaitSpin{spinnerIndex:00}");
            var iconRect = new Rect(overlayRect.x + (overlayRect.width - 16f) * 0.5f, overlayRect.y + 8f, 16f, 16f);
            GUI.Label(iconRect, spinnerIcon);

            var labelRect = new Rect(overlayRect.x + 8f, overlayRect.y + 30f, overlayRect.width - 16f, 20f);
            GUI.Label(labelRect, OxLoc.T("Analyzing...", "Analisando..."), EditorStyles.centeredGreyMiniLabel);
        }
    }
}
