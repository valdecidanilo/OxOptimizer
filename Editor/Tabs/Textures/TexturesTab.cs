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
    public class TexturesTab : EditorWindow
    {
        private static MultiColumnHeaderState _multiColumnHeaderState;
        private static TextureTree _textureCompressionTree;

        private static bool _isAnalyzing;
        private static bool _includeFilesFromPackages;
        private static List<string> _pendingTexturePaths;
        private static List<TextureTreeItem> _pendingTreeElements;
        private static int _pendingIdIncrement;
        private const int BatchSize = 25;

        public static void RenderGUI()
        {
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label(OxLoc.T("Press \"Analyze textures\" button to load the table.", "Clique em \"Analisar texturas\" para carregar a tabela."));
            GUILayout.Label(OxLoc.T("Press it again when you need to refresh the data.", "Clique de novo quando precisar atualizar os dados."));
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _textureCompressionTree?.OnGUI(rect);
            if (_isAnalyzing)
            {
                DrawLoadingOverlay(rect);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_isAnalyzing ? OxLoc.T("Analyzing...", "Analisando...") : OxLoc.T("Analyze textures", "Analisar texturas"), GUILayout.Width(200)))
            {
                AnalyzeTextures();
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
                    "This utility gives you an overview of the textures used in your project. By optimizing various settings, you will be able to considerably decrease your final build size. You can click on a texture to select it in the Project view.",
                    "Este utilitário dá uma visão geral das texturas usadas no projeto. Otimizando as configurações, você consegue reduzir consideravelmente o tamanho final do build. Clique em uma textura para selecioná-la na aba Project."),
                EditorStyles.wordWrappedLabel);


            BuildExplanation("Max size",
                OxLoc.T(
                    "Decrease the max size as much as possible while the texture still looks good in game. You most likely don't need the default 2048 set by Unity.",
                    "Reduza o tamanho máximo o quanto der, desde que a textura continue boa no jogo. Você provavelmente não precisa do padrão 2048 da Unity."));
            BuildExplanation("Compression",
                OxLoc.T("Lower quality will decrease the final build size.", "Qualidade menor reduz o tamanho final do build."));
            BuildExplanation("Crunch compression",
                OxLoc.T(
                    "All the textures with crunch compression enabled will be compressed together, decreasing the final build size.",
                    "Todas as texturas com crunch ativado são comprimidas juntas, reduzindo o tamanho final do build."));
            BuildExplanation("Crunch comp. quality",
                OxLoc.T(
                    "A higher compression quality means larger textures and longer compression times.",
                    "Qualidade de compressão maior significa texturas maiores e compressão mais demorada."));
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
         * Find recursively the textures on which this scene depends.
         */
        static List<string> GetSceneTextureDependencies(string scenePath)
        {
            var textureDependencies = new List<string>();
            var assetDependencies = AssetDatabase.GetDependencies(scenePath, true);
            foreach (var assetDependency in assetDependencies)
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(assetDependency) == typeof(Texture2D))
                {
                    textureDependencies.Add(assetDependency);
                }
            }

            return textureDependencies;
        }

        static List<string> GetUsedTexturesInBuildScenes()
        {
            var usedTexturePaths = new HashSet<string>();

            var scenesInBuild = OxAudit.GetScenesInBuildPath();
            foreach (var scenePath in scenesInBuild)
            {
                var texturesUsedInScene = GetSceneTextureDependencies(scenePath);
                foreach (var texturePath in texturesUsedInScene)
                {
                    usedTexturePaths.Add(texturePath);
                }
            }

            return usedTexturePaths.ToList();
        }

        /**
         * Get the list of textures in the Resources folders, or on which assets from the Resources folder depend.
         */
        static List<string> GetUsedTexturesInResources()
        {
            var usedTexturePaths = new HashSet<string>();
            var allAssetPaths = AssetDatabase.FindAssets("", new[] {"Assets"}).Select(AssetDatabase.GUIDToAssetPath).ToList();

            // keep only the assets inside a Resources folder, that is not inside an Editor folder
            var rx = new Regex(@"\w*(?<!Editor\/)Resources\/", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            allAssetPaths = allAssetPaths.Where(assetPath => (rx.IsMatch(assetPath))).ToList();

            // find all the textures on which the assets from the Resources folder depend
            foreach (var assetPath in allAssetPaths)
            {
                var assetDependencies = AssetDatabase.GetDependencies(assetPath, true);
                foreach (var assetDependency in assetDependencies)
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(assetDependency) == typeof(Texture2D))
                    {
                        usedTexturePaths.Add(assetDependency);
                    }
                }
            }

            return usedTexturePaths.ToList();
        }

        public static void AnalyzeTextures()
        {
            if (_isAnalyzing)
            {
                return;
            }

            _isAnalyzing = true;
            _pendingTexturePaths = null;
            _pendingTreeElements = null;
            _pendingIdIncrement = 0;
            if (OxOptimizerWindow.EditorWindowInstance != null)
            {
                OxOptimizerWindow.EditorWindowInstance.Repaint();
            }

            EditorApplication.delayCall += StartAnalyzeTextures;
        }

        private static void StartAnalyzeTextures()
        {
            if (!_isAnalyzing)
            {
                return;
            }

            var usedTexturePaths = new HashSet<string>();

            GetUsedTexturesInBuildScenes().ForEach(path => usedTexturePaths.Add(path));
            GetUsedTexturesInResources().ForEach(path => usedTexturePaths.Add(path));

            _pendingTexturePaths = usedTexturePaths.ToList();
            _pendingTreeElements = new List<TextureTreeItem>();
            _pendingTreeElements.Add(new TextureTreeItem("Root", -1, _pendingIdIncrement, null, null));
            EditorApplication.update += UpdateAnalyzeTextures;
        }

        private static void UpdateAnalyzeTextures()
        {
            try
            {
                var processed = 0;
                while (_pendingTexturePaths != null && _pendingIdIncrement < _pendingTexturePaths.Count && processed < BatchSize)
                {
                    var texturePath = _pendingTexturePaths[_pendingIdIncrement];
                    _pendingIdIncrement++;
                    processed++;

                    if (texturePath.StartsWith("Packages/") && !_includeFilesFromPackages)
                    {
                        continue;
                    }

                    try
                    {
                        var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                        _pendingTreeElements.Add(new TextureTreeItem("Texture2D", 0, _pendingTreeElements.Count, texturePath, textureImporter));
                    }
                    catch (Exception)
                    {
                        Debug.LogWarning("Failed to analyze texture at path: " + texturePath);
                    }
                }

                if (_pendingTexturePaths == null || _pendingIdIncrement >= _pendingTexturePaths.Count)
                {
                    CompleteAnalyzeTextures();
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
                CompleteAnalyzeTextures();
            }
        }

        private static void CompleteAnalyzeTextures()
        {
            EditorApplication.update -= UpdateAnalyzeTextures;

            if (_pendingTreeElements == null)
            {
                _isAnalyzing = false;
                return;
            }

            var treeModel = new TreeModel<TextureTreeItem>(_pendingTreeElements);
            var treeViewState = new TreeViewState();
            if (_multiColumnHeaderState == null)
                _multiColumnHeaderState = new MultiColumnHeaderState(new[]
                {
                    // when adding a new column don't forget to check the sorting method, and the CellGUI method
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Texture"}, width = 150, minWidth = 150, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Type"}, width = 60, minWidth = 60, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Max size"}, width = 60, minWidth = 60, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Compression"}, width = 80, minWidth = 80, canSort = true},
                    new MultiColumnHeaderState.Column()
                        {headerContent = new GUIContent() {text = "Crunch compression"}, width = 120, minWidth = 120, canSort = true},
                    new MultiColumnHeaderState.Column()
                        {headerContent = new GUIContent() {text = "Crunch comp. quality"}, width = 128, minWidth = 128, canSort = true},
                });
            _textureCompressionTree = new TextureTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);
            _isAnalyzing = false;
            _pendingTexturePaths = null;
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
