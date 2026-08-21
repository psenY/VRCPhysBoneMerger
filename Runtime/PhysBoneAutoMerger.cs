using System;
using UnityEngine;
using VRC.SDKBase;

namespace PsenY7.VRCPhysBoneMerger
{
    [DisallowMultipleComponent]
    [AddComponentMenu("模型工具/VRC 动骨非破坏性自动合并组件 (PhysBone Auto Merger)")]
    public class PhysBoneAutoMerger : MonoBehaviour, IEditorOnly
    {
        public enum StrategyType
        {
            Strict,
            Custom,
            Aggressive
        }

        [Tooltip("上传 VRChat 或进入 Play 模式时自动合并近似 PhysBone")]
        public bool EnabledOnUpload = true;

        [Tooltip("合并策略：Strict 为零风险严格策略，Aggressive 为激进策略，Custom 为自定义策略")]
        public StrategyType Strategy = StrategyType.Strict;

        [Range(0.001f, 1f)]
        public float NumericTolerance = 0.001f;

        [Range(0.001f, 1f)]
        public float CurveTolerance = 0.001f;

        public bool IgnoreLimitsRotation = false;
        public bool IgnoreEndpointPosition = false;
        public bool IgnoreCurves = false;
    }
}
