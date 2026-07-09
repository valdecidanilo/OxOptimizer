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

        // Decompress On Load keeps the clip uncompressed in RAM — not ideal for WebGL.
        public OxGui.Grade LoadTypeGrade =>
            _platformSettings.loadType == AudioClipLoadType.DecompressOnLoad ? OxGui.Grade.Warning : OxGui.Grade.Ok;

        public OxGui.Grade ForceToMonoGrade => IsForceToMono ? OxGui.Grade.Ok : OxGui.Grade.Warning;

        public OxGui.Grade QualityGrade =>
            Quality <= IdealQuality ? OxGui.Grade.Ok
            : Quality <= 90 ? OxGui.Grade.Warning
            : OxGui.Grade.Bad;

        private readonly AudioImporter _audioImporter;
        private readonly AudioImporterSampleSettings _platformSettings;

        public AudioTreeItem(string name, int depth, int id, string audioPath, AudioImporter audioImporter) : base(name, depth, id)
        {
            if (depth == -1)
                return;

            AudioPath = audioPath;
            AudioName = Path.GetFileName(audioPath);

            _audioImporter = audioImporter;
            _platformSettings = _audioImporter.GetOverrideSampleSettings("WebGL");
        }
    }
}