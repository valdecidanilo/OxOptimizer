using OxenteGames.OxOptimizer.TreeLib;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OxenteGames.OxOptimizer.Tabs
{
    public class AudioTreeItem : TreeElement
    {
        public string AudioPath { get; }
        public string AudioName { get; }

        public string LoadType
        {
            get
            {
                switch (_platformSettings.loadType)
                {
                    case AudioClipLoadType.DecompressOnLoad:
                        return "Decompress on load";
                    case AudioClipLoadType.CompressedInMemory:
                        return "Compressed in memory";
                    case AudioClipLoadType.Streaming:
                        return "Streaming";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public int Quality => Mathf.RoundToInt(_platformSettings.quality * 100);

        public bool IsForceToMono => _audioImporter.forceToMono;

        // Ideal targets (see the explanations at the bottom of the Audio tab).
        public const int IdealQuality = 70;
        private const int MusicQualityCap = 50;
        private const int MinRecommendedQuality = 35;
        private const float MusicLengthSeconds = 10f;

        /// <summary>
        /// Quality that avoids inflating the build. Unity re-encodes the source on
        /// import, so setting the importer quality above the source's own bitrate
        /// only makes the asset bigger with zero quality gain (the source is the
        /// quality ceiling). Rule of thumb for WebGL: music 35-50, short effects 50-70.
        /// </summary>
        public int RecommendedQuality
        {
            get
            {
                var cap = _clipLength >= MusicLengthSeconds ? MusicQualityCap : IdealQuality;
                if (!_hasCompressedSource || _sourceKbps <= 0f)
                    return cap;
                // Unity's Vorbis quality slider maps roughly to bitrate; target the
                // quality whose output bitrate matches the (already lossy) source.
                var match = Mathf.RoundToInt(_sourceKbps * 0.55f / 5f) * 5;
                return Mathf.Clamp(match, MinRecommendedQuality, cap);
            }
        }

        public bool NeedsQualityFix => Quality > RecommendedQuality;

        // Decompress On Load keeps the clip uncompressed in RAM — not ideal for WebGL.
        public OxGui.Grade LoadTypeGrade =>
            _platformSettings.loadType == AudioClipLoadType.DecompressOnLoad ? OxGui.Grade.Warning : OxGui.Grade.Ok;

        public OxGui.Grade ForceToMonoGrade => IsForceToMono ? OxGui.Grade.Ok : OxGui.Grade.Warning;

        public OxGui.Grade QualityGrade =>
            !NeedsQualityFix ? OxGui.Grade.Ok
            : Quality <= RecommendedQuality + 20 ? OxGui.Grade.Warning
            : OxGui.Grade.Bad;

        private readonly AudioImporter _audioImporter;
        private AudioImporterSampleSettings _platformSettings;
        private readonly float _clipLength;
        private readonly float _sourceKbps;
        private readonly bool _hasCompressedSource;

        public AudioTreeItem(string name, int depth, int id, string audioPath, AudioImporter audioImporter) : base(name, depth, id)
        {
            if (depth == -1)
                return;

            AudioPath = audioPath;
            AudioName = Path.GetFileName(audioPath);

            _audioImporter = audioImporter;
            _platformSettings = _audioImporter.GetOverrideSampleSettings("WebGL");

            var extension = Path.GetExtension(audioPath).ToLowerInvariant();
            _hasCompressedSource = extension == ".ogg" || extension == ".mp3";

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
            _clipLength = clip != null ? clip.length : 0f;
            if (_clipLength > 0.1f)
            {
                // Asset paths are relative to the project root, which is Unity's CWD.
                var fileInfo = new FileInfo(audioPath);
                if (fileInfo.Exists)
                    _sourceKbps = fileInfo.Length * 8f / _clipLength / 1000f;
            }
        }

        public void ApplyRecommendedQuality()
        {
            var quality = RecommendedQuality / 100f;
            if (_audioImporter.ContainsSampleSettingsOverride("WebGL"))
            {
                var settings = _audioImporter.GetOverrideSampleSettings("WebGL");
                settings.quality = quality;
                _audioImporter.SetOverrideSampleSettings("WebGL", settings);
            }
            else
            {
                var settings = _audioImporter.defaultSampleSettings;
                settings.quality = quality;
                _audioImporter.defaultSampleSettings = settings;
            }

            _audioImporter.SaveAndReimport();
            _platformSettings = _audioImporter.GetOverrideSampleSettings("WebGL");
        }
    }
}