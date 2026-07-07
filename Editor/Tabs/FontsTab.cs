using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OxenteGames.OxOptimizer.Tabs
{
    /// <summary>
    /// Audits .ttf/.otf fonts: "Include Font Data" embeds the whole font file in the build.
    /// Projects that render text with TextMesh Pro (SDF atlases) don't need the source font
    /// embedded, so unchecking it can save hundreds of KB per font. Legacy UI Text with
    /// dynamic fonts still needs the font data — the hint below warns about that.
    /// </summary>
    public class FontsTab
    {
        private class FontEntry
        {
            public string Path;
            public TrueTypeFontImporter Importer;
            public long SizeBytes;
        }

        private static List<FontEntry> _fonts;
        private static Vector2 _scrollPos;

        public static void RenderGUI()
        {
            OxGui.Section(OxLoc.T("Fonts audit (.ttf / .otf)", "Auditoria de fontes (.ttf / .otf)"));

            EditorGUILayout.HelpBox(
                OxLoc.T(
                    "\"Include Font Data\" embeds the entire font file in the build. If your text is rendered with " +
                    "TextMesh Pro (SDF font assets), the source font doesn't need to be embedded — disable it to shrink " +
                    "the build. Keep it enabled only for fonts used by legacy UI Text / Text Mesh components.",
                    "\"Include Font Data\" embute o arquivo inteiro da fonte no build. Se o texto é renderizado com " +
                    "TextMesh Pro (assets SDF), a fonte original não precisa ser embutida — desative para reduzir o build. " +
                    "Mantenha ativado apenas em fontes usadas por componentes legados UI Text / Text Mesh."),
                MessageType.Info);

            if (_fonts == null)
                ScanFonts();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(OxLoc.T($"{_fonts.Count} font(s) found", $"{_fonts.Count} fonte(s) encontrada(s)"), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(OxLoc.T("Rescan", "Reescanear"), GUILayout.Width(90)))
                ScanFonts();
            EditorGUILayout.EndHorizontal();

            if (_fonts.Count == 0)
            {
                OxGui.InfoRow(OxLoc.T("No .ttf/.otf fonts found under Assets/.", "Nenhuma fonte .ttf/.otf encontrada em Assets/."));
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            foreach (var font in _fonts)
            {
                var entry = font;
                OxGui.StatusRow(
                    $"{Path.GetFileName(entry.Path)}  ({entry.SizeBytes / 1024} KB)  —  {entry.Path}",
                    !entry.Importer.includeFontData,
                    () => SetIncludeFontData(entry, false));
            }
            EditorGUILayout.EndScrollView();

            var embeddedCount = _fonts.Count(f => f.Importer.includeFontData);
            if (embeddedCount > 0)
            {
                GUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(
                        OxLoc.T($"Disable \"Include Font Data\" on all ({embeddedCount})", $"Desativar \"Include Font Data\" em todas ({embeddedCount})"),
                        GUILayout.Height(28), GUILayout.Width(300)))
                {
                    if (EditorUtility.DisplayDialog(
                            OxLoc.T("Disable Include Font Data?", "Desativar Include Font Data?"),
                            OxLoc.T(
                                "This unchecks \"Include Font Data\" on all listed fonts and reimports them.\n\n" +
                                "Safe if your text uses TextMesh Pro. Fonts used by legacy UI Text / Text Mesh components " +
                                "will stop rendering their glyphs in the build — re-enable those individually if needed.",
                                "Isto desmarca \"Include Font Data\" em todas as fontes listadas e as reimporta.\n\n" +
                                "Seguro se o texto usa TextMesh Pro. Fontes usadas por componentes legados UI Text / Text Mesh " +
                                "deixarão de renderizar no build — reative essas individualmente se necessário."),
                            OxLoc.T("Disable all", "Desativar todas"),
                            OxLoc.T("Cancel", "Cancelar")))
                    {
                        foreach (var font in _fonts.Where(f => f.Importer.includeFontData))
                            SetIncludeFontData(font, false);
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void ScanFonts()
        {
            _fonts = new List<FontEntry>();
            foreach (var guid in AssetDatabase.FindAssets("t:Font"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                // only project fonts (skip read-only packages) and only source .ttf/.otf files
                if (!path.StartsWith("Assets/"))
                    continue;
                var extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".ttf" && extension != ".otf")
                    continue;
                if (AssetImporter.GetAtPath(path) is TrueTypeFontImporter importer)
                {
                    _fonts.Add(new FontEntry
                    {
                        Path = path,
                        Importer = importer,
                        SizeBytes = new FileInfo(path).Length,
                    });
                }
            }

            _fonts = _fonts.OrderByDescending(f => f.Importer.includeFontData).ThenByDescending(f => f.SizeBytes).ToList();
        }

        private static void SetIncludeFontData(FontEntry entry, bool include)
        {
            entry.Importer.includeFontData = include;
            entry.Importer.SaveAndReimport();
            Debug.Log($"[OxOptimizer] \"Include Font Data\" set to {include} on \"{entry.Path}\".");
        }
    }
}
