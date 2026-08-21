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
            UltraStrict = 0, // 极限无损 (0.0001 容差，全属性绝对一致)
            Strict = 1,      // 严格零风险 (推荐默认，0.001 容差)
            Balanced = 2,    // 平衡优化 (0.02 容差，智能融合微小手感差异)
            Aggressive = 3,  // 激进压缩 (同父节点大范围合并，最大化减少组件)
            Custom = 4       // 完全自定义容差
        }

        [Tooltip("上传 VRChat 或进入 Play 模式时自动合并同层级 PhysBone")]
        public bool EnabledOnUpload = true;

        [Tooltip("合并策略：可按需选择不同精度的预设等级")]
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