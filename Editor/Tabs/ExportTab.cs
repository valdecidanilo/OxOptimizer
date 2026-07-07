using System.Collections.Generic;
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
        public static void RenderGUI()
        {
            OxGui.Section(OxLoc.T("Export settings audit", "Auditoria das opções de exportação"));

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
                        "\"Fix\" define o suporte a exceções como \"Explicitly thrown exceptions only\". \"None\" tem desempenho ainda melhor, mas leia sobre os trade-offs na documentação da Unity antes."));
            }

            if (typeof(PlayerSettings).GetProperty("stripEngineCode") != null)
            {
                OxGui.StatusRow("Strip Engine Code",
                    PlayerSettings.stripEngineCode,
                    () => PlayerSettings.stripEngineCode = true,
                    OxLoc.T(
                        "Medium or High stripping in Player Settings shrinks the build further, but read about the trade-offs in the Unity documentation first.",
                        "Stripping Medium ou High no Player Settings reduz ainda mais o build, mas leia sobre os trade-offs na documentação da Unity antes."));
            }

#if UNITY_2020 || UNITY_2021 || UNITY_2022 || UNITY_2023_1_OR_NEWER
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                RenderPostProcessingAudit(GraphicsSettings.defaultRenderPipeline);
            }
#endif

#if UNITY_2021 || UNITY_2022 || UNITY_2023_1_OR_NEWER
            // Unity has no public API for the preloaded shaders list, so read it from the serialized GraphicsSettings
            var serializedGraphicsSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var preloadedShadersCount = serializedGraphicsSettings.FindProperty("m_PreloadedShaders").arraySize;
            if (preloadedShadersCount > 0)
            {
                OxGui.InfoRow(
                    OxLoc.T(
                        $"Your project is preloading {preloadedShadersCount} shader(s). On WebGL, preloading shaders may considerably slow down the loading of the game.",
                        $"Seu projeto pré-carrega {preloadedShadersCount} shader(s). No WebGL, pré-carregar shaders pode deixar o carregamento do jogo consideravelmente mais lento."));
            }
#endif

            GUILayout.Space(10);
            OxGui.LinkButton(
                OxLoc.T("More WebGL optimization tips (Unity docs)", "Mais dicas de otimização WebGL (docs da Unity)"),
                "https://docs.unity3d.com/Manual/webgl-performance.html");
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
                    OxLoc.T("URP post-processing shaders excluded from the build", "Shaders de pós-processamento da URP excluídos do build"),
                    true, null);
                return;
            }

            OxGui.ActionRow(
                OxLoc.T(
                    "Your URP renderer includes post-processing shaders (~1 MB). If the game doesn't use post-processing (bloom, vignette, tonemapping...), you can strip them from the build.",
                    "Seu renderer URP inclui shaders de pós-processamento (~1 MB). Se o jogo não usa pós-processamento (bloom, vignette, tonemapping...), você pode removê-los do build."),
                OxLoc.T("Disable", "Desativar"),
                () => DisablePostProcessing(renderersWithPostFx));
        }

        private static SerializedProperty FindPostProcessDataProperty(SerializedObject rendererData)
        {
            return rendererData.FindProperty("postProcessData") ?? rendererData.FindProperty("m_PostProcessData");
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
                "Isto limpa a referência \"Post Process Data\" do(s) renderer(s) URP, removendo os shaders de " +
                "pós-processamento do build (~1 MB).\n\n" +
                "O jogo NÃO vai crashar: a URP simplesmente pula o pós-processamento quando os dados não existem. Mas se " +
                "o jogo usa efeitos como bloom, vignette ou tonemapping, eles deixarão de renderizar.\n\n" +
                "É reversível: pressione Ctrl+Z ou reatribua o \"Post Process Data\" no asset do renderer.");

            if (volumeProfileCount > 0)
                message += OxLoc.T(
                    $"\n\nWarning: {volumeProfileCount} Volume Profile asset(s) were found in this project — the game may be using post-processing. Double-check before disabling.",
                    $"\n\nAtenção: {volumeProfileCount} Volume Profile(s) foram encontrados no projeto — o jogo pode estar usando pós-processamento. Confira antes de desativar.");

            if (!EditorUtility.DisplayDialog(
                    OxLoc.T("Strip URP post-processing from build?", "Remover pós-processamento da URP do build?"),
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
