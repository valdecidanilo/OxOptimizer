using System;
using System.IO;
using OxenteGames.OxOptimizer.TreeLib;
using UnityEditor;

namespace OxenteGames.OxOptimizer.Tabs
{
    public class BuildLogTreeItem : TreeElement
    {
        public readonly float size;
        public readonly string sizeUnit;
        public readonly float sizePercentage;
        public readonly string filePath;

        // Absolute-size thresholds (MB). Unlike the percentage, these actually go down as you
        // optimize, so green reflects real progress instead of a file's share of the build.
        public const float IdealSizeMB = 1f;
        public const float HeavySizeMB = 4f;

        // A single file shouldn't dominate the build. There is no official Unity threshold —
        // 6% is the same heuristic the Export tab uses to grade the overall build size.
        public const float IdealSizePercentage = 6f;

        /// <summary>
        /// Grades a build file by its absolute size (the metric that shrinks as you optimize),
        /// then escalates to at least a warning if it dominates more than
        /// <see cref="IdealSizePercentage"/> of the whole build.
        /// </summary>
        public OxGui.Grade SizeGrade
        {
            get
            {
                var mb = SizeInMegabytes;
                var grade = mb >= HeavySizeMB ? OxGui.Grade.Bad
                    : mb >= IdealSizeMB ? OxGui.Grade.Warning
                    : OxGui.Grade.Ok;

                if (grade == OxGui.Grade.Ok && sizePercentage > IdealSizePercentage)
                    grade = OxGui.Grade.Warning; // small file, but it dominates the build

                return grade;
            }
        }

        private float SizeInMegabytes
        {
            get
            {
                switch (sizeUnit)
                {
                    case "b":
                        return size / (1024f * 1024f);
                    case "kb":
                        return size / 1024f;
                    case "mb":
                        return size;
                    case "gb":
                        return size * 1024f;
                    default:
                        return size; // unknown unit: treat the raw number as MB (conservative)
                }
            }
        }

        public float sizeInBytes
        {
            get
            {
                switch (sizeUnit)
                {
                    case "kb":
                        return size * 1024;
                    case "mb":
                        return size * 1024 * 1024;
                    default:
                        throw new Exception("Unknown size unit " + sizeUnit);
                }
            }
        }


        public BuildLogTreeItem(string name, int depth, int id, float size, string sizeUnit, float sizePercentage, string filePath) : base(name, depth, id)
        {
            if (depth == -1)
                return;
            this.size = size;
            this.sizeUnit = sizeUnit;
            this.sizePercentage = sizePercentage;
            this.filePath = filePath;
        }
    }
}