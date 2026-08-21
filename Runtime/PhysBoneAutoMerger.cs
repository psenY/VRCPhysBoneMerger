using System;
using UnityEngine;
using VRC.SDKBase;

namespace PsenY7.VRCPhysBoneMerger
{
    /// <summary>
    /// Non-destructive VRChat PhysBone merging component.
    /// Merges compatible sibling PhysBones in temporary clone during build/Play mode.
    /// Implements IEditorOnly to bypass VRChat SDK client validation.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("VRC PhysBone Merger/PhysBone Auto Merger (动骨自动合并组件)")]
    public class PhysBoneAutoMerger : MonoBehaviour, IEditorOnly
    {
        public enum MergeStrategy
        {
            Strict = 0,     // 零风险严格匹配策略
            Aggressive = 1, // 激进合并策略
            Custom = 2      // 自定义容差策略
        }

        [Tooltip("上传 VRChat 或进入 Play 模式时自动合并同层级 PhysBone")]
        public bool EnabledOnUpload = true;

        [Tooltip("合并策略：Strict (严格零风险), Aggressive (激进同父合并), Custom (自定义容差)")]
        public MergeStrategy Strategy = MergeStrategy.Strict;

        [Range(0.0001f, 0.5f)]
        public float NumericTolerance = 0.001f;

        [Range(0.0001f, 0.5f)]
        public float CurveTolerance = 0.001f;

        public bool IgnoreLimitsRotation = false;
        public bool IgnoreEndpointPosition = false;
        public bool IgnoreCurves = false;
        public bool DeduplicateColliders = true;
    }
}