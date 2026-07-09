using OxenteGames.OxOptimizer.TreeLib;
using System;
using System.IO;
using UnityEditor;

namespace OxenteGames.OxOptimizer.Tabs
{
    public class TextureTreeItem : TreeElement
    {
        public string TexturePath { get; }
        public string TextureName { get; }

        public int TextureMaxSize => _platformSettings.maxTextureSize;
        public int CrunchCompressionQuality => _platformSettings.compressionQuality;
        public bool HasCrunchCompression => _platformSettings.crunchedCompression;
        public TextureImporterFormat TextureFormat => _platformSettings.format;
        public TextureImporterType TextureType => _textureImporter.textureType;
        public TextureImporterCompression TextureCompression => _platformSettings.textureCompression;

        public string TextureCompressionName
        {
            get
            {
                switch (TextureCompression)
                {
                    case TextureImporterCompression.Uncompressed:
                        return "Uncompressed";
                    case TextureImporterCompression.Compressed:
                        return "Normal";
                    case TextureImporterCompression.CompressedHQ:
                        return "High";
                    case TextureImporterCompression.CompressedLQ:
                        return "Low";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        // Ideal targets for WebGL (see the explanations at the bottom of the Textures tab).
        public const int IdealMaxSize = 1024;
        public const int IdealCrunchQuality = 70;

        public OxGui.Grade MaxSizeGrade =>
            TextureMaxSize <= IdealMaxSize ? OxGui.Grade.Ok
            : TextureMaxSize <= 2048 ? OxGui.Grade.Warning
            : OxGui.Grade.Bad;

        public OxGui.Grade CompressionGrade =>
            TextureCompression == TextureImporterCompression.Uncompressed ? OxGui.Grade.Bad : OxGui.Grade.Ok;

        public OxGui.Grade CrunchGrade => HasCrunchCompression ? OxGui.Grade.Ok : OxGui.Grade.Warning;

        public OxGui.Grade CrunchQualityGrade
        {
            get
            {
                if (!HasCrunchCompression)
                    return OxGui.Grade.Ok; // quality is irrelevant when crunch is disabled
                if (CrunchCompressionQuality <= IdealCrunchQuality)
                    return OxGui.Grade.Ok;
                return CrunchCompressionQuality <= 85 ? OxGui.Grade.Warning : OxGui.Grade.Bad;
            }
        }

        private readonly TextureImporter _textureImporter;
        private readonly TextureImporterPlatformSettings _platformSettings;

        public TextureTreeItem(string name, int depth, int id, string texturePath, TextureImporter textureImporter) : base(name, depth, id)
        {
            if (depth == -1)
                return;

            TexturePath = texturePath;
            TextureName = Path.GetFileName(texturePath);

            _textureImporter = textureImporter;
            // GetPlatformTextureSettings returns the stored WebGL block even when the
            // "Override for WebGL" toggle is off; Unity then actually uses the Default
            // settings, so fall back to them to match what the inspector shows.
            var webglSettings = _textureImporter.GetPlatformTextureSettings("WebGL");
            _platformSettings = webglSettings.overridden
                ? webglSettings
                : _textureImporter.GetDefaultPlatformTextureSettings();
        }
    }
}