using OxenteGames.OxOptimizer.TreeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace OxenteGames.OxOptimizer.Tabs
{
    public class ModelsTab : EditorWindow
    {
        private static MultiColumnHeaderState _multiColumnHeaderState;
        private static ModelTree _modelTree;

        private static bool _isAnalyzing;
        private static bool _includeFilesFromPackages;
        private static List<string> _pendingModelPaths;
        private static List<ModelTreeItem> _pendingTreeElements;
        private static int _pendingPathIndex;
        private const int BatchSize = 25;

        private static readonly List<string> _modelFormats = new List<string>() { ".fbx", ".dae", ".3ds", ".dxf", ".obj" };

        public static void RenderGUI()
        {
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label(OxLoc.T("Press \"Analyze models\" button to load the table.", "Clique em \"Analisar modelos\" para carregar a tabela."));
            GUILayout.Label(OxLoc.T("Press it again when you need to refresh the data.", "Clique de novo quando precisar atualizar os dados."));
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _modelTree?.OnGUI(rect);
            if (_isAnalyzing)
            {
                DrawLoadingOverlay(rect);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(_isAnalyzing ? OxLoc.T("Analyzing...", "Analisando...") : OxLoc.T("Analyze models", "Analisar modelos"), GUILayout.Width(200)))
            {
                AnalyzeModels();
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
                    "This utility gives you an overview of the models used in your project. By optimizing various settings, you will be able to considerably decrease your final build size. You can click on a model to select it in the Project view.",
                    "Este utilitário dá uma visão geral dos modelos usados no projeto. Otimizando as configurações, você consegue reduzir consideravelmente o tamanho final do build. Clique em um modelo para selecioná-lo na aba Project."),
                EditorStyles.wordWrappedLabel);

            BuildExplanation("R/W enabled",
                OxLoc.T(
                    "When a Mesh is read/write enabled, Unity uploads the Mesh data to GPU-addressable memory, but also keeps it in CPU-addressable memory. In most cases, you should disable this option to save runtime memory usage.",
                    "Com read/write ativado, a Unity envia a malha para a memória da GPU mas também a mantém na memória da CPU. Na maioria dos casos, desative para economizar memória em runtime."));
            BuildExplanation("Polygons optimized",
                OxLoc.T(
                    "Optimize the order of polygons in the mesh to make better use of the GPUs internal caches to improve rendering performance.",
                    "Otimiza a ordem dos polígonos da malha para aproveitar melhor os caches internos da GPU e melhorar a performance de renderização."));
            BuildExplanation("Vertices optimized",
                OxLoc.T(
                    "Optimize the order of vertices in the mesh to make better use of the GPUs internal caches to improve rendering performance.",
                    "Otimiza a ordem dos vértices da malha para aproveitar melhor os caches internos da GPU e melhorar a performance de renderização."));
            BuildExplanation("Mesh compression",
                OxLoc.T(
                    "Compressing meshes will decrease the final build size, but more compression introduces more artifacts in vertex data.",
                    "Comprimir malhas reduz o tamanho final do build, mas mais compressão introduz mais artefatos nos vértices."));
            BuildExplanation("Animation compression",
                OxLoc.T(
                    "Compressing animations will decrease the final build size, but more compression introduces more artifacts in the animations.",
                    "Comprimir animações reduz o tamanho final do build, mas mais compressão introduz mais artefatos nas animações."));
        }

        static void BuildExplanation(string label, string explanation)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(130));
            GUILayout.Label(explanation, EditorStyles.wordWrappedLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /**
         * Find recursively the models on which this scene depends.
         */
        static List<string> GetSceneModelDependencies(string scenePath)
        {
            var modelDependencies = new List<string>();
            var assetDependencies = AssetDatabase.GetDependencies(scenePath, true);

            foreach (var assetDependency in assetDependencies)
            {
                if (IsModelAtPath(assetDependency))
                {
                    modelDependencies.Add(assetDependency);
                }
            }

            return modelDependencies;
        }

        static List<string> GetUsedModelsInBuildScenes()
        {
            var usedModelPaths = new HashSet<string>();
            var scenesInBuild = OxAudit.GetScenesInBuildPath();

            foreach (var scenePath in scenesInBuild)
            {
                var modelsUsedInScene = GetSceneModelDependencies(scenePath);

                foreach (var modelPath in modelsUsedInScene)
                {
                    usedModelPaths.Add(modelPath);
                }
            }

            return usedModelPaths.ToList();
        }

        /**
         * Get the list of models in the Resources folders, or on which assets from the Resources folder depend.
         */
        static List<string> GetUsedModelsInResources()
        {
            var usedModelPaths = new HashSet<string>();
            var allAssetPaths = AssetDatabase.FindAssets("", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToList();

            // keep only the assets inside a Resources folder, that is not inside an Editor folder
            var rx = new Regex(@"\w*(?<!Editor\/)Resources\/", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            allAssetPaths = allAssetPaths.Where(assetPath => (rx.IsMatch(assetPath))).ToList();

            // find all the models on which the assets from the Resources folder depend
            foreach (var assetPath in allAssetPaths)
            {
                var assetDependencies = AssetDatabase.GetDependencies(assetPath, true);

                foreach (var assetDependency in assetDependencies)
                {
                    if (IsModelAtPath(assetDependency))
                    {
                        string ext = System.IO.Path.GetExtension(assetDependency);
                        usedModelPaths.Add(assetDependency);
                    }
                }
            }

            return usedModelPaths.ToList();
        }

        static bool IsModelAtPath(string assetDependency)
        {
            return AssetDatabase.GetMainAssetTypeAtPath(assetDependency) == typeof(GameObject) &&
                   _modelFormats.Contains(System.IO.Path.GetExtension(assetDependency).ToLowerInvariant());
        }

        public static void AnalyzeModels()
        {
            if (_isAnalyzing)
            {
                return;
            }

            _isAnalyzing = true;
            _pendingModelPaths = null;
            _pendingTreeElements = null;
            _pendingPathIndex = 0;

            if (OxOptimizerWindow.EditorWindowInstance != null)
            {
                OxOptimizerWindow.EditorWindowInstance.Repaint();
            }

            EditorApplication.delayCall += StartAnalyzeModels;
        }

        private static void StartAnalyzeModels()
        {
            if (!_isAnalyzing)
            {
                return;
            }

            var usedModelPaths = new HashSet<string>();

            GetUsedModelsInBuildScenes().ForEach(path => usedModelPaths.Add(path));
            GetUsedModelsInResources().ForEach(path => usedModelPaths.Add(path));

            _pendingModelPaths = usedModelPaths.ToList();
            _pendingTreeElements = new List<ModelTreeItem>();
            _pendingTreeElements.Add(new ModelTreeItem("Root", -1, 0, null, null));
            EditorApplication.update += UpdateAnalyzeModels;
        }

        private static void UpdateAnalyzeModels()
        {
            try
            {
                var processed = 0;
                while (_pendingModelPaths != null && _pendingPathIndex < _pendingModelPaths.Count && processed < BatchSize)
                {
                    var modelPath = _pendingModelPaths[_pendingPathIndex];
                    _pendingPathIndex++;
                    processed++;

                    if (modelPath.StartsWith("Packages/") && !_includeFilesFromPackages)
                    {
                        continue;
                    }

                    try
                    {
                        var modelImporter = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                        _pendingTreeElements.Add(new ModelTreeItem("Model", 0, _pendingTreeElements.Count, modelPath, modelImporter));
                    }
                    catch (Exception)
                    {
                        Debug.LogWarning("Failed to analyze model at path: " + modelPath);
                    }
                }

                if (_pendingModelPaths == null || _pendingPathIndex >= _pendingModelPaths.Count)
                {
                    CompleteAnalyzeModels();
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
                CompleteAnalyzeModels();
            }
        }

        private static void CompleteAnalyzeModels()
        {
            EditorApplication.update -= UpdateAnalyzeModels;

            if (_pendingTreeElements == null)
            {
                _isAnalyzing = false;
                return;
            }

            var treeModel = new TreeModel<ModelTreeItem>(_pendingTreeElements);
            var treeViewState = new TreeViewState();

            if (_multiColumnHeaderState == null)
                _multiColumnHeaderState = new MultiColumnHeaderState(new[]
                {
                    // when adding a new column don't forget to check the sorting method, and the CellGUI method
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent() { text = "Model" }, width = 150, minWidth = 150, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "R/W enabled" }, width = 80, minWidth = 80, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Polygons optimized" }, width = 120, minWidth = 120, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Vertices optimized" }, width = 120, minWidth = 120, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Mesh compression" }, width = 120, minWidth = 120, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "Animation compression" }, width = 140, minWidth = 140, canSort = true },
                });

            _modelTree = new ModelTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);
            _isAnalyzing = false;
            _pendingModelPaths = null;
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
