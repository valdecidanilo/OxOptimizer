using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using OxenteGames.OxOptimizer;
using OxenteGames.OxOptimizer.TreeLib;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OxenteGames.OxOptimizer.Tabs
{
    public class BuildLogsTab
    {
        private static MultiColumnHeaderState _multiColumnHeaderState;
        private static BuildLogTree _buildLogTree;
        private static bool _isAnalyzing;
        private static string _errorMessage;
        private static bool _includeFilesFromPackages;

        private static List<string> _pendingBuildReportLines;
        private static List<BuildLogTreeItem> _pendingTreeElements;
        private static int _pendingBuildReportLineIndex;
        private static int _pendingIdIncrement;

        // Per-file size grades from the last successful analysis, so the Export tab's
        // optimization score matches the table colors. Null until the logs are analyzed.
        private static List<OxGui.Grade> _analyzedFileGrades;

        /// <summary>True once the build logs have been analyzed at least once this session.</summary>
        public static bool HasAnalysis => _analyzedFileGrades != null;

        /// <summary>Size grade of every file found in the last analysis (same as the table colors).</summary>
        public static IReadOnlyList<OxGui.Grade> AnalyzedFileGrades => _analyzedFileGrades;

        private const int BuildLogBatchSize = 25;

        public static void RenderGUI()
        {
            EnsureTree();

            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            _buildLogTree?.OnGUI(rect);
            if (_isAnalyzing)
            {
                DrawLoadingOverlay(rect);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            /*GUILayout.Label(OxLoc.T(
                "Press \"Analyze build logs\" button, but be sure the project was built at least once on this machine.",
                "Clique em \"Analisar logs de build\" - o projeto precisa ter sido buildado ao menos uma vez nesta maquina."), EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label(OxLoc.T("Press it again when you need to refresh the data.", "Clique de novo quando precisar atualizar os dados."), EditorStyles.wordWrappedMiniLabel);
            */
            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_isAnalyzing ? OxLoc.T("Analyzing...", "Analisando...") : OxLoc.T("Analyze build logs", "Analisar logs de build"), GUILayout.Width(200)))
            {
                AnalyzeBuildLogs();
            }

            var originalValue = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 160;
            _includeFilesFromPackages = EditorGUILayout.Toggle(OxLoc.T("Include files from Packages", "Incluir arquivos de Packages"), _includeFilesFromPackages);
            EditorGUIUtility.labelWidth = originalValue;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open Editor.log", GUILayout.Width(200)))
            {
                var processName = Application.platform == RuntimePlatform.OSXEditor ? "open" : "notepad.exe";
                Process.Start(processName, GetEditorLogPath());
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_errorMessage))
            {
                GUILayout.Label(_errorMessage, new GUIStyle
                {
                    wordWrap = true,
                    normal =
                    {
                        textColor = Color.red
                    }
                });
            }

            EditorGUILayout.Space(5);

            GUILayout.Label(
                OxLoc.T(
                    "This utility analyzes the Build Report from the Editor.log file. It will display all the files included in your final build, and the memory they occupy. You can use this utility to detect more opportunities to decrease the final build size. There may be textures that still occupy a lot of memory, uncompressed sounds, or stuff forgotten in the Resources folders that gets included in the build.",
                    "Este utilitario analisa o Build Report do arquivo Editor.log. Ele mostra todos os arquivos incluidos no build final e a memoria que ocupam. Use-o para encontrar mais oportunidades de reduzir o tamanho do build: texturas que ainda ocupam muita memoria, sons sem compressao, ou coisas esquecidas em pastas Resources que entram no build."),
                EditorStyles.wordWrappedLabel);

            GUILayout.Label(OxLoc.T(
                $"Ideal: keep each file under {BuildLogTreeItem.IdealSizeMB} MB (colors follow the absolute size, which drops as you optimize). Files over {BuildLogTreeItem.IdealSizePercentage}% of the build are also flagged for dominating it.",
                $"Ideal: manter cada arquivo abaixo de {BuildLogTreeItem.IdealSizeMB} MB (as cores seguem o tamanho absoluto, que cai conforme você otimiza). Arquivos acima de {BuildLogTreeItem.IdealSizePercentage}% do build também são marcados por dominarem o build."),
                EditorStyles.wordWrappedMiniLabel);
            OxGui.GradeLegend();
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

        private static string GetEditorLogPath()
        {
            string path;
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                var personalPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                path = $"{personalPath}/Library/Logs/Unity/Editor.log";
            }
            else
            {
                var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                path = $@"{localAppDataPath}\Unity\Editor\Editor.log";
            }

            return path;
        }

        /**
         * Return the contents of the Editor.log file.
         */
        private static string GetEditorLog()
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var originalEditorLogPath = GetEditorLogPath();
            var tempEditorLogPath = $@"{localAppDataPath}\Unity\Editor\EditorOxOptimizerTemp.log";

            // original file is blocked, perhaps by Unity editor. Need to copy it and read from the copied file.
            File.Copy(originalEditorLogPath, tempEditorLogPath, true);
            var editorLogStr = File.ReadAllText(tempEditorLogPath);
            File.Delete(tempEditorLogPath);
            return editorLogStr;
        }

        public static void AnalyzeBuildLogs()
        {
            if (_isAnalyzing)
            {
                return;
            }

            _isAnalyzing = true;
            _errorMessage = "";
            _pendingBuildReportLines = null;
            _pendingTreeElements = null;
            _pendingBuildReportLineIndex = 0;
            _pendingIdIncrement = 0;

            if (OxOptimizerWindow.EditorWindowInstance != null)
            {
                OxOptimizerWindow.EditorWindowInstance.Repaint();
            }

            string editorLogStr;
            try
            {
                editorLogStr = GetEditorLog();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                FailAnalysis(OxLoc.T(
                    "Failed to read Editor.log file, check console for more details.",
                    "Falha ao ler o arquivo Editor.log, confira o console para mais detalhes."));
                return;
            }

            var buildReportStr = editorLogStr
                .Split(new[] {$"----------------------{Environment.NewLine}Build Report{Environment.NewLine}"}, StringSplitOptions.None).Last();
            if (!buildReportStr.StartsWith("Uncompressed usage by category"))
            {
                FailAnalysis(OxLoc.T(
                    "Failed to find Build Report in the Editor.log file. Please be sure the project was recently built on this machine. If the error persists, feel free to contact us.",
                    "Falha ao encontrar o Build Report no arquivo Editor.log. Verifique se o projeto foi buildado recentemente nesta maquina. Se o erro continuar, fale conosco."));
                return;
            }

            _pendingBuildReportLines = buildReportStr.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();
            _pendingBuildReportLineIndex = _pendingBuildReportLines.FindIndex(line => line.StartsWith("Used Assets and files from the Resources folder"));
            if (_pendingBuildReportLineIndex < 0)
            {
                FailAnalysis(OxLoc.T(
                    "Failed to find Build Report in the Editor.log file. Please be sure the project was recently built on this machine. If the error persists, feel free to contact us.",
                    "Falha ao encontrar o Build Report no arquivo Editor.log. Verifique se o projeto foi buildado recentemente nesta maquina. Se o erro continuar, fale conosco."));
                return;
            }

            _pendingBuildReportLineIndex++;
            _pendingTreeElements = new List<BuildLogTreeItem>();
            _pendingTreeElements.Add(new BuildLogTreeItem("Root", -1, _pendingIdIncrement, 0, "", 0, ""));

            EditorApplication.update += UpdateAnalyzeBuildLogs;
        }

        private static void UpdateAnalyzeBuildLogs()
        {
            try
            {
                var processedLines = 0;
                while (_pendingBuildReportLines != null &&
                       _pendingBuildReportLineIndex < _pendingBuildReportLines.Count &&
                       processedLines < BuildLogBatchSize)
                {
                    var line = _pendingBuildReportLines[_pendingBuildReportLineIndex];
                    if (line.StartsWith("------------"))
                    {
                        CompleteAnalysis();
                        return;
                    }

                    _pendingBuildReportLineIndex++;
                    processedLines++;

                    var splitLine = line.Replace("\t", " ").Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    if (splitLine.Length < 3)
                    {
                        continue;
                    }

                    var size = float.Parse(splitLine[0], CultureInfo.InvariantCulture.NumberFormat);
                    var sizeUnit = splitLine[1];
                    var sizePercentage = float.Parse(splitLine[2].Replace("%", ""), CultureInfo.InvariantCulture.NumberFormat);

                    var path = line.Split(new[] { splitLine[2] }, StringSplitOptions.None).Last().Trim();

                    if (path.StartsWith("Packages/") && !_includeFilesFromPackages)
                    {
                        continue;
                    }

                    _pendingIdIncrement++;
                    _pendingTreeElements.Add(new BuildLogTreeItem("BuildLogLine", 0, _pendingIdIncrement, size, sizeUnit, sizePercentage, path));
                }

                if (_pendingBuildReportLines == null || _pendingBuildReportLineIndex >= _pendingBuildReportLines.Count)
                {
                    CompleteAnalysis();
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
                FailAnalysis(OxLoc.T(
                    "Failed to analyze the build report. Check the console for more details.",
                    "Falha ao analisar o build report. Confira o console para mais detalhes."));
            }
        }

        /// <summary>
        /// Creates an empty tree (header + columns, no rows) so the list is visible in the tab
        /// before any analysis. Analyzing later just repopulates it with the build files.
        /// </summary>
        private static void EnsureTree()
        {
            if (_buildLogTree != null)
            {
                return;
            }

            BuildTree(new List<BuildLogTreeItem>
            {
                new BuildLogTreeItem("Root", -1, 0, 0f, "", 0f, "")
            });
        }

        private static void BuildTree(List<BuildLogTreeItem> elements)
        {
            var treeModel = new TreeModel<BuildLogTreeItem>(elements);
            var treeViewState = new TreeViewState();
            _multiColumnHeaderState = _multiColumnHeaderState ?? new MultiColumnHeaderState(new[]
            {
                // when adding a new column don't forget to check the sorting method, and the CellGUI method
                new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Size"}, width = 80, minWidth = 60, canSort = true},
                new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Size %"}, width = 60, minWidth = 40, canSort = true},
                new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "Path"}, width = 300, minWidth = 200, canSort = true},
            });
            _buildLogTree = new BuildLogTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);
        }

        private static void CompleteAnalysis()
        {
            EditorApplication.update -= UpdateAnalyzeBuildLogs;

            BuildTree(_pendingTreeElements);

            _analyzedFileGrades = _pendingTreeElements
                .Where(item => item.depth == 0)
                .Select(item => item.SizeGrade)
                .ToList();

            _pendingBuildReportLines = null;
            _pendingTreeElements = null;
            _isAnalyzing = false;

            if (OxOptimizerWindow.EditorWindowInstance != null)
            {
                OxOptimizerWindow.EditorWindowInstance.Repaint();
            }
        }

        private static void FailAnalysis(string message)
        {
            EditorApplication.update -= UpdateAnalyzeBuildLogs;
            _pendingBuildReportLines = null;
            _pendingTreeElements = null;
            _pendingBuildReportLineIndex = 0;
            _pendingIdIncrement = 0;
            _errorMessage = message;
            _isAnalyzing = false;

            if (OxOptimizerWindow.EditorWindowInstance != null)
            {
                OxOptimizerWindow.EditorWindowInstance.Repaint();
            }
        }
    }
}
