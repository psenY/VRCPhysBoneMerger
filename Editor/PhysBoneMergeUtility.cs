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
    public static class PhysBoneMergeUtility
    {
        public const float DefaultNumericTolerance = 0.1f;
        public const float DefaultCurveTolerance = 0.1f;
        public const int DefaultCurveSampleCount = 9;

        private const string MergeUndoName = "Merge PhysBones";

        private static readonly string[] IgnoredFieldNames =
        {
            "m_Script",
            "m_GameObject",
            "m_CorrespondingSourceObject",
            "m_PrefabInstance",
            "m_PrefabAsset",
            "m_EditorHideFlags"
        };

        private static readonly Dictionary<Type, FieldInfo[]> SerializableFieldCache = new Dictionary<Type, FieldInfo[]>();

        [Serializable]
        public sealed class ApproximationOptions
        {
            public ApproximationStrategyMode StrategyMode = ApproximationStrategyMode.Custom;
            public float NumericTolerance = DefaultNumericTolerance;
            public float CurveTolerance = DefaultCurveTolerance;
            public int CurveSampleCount = DefaultCurveSampleCount;
            public bool IgnoreLimitsRotation = true;
            public bool IgnoreEndpointPosition = true;
            public bool IgnoreCurves = false;
            public bool IgnoreTypeModes = false;
            public bool IgnoreMultiChildType = false;
            public bool IgnoreIgnoreTransforms = false;
            public bool IgnoreEndpointStructure = false;
            public bool IgnoreChildStructure = false;
        }

        public enum ApproximationStrategyMode
        {
            Custom = 0,
            Aggressive = 1
        }

        public sealed class PhysBoneSiblingGroup
        {
            public Transform Parent { get; internal set; }
            public List<VRCPhysBone> Bones { get; internal set; }
            public ApproximationOptions MatchOptions { get; internal set; }
            public string FirstMismatchField { get; internal set; }
            public string FirstMismatchSummary { get; internal set; }
            public VRCPhysBone FirstMismatchReference { get; internal set; }
            public VRCPhysBone FirstMismatchCandidate { get; internal set; }

            public VRCPhysBone Representative
            {
                get { return Bones != null && Bones.Count > 0 ? Bones[0] : null; }
            }
        }

        public sealed class PhysBoneMergeResult
        {
            public bool Success { get; internal set; }
            public string Error { get; internal set; }
            public GameObject CreatedParent { get; internal set; }
            public VRCPhysBone KeptComponent { get; internal set; }
            public List<VRCPhysBone> RemovedComponents { get; internal set; }
        }

        public sealed class HierarchyScanResult
        {
            public sealed class ApproximationFailureExample
            {
                public Transform Parent { get; internal set; }
                public VRCPhysBone Left { get; internal set; }
                public VRCPhysBone Right { get; internal set; }
                public string MismatchField { get; internal set; }
            }

            public GameObject RootObject { get; internal set; }
            public List<PhysBoneSiblingGroup> CandidateGroups { get; internal set; } = new List<PhysBoneSiblingGroup>();
            public List<ApproximationFailureExample> ApproximationFailureExamples { get; internal set; } = new List<ApproximationFailureExample>();
            public int ParentCountScanned { get; internal set; }
            public int PhysBoneCountScanned { get; internal set; }
            public int ParentCountWithPhysBones { get; internal set; }
            public int PhysBoneCountExcludedByRootTransform { get; internal set; }
            public int PhysBoneCountExcludedByMissingSibling { get; internal set; }
            public int PhysBoneCountExcludedByApproximation { get; internal set; }

            public int CandidateGroupCount
            {
                get { return CandidateGroups != null ? CandidateGroups.Count : 0; }
            }

            public int CandidatePhysBoneCount
            {
                get
                {
                    if (CandidateGroups == null)
                    {
                        return 0;
                    }

                    int count = 0;
                    for (int i = 0; i < CandidateGroups.Count; i++)
                    {
                        if (CandidateGroups[i]?.Bones != null)
                        {
                            count += CandidateGroups[i].Bones.Count;
                        }
                    }

                    return count;
                }
            }
        }

        public static List<VRCPhysBone> ScanSiblingPhysBones(Transform parent, bool includeInactive = true)
        {
            List<VRCPhysBone> bones = new List<VRCPhysBone>();
            if (parent == null)
            {
                return bones;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (!includeInactive && !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                VRCPhysBone[] childBones = child.GetComponents<VRCPhysBone>();
                if (childBones == null || childBones.Length == 0)
                {
                    continue;
                }

                for (int j = 0; j < childBones.Length; j++)
                {
                    if (childBones[j] != null)
                    {
                        bones.Add(childBones[j]);
                    }
                }
            }

            return bones;
        }

        public static List<PhysBoneSiblingGroup> GroupSiblingPhysBones(Transform parent, ApproximationOptions options = null, bool includeInactive = true)
        {
            List<PhysBoneSiblingGroup> groups = new List<PhysBoneSiblingGroup>();
            List<VRCPhysBone> bones = ScanSiblingPhysBones(parent, includeInactive);
            if (bones.Count == 0)
            {
                return groups;
            }

            options = options ?? new ApproximationOptions();
            bones.Sort(CompareByHierarchy);

            bool[] used = new bool[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                if (used[i] || bones[i] == null)
                {
                    continue;
                }

                PhysBoneSiblingGroup group = new PhysBoneSiblingGroup
                {
                    Parent = parent,
                    Bones = new List<VRCPhysBone> { bones[i] },
                    MatchOptions = CloneOptions(options)
                };
                used[i] = true;

                for (int j = i + 1; j < bones.Count; j++)
                {
                    if (used[j] || bones[j] == null)
                    {
                        continue;
                    }

                    string mismatchField;
                    VRCPhysBone mismatchReference;
                    if (IsApproximateMatch(group.Bones, bones[j], options, out mismatchField, out mismatchReference))
                    {
                        group.Bones.Add(bones[j]);
                        used[j] = true;
                    }
                    else if (string.IsNullOrEmpty(group.FirstMismatchField))
                    {
                        group.FirstMismatchField = mismatchField;
                        group.FirstMismatchReference = mismatchReference;
                        group.FirstMismatchCandidate = bones[j];
                        group.FirstMismatchSummary = BuildMismatchSummary(mismatchReference, bones[j]);
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        public static HierarchyScanResult ScanHierarchy(GameObject rootObject, ApproximationOptions options = null, bool includeInactive = true)
        {
            HierarchyScanResult result = new HierarchyScanResult
            {
                RootObject = rootObject
            };

            if (rootObject == null)
            {
                return result;
            }

            options = options ?? new ApproximationOptions();
            result.PhysBoneCountScanned = rootObject.GetComponentsInChildren<VRCPhysBone>(includeInactive).Length;
            result.PhysBoneCountExcludedByRootTransform = CountPhysBonesExcludedByRootTransform(rootObject, includeInactive);

            Transform[] transforms = rootObject.GetComponentsInChildren<Transform>(includeInactive);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform parent = transforms[i];
                if (parent == null)
                {
                    continue;
                }

                result.ParentCountScanned++;

                List<VRCPhysBone> siblingBones = ScanSiblingPhysBones(parent, includeInactive);
                if (siblingBones.Count <= 1)
                {
                    result.PhysBoneCountExcludedByMissingSibling += siblingBones.Count;
                    continue;
                }

                result.ParentCountWithPhysBones++;

                int eligibleSiblingCount = 0;
                for (int siblingIndex = 0; siblingIndex < siblingBones.Count; siblingIndex++)
                {
                    if (HasMergeCompatibleRootTransform(siblingBones[siblingIndex]))
                    {
                        eligibleSiblingCount++;
                    }
                }

                if (eligibleSiblingCount <= 1)
                {
                    result.PhysBoneCountExcludedByMissingSibling += eligibleSiblingCount;
                    continue;
                }

                List<PhysBoneSiblingGroup> groups = GroupSiblingPhysBones(parent, options, includeInactive);
                int candidateBoneCountForParent = 0;
                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    PhysBoneSiblingGroup group = groups[groupIndex];
                    if (group != null
                        && !string.IsNullOrEmpty(group.FirstMismatchField)
                        && group.FirstMismatchReference != null
                        && group.FirstMismatchCandidate != null
                        && result.ApproximationFailureExamples.Count < 8)
                    {
                        result.ApproximationFailureExamples.Add(new HierarchyScanResult.ApproximationFailureExample
                        {
                            Parent = parent,
                            Left = group.FirstMismatchReference,
                            Right = group.FirstMismatchCandidate,
                            MismatchField = group.FirstMismatchField
                        });
                    }

                    if (group != null && group.Bones != null && group.Bones.Count > 1)
                    {
                        result.CandidateGroups.Add(group);
                        candidateBoneCountForParent += group.Bones.Count;
                    }
                }

                result.PhysBoneCountExcludedByApproximation += Mathf.Max(0, eligibleSiblingCount - candidateBoneCountForParent);
            }

            return result;
        }

        public static bool AreApproximatelyEquivalent(VRCPhysBone left, VRCPhysBone right, float numericTolerance = DefaultNumericTolerance, float curveTolerance = DefaultCurveTolerance, int curveSampleCount = DefaultCurveSampleCount, bool ignoreLimitsRotation = true, bool ignoreEndpointPosition = true)
        {
            ApproximationOptions options = new ApproximationOptions
            {
                NumericTolerance = numericTolerance,
                CurveTolerance = curveTolerance,
                CurveSampleCount = curveSampleCount,
                IgnoreLimitsRotation = ignoreLimitsRotation,
                IgnoreEndpointPosition = ignoreEndpointPosition
            };
            return AreApproximatelyEquivalent(left, right, options);
        }

        public static bool AreApproximatelyEquivalent(VRCPhysBone left, VRCPhysBone right, ApproximationOptions options)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (ReferenceEquals(left, right))
            {
                return true;
            }

            options = options ?? new ApproximationOptions();

            string _;
            if (!AreApproximatelyEquivalent(left, right, options, out _))
            {
                return false;
            }

            return true;
        }

        private static bool AreApproximatelyEquivalent(VRCPhysBone left, VRCPhysBone right, ApproximationOptions options, out string mismatchField)
        {
            mismatchField = null;

            if (!HasMergeCompatibleRootTransform(left) || !HasMergeCompatibleRootTransform(right))
            {
                mismatchField = "rootTransform";
                return false;
            }

            Behaviour leftBehaviour = left as Behaviour;
            Behaviour rightBehaviour = right as Behaviour;
            if (leftBehaviour != null && rightBehaviour != null && leftBehaviour.enabled != rightBehaviour.enabled)
            {
                mismatchField = "enabled";
                return false;
            }

            if (options.StrategyMode == ApproximationStrategyMode.Aggressive)
            {
                return AreApproximatelyEquivalentAggressive(left, right, options, out mismatchField);
            }

            return AreApproximatelyEquivalentCustom(left, right, options, out mismatchField);
        }

        public static bool TryMergeGroup(IReadOnlyList<VRCPhysBone> candidates, out PhysBoneMergeResult result, VRCPhysBone keep = null, string mergedParentName = null, bool useUndo = true, ApproximationOptions comparisonOptions = null)
        {
            result = new PhysBoneMergeResult
            {
                RemovedComponents = new List<VRCPhysBone>()
            };

            if (candidates == null || candidates.Count == 0)
            {
                result.Error = "No candidates were supplied.";
                return false;
            }

            List<VRCPhysBone> bones = new List<VRCPhysBone>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] != null && !bones.Contains(candidates[i]))
                {
                    bones.Add(candidates[i]);
                }
            }

            if (bones.Count == 0)
            {
                result.Error = "No valid VRCPhysBone components were supplied.";
                return false;
            }

            bones.Sort(CompareByHierarchy);
            comparisonOptions = comparisonOptions ?? new ApproximationOptions();

            if (keep == null || !bones.Contains(keep))
            {
                keep = bones[0];
            }

            Transform commonParent = keep.transform.parent;
            if (commonParent == null)
            {
                result.Error = "The kept component does not have a parent transform.";
                return false;
            }

            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].transform.parent != commonParent)
                {
                    result.Error = "All candidates must share the same parent transform.";
                    return false;
                }

                if (!HasMergeCompatibleRootTransform(bones[i]))
                {
                    result.Error = "All candidates must have a null rootTransform or use their own transform as rootTransform.";
                    return false;
                }
            }

            for (int i = 1; i < bones.Count; i++)
            {
                string mismatchField;
                if (!AreApproximatelyEquivalent(keep, bones[i], comparisonOptions, out mismatchField))
                {
                    result.Error = "Candidates are not equivalent enough to merge safely. First mismatch: " + mismatchField;
                    return false;
                }
            }

            int undoGroup = -1;
            GameObject createdParent = null;
            if (useUndo)
            {
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(MergeUndoName);
            }

            try
            {
                createdParent = new GameObject(string.IsNullOrEmpty(mergedParentName) ? GetSmartMergedName(bones) : mergedParentName);
                if (useUndo)
                {
                    Undo.RegisterCreatedObjectUndo(createdParent, MergeUndoName);
                }

                Transform mergedTransform = createdParent.transform;
                if (useUndo)
                {
                    Undo.RecordObject(mergedTransform, MergeUndoName);
                    Undo.RecordObject(commonParent, MergeUndoName);
                }

                if (useUndo)
                {
                    Undo.SetTransformParent(mergedTransform, commonParent, MergeUndoName);
                }
                else
                {
                    mergedTransform.SetParent(commonParent, false);
                }
                mergedTransform.localPosition = keep.transform.localPosition;
                mergedTransform.localRotation = keep.transform.localRotation;
                mergedTransform.localScale = keep.transform.localScale;
                mergedTransform.SetSiblingIndex(keep.transform.GetSiblingIndex());

                VRCPhysBone sourceKeep = keep;
                VRCPhysBone mergedBone = useUndo
                    ? Undo.AddComponent<VRCPhysBone>(createdParent)
                    : createdParent.AddComponent<VRCPhysBone>();
                EditorUtility.CopySerialized(sourceKeep, mergedBone);
                mergedBone.rootTransform = null;

                // Gather, deduplicate, and clean up colliders across all merged candidates
                if (sourceKeep.colliders != null)
                {
                    var mergedColliders = (IList)Activator.CreateInstance(sourceKeep.colliders.GetType());
                    for (int i = 0; i < bones.Count; i++)
                    {
                        if (bones[i] != null && bones[i].colliders != null)
                        {
                            foreach (var col in bones[i].colliders)
                            {
                                if (col != null && !mergedColliders.Contains(col))
                                {
                                    mergedColliders.Add(col);
                                }
                            }
                        }
                    }
                    mergedBone.colliders = (dynamic)mergedColliders;
                }

                List<Transform> uniqueHosts = GetUniqueHostsSorted(bones);
                for (int i = 0; i < uniqueHosts.Count; i++)
                {
                    if (useUndo)
                    {
                        Undo.SetTransformParent(uniqueHosts[i], mergedTransform, MergeUndoName);
                    }
                    else
                    {
                        uniqueHosts[i].SetParent(mergedTransform, true);
                    }
                }

                for (int i = 0; i < bones.Count; i++)
                {
                    VRCPhysBone bone = bones[i];
                    if (bone == null)
                    {
                        continue;
                    }

                    if (useUndo)
                    {
                        Undo.DestroyObjectImmediate(bone);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(bone);
                    }

                    result.RemovedComponents.Add(bone);
                }

                result.Success = true;
                result.CreatedParent = createdParent;
                result.KeptComponent = mergedBone;

                if (useUndo)
                {
                    Undo.CollapseUndoOperations(undoGroup);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (useUndo)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                }
                else if (createdParent != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdParent);
                }

                result.Error = ex.Message;
                return false;
            }
        }

        public static bool TryMergeGroups(IReadOnlyList<PhysBoneSiblingGroup> groups, out List<PhysBoneMergeResult> results, bool useUndo = true)
        {
            results = new List<PhysBoneMergeResult>();
            if (groups == null || groups.Count == 0)
            {
                return true;
            }

            bool allSucceeded = true;
            int undoGroup = -1;
            if (useUndo)
            {
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(MergeUndoName);
            }

            try
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    PhysBoneSiblingGroup group = groups[i];
                    PhysBoneMergeResult result = null;
                    bool succeeded = group != null
                        && group.Bones != null
                        && group.Bones.Count > 1
                        && TryMergeGroup(group.Bones, out result, group.Representative, null, useUndo, group.MatchOptions);

                    if (!succeeded && result == null)
                    {
                        result = new PhysBoneMergeResult
                        {
                            Success = false,
                            Error = "Invalid PhysBone merge group.",
                            RemovedComponents = new List<VRCPhysBone>()
                        };
                    }

                    if (result != null)
                    {
                        results.Add(result);
                        if (!result.Success)
                        {
                            allSucceeded = false;
                            if (useUndo)
                            {
                                Undo.RevertAllDownToGroup(undoGroup);
                            }

                            return false;
                        }
                    }
                    else
                    {
                        allSucceeded = false;
                        if (useUndo)
                        {
                            Undo.RevertAllDownToGroup(undoGroup);
                        }

                        return false;
                    }
                }
            }
            finally
            {
                if (useUndo && allSucceeded)
                {
                    Undo.CollapseUndoOperations(undoGroup);
                }
            }

            return allSucceeded;
        }

        public static string GetRelativePath(Transform root, Transform target)
        {
            if (target == null)
            {
                return "(null)";
            }

            if (root == null)
            {
                return GetHierarchyPath(target);
            }

            if (target == root)
            {
                return root.name;
            }

            Stack<string> parts = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            if (current == root)
            {
                parts.Push(root.name);
                return string.Join("/", parts.ToArray());
            }

            return GetHierarchyPath(target);
        }

        private static List<Transform> GetUniqueHostsSorted(List<VRCPhysBone> bones)
        {
            List<Transform> hosts = new List<Transform>();
            for (int i = 0; i < bones.Count; i++)
            {
                Transform host = bones[i] != null ? bones[i].transform : null;
                if (host != null && !hosts.Contains(host))
                {
                    hosts.Add(host);
                }
            }

            hosts.Sort(CompareTransformsByHierarchy);
            return hosts;
        }

        private static bool IsApproximateMatch(List<VRCPhysBone> existingGroup, VRCPhysBone candidate, ApproximationOptions options, out string mismatchField, out VRCPhysBone mismatchReference)
        {
            mismatchField = null;
            mismatchReference = null;

            for (int i = 0; i < existingGroup.Count; i++)
            {
                if (!AreApproximatelyEquivalent(existingGroup[i], candidate, options, out mismatchField))
                {
                    mismatchReference = existingGroup[i];
                    return false;
                }
            }

            return true;
        }

        public static ApproximationOptions CloneOptions(ApproximationOptions options)
        {
            options = options ?? new ApproximationOptions();
            return new ApproximationOptions
            {
                StrategyMode = options.StrategyMode,
                NumericTolerance = options.NumericTolerance,
                CurveTolerance = options.CurveTolerance,
                CurveSampleCount = options.CurveSampleCount,
                IgnoreLimitsRotation = options.IgnoreLimitsRotation,
                IgnoreEndpointPosition = options.IgnoreEndpointPosition,
                IgnoreCurves = options.IgnoreCurves,
                IgnoreTypeModes = options.IgnoreTypeModes,
                IgnoreMultiChildType = options.IgnoreMultiChildType,
                IgnoreIgnoreTransforms = options.IgnoreIgnoreTransforms,
                IgnoreEndpointStructure = options.IgnoreEndpointStructure,
                IgnoreChildStructure = options.IgnoreChildStructure
            };
        }

        private static bool AreApproximatelyEquivalentAggressive(VRCPhysBone left, VRCPhysBone right, ApproximationOptions options, out string mismatchField)
        {
            mismatchField = null;

            if (!ComparePanelField(left.pull, right.pull, options.NumericTolerance))
                return SetMismatch("pull", out mismatchField);
            if (!ComparePanelField(left.spring, right.spring, options.NumericTolerance))
                return SetMismatch("spring", out mismatchField);
            if (!ComparePanelField(left.stiffness, right.stiffness, options.NumericTolerance))
                return SetMismatch("stiffness", out mismatchField);
            if (!ComparePanelField(left.gravity, right.gravity, options.NumericTolerance))
                return SetMismatch("gravity", out mismatchField);
            if (!ComparePanelField(left.gravityFalloff, right.gravityFalloff, options.NumericTolerance))
                return SetMismatch("gravityFalloff", out mismatchField);
            if (!ComparePanelField(left.immobile, right.immobile, options.NumericTolerance))
                return SetMismatch("immobile", out mismatchField);
            if (!ComparePanelField(left.maxAngleX, right.maxAngleX, options.NumericTolerance))
                return SetMismatch("maxAngleX", out mismatchField);
            if (!ComparePanelField(left.maxAngleZ, right.maxAngleZ, options.NumericTolerance))
                return SetMismatch("maxAngleZ", out mismatchField);
            if (!ComparePanelField(left.radius, right.radius, options.NumericTolerance))
                return SetMismatch("radius", out mismatchField);
            if (!ComparePanelField(left.maxStretch, right.maxStretch, options.NumericTolerance))
                return SetMismatch("maxStretch", out mismatchField);

            if (!ComparePanelField(left.limitRotation, right.limitRotation, options.NumericTolerance, options.IgnoreLimitsRotation))
                return SetMismatch("limitRotation", out mismatchField);
            if (!ComparePanelField(left.endpointPosition, right.endpointPosition, options.NumericTolerance, options.IgnoreEndpointPosition))
                return SetMismatch("endpointPosition", out mismatchField);

            if (!ComparePanelField(left.collisionFilter, right.collisionFilter))
                return SetMismatch("collisionFilter", out mismatchField);
            if (!ComparePanelField(left.grabFilter, right.grabFilter))
                return SetMismatch("grabFilter", out mismatchField);
            if (!ComparePanelField(left.poseFilter, right.poseFilter))
                return SetMismatch("poseFilter", out mismatchField);
            if (!ComparePanelField(left.snapToHand, right.snapToHand))
                return SetMismatch("snapToHand", out mismatchField);

            if (HasField("stretchMotion"))
            {
                object leftStretchMotion = GetFieldValue(left, "stretchMotion");
                object rightStretchMotion = GetFieldValue(right, "stretchMotion");
                if (!CompareDynamicPanelField(leftStretchMotion, rightStretchMotion, options.NumericTolerance))
                    return SetMismatch("stretchMotion", out mismatchField);
            }

            if (HasField("maxSquish"))
            {
                object leftMaxSquish = GetFieldValue(left, "maxSquish");
                object rightMaxSquish = GetFieldValue(right, "maxSquish");
                if (!CompareDynamicPanelField(leftMaxSquish, rightMaxSquish, options.NumericTolerance))
                    return SetMismatch("maxSquish", out mismatchField);
            }

            return true;
        }

        private static bool AreApproximatelyEquivalentCustom(VRCPhysBone left, VRCPhysBone right, ApproximationOptions options, out string mismatchField)
        {
            mismatchField = null;

            if (!ComparePanelField(left.pull, right.pull, options.NumericTolerance))
                return SetMismatch("pull", out mismatchField);
            if (!CompareCurveField(GetAnimationCurve(left, "pullCurve"), GetAnimationCurve(right, "pullCurve"), options))
                return SetMismatch("pullCurve", out mismatchField);

            if (!ComparePanelField(left.spring, right.spring, options.NumericTolerance))
                return SetMismatch("spring", out mismatchField);
            if (!CompareCurveField(GetAnimationCurve(left, "springCurve"), GetAnimationCurve(right, "springCurve"), options))
                return SetMismatch("springCurve", out mismatchField);

            if (!ComparePanelField(left.stiffness, right.stiffness, options.NumericTolerance))
                return SetMismatch("stiffness", out mismatchField);
            if (!CompareCurveField(GetAnimationCurve(left, "stiffnessCurve"), GetAnimationCurve(right, "stiffnessCurve"), options))
                return SetMismatch("stiffnessCurve", out mismatchField);

            if (!ComparePanelField(left.gravity, right.gravity, options.NumericTolerance))
                return SetMismatch("gravity", out mismatchField);
            if (!CompareCurveField(GetAnimationCurve(left, "gravityCurve"), GetAnimationCurve(right, "gravityCurve"), options))
                return SetMismatch("gravityCurve", out mismatchField);

            if (!ComparePanelField(left.gravityFalloff, right.gravityFalloff, options.NumericTolerance))
                return SetMismatch("gravityFalloff", out mismatchField);
            if (!CompareCurveField(GetAnimationCurve(left, "gravityFalloffCurve"), GetAnimationCurve(right, "gravityFalloffCurve"), options))
                return SetMismatch("gravityFalloffCurve", out mismatchField);

            if (!ComparePanelField(left.immobile, right.immobile, options.NumericTolerance))
                return SetMismatch("immobile", out mismatchField);
            if (!CompareCurveField(GetAnimationCurve(left, "immobileCurve"), GetAnimationCurve(right, "immobileCurve"), options))
                return SetMismatch("immobileCurve", out mismatchField);

            if (!ComparePanelField(left.maxAngleX, right.maxAngleX, options.NumericTolerance))
                return SetMismatch("maxAngleX", out mismatchField);
            if (!CompareCurveField(GetAnimationCurve(left, "maxAngleXCurve"), GetAnimationCurve(right, "maxAngleXCurve"), options))
                return SetMismatch("maxAngleXCurve", out mismatchField);

            if (!ComparePanelField(left.maxAngleZ, right.maxAngleZ, options.NumericTolerance))
                return SetMismatch("maxAngleZ", out mismatchField);
            if (!CompareCurveField(GetAnimationCurve(left, "maxAngleZCurve"), GetAnimationCurve(right, "maxAngleZCurve"), options))
                return SetMismatch("maxAngleZCurve", out mismatchField);

            if (!ComparePanelField(left.limitRotation, right.limitRotation, options.NumericTolerance, options.IgnoreLimitsRotation))
                return SetMismatch("limitRotation", out mismatchField);
            if (!options.IgnoreLimitsRotation)
            {
                if (!CompareCurveField(GetAnimationCurve(left, "limitRotationXCurve"), GetAnimationCurve(right, "limitRotationXCurve"), options))
                    return SetMismatch("limitRotationXCurve", out mismatchField);
                if (!CompareCurveField(GetAnimationCurve(left, "limitRotationYCurve"), GetAnimationCurve(right, "limitRotationYCurve"), options))
                    return SetMismatch("limitRotationYCurve", out mismatchField);
                if (!CompareCurveField(GetAnimationCurve(left, "limitRotationZCurve"), GetAnimationCurve(right, "limitRotationZCurve"), options))
                    return SetMismatch("limitRotationZCurve", out mismatchField);
            }

            if (!ComparePanelField(left.radius, right.radius, options.NumericTolerance))
                return SetMismatch("radius", out mismatchField);
            if (!CompareCurveField(GetAnimationCurve(left, "radiusCurve"), GetAnimationCurve(right, "radiusCurve"), options))
                return SetMismatch("radiusCurve", out mismatchField);

            if (!ComparePanelField(left.endpointPosition, right.endpointPosition, options.NumericTolerance, options.IgnoreEndpointPosition))
                return SetMismatch("endpointPosition", out mismatchField);
            if (!CompareEndpointStructure(left, right, options))
                return SetMismatch("endpointStructure", out mismatchField);
            if (!CompareChildStructure(left, right, options))
                return SetMismatch("childStructure", out mismatchField);

            if (!ComparePanelField(left.collisionFilter, right.collisionFilter))
                return SetMismatch("collisionFilter", out mismatchField);
            if (!CompareAdvancedBoolField(left, right, "allowCollision", options))
                return SetMismatch("allowCollision", out mismatchField);

            if (!ComparePanelField(left.maxStretch, right.maxStretch, options.NumericTolerance))
                return SetMismatch("maxStretch", out mismatchField);
            if (!CompareCurveField(GetAnimationCurve(left, "maxStretchCurve"), GetAnimationCurve(right, "maxStretchCurve"), options))
                return SetMismatch("maxStretchCurve", out mismatchField);

            if (!CompareDynamicPanelField(GetFieldValue(left, "stretchMotion"), GetFieldValue(right, "stretchMotion"), options.NumericTolerance))
                return SetMismatch("stretchMotion", out mismatchField);
            if (!CompareCurveField(GetAnimationCurve(left, "stretchMotionCurve"), GetAnimationCurve(right, "stretchMotionCurve"), options))
                return SetMismatch("stretchMotionCurve", out mismatchField);

            if (!CompareDynamicPanelField(GetFieldValue(left, "maxSquish"), GetFieldValue(right, "maxSquish"), options.NumericTolerance))
                return SetMismatch("maxSquish", out mismatchField);
            if (!CompareCurveField(GetAnimationCurve(left, "maxSquishCurve"), GetAnimationCurve(right, "maxSquishCurve"), options))
                return SetMismatch("maxSquishCurve", out mismatchField);

            if (!ComparePanelField(left.grabFilter, right.grabFilter))
                return SetMismatch("grabFilter", out mismatchField);
            if (!CompareAdvancedBoolField(left, right, "allowGrabbing", options))
                return SetMismatch("allowGrabbing", out mismatchField);

            if (!ComparePanelField(left.poseFilter, right.poseFilter))
                return SetMismatch("poseFilter", out mismatchField);
            if (!CompareAdvancedBoolField(left, right, "allowPosing", options))
                return SetMismatch("allowPosing", out mismatchField);

            if (!ComparePanelField(left.snapToHand, right.snapToHand))
                return SetMismatch("snapToHand", out mismatchField);

            if (!CompareTypeModeField(left, right, "integrationType", options))
                return SetMismatch("integrationType", out mismatchField);
            if (!CompareTypeModeField(left, right, "immobileType", options))
                return SetMismatch("immobileType", out mismatchField);
            if (!CompareTypeModeField(left, right, "limitType", options))
                return SetMismatch("limitType", out mismatchField);
            if (!CompareTypeModeField(left, right, "multiChildType", new ApproximationOptions
            {
                StrategyMode = options.StrategyMode,
                IgnoreTypeModes = options.IgnoreTypeModes,
                IgnoreMultiChildType = options.IgnoreMultiChildType
            }))
                return SetMismatch("multiChildType", out mismatchField);

            if (!CompareIgnoreTransforms(left, right, options))
                return SetMismatch("ignoreTransforms", out mismatchField);

            if (!CompareBoolField(left, right, "isAnimated"))
                return SetMismatch("isAnimated", out mismatchField);

            if (!CompareColliders(left, right))
                return SetMismatch("colliders", out mismatchField);

            if (!CompareParameter(left, right))
                return SetMismatch("parameter", out mismatchField);

            return true;
        }

        private static string BuildMismatchSummary(VRCPhysBone left, VRCPhysBone right)
        {
            string leftPath = left != null ? GetHierarchyPath(left.transform) : "(null)";
            string rightPath = right != null ? GetHierarchyPath(right.transform) : "(null)";
            return leftPath + " <-> " + rightPath;
        }

        private static int CompareByHierarchy(VRCPhysBone left, VRCPhysBone right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return CompareTransformsByHierarchy(left.transform, right.transform);
        }

        private static int CompareTransformsByHierarchy(Transform left, Transform right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int leftIndex = left.GetSiblingIndex();
            int rightIndex = right.GetSiblingIndex();
            if (leftIndex != rightIndex)
            {
                return leftIndex.CompareTo(rightIndex);
            }

            return string.CompareOrdinal(GetHierarchyPath(left), GetHierarchyPath(right));
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            Stack<string> parts = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts.ToArray());
        }

        public static string GetSmartMergedName(List<VRCPhysBone> bones)
        {
            if (bones == null || bones.Count == 0)
            {
                return "Merged_PhysBone";
            }

            if (bones.Count == 1)
            {
                return bones[0] != null && bones[0].gameObject != null ? bones[0].gameObject.name + "_Merged" : "Merged_PhysBone";
            }

            List<string> validNames = new List<string>();
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] != null && bones[i].gameObject != null && !string.IsNullOrWhiteSpace(bones[i].gameObject.name))
                {
                    validNames.Add(bones[i].gameObject.name.Trim());
                }
            }

            if (validNames.Count == 0)
            {
                return "Merged_PhysBone";
            }

            string prefix = validNames[0];
            for (int i = 1; i < validNames.Count; i++)
            {
                string name = validNames[i];
                int j = 0;
                while (j < prefix.Length && j < name.Length && prefix[j] == name[j])
                {
                    j++;
                }
                prefix = prefix.Substring(0, j);
                if (prefix.Length == 0)
                {
                    break;
                }
            }

            prefix = prefix.TrimEnd('_', '-', ' ', '.');

            if (prefix.Length >= 2)
            {
                return prefix + "_Merged";
            }

            string parentName = bones[0].transform.parent != null ? bones[0].transform.parent.name : null;
            if (!string.IsNullOrEmpty(parentName))
            {
                return parentName + "_PhysBones_Merged";
            }

            return validNames[0] + "_Merged";
        }

        private static bool HasMergeCompatibleRootTransform(VRCPhysBone physBone)
        {
            return physBone != null && (physBone.rootTransform == null || physBone.rootTransform == physBone.transform);
        }

        private static int CountPhysBonesExcludedByRootTransform(GameObject rootObject, bool includeInactive)
        {
            if (rootObject == null)
            {
                return 0;
            }

            int count = 0;
            VRCPhysBone[] physBones = rootObject.GetComponentsInChildren<VRCPhysBone>(includeInactive);
            for (int i = 0; i < physBones.Length; i++)
            {
                if (!HasMergeCompatibleRootTransform(physBones[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool AreApproximatelyEquivalentValues(object left, object right, Type type, string path, ApproximationOptions options)
        {
            if (ShouldIgnorePath(path, options))
            {
                return true;
            }

            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (type == typeof(string))
            {
                return string.Equals((string)left, (string)right, StringComparison.Ordinal);
            }

            if (type.IsEnum)
            {
                return left.Equals(right);
            }

            if (type == typeof(float))
            {
                return Mathf.Abs((float)left - (float)right) <= options.NumericTolerance;
            }

            if (type == typeof(double))
            {
                return Math.Abs((double)left - (double)right) <= options.NumericTolerance;
            }

            if (type == typeof(int))
            {
                return (int)left == (int)right;
            }

            if (type == typeof(long))
            {
                return (long)left == (long)right;
            }

            if (type == typeof(short))
            {
                return (short)left == (short)right;
            }

            if (type == typeof(byte))
            {
                return (byte)left == (byte)right;
            }

            if (type == typeof(bool))
            {
                return (bool)left == (bool)right;
            }

            if (type == typeof(Vector2))
            {
                Vector2 a = (Vector2)left;
                Vector2 b = (Vector2)right;
                return Approximately(a.x, b.x, options.NumericTolerance) && Approximately(a.y, b.y, options.NumericTolerance);
            }

            if (type == typeof(Vector3))
            {
                Vector3 a = (Vector3)left;
                Vector3 b = (Vector3)right;
                return Approximately(a.x, b.x, options.NumericTolerance) &&
                       Approximately(a.y, b.y, options.NumericTolerance) &&
                       Approximately(a.z, b.z, options.NumericTolerance);
            }

            if (type == typeof(Vector4))
            {
                Vector4 a = (Vector4)left;
                Vector4 b = (Vector4)right;
                return Approximately(a.x, b.x, options.NumericTolerance) &&
                       Approximately(a.y, b.y, options.NumericTolerance) &&
                       Approximately(a.z, b.z, options.NumericTolerance) &&
                       Approximately(a.w, b.w, options.NumericTolerance);
            }

            if (type == typeof(Color))
            {
                Color a = (Color)left;
                Color b = (Color)right;
                return Approximately(a.r, b.r, options.NumericTolerance) &&
                       Approximately(a.g, b.g, options.NumericTolerance) &&
                       Approximately(a.b, b.b, options.NumericTolerance) &&
                       Approximately(a.a, b.a, options.NumericTolerance);
            }

            if (type == typeof(Quaternion))
            {
                Quaternion a = (Quaternion)left;
                Quaternion b = (Quaternion)right;
                return Approximately(a.x, b.x, options.NumericTolerance) &&
                       Approximately(a.y, b.y, options.NumericTolerance) &&
                       Approximately(a.z, b.z, options.NumericTolerance) &&
                       Approximately(a.w, b.w, options.NumericTolerance);
            }

            if (type == typeof(Rect))
            {
                Rect a = (Rect)left;
                Rect b = (Rect)right;
                return Approximately(a.x, b.x, options.NumericTolerance) &&
                       Approximately(a.y, b.y, options.NumericTolerance) &&
                       Approximately(a.width, b.width, options.NumericTolerance) &&
                       Approximately(a.height, b.height, options.NumericTolerance);
            }

            if (type == typeof(Bounds))
            {
                Bounds a = (Bounds)left;
                Bounds b = (Bounds)right;
                return AreApproximatelyEquivalentValues(a.center, b.center, typeof(Vector3), path + ".center", options) &&
                       AreApproximatelyEquivalentValues(a.size, b.size, typeof(Vector3), path + ".size", options);
            }

            if (type == typeof(AnimationCurve))
            {
                return AreCurvesApproximatelyEqual((AnimationCurve)left, (AnimationCurve)right, options);
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return (UnityEngine.Object)left == (UnityEngine.Object)right;
            }

            if (type.IsArray)
            {
                return AreArraysApproximatelyEqual((Array)left, (Array)right, type.GetElementType(), path, options);
            }

            if (typeof(IList).IsAssignableFrom(type))
            {
                return AreListsApproximatelyEqual((IList)left, (IList)right, GetListElementType(type), path, options);
            }

            FieldInfo[] fields = GetSerializableFields(type);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null)
                {
                    continue;
                }

                if (ShouldIgnoreField(field, path, options))
                {
                    continue;
                }

                object leftValue = field.GetValue(left);
                object rightValue = field.GetValue(right);
                string childPath = string.IsNullOrEmpty(path) ? field.Name : path + "." + field.Name;
                if (!AreApproximatelyEquivalentValues(leftValue, rightValue, field.FieldType, childPath, options))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ComparePanelField(float left, float right, float tolerance)
        {
            return Approximately(left, right, tolerance);
        }

        private static bool ComparePanelField(Vector3 left, Vector3 right, float tolerance, bool ignored)
        {
            return ignored || (Approximately(left.x, right.x, tolerance)
                && Approximately(left.y, right.y, tolerance)
                && Approximately(left.z, right.z, tolerance));
        }

        private static bool ComparePanelField(object left, object right)
        {
            return Equals(left, right);
        }

        private static bool CompareDynamicPanelField(object left, object right, float tolerance)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;

            Type type = left.GetType();
            if (type != right.GetType())
                return false;

            if (type == typeof(float))
                return Approximately((float)left, (float)right, tolerance);
            if (type.IsEnum)
                return left.Equals(right);

            return left.Equals(right);
        }

        private static bool CompareCurveField(AnimationCurve left, AnimationCurve right, ApproximationOptions options)
        {
            if (options == null || options.IgnoreCurves)
            {
                return true;
            }

            return AreCurvesApproximatelyEqual(left, right, options);
        }

        private static bool CompareBoolField(VRCPhysBone left, VRCPhysBone right, string fieldName)
        {
            object leftValue = GetFieldValue(left, fieldName);
            object rightValue = GetFieldValue(right, fieldName);
            return Equals(leftValue, rightValue);
        }

        private static bool CompareAdvancedBoolField(VRCPhysBone left, VRCPhysBone right, string fieldName, ApproximationOptions options)
        {
            if (options != null && options.IgnoreTypeModes)
            {
                return true;
            }

            object leftValue = GetFieldValue(left, fieldName);
            object rightValue = GetFieldValue(right, fieldName);
            return Equals(leftValue, rightValue);
        }

        private static bool CompareTypeModeField(VRCPhysBone left, VRCPhysBone right, string fieldName, ApproximationOptions options)
        {
            if (fieldName == "multiChildType")
            {
                if (options != null && options.IgnoreMultiChildType)
                {
                    return true;
                }
            }
            else if (options != null && options.IgnoreTypeModes)
            {
                return true;
            }

            object leftValue = GetFieldValue(left, fieldName);
            object rightValue = GetFieldValue(right, fieldName);
            return Equals(leftValue, rightValue);
        }

        private static bool CompareIgnoreTransforms(VRCPhysBone left, VRCPhysBone right, ApproximationOptions options)
        {
            if (options != null && options.IgnoreIgnoreTransforms)
            {
                return true;
            }

            List<string> leftPaths = GetRelativeTransformPathList(left, "ignoreTransforms");
            List<string> rightPaths = GetRelativeTransformPathList(right, "ignoreTransforms");
            if (leftPaths.Count != rightPaths.Count)
            {
                return false;
            }

            for (int i = 0; i < leftPaths.Count; i++)
            {
                if (!string.Equals(leftPaths[i], rightPaths[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CompareColliders(VRCPhysBone left, VRCPhysBone right)
        {
            var leftColliders = left != null ? left.colliders : null;
            var rightColliders = right != null ? right.colliders : null;

            if (leftColliders == null && rightColliders == null)
            {
                return true;
            }

            if (leftColliders == null || rightColliders == null)
            {
                return false;
            }

            if (leftColliders.Count != rightColliders.Count)
            {
                return false;
            }

            for (int i = 0; i < leftColliders.Count; i++)
            {
                if (leftColliders[i] != rightColliders[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CompareParameter(VRCPhysBone left, VRCPhysBone right)
        {
            object leftParam = GetFieldValue(left, "parameter");
            object rightParam = GetFieldValue(right, "parameter");
            return Equals(leftParam, rightParam);
        }

        private static bool CompareEndpointStructure(VRCPhysBone left, VRCPhysBone right, ApproximationOptions options)
        {
            if (options != null && options.IgnoreEndpointStructure)
            {
                return true;
            }

            return UsesEndpointPosition(left) == UsesEndpointPosition(right);
        }

        private static bool CompareChildStructure(VRCPhysBone left, VRCPhysBone right, ApproximationOptions options)
        {
            if (options != null && options.IgnoreChildStructure)
            {
                return true;
            }

            ChildStructureSignature leftSignature = BuildChildStructureSignature(left);
            ChildStructureSignature rightSignature = BuildChildStructureSignature(right);

            return leftSignature.DirectChildCount == rightSignature.DirectChildCount
                && leftSignature.LeafCount == rightSignature.LeafCount
                && leftSignature.MaxDepth == rightSignature.MaxDepth
                && leftSignature.IgnoredBranchCount == rightSignature.IgnoredBranchCount
                && leftSignature.UsesEndpointPosition == rightSignature.UsesEndpointPosition;
        }

        private static bool SetMismatch(string fieldName, out string mismatchField)
        {
            mismatchField = fieldName;
            return false;
        }

        private static bool HasField(string fieldName)
        {
            return typeof(VRCPhysBone).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
        }

        private static AnimationCurve GetAnimationCurve(VRCPhysBone physBone, string fieldName)
        {
            return GetFieldValue(physBone, fieldName) as AnimationCurve;
        }

        private static object GetFieldValue(VRCPhysBone physBone, string fieldName)
        {
            FieldInfo field = typeof(VRCPhysBone).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? field.GetValue(physBone) : null;
        }

        private static List<string> GetRelativeTransformPathList(VRCPhysBone physBone, string fieldName)
        {
            List<string> paths = new List<string>();
            object value = GetFieldValue(physBone, fieldName);
            IList list = value as IList;
            if (list == null)
            {
                return paths;
            }

            Transform root = physBone != null ? physBone.transform : null;
            for (int i = 0; i < list.Count; i++)
            {
                Transform transform = list[i] as Transform;
                paths.Add(GetRelativePath(root, transform));
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        private static bool UsesEndpointPosition(VRCPhysBone physBone)
        {
            if (physBone == null)
            {
                return false;
            }

            bool hasEndpointPosition = physBone.endpointPosition.sqrMagnitude > 0.000001f;
            bool hasDirectChildren = physBone.transform != null && physBone.transform.childCount > 0;
            return hasEndpointPosition && !hasDirectChildren;
        }

        private struct ChildStructureSignature
        {
            public int DirectChildCount;
            public int LeafCount;
            public int MaxDepth;
            public int IgnoredBranchCount;
            public bool UsesEndpointPosition;
        }

        private static ChildStructureSignature BuildChildStructureSignature(VRCPhysBone physBone)
        {
            ChildStructureSignature signature = new ChildStructureSignature();
            if (physBone == null || physBone.transform == null)
            {
                return signature;
            }

            HashSet<Transform> ignored = new HashSet<Transform>();
            object ignoreValue = GetFieldValue(physBone, "ignoreTransforms");
            IList ignoreList = ignoreValue as IList;
            if (ignoreList != null)
            {
                for (int i = 0; i < ignoreList.Count; i++)
                {
                    Transform ignoredTransform = ignoreList[i] as Transform;
                    if (ignoredTransform != null)
                    {
                        ignored.Add(ignoredTransform);
                    }
                }
            }

            signature.DirectChildCount = CountNonIgnoredChildren(physBone.transform, ignored, ref signature.IgnoredBranchCount);
            signature.LeafCount = CountLeaves(physBone.transform, ignored);
            signature.MaxDepth = CountDepth(physBone.transform, ignored);
            signature.UsesEndpointPosition = UsesEndpointPosition(physBone);
            return signature;
        }

        private static int CountNonIgnoredChildren(Transform transform, HashSet<Transform> ignored, ref int ignoredBranchCount)
        {
            int count = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (ignored.Contains(child))
                {
                    ignoredBranchCount++;
                    continue;
                }

                count++;
            }

            return count;
        }

        private static int CountLeaves(Transform transform, HashSet<Transform> ignored)
        {
            int childCount = 0;
            int leafCount = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null || ignored.Contains(child))
                {
                    continue;
                }

                childCount++;
                leafCount += CountLeaves(child, ignored);
            }

            return childCount == 0 ? 1 : leafCount;
        }

        private static int CountDepth(Transform transform, HashSet<Transform> ignored)
        {
            int maxDepth = 1;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null || ignored.Contains(child))
                {
                    continue;
                }

                maxDepth = Mathf.Max(maxDepth, 1 + CountDepth(child, ignored));
            }

            return maxDepth;
        }

        private static bool AreArraysApproximatelyEqual(Array left, Array right, Type elementType, string path, ApproximationOptions options)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                string childPath = path + "[" + i + "]";
                if (!AreApproximatelyEquivalentValues(left.GetValue(i), right.GetValue(i), elementType, childPath, options))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreListsApproximatelyEqual(IList left, IList right, Type elementType, string path, ApproximationOptions options)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                string childPath = path + "[" + i + "]";
                if (!AreApproximatelyEquivalentValues(left[i], right[i], elementType, childPath, options))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreCurvesApproximatelyEqual(AnimationCurve left, AnimationCurve right, ApproximationOptions options)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (left.preWrapMode != right.preWrapMode || left.postWrapMode != right.postWrapMode)
            {
                return false;
            }

            Keyframe[] leftKeys = left.keys;
            Keyframe[] rightKeys = right.keys;
            if (leftKeys.Length == 0 || rightKeys.Length == 0)
            {
                return leftKeys.Length == rightKeys.Length;
            }

            float minTime = Mathf.Min(leftKeys[0].time, rightKeys[0].time);
            float maxTime = Mathf.Max(leftKeys[leftKeys.Length - 1].time, rightKeys[rightKeys.Length - 1].time);
            int sampleCount = Mathf.Max(3, options.CurveSampleCount);

            if (Mathf.Approximately(minTime, maxTime))
            {
                return Approximately(left.Evaluate(minTime), right.Evaluate(minTime), options.CurveTolerance);
            }

            for (int i = 0; i < sampleCount; i++)
            {
                float t = Mathf.Lerp(minTime, maxTime, sampleCount == 1 ? 0f : (float)i / (sampleCount - 1));
                float leftValue = left.Evaluate(t);
                float rightValue = right.Evaluate(t);
                if (!Approximately(leftValue, rightValue, options.CurveTolerance))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldIgnoreField(FieldInfo field, string path, ApproximationOptions options)
        {
            if (field == null)
            {
                return true;
            }

            for (int i = 0; i < IgnoredFieldNames.Length; i++)
            {
                if (string.Equals(field.Name, IgnoredFieldNames[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            if (options.IgnoreLimitsRotation && !string.IsNullOrEmpty(path))
            {
                string lowerPath = path.ToLowerInvariant();
                if (lowerPath == "roottransform" || lowerPath.EndsWith(".roottransform", StringComparison.Ordinal))
                {
                    return true;
                }

                if (lowerPath.Contains("limit") && lowerPath.EndsWith(".rotation", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldIgnorePath(string path, ApproximationOptions options)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string lowerPath = path.ToLowerInvariant();
            if (lowerPath == "roottransform" || lowerPath.EndsWith(".roottransform", StringComparison.Ordinal))
            {
                return true;
            }

            if (options.IgnoreEndpointPosition
                && (lowerPath == "endpointposition" || lowerPath.EndsWith(".endpointposition", StringComparison.Ordinal)))
            {
                return true;
            }

            if (!options.IgnoreLimitsRotation)
            {
                return false;
            }

            return lowerPath.Contains("limit") && lowerPath.EndsWith(".rotation", StringComparison.Ordinal);
        }

        private static FieldInfo[] GetSerializableFields(Type type)
        {
            FieldInfo[] cached;
            if (SerializableFieldCache.TryGetValue(type, out cached))
            {
                return cached;
            }

            List<FieldInfo> fields = new List<FieldInfo>();
            Type current = type;
            while (current != null && current != typeof(object))
            {
                FieldInfo[] declaredFields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < declaredFields.Length; i++)
                {
                    FieldInfo field = declaredFields[i];
                    if (field == null || field.IsStatic || field.IsNotSerialized)
                    {
                        continue;
                    }

                    if (!field.IsPublic && field.GetCustomAttributes(typeof(SerializeField), true).Length == 0)
                    {
                        continue;
                    }

                    fields.Add(field);
                }

                current = current.BaseType;
            }

            cached = fields.ToArray();
            SerializableFieldCache[type] = cached;
            return cached;
        }

        private static Type GetListElementType(Type listType)
        {
            if (listType == null)
            {
                return typeof(object);
            }

            if (listType.IsArray)
            {
                return listType.GetElementType();
            }

            Type[] genericArguments = listType.GetGenericArguments();
            return genericArguments != null && genericArguments.Length > 0 ? genericArguments[0] : typeof(object);
        }

        private static bool Approximately(float left, float right, float tolerance)
        {
            return Mathf.Abs(left - right) <= tolerance;
        }

        public struct PhysBonePerformanceStats
        {
            public int ComponentCount;
            public int TransformCount;
            public int ColliderCount;
            public string Rating;
        }

        public static PhysBonePerformanceStats CalculatePerformanceStats(GameObject root)
        {
            PhysBonePerformanceStats stats = new PhysBonePerformanceStats();
            if (root == null) return stats;

            var physBones = root.GetComponentsInChildren<VRCPhysBone>(true);
            stats.ComponentCount = physBones.Length;

            HashSet<Transform> affectedTransforms = new HashSet<Transform>();
            HashSet<UnityEngine.Object> colliders = new HashSet<UnityEngine.Object>();

            for (int i = 0; i < physBones.Length; i++)
            {
                var pb = physBones[i];
                if (pb == null) continue;

                Transform start = pb.rootTransform != null ? pb.rootTransform : pb.transform;
                if (start != null)
                {
                    foreach (var t in start.GetComponentsInChildren<Transform>(true))
                    {
                        affectedTransforms.Add(t);
                    }
                }

                if (pb.colliders != null)
                {
                    foreach (var col in pb.colliders)
                    {
                        if (col != null) colliders.Add((UnityEngine.Object)col);
                    }
                }
            }

            stats.TransformCount = affectedTransforms.Count;
            stats.ColliderCount = colliders.Count;
            stats.Rating = GetPhysBoneRank(stats.ComponentCount, stats.TransformCount, stats.ColliderCount);

            return stats;
        }

        public static string GetPhysBoneRank(int compCount, int transformCount, int colliderCount)
        {
            int compRank = compCount <= 4 ? 0 : compCount <= 8 ? 1 : compCount <= 16 ? 2 : compCount <= 32 ? 3 : 4;
            int transRank = transformCount <= 32 ? 0 : transformCount <= 64 ? 1 : transformCount <= 128 ? 2 : transformCount <= 256 ? 3 : 4;
            int colRank = colliderCount <= 4 ? 0 : colliderCount <= 8 ? 1 : colliderCount <= 16 ? 2 : colliderCount <= 32 ? 3 : 4;

            int maxRank = Math.Max(compRank, Math.Max(transRank, colRank));
            switch (maxRank)
            {
                case 0: return "Excellent";
                case 1: return "Good";
                case 2: return "Medium";
                case 3: return "Poor";
                default: return "Very Poor";
            }
        }

        public static int CleanupPhysBoneColliders(GameObject root, bool useUndo = true)
        {
            if (root == null) return 0;

            var physBones = root.GetComponentsInChildren<VRCPhysBone>(true);
            int cleanedCount = 0;

            for (int i = 0; i < physBones.Length; i++)
            {
                var pb = physBones[i];
                if (pb == null || pb.colliders == null) continue;

                var cleanList = (IList)Activator.CreateInstance(pb.colliders.GetType());
                bool changed = false;

                foreach (var col in pb.colliders)
                {
                    if (col == null)
                    {
                        changed = true;
                        continue;
                    }

                    if (!cleanList.Contains(col))
                    {
                        cleanList.Add(col);
                    }
                    else
                    {
                        changed = true;
                    }
                }

                if (changed)
                {
                    if (useUndo) Undo.RecordObject(pb, "Cleanup PhysBone Colliders");
                    pb.colliders = (dynamic)cleanList;
                    cleanedCount++;
                }
            }

            return cleanedCount;
        }
    }
}
#endif

