#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace PsenY7.VRCPhysBoneMerger
{
    public static class PhysBoneMergeCore
    {
        public sealed class MergeCluster
        {
            public Transform Parent;
            public List<VRCPhysBone> SiblingBones = new List<VRCPhysBone>();
            public string SmartName;
            public int BoneCount => SiblingBones != null ? SiblingBones.Count : 0;
        }

        public sealed class PerformanceStats
        {
            public int CurrentBoneCount;
            public int PredictedBoneCount;
            public int MergedGroupCount;
            public int ReducedBoneCount;

            public string CurrentRank;
            public string PredictedRank;
        }

        public static List<MergeCluster> Scan(GameObject avatarRoot, PhysBoneAutoMerger config)
        {
            List<MergeCluster> clusters = new List<MergeCluster>();
            if (avatarRoot == null || config == null) return clusters;

            var allBones = avatarRoot.GetComponentsInChildren<VRCPhysBone>(true);
            if (allBones == null || allBones.Length < 2) return clusters;

            // Group sibling PhysBones by parent transform
            Dictionary<Transform, List<VRCPhysBone>> parentMap = new Dictionary<Transform, List<VRCPhysBone>>();
            for (int i = 0; i < allBones.Length; i++)
            {
                var bone = allBones[i];
                if (bone == null || bone.transform == null) continue;

                Transform parent = bone.transform.parent;
                if (parent == null) continue;

                if (!parentMap.TryGetValue(parent, out var list))
                {
                    list = new List<VRCPhysBone>();
                    parentMap[parent] = list;
                }
                list.Add(bone);
            }

            foreach (var kvp in parentMap)
            {
                var siblings = kvp.Value;
                if (siblings.Count < 2) continue;

                List<List<VRCPhysBone>> grouped = ClusterSiblings(siblings, config);
                for (int g = 0; g < grouped.Count; g++)
                {
                    if (grouped[g].Count >= 2)
                    {
                        string smartName = GenerateSmartName(grouped[g]);
                        clusters.Add(new MergeCluster
                        {
                            Parent = kvp.Key,
                            SiblingBones = grouped[g],
                            SmartName = smartName
                        });
                    }
                }
            }

            return clusters;
        }

        private static List<List<VRCPhysBone>> ClusterSiblings(List<VRCPhysBone> bones, PhysBoneAutoMerger config)
        {
            List<List<VRCPhysBone>> result = new List<List<VRCPhysBone>>();
            if (bones == null || bones.Count == 0) return result;

            if (config.Strategy == PhysBoneAutoMerger.MergeStrategy.Aggressive)
            {
                result.Add(new List<VRCPhysBone>(bones));
                return result;
            }

            float numTol = config.Strategy == PhysBoneAutoMerger.MergeStrategy.Strict ? 0.001f : config.NumericTolerance;
            float curveTol = config.Strategy == PhysBoneAutoMerger.MergeStrategy.Strict ? 0.001f : config.CurveTolerance;
            bool ignoreCurves = config.Strategy == PhysBoneAutoMerger.MergeStrategy.Custom && config.IgnoreCurves;
            bool ignoreLimits = config.Strategy == PhysBoneAutoMerger.MergeStrategy.Custom && config.IgnoreLimitsRotation;

            List<VRCPhysBone> remaining = new List<VRCPhysBone>(bones);
            while (remaining.Count > 0)
            {
                var primary = remaining[0];
                remaining.RemoveAt(0);

                List<VRCPhysBone> currentGroup = new List<VRCPhysBone> { primary };
                for (int i = remaining.Count - 1; i >= 0; i--)
                {
                    var candidate = remaining[i];
                    if (ArePhysBonesCompatible(primary, candidate, numTol, curveTol, ignoreCurves, ignoreLimits))
                    {
                        currentGroup.Add(candidate);
                        remaining.RemoveAt(i);
                    }
                }
                result.Add(currentGroup);
            }

            return result;
        }

        private static bool ArePhysBonesCompatible(VRCPhysBone a, VRCPhysBone b, float numTol, float curveTol, bool ignoreCurves, bool ignoreLimits)
        {
            if (a == null || b == null) return false;

            // Physics parameters
            if (Mathf.Abs(a.pull - b.pull) > numTol) return false;
            if (Mathf.Abs(a.spring - b.spring) > numTol) return false;
            if (Mathf.Abs(a.stiffness - b.stiffness) > numTol) return false;
            if (Mathf.Abs(a.gravity - b.gravity) > numTol) return false;
            if (Mathf.Abs(a.gravityFalloff - b.gravityFalloff) > numTol) return false;
            if (Mathf.Abs(a.radius - b.radius) > numTol) return false;
            if (Mathf.Abs(a.immobile - b.immobile) > numTol) return false;

            // Limits
            if (!ignoreLimits)
            {
                if (Mathf.Abs(a.maxAngleX - b.maxAngleX) > numTol) return false;
                if (Mathf.Abs(a.maxAngleZ - b.maxAngleZ) > numTol) return false;
            }

            // Curves check
            if (!ignoreCurves)
            {
                if (!AreCurvesEqual(GetCurve(a, "pullCurve"), GetCurve(b, "pullCurve"), curveTol)) return false;
                if (!AreCurvesEqual(GetCurve(a, "springCurve"), GetCurve(b, "springCurve"), curveTol)) return false;
                if (!AreCurvesEqual(GetCurve(a, "stiffnessCurve"), GetCurve(b, "stiffnessCurve"), curveTol)) return false;
                if (!AreCurvesEqual(GetCurve(a, "gravityCurve"), GetCurve(b, "gravityCurve"), curveTol)) return false;
            }

            return true;
        }

        private static AnimationCurve GetCurve(VRCPhysBone bone, string fieldName)
        {
            if (bone == null) return null;
            try
            {
                FieldInfo field = typeof(VRCPhysBone).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (field != null) return field.GetValue(bone) as AnimationCurve;

                PropertyInfo prop = typeof(VRCPhysBone).GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null) return prop.GetValue(bone) as AnimationCurve;
            }
            catch { }
            return null;
        }

        private static bool AreCurvesEqual(AnimationCurve c1, AnimationCurve c2, float tol)
        {
            if (c1 == null && c2 == null) return true;
            if (c1 == null || c2 == null) return false;
            if (c1.length == 0 && c2.length == 0) return true;

            const int samples = 7;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                if (Mathf.Abs(c1.Evaluate(t) - c2.Evaluate(t)) > tol) return false;
            }
            return true;
        }

        public static int ExecuteMerge(List<MergeCluster> clusters, bool deduplicateColliders, bool useUndo = false)
        {
            if (clusters == null || clusters.Count == 0) return 0;
            int mergedCount = 0;

            for (int c = 0; c < clusters.Count; c++)
            {
                var cluster = clusters[c];
                if (cluster == null || cluster.SiblingBones == null || cluster.SiblingBones.Count < 2) continue;

                var bones = cluster.SiblingBones;
                var prime = bones[0];
                if (prime == null) continue;

                // Create container GameObject for merged PhysBone
                GameObject holder = new GameObject(cluster.SmartName ?? "Merged_PhysBone");
                if (useUndo) Undo.RegisterCreatedObjectUndo(holder, "Merge PhysBones");

                holder.transform.SetParent(cluster.Parent, false);
                holder.transform.localPosition = prime.transform.localPosition;
                holder.transform.localRotation = prime.transform.localRotation;
                holder.transform.localScale = prime.transform.localScale;
                holder.transform.SetSiblingIndex(prime.transform.GetSiblingIndex());

                VRCPhysBone merged = useUndo ? Undo.AddComponent<VRCPhysBone>(holder) : holder.AddComponent<VRCPhysBone>();
                EditorUtility.CopySerialized(prime, merged);
                merged.rootTransform = null; // Automatically drives all child branches

                // Reparent original sibling bone transforms under the new merged container
                for (int i = 0; i < bones.Count; i++)
                {
                    var b = bones[i];
                    if (b != null && b.transform != null)
                    {
                        if (useUndo) Undo.SetTransformParent(b.transform, holder.transform, "Merge PhysBones");
                        else b.transform.SetParent(holder.transform, true);
                    }
                }

                // Deduplicate colliders
                if (deduplicateColliders)
                {
                    MergeAndCleanColliders(merged, bones);
                }

                // Destroy original PhysBone components
                for (int i = 0; i < bones.Count; i++)
                {
                    if (bones[i] != null)
                    {
                        if (useUndo) Undo.DestroyObjectImmediate(bones[i]);
                        else UnityEngine.Object.DestroyImmediate(bones[i]);
                    }
                }

                mergedCount++;
            }

            return mergedCount;
        }

        private static void MergeAndCleanColliders(VRCPhysBone target, List<VRCPhysBone> sources)
        {
            if (target == null || sources == null) return;
            try
            {
                FieldInfo collidersField = typeof(VRCPhysBone).GetField("colliders", BindingFlags.Public | BindingFlags.Instance);
                if (collidersField == null) return;

                IList targetList = collidersField.GetValue(target) as IList;
                if (targetList == null) return;

                HashSet<object> seen = new HashSet<object>();
                for (int i = 0; i < sources.Count; i++)
                {
                    if (sources[i] == null) continue;
                    IList srcList = collidersField.GetValue(sources[i]) as IList;
                    if (srcList == null) continue;

                    foreach (var collider in srcList)
                    {
                        if (collider != null && !seen.Contains(collider))
                        {
                            seen.Add(collider);
                        }
                    }
                }

                targetList.Clear();
                foreach (var c in seen)
                {
                    targetList.Add(c);
                }
            }
            catch { }
        }

        public static PerformanceStats Evaluate(GameObject avatarRoot, List<MergeCluster> clusters)
        {
            var stats = new PerformanceStats();
            if (avatarRoot == null) return stats;

            var bones = avatarRoot.GetComponentsInChildren<VRCPhysBone>(true);
            stats.CurrentBoneCount = bones != null ? bones.Length : 0;

            int candidateBones = 0;
            if (clusters != null)
            {
                stats.MergedGroupCount = clusters.Count;
                for (int i = 0; i < clusters.Count; i++)
                {
                    if (clusters[i] != null) candidateBones += clusters[i].BoneCount;
                }
            }

            stats.ReducedBoneCount = candidateBones > 0 ? (candidateBones - stats.MergedGroupCount) : 0;
            stats.PredictedBoneCount = Mathf.Max(0, stats.CurrentBoneCount - stats.ReducedBoneCount);

            stats.CurrentRank = GetPerformanceRank(stats.CurrentBoneCount);
            stats.PredictedRank = GetPerformanceRank(stats.PredictedBoneCount);

            return stats;
        }

        private static string GetPerformanceRank(int count)
        {
            if (count <= 4) return "Excellent (极佳)";
            if (count <= 8) return "Good (良好)";
            if (count <= 16) return "Medium (中等)";
            if (count <= 32) return "Poor (较差)";
            return "Very Poor (极差)";
        }

        private static string GenerateSmartName(List<VRCPhysBone> bones)
        {
            if (bones == null || bones.Count == 0) return "Merged_PhysBone";
            string name0 = bones[0].gameObject.name;
            if (bones.Count == 1) return name0 + "_Merged";

            string prefix = name0;
            for (int i = 1; i < bones.Count; i++)
            {
                string curr = bones[i].gameObject.name;
                int j = 0;
                while (j < prefix.Length && j < curr.Length && prefix[j] == curr[j]) j++;
                prefix = prefix.Substring(0, j);
            }

            prefix = prefix.TrimEnd('_', ' ', '-', '.');
            if (string.IsNullOrWhiteSpace(prefix) || prefix.Length < 2)
            {
                return $"{name0}_Group_Merged";
            }
            return $"{prefix}_Merged";
        }
    }
}
#endif