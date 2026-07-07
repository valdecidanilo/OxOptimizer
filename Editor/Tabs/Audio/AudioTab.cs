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

        public static void RenderGUI()
        {
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label(OxLoc.T("Press \"Analyze audio\" button to load the table.", "Clique em \"Analisar áudio\" para carregar a tabela."));
            GUILayout.Label(OxLoc.T("Press it again when you need to refresh the data.", "Clique de novo quando precisar atualizar os dados."));
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _audioCompressionTree?.OnGUI(rect);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_isAnalyzing ? OxLoc.T("Analyzing...", "Analisando...") : OxLoc.T("Analyze audio", "Analisar áudio"), GUILayout.Width(200)))
            {
                AnalyzeAudio();
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


            BuildExplanation("Load type",
                OxLoc.T(
                    "The default option, Decompress On Load, is good for audio clips that require precision when played, for example, audio effects or dialogues. For background audio clips Compressed In Memory is recommended, since it reduces the runtime memory, though audio playback is less precise and may introduce latency.",
                    "A opção padrão, Decompress On Load, é boa para áudios que exigem precisão ao tocar, como efeitos e diálogos. Para áudios de fundo, Compressed In Memory é recomendado por reduzir a memória em runtime, embora a reprodução seja menos precisa e possa ter latência."));
            BuildExplanation("Quality",
                OxLoc.T(
                    "Lowering the quality will reduce the build size. You can experiment with a lower audio quality for background audio.",
                    "Reduzir a qualidade diminui o tamanho do build. Experimente uma qualidade menor nos áudios de fundo."));
        }

        static void BuildExplanation(string label, string explanation)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(130));
            GUILayout.Label(
                explanation,
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

        static void AnalyzeAudio()
        {
            _isAnalyzing = true;
            if (OxOptimizerWindow.EditorWindowInstance != null)
            {
                OxOptimizerWindow.EditorWindowInstance.Repaint();
            }

            var usedAudioPaths = new HashSet<string>();

            GetUsedAudioInBuildScenes().ForEach(path => usedAudioPaths.Add(path));
            GetUsedAudioInResources().ForEach(path => usedAudioPaths.Add(path));

            var treeElements = new List<AudioTreeItem>();
            var idIncrement = 0;
            var root = new AudioTreeItem("Root", -1, idIncrement, null, null);
            treeElements.Add(root);

            foreach (var audioPath in usedAudioPaths)
            {
                if (audioPath.StartsWith("Packages/") && !_includeFilesFromPackages)
                {
                    continue;
                }

                idIncrement++;
                try
                {
                    var audioImporter = (AudioImporter)AssetImporter.GetAtPath(audioPath);
                    treeElements.Add(new AudioTreeItem("AudioClip", 0, idIncrement, audioPath, audioImporter));
                }
                catch (Exception)
                {
                    Debug.LogWarning("Failed to analyze audio clip at path: " + audioPath);
                }
            }

            var treeModel = new TreeModel<AudioTreeItem>(treeElements);
            var treeViewState = new TreeViewState();
            if (_multiColumnHeaderState == null)
                _multiColumnHeaderState = new MultiColumnHeaderState(new[]
                {
                    // when adding a new column don't forget to check the sorting method, and the CellGUI method
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Audio clip" }, width = 150, minWidth = 150, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Load type" }, width = 150, minWidth = 150, canSort = true },
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent() { text = "Quality" }, width = 60, minWidth = 60, canSort = true },
                });
            _audioCompressionTree = new AudioTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);
            _isAnalyzing = false;
            if (OxOptimizerWindow.EditorWindowInstance != null)
            {
                OxOptimizerWindow.EditorWindowInstance.Repaint();
            }
        }
    }
}