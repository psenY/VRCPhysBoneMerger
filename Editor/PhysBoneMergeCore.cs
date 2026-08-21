#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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
            public VRCPhysBone Representative => SiblingBones != null && SiblingBones.Count > 0 ? SiblingBones[0] : null;
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

                // Safety: must have compatible rootTransform (null or pointing to self)
                if (!HasMergeCompatibleRootTransform(bone)) continue;

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

            List<VRCPhysBone> remaining = new List<VRCPhysBone>(bones);
            remaining.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            while (remaining.Count > 0)
            {
                var primary = remaining[0];
                remaining.RemoveAt(0);

                List<VRCPhysBone> currentGroup = new List<VRCPhysBone> { primary };
                for (int i = remaining.Count - 1; i >= 0; i--)
                {
                    var candidate = remaining[i];
                    if (ArePhysBonesEquivalent(primary, candidate, config))
                    {
                        currentGroup.Add(candidate);
                        remaining.RemoveAt(i);
                    }
                }
                result.Add(currentGroup);
            }

            return result;
        }

        private static bool ArePhysBonesEquivalent(VRCPhysBone left, VRCPhysBone right, PhysBoneAutoMerger config)
        {
            if (left == null || right == null) return false;
            if (ReferenceEquals(left, right)) return true;

            // 1. Strict RootTransform Check
            if (!HasMergeCompatibleRootTransform(left) || !HasMergeCompatibleRootTransform(right)) return false;

            // 2. Component enabled check
            if (left.enabled != right.enabled) return false;

            // 3. Animation and FX Parameter Safety Check
            // If bone is animated or has a parameter driven by FX / Gestures, do not merge unless strictly identical
            if (GetBoolField(left, "isAnimated") != GetBoolField(right, "isAnimated")) return false;
            if (GetStringField(left, "parameter") != GetStringField(right, "parameter")) return false;

            // If aggressive mode, relax physics tolerances
            if (config.Strategy == PhysBoneAutoMerger.MergeStrategy.Aggressive)
            {
                return ComparePanelField(left.pull, right.pull, 0.1f)
                    && ComparePanelField(left.spring, right.spring, 0.1f)
                    && ComparePanelField(left.stiffness, right.stiffness, 0.1f)
                    && ComparePanelField(left.gravity, right.gravity, 0.1f)
                    && ComparePanelField(left.gravityFalloff, right.gravityFalloff, 0.1f)
                    && ComparePanelField(left.immobile, right.immobile, 0.1f)
                    && ComparePanelField(left.maxAngleX, right.maxAngleX, 0.1f)
                    && ComparePanelField(left.maxAngleZ, right.maxAngleZ, 0.1f)
                    && ComparePanelField(left.radius, right.radius, 0.1f);
            }

            // Strict / Custom Mode
            float numTol = config.Strategy == PhysBoneAutoMerger.MergeStrategy.Strict ? 0.001f : config.NumericTolerance;
            float curveTol = config.Strategy == PhysBoneAutoMerger.MergeStrategy.Strict ? 0.001f : config.CurveTolerance;
            bool ignoreCurves = config.Strategy == PhysBoneAutoMerger.MergeStrategy.Custom && config.IgnoreCurves;
            bool ignoreLimits = config.Strategy == PhysBoneAutoMerger.MergeStrategy.Custom && config.IgnoreLimitsRotation;
            bool ignoreEndpoint = config.Strategy == PhysBoneAutoMerger.MergeStrategy.Custom && config.IgnoreEndpointPosition;

            // Standard Physics
            if (!ComparePanelField(left.pull, right.pull, numTol)) return false;
            if (!ComparePanelField(left.spring, right.spring, numTol)) return false;
            if (!ComparePanelField(left.stiffness, right.stiffness, numTol)) return false;
            if (!ComparePanelField(left.gravity, right.gravity, numTol)) return false;
            if (!ComparePanelField(left.gravityFalloff, right.gravityFalloff, numTol)) return false;
            if (!ComparePanelField(left.immobile, right.immobile, numTol)) return false;
            if (!ComparePanelField(left.radius, right.radius, numTol)) return false;
            if (!ComparePanelField(left.maxStretch, right.maxStretch, numTol)) return false;

            // Rotation & Limits
            if (!ignoreLimits)
            {
                if (!ComparePanelField(left.maxAngleX, right.maxAngleX, numTol)) return false;
                if (!ComparePanelField(left.maxAngleZ, right.maxAngleZ, numTol)) return false;
                if (!CompareVector3(left.limitRotation, right.limitRotation, numTol)) return false;
            }

            // Endpoint position
            if (!ignoreEndpoint)
            {
                if (!CompareVector3(left.endpointPosition, right.endpointPosition, numTol)) return false;
            }

            // Enums / Modes
            if (left.integrationType != right.integrationType) return false;
            if (left.immobileType != right.immobileType) return false;
            if (left.limitType != right.limitType) return false;
            if (left.multiChildType != right.multiChildType) return false;

            // Filters & Interactions
            if (!ComparePanelField(left.collisionFilter, right.collisionFilter)) return false;
            if (!ComparePanelField(left.grabFilter, right.grabFilter)) return false;
            if (!ComparePanelField(left.poseFilter, right.poseFilter)) return false;
            if (!ComparePanelField(left.snapToHand, right.snapToHand)) return false;

            // Curves
            if (!ignoreCurves)
            {
                if (!CompareCurveField(GetCurve(left, "pullCurve"), GetCurve(right, "pullCurve"), curveTol)) return false;
                if (!CompareCurveField(GetCurve(left, "springCurve"), GetCurve(right, "springCurve"), curveTol)) return false;
                if (!CompareCurveField(GetCurve(left, "stiffnessCurve"), GetCurve(right, "stiffnessCurve"), curveTol)) return false;
                if (!CompareCurveField(GetCurve(left, "gravityCurve"), GetCurve(right, "gravityCurve"), curveTol)) return false;
                if (!CompareCurveField(GetCurve(left, "gravityFalloffCurve"), GetCurve(right, "gravityFalloffCurve"), curveTol)) return false;
                if (!CompareCurveField(GetCurve(left, "immobileCurve"), GetCurve(right, "immobileCurve"), curveTol)) return false;
                if (!CompareCurveField(GetCurve(left, "radiusCurve"), GetCurve(right, "radiusCurve"), curveTol)) return false;
                if (!CompareCurveField(GetCurve(left, "maxAngleXCurve"), GetCurve(right, "maxAngleXCurve"), curveTol)) return false;
                if (!CompareCurveField(GetCurve(left, "maxAngleZCurve"), GetCurve(right, "maxAngleZCurve"), curveTol)) return false;
                if (!CompareCurveField(GetCurve(left, "maxStretchCurve"), GetCurve(right, "maxStretchCurve"), curveTol)) return false;
            }

            // Ignore Transforms & Child Structure Check
            if (!CompareIgnoreTransforms(left, right)) return false;
            if (!CompareChildHierarchyStructure(left, right)) return false;

            return true;
        }

        private static bool HasMergeCompatibleRootTransform(VRCPhysBone bone)
        {
            return bone != null && (bone.rootTransform == null || bone.rootTransform == bone.transform);
        }

        private static bool ComparePanelField<T>(T a, T b)
        {
            return EqualityComparer<T>.Default.Equals(a, b);
        }

        private static bool ComparePanelField(float a, float b, float tol)
        {
            return Mathf.Abs(a - b) <= tol;
        }

        private static bool CompareVector3(Vector3 a, Vector3 b, float tol)
        {
            return Mathf.Abs(a.x - b.x) <= tol && Mathf.Abs(a.y - b.y) <= tol && Mathf.Abs(a.z - b.z) <= tol;
        }

        private static bool CompareCurveField(AnimationCurve c1, AnimationCurve c2, float tol)
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

        private static bool CompareIgnoreTransforms(VRCPhysBone left, VRCPhysBone right)
        {
            var lList = left.ignoreTransforms;
            var rList = right.ignoreTransforms;
            if ((lList == null || lList.Count == 0) && (rList == null || rList.Count == 0)) return true;
            if (lList == null || rList == null) return false;
            if (lList.Count != rList.Count) return false;

            for (int i = 0; i < lList.Count; i++)
            {
                if (lList[i] != rList[i]) return false;
            }
            return true;
        }

        private static bool CompareChildHierarchyStructure(VRCPhysBone left, VRCPhysBone right)
        {
            if (left.transform.childCount != right.transform.childCount) return false;
            return true;
        }

        private static bool GetBoolField(VRCPhysBone bone, string name)
        {
            if (bone == null) return false;
            try
            {
                FieldInfo fi = typeof(VRCPhysBone).GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (fi != null) return (bool)fi.GetValue(bone);
            }
            catch { }
            return false;
        }

        private static string GetStringField(VRCPhysBone bone, string name)
        {
            if (bone == null) return null;
            try
            {
                FieldInfo fi = typeof(VRCPhysBone).GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (fi != null) return fi.GetValue(bone) as string;
            }
            catch { }
            return null;
        }

        private static AnimationCurve GetCurve(VRCPhysBone bone, string fieldName)
        {
            if (bone == null) return null;
            try
            {
                FieldInfo field = typeof(VRCPhysBone).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (field != null) return field.GetValue(bone) as AnimationCurve;
            }
            catch { }
            return null;
        }

        public static int ExecuteMerge(List<MergeCluster> clusters, bool deduplicateColliders, bool useUndo = false)
        {
            if (clusters == null || clusters.Count == 0) return 0;
            int mergedCount = 0;
            int totalOriginalBones = 0;

            StringBuilder logBuilder = new StringBuilder();
            logBuilder.AppendLine("[VRC PhysBone Merger] ========== ⚡ 动骨非破坏性自动合并构建报告 ==========");

            try
            {
                for (int c = 0; c < clusters.Count; c++)
                {
                    var cluster = clusters[c];
                    if (cluster == null || cluster.SiblingBones == null || cluster.SiblingBones.Count < 2) continue;

                    var bones = cluster.SiblingBones;
                    var prime = bones[0];
                    if (prime == null) continue;

                    float progress = (float)(c + 1) / clusters.Count;
                    string stepInfo = $"[组 {c + 1}/{clusters.Count}] 正在合并 \"{cluster.SmartName}\" ({bones.Count} 个动骨)...";
                    PhysBoneBuildProgressWindow.ShowProgress("VRC PhysBone Merger (非破坏性动骨合并)", stepInfo, progress);
                    EditorUtility.DisplayProgressBar("VRC PhysBone Merger", $"⚡ {stepInfo}", progress);

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
                    List<string> mergedBoneNames = new List<string>();
                    for (int i = 0; i < bones.Count; i++)
                    {
                        var b = bones[i];
                        if (b != null && b.transform != null)
                        {
                            mergedBoneNames.Add(b.gameObject.name);
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

                    totalOriginalBones += bones.Count;
                    logBuilder.AppendLine($"\n📦 [组 {c + 1}/{clusters.Count}] 父节点: \"{cluster.Parent.name}\"");
                    logBuilder.AppendLine($"   ├─ 合并了 {bones.Count} 个动骨: [{string.Join(", ", mergedBoneNames)}]");
                    logBuilder.AppendLine($"   └─ 生成合并容器: \"{holder.name}\" (PhysBone 根驱动)");

                    mergedCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            int reducedBones = totalOriginalBones - mergedCount;
            PhysBoneBuildProgressWindow.Finish(totalOriginalBones, mergedCount);
            logBuilder.AppendLine($"\n🎉 ========== 构建完成：成功合并 {mergedCount} 组 PhysBone 动骨！ ==========");
            logBuilder.AppendLine($"📊 统计总结：共将 {totalOriginalBones} 个原始 PhysBone 压缩为 {mergedCount} 个组件 (总计消减 {reducedBones} 个动骨，性能大幅提升！)");
            Debug.Log(logBuilder.ToString());

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