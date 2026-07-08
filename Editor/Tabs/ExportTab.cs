using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OxenteGames.OxOptimizer.Tabs
{
    /// <summary>
    /// Audits the WebGL export settings that have the biggest impact on build size
    /// and loading time, with one-click fixes.
    /// </summary>
    public class ExportTab
    {
        private const double ScoreRefreshIntervalSeconds = 2.0;
        private static OptimizationScore _cachedScore;
        private static double _lastScoreRefreshTime = -ScoreRefreshIntervalSeconds;

        private struct OptimizationScore
        {
            public float PassedWeight;
            public float TotalWeight;
            public int PassedChecks;
            public int TotalChecks;

            public float Score => TotalWeight <= 0f ? 0f : PassedWeight / TotalWeight;
            public int Percentage => Mathf.RoundToInt(Score * 100f);
        }

        public static void RenderGUI()
        {
            var stats = EvaluateOptimizationScore();
            RenderOverview(stats);

            OxGui.Section(OxLoc.T("Export settings audit", "Auditoria das opcoes de exportacao"));

            if (typeof(PlayerSettings.WebGL).GetProperty("compressionFormat") != null)
            {
                OxGui.StatusRow("Compression Format: Brotli",
                    PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Brotli,
                    () => PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli);
            }

            if (typeof(PlayerSettings.WebGL).GetProperty("nameFilesAsHashes") != null)
            {
                OxGui.StatusRow("Name Files As Hashes",
                    PlayerSettings.WebGL.nameFilesAsHashes,
                    () => PlayerSettings.WebGL.nameFilesAsHashes = true);
            }

            if (typeof(PlayerSettings.WebGL).GetProperty("exceptionSupport") != null)
            {
                OxGui.StatusRow("Enable Exceptions",
                    PlayerSettings.WebGL.exceptionSupport == WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly,
                    () => PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly,
                    OxLoc.T(
                        "\"Fix\" sets exception support to \"Explicitly thrown exceptions only\". \"None\" performs even better, but read about the trade-offs in the Unity documentation first.",
                        "\"Corrigir\" define o suporte a excecoes como \"Explicitly thrown exceptions only\". \"None\" tem desempenho ainda melhor, mas leia sobre os trade-offs na documentacao da Unity antes."));
            }

            if (typeof(PlayerSettings).GetProperty("stripEngineCode") != null)
            {
                OxGui.StatusRow("Strip Engine Code",
                    PlayerSettings.stripEngineCode,
                    () => PlayerSettings.stripEngineCode = true,
                    OxLoc.T(
                        "Medium or High stripping in Player Settings shrinks the build further, but read about the trade-offs in the Unity documentation first.",
                        "Stripping Medium ou High no Player Settings reduz ainda mais o build, mas leia sobre os trade-offs na documentacao da Unity antes."));
            }

#if UNITY_2020 || UNITY_2021 || UNITY_2022 || UNITY_2023_1_OR_NEWER
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                RenderPostProcessingAudit(GraphicsSettings.defaultRenderPipeline);
            }
#endif

#if UNITY_2021 || UNITY_2022 || UNITY_2023_1_OR_NEWER
            // Unity has no public API for the preloaded shaders list, so read it from the serialized GraphicsSettings.
            var serializedGraphicsSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var preloadedShadersCount = serializedGraphicsSettings.FindProperty("m_PreloadedShaders").arraySize;
            if (preloadedShadersCount > 0)
            {
                OxGui.InfoRow(
                    OxLoc.T(
                        $"Your project is preloading {preloadedShadersCount} shader(s). On WebGL, preloading shaders may considerably slow down the loading of the game.",
                        $"Seu projeto pre-carrega {preloadedShadersCount} shader(s). No WebGL, pre-carregar shaders pode deixar o carregamento do jogo consideravelmente mais lento."));
            }
#endif

            GUILayout.Space(10);
            OxGui.LinkButton(
                OxLoc.T("More WebGL optimization tips (Unity docs)", "Mais dicas de otimizacao WebGL (docs da Unity)"),
                "https://docs.unity3d.com/Manual/webgl-performance.html");
        }

        private static void RenderOverview(OptimizationScore stats)
        {
            const float height = 108f;
            var rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));

            EditorGUI.DrawRect(rect, OxGui.Dark);

            var border = new Rect(rect.x, rect.y, rect.width, 2f);
            EditorGUI.DrawRect(border, OxGui.Accent);

            var titleRect = new Rect(rect.x + 18f, rect.y + 16f, rect.width * 0.55f, 24f);
            GUI.Label(titleRect, OxLoc.T("Overall optimization", "Visao geral da otimizacao"), EditorStyles.boldLabel);

            var subtitleRect = new Rect(rect.x + 18f, rect.y + 44f, rect.width * 0.6f, 42f);
            GUI.Label(subtitleRect, OxLoc.T(
                $"{stats.PassedChecks} of {stats.TotalChecks} project checks are already optimized.",
                $"{stats.PassedChecks} de {stats.TotalChecks} verificacoes do projeto ja estao otimizadas."),
                EditorStyles.wordWrappedMiniLabel);

            var gaugeRect = new Rect(rect.xMax - 96f, rect.y + 12f, 72f, 72f);
            DrawCircularGauge(gaugeRect, stats.Score);

            var percentageRect = new Rect(gaugeRect.x - 2f, gaugeRect.y + 76f, gaugeRect.width + 4f, 18f);
            GUI.Label(percentageRect, OxLoc.T(
                $"{stats.Percentage}% optimized",
                $"{stats.Percentage}% otimizado"),
                EditorStyles.centeredGreyMiniLabel);
        }

        private static void DrawCircularGauge(Rect rect, float progress)
        {
            progress = Mathf.Clamp01(progress);

            var center = rect.center;
            var outerRadius = rect.width * 0.5f;
            var innerRadius = outerRadius * 0.64f;

            Handles.BeginGUI();
            var previousColor = Handles.color;

            Handles.color = new Color(OxGui.Dark.r, OxGui.Dark.g, OxGui.Dark.b, 1f);
            Handles.DrawSolidDisc(center, Vector3.forward, outerRadius);

            Handles.color = new Color(1f, 1f, 1f, 0.08f);
            Handles.DrawWireDisc(center, Vector3.forward, outerRadius - 0.5f);

            if (progress > 0f)
            {
                Handles.color = OxGui.Accent;
                Handles.DrawSolidArc(center, Vector3.forward, Vector3.up, 360f * progress, outerRadius);
            }

            Handles.color = new Color(OxGui.Dark.r, OxGui.Dark.g, OxGui.Dark.b, 1f);
            Handles.DrawSolidDisc(center, Vector3.forward, innerRadius);

            Handles.color = previousColor;
            Handles.EndGUI();

            GUI.Label(rect, $"{Mathf.RoundToInt(progress * 100f)}%", new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                normal =
                {
                    textColor = Color.white
                }
            });
        }

        private static OptimizationScore EvaluateOptimizationScore()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastScoreRefreshTime < ScoreRefreshIntervalSeconds)
            {
                return _cachedScore;
            }

            var stats = new OptimizationScore();

            AddExportChecks(ref stats);
            AddMemoryChecks(ref stats);
            AddTextureChecks(ref stats);
            AddAudioChecks(ref stats);
            AddFontChecks(ref stats);
            AddModelChecks(ref stats);

            _cachedScore = stats;
            _lastScoreRefreshTime = now;
            return stats;
        }

        private static void AddExportChecks(ref OptimizationScore stats)
        {
            AddCheck(typeof(PlayerSettings.WebGL).GetProperty("compressionFormat") != null,
                PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Brotli, ref stats);

            AddCheck(typeof(PlayerSettings.WebGL).GetProperty("nameFilesAsHashes") != null,
                PlayerSettings.WebGL.nameFilesAsHashes, ref stats);

            AddCheck(typeof(PlayerSettings.WebGL).GetProperty("exceptionSupport") != null,
                PlayerSettings.WebGL.exceptionSupport == WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly, ref stats);

            AddCheck(typeof(PlayerSettings).GetProperty("stripEngineCode") != null,
                PlayerSettings.stripEngineCode, ref stats);

#if UNITY_2020 || UNITY_2021 || UNITY_2022 || UNITY_2023_1_OR_NEWER
            if (GraphicsSettings.defaultRenderPipeline != null && IsUrpPipeline(GraphicsSettings.defaultRenderPipeline))
            {
                AddCheck(true, HasURPPostProcessingDisabled(GraphicsSettings.defaultRenderPipeline), ref stats);
            }
#endif

#if UNITY_2021 || UNITY_2022 || UNITY_2023_1_OR_NEWER
            var serializedGraphicsSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var preloadedShaders = serializedGraphicsSettings.FindProperty("m_PreloadedShaders");
            if (preloadedShaders != null)
            {
                AddCheck(true, preloadedShaders.arraySize == 0, ref stats);
            }
#endif
        }

        private static void AddMemoryChecks(ref OptimizationScore stats)
        {
#if UNITY_2021_2_OR_NEWER
            AddCheck(true, PlayerSettings.WebGL.initialMemorySize == MemoryTab.RecommendedInitialMemoryMB, ref stats);
            AddCheck(true, PlayerSettings.WebGL.maximumMemorySize == MemoryTab.RecommendedMaximumMemoryMB, ref stats);
            AddCheck(true, PlayerSettings.WebGL.memoryGrowthMode == WebGLMemoryGrowthMode.Geometric, ref stats);
            AddCheck(true, Mathf.Approximately(PlayerSettings.WebGL.geometricMemoryGrowthStep, MemoryTab.RecommendedGeometricGrowthStep), ref stats);
            AddCheck(true, PlayerSettings.WebGL.memoryGeometricGrowthCap == MemoryTab.RecommendedGeometricGrowthCapMB, ref stats);
#endif
        }

        private static void AddTextureChecks(ref OptimizationScore stats)
        {
            foreach (var texturePath in FindProjectAssetPaths("t:Texture2D"))
            {
                if (!(AssetImporter.GetAtPath(texturePath) is TextureImporter textureImporter))
                {
                    continue;
                }

                var platformSettings = textureImporter.GetPlatformTextureSettings("WebGL");
                AddCheck(true, textureImporter.crunchedCompression, ref stats, 3f);
                AddCheck(true, IsTexturePowerOfTwo(textureImporter), ref stats);
                AddCheck(true, textureImporter.crunchedCompression && platformSettings.compressionQuality <= 70, ref stats);
                AddCheck(true, platformSettings.maxTextureSize > 0 && platformSettings.maxTextureSize <= 1024, ref stats);
            }
        }

        private static void AddAudioChecks(ref OptimizationScore stats)
        {
            foreach (var audioPath in FindProjectAssetPaths("t:AudioClip"))
            {
                if (!(AssetImporter.GetAtPath(audioPath) is AudioImporter audioImporter))
                {
                    continue;
                }

                var settings = audioImporter.GetOverrideSampleSettings("WebGL");

                AddCheck(true, audioImporter.forceToMono, ref stats);
                AddCheck(true,
                    settings.loadType == AudioClipLoadType.CompressedInMemory ||
                    settings.loadType == AudioClipLoadType.Streaming,
                    ref stats);
            }
        }

        private static void AddFontChecks(ref OptimizationScore stats)
        {
            foreach (var fontPath in FindProjectAssetPaths("t:Font"))
            {
                var extension = Path.GetExtension(fontPath).ToLowerInvariant();
                if (extension != ".ttf" && extension != ".otf")
                {
                    continue;
                }

                if (AssetImporter.GetAtPath(fontPath) is TrueTypeFontImporter fontImporter)
                {
                    AddCheck(true, !fontImporter.includeFontData, ref stats);
                }
            }
        }

        private static void AddModelChecks(ref OptimizationScore stats)
        {
            foreach (var modelPath in FindProjectAssetPaths("t:GameObject"))
            {
                if (!IsModelAssetPath(modelPath))
                {
                    continue;
                }

                if (AssetImporter.GetAtPath(modelPath) is ModelImporter modelImporter)
                {
                    AddCheck(true, modelImporter.meshCompression == ModelImporterMeshCompression.High, ref stats);
                }
            }
        }

        private static void AddCheck(bool applicable, bool ok, ref OptimizationScore stats, float weight = 1f)
        {
            if (!applicable)
            {
                return;
            }

            stats.TotalChecks++;
            stats.TotalWeight += weight;
            if (ok)
            {
                stats.PassedChecks++;
                stats.PassedWeight += weight;
            }
        }

        private static IEnumerable<string> FindProjectAssetPaths(string filter)
        {
            return AssetDatabase.FindAssets(filter, new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsRuntimeProjectAssetPath);
        }

        private static bool IsTexturePowerOfTwo(TextureImporter textureImporter)
        {
            try
            {
                textureImporter.GetSourceTextureWidthAndHeight(out var width, out var height);
                return Mathf.IsPowerOfTwo(width) && Mathf.IsPowerOfTwo(height);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsRuntimeProjectAssetPath(string path)
        {
            return path.StartsWith("Assets/") &&
                   path.IndexOf("/Editor/", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                   !path.EndsWith("/Editor", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsModelAssetPath(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".fbx" ||
                   extension == ".dae" ||
                   extension == ".3ds" ||
                   extension == ".dxf" ||
                   extension == ".obj";
        }

        /// <summary>
        /// URP ships ~1 MB of post-processing shaders in every build. When the project doesn't
        /// use post-processing, clearing the renderer's PostProcessData reference strips them.
        /// Everything is accessed through SerializedObject so the package doesn't need a
        /// compile-time dependency on the URP assembly (field is "postProcessData" on the 3D
        /// UniversalRendererData and "m_PostProcessData" on the 2D Renderer2DData).
        /// </summary>
        private static void RenderPostProcessingAudit(RenderPipelineAsset pipeline)
        {
            var pipelineObject = new SerializedObject(pipeline);
            var rendererList = pipelineObject.FindProperty("m_RendererDataList");
            if (rendererList == null || !rendererList.isArray)
                return; // not URP (e.g. HDRP), nothing to audit here

            var renderersWithPostFx = new List<Object>();
            for (var i = 0; i < rendererList.arraySize; i++)
            {
                var rendererData = rendererList.GetArrayElementAtIndex(i).objectReferenceValue;
                if (rendererData == null)
                    continue;
                var postProcessData = FindPostProcessDataProperty(new SerializedObject(rendererData));
                if (postProcessData != null && postProcessData.objectReferenceValue != null)
                    renderersWithPostFx.Add(rendererData);
            }

            if (renderersWithPostFx.Count == 0)
            {
                OxGui.StatusRow(
                    OxLoc.T("URP post-processing shaders excluded from the build", "Shaders de pos-processamento da URP excluidos do build"),
                    true, null);
                return;
            }

            OxGui.ActionRow(
                OxLoc.T(
                    "Your URP renderer includes post-processing shaders (~1 MB). If the game doesn't use post-processing (bloom, vignette, tonemapping...), you can strip them from the build.",
                    "Seu renderer URP inclui shaders de pos-processamento (~1 MB). Se o jogo nao usa pos-processamento (bloom, vignette, tonemapping...), voce pode remove-los do build."),
                OxLoc.T("Disable", "Desativar"),
                () => DisablePostProcessing(renderersWithPostFx));
        }

        private static SerializedProperty FindPostProcessDataProperty(SerializedObject rendererData)
        {
            return rendererData.FindProperty("postProcessData") ?? rendererData.FindProperty("m_PostProcessData");
        }

        private static bool HasURPPostProcessingDisabled(RenderPipelineAsset pipeline)
        {
            var pipelineObject = new SerializedObject(pipeline);
            var rendererList = pipelineObject.FindProperty("m_RendererDataList");
            if (rendererList == null || !rendererList.isArray)
                return false;

            for (var i = 0; i < rendererList.arraySize; i++)
            {
                var rendererData = rendererList.GetArrayElementAtIndex(i).objectReferenceValue;
                if (rendererData == null)
                    continue;
                var postProcessData = FindPostProcessDataProperty(new SerializedObject(rendererData));
                if (postProcessData != null && postProcessData.objectReferenceValue != null)
                    return false;
            }

            return true;
        }

        private static bool IsUrpPipeline(RenderPipelineAsset pipeline)
        {
            var pipelineObject = new SerializedObject(pipeline);
            var rendererList = pipelineObject.FindProperty("m_RendererDataList");
            return rendererList != null && rendererList.isArray;
        }

        private static void DisablePostProcessing(List<Object> renderersWithPostFx)
        {
            var volumeProfileCount = AssetDatabase.FindAssets("t:VolumeProfile").Length;

            var message = OxLoc.T(
                "This clears the \"Post Process Data\" reference on your URP renderer(s), removing the post-processing " +
                "shaders from the build (~1 MB).\n\n" +
                "The game will NOT crash: URP simply skips post-processing when the data is missing. But if the game " +
                "does use effects such as bloom, vignette or tonemapping, they will stop rendering.\n\n" +
                "This is reversible: press Ctrl+Z, or reassign \"Post Process Data\" on the renderer asset.",
                "Isto limpa a referencia \"Post Process Data\" do(s) renderer(s) URP, removendo os shaders de " +
                "pos-processamento do build (~1 MB).\n\n" +
                "O jogo NAO vai crashar: a URP simplesmente pula o pos-processamento quando os dados nao existem. Mas se " +
                "o jogo usa efeitos como bloom, vignette ou tonemapping, eles deixarao de renderizar.\n\n" +
                "E reversivel: pressione Ctrl+Z ou reatribua o \"Post Process Data\" no asset do renderer.");

            if (volumeProfileCount > 0)
                message += OxLoc.T(
                    $"\n\nWarning: {volumeProfileCount} Volume Profile asset(s) were found in this project - the game may be using post-processing. Double-check before disabling.",
                    $"\n\nAtencao: {volumeProfileCount} Volume Profile(s) foram encontrados no projeto - o jogo pode estar usando pos-processamento. Confira antes de desativar.");

            if (!EditorUtility.DisplayDialog(
                    OxLoc.T("Strip URP post-processing from build?", "Remover pos-processamento da URP do build?"),
                    message,
                    OxLoc.T("Disable", "Desativar"),
                    OxLoc.T("Cancel", "Cancelar")))
                return;

            foreach (var rendererData in renderersWithPostFx)
            {
                var rendererObject = new SerializedObject(rendererData);
                var postProcessData = FindPostProcessDataProperty(rendererObject);
                Debug.Log($"[OxOptimizer] Cleared Post Process Data \"{postProcessData.objectReferenceValue.name}\" on renderer \"{rendererData.name}\". Reassign it in the renderer inspector to restore.");
                postProcessData.objectReferenceValue = null;
                rendererObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(rendererData);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
