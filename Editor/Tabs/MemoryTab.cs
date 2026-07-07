using UnityEditor;
using UnityEngine;

namespace OxenteGames.OxOptimizer.Tabs
{
    /// <summary>
    /// WebGL memory settings tab. The recommended preset below is the configuration that
    /// OxenteGames validated on real devices: it is the only combination that ran reliably
    /// on the majority of the mobile phones tested. Browsers on low/mid-range phones kill
    /// tabs that allocate too much memory upfront, so the heap starts small (128 MB) and
    /// grows geometrically in small capped steps up to a 768 MB ceiling.
    /// </summary>
    public class MemoryTab
    {
        public const int RecommendedInitialMemoryMB = 128;
        public const int RecommendedMaximumMemoryMB = 768;
        public const float RecommendedGeometricGrowthStep = 0.2f;
        public const int RecommendedGeometricGrowthCapMB = 32;

        public static void RenderGUI()
        {
#if UNITY_2021_2_OR_NEWER
            OxGui.Section(OxLoc.T("WebGL memory audit", "Auditoria de memória WebGL"));
            EditorGUILayout.HelpBox(
                OxLoc.T(
                    "Mobile-safe preset validated by OxenteGames on real devices. Starting with a small heap that grows " +
                    "geometrically in capped steps avoids the browser killing the tab on low-end phones, while still " +
                    "allowing the game to reach the memory it needs.",
                    "Preset seguro para mobile validado pela OxenteGames em aparelhos reais. Começar com um heap pequeno que " +
                    "cresce geometricamente em passos limitados evita que o navegador mate a aba em celulares mais fracos, " +
                    "sem impedir o jogo de alcançar a memória de que precisa."),
                MessageType.Info);

            OxGui.StatusRow(
                $"Initial Memory Size: {PlayerSettings.WebGL.initialMemorySize} MB " +
                OxLoc.T("(recommended", "(recomendado") + $": {RecommendedInitialMemoryMB} MB)",
                PlayerSettings.WebGL.initialMemorySize == RecommendedInitialMemoryMB,
                () => PlayerSettings.WebGL.initialMemorySize = RecommendedInitialMemoryMB,
                OxLoc.T(
                    "Memory allocated as soon as the page loads. Large initial allocations are the main cause of crashes on mobile browsers.",
                    "Memória alocada assim que a página carrega. Alocações iniciais grandes são a principal causa de crash em navegadores mobile."));

            OxGui.StatusRow(
                $"Maximum Memory Size: {PlayerSettings.WebGL.maximumMemorySize} MB " +
                OxLoc.T("(recommended", "(recomendado") + $": {RecommendedMaximumMemoryMB} MB)",
                PlayerSettings.WebGL.maximumMemorySize == RecommendedMaximumMemoryMB,
                () => PlayerSettings.WebGL.maximumMemorySize = RecommendedMaximumMemoryMB,
                OxLoc.T(
                    "Hard ceiling for the WASM heap. Values above ~1 GB frequently fail on 32-bit mobile browsers.",
                    "Teto do heap WASM. Valores acima de ~1 GB falham com frequência em navegadores mobile de 32 bits."));

            OxGui.StatusRow(
                $"Memory Growth Mode: {PlayerSettings.WebGL.memoryGrowthMode} " +
                OxLoc.T("(recommended: Geometric)", "(recomendado: Geometric)"),
                PlayerSettings.WebGL.memoryGrowthMode == WebGLMemoryGrowthMode.Geometric,
                () => PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric,
                OxLoc.T(
                    "Geometric growth resizes the heap in progressively larger steps, reducing the number of expensive heap reallocations.",
                    "O crescimento geométrico redimensiona o heap em passos progressivamente maiores, reduzindo o número de realocações caras."));

            OxGui.StatusRow(
                $"Geometric Memory Growth Step: {PlayerSettings.WebGL.geometricMemoryGrowthStep:0.##} " +
                OxLoc.T("(recommended", "(recomendado") + $": {RecommendedGeometricGrowthStep:0.##})",
                Mathf.Approximately(PlayerSettings.WebGL.geometricMemoryGrowthStep, RecommendedGeometricGrowthStep),
                () => PlayerSettings.WebGL.geometricMemoryGrowthStep = RecommendedGeometricGrowthStep,
                OxLoc.T(
                    "Each growth increases the heap by 20% of its current size.",
                    "Cada crescimento aumenta o heap em 20% do tamanho atual."));

            OxGui.StatusRow(
                $"Geometric Memory Growth Cap: {PlayerSettings.WebGL.memoryGeometricGrowthCap} MB " +
                OxLoc.T("(recommended", "(recomendado") + $": {RecommendedGeometricGrowthCapMB} MB)",
                PlayerSettings.WebGL.memoryGeometricGrowthCap == RecommendedGeometricGrowthCapMB,
                () => PlayerSettings.WebGL.memoryGeometricGrowthCap = RecommendedGeometricGrowthCapMB,
                OxLoc.T(
                    "Limits a single growth step to 32 MB, keeping each reallocation small enough for constrained devices.",
                    "Limita cada passo de crescimento a 32 MB, mantendo cada realocação pequena o suficiente para aparelhos limitados."));

            GUILayout.Space(15);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(OxLoc.T("Apply mobile-safe preset (all settings)", "Aplicar preset mobile (todas as opções)"), GUILayout.Height(30), GUILayout.Width(300)))
            {
                PlayerSettings.WebGL.initialMemorySize = RecommendedInitialMemoryMB;
                PlayerSettings.WebGL.maximumMemorySize = RecommendedMaximumMemoryMB;
                PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
                PlayerSettings.WebGL.geometricMemoryGrowthStep = RecommendedGeometricGrowthStep;
                PlayerSettings.WebGL.memoryGeometricGrowthCap = RecommendedGeometricGrowthCapMB;
                Debug.Log("[OxOptimizer] Applied mobile-safe WebGL memory preset.");
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                OxLoc.T(
                    "If your game genuinely needs a bigger heap on desktop, raise Maximum Memory Size only — keep the initial " +
                    "size and growth settings small so mobile browsers can still start the game.",
                    "Se o jogo realmente precisa de mais memória no desktop, aumente apenas a memória máxima — mantenha o valor " +
                    "inicial e o crescimento pequenos para que navegadores mobile ainda consigam iniciar o jogo."),
                MessageType.None);
#else
            EditorGUILayout.HelpBox(
                OxLoc.T(
                    "WebGL memory growth settings require Unity 2021.2 or newer. Configure the memory size manually in Player Settings.",
                    "As opções de crescimento de memória WebGL exigem Unity 2021.2 ou mais novo. Configure a memória manualmente no Player Settings."),
                MessageType.Warning);
#endif
        }
    }
}
