#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace PsenY7.VRCPhysBoneMerger
{
    [InitializeOnLoad]
    public static class PhysBoneHierarchyOverlay
    {
        private const string PREF_KEY = "PsenY7.VRCPhysBoneMerger.HierarchyBadges";
        private static bool isEnabled = true;

        private struct NodeBoneStats
        {
            public int DirectCount;
            public int SubtreeCount;
        }

        private static readonly Dictionary<int, NodeBoneStats> statsCache = new Dictionary<int, NodeBoneStats>();
        private static bool isCacheDirty = true;
        private static double lastRebuildTime = 0;

        private static GUIStyle directBadgeStyle;
        private static GUIStyle containerBadgeStyle;

        static PhysBoneHierarchyOverlay()
        {
            isEnabled = EditorPrefs.GetBool(PREF_KEY, true);
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemGUI;
            EditorApplication.hierarchyChanged += MarkCacheDirty;
        }

        public static bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (isEnabled != value)
                {
                    isEnabled = value;
                    EditorPrefs.SetBool(PREF_KEY, value);
                    MarkCacheDirty();
                    EditorApplication.RepaintHierarchyWindow();
                }
            }
        }

        public static void ToggleHierarchyBadges()
        {
            IsEnabled = !IsEnabled;
        }

        public static void MarkCacheDirty()
        {
            isCacheDirty = true;
        }

        private static void InitStyles()
        {
            if (directBadgeStyle == null)
            {
                directBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.2f, 0.95f, 1f, 1f) }
                };
            }

            if (containerBadgeStyle == null)
            {
                containerBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.35f, 1f, 0.7f, 1f) }
                };
            }
        }

        private static void RebuildCacheIfNeeded()
        {
            if (!isEnabled) return;
            if (!isCacheDirty && EditorApplication.timeSinceStartup - lastRebuildTime < 2.0) return;

            statsCache.Clear();
            isCacheDirty = false;
            lastRebuildTime = EditorApplication.timeSinceStartup;

            VRCPhysBone[] allBones;
            #if UNITY_2021_2_OR_NEWER
            allBones = Object.FindObjectsByType<VRCPhysBone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            #else
            allBones = Object.FindObjectsOfType<VRCPhysBone>(true);
            #endif

            if (allBones == null || allBones.Length == 0) return;

            for (int i = 0; i < allBones.Length; i++)
            {
                var pb = allBones[i];
                if (pb == null) continue;

                var go = pb.gameObject;
                int id = go.GetInstanceID();

                if (!statsCache.TryGetValue(id, out NodeBoneStats selfStats))
                {
                    selfStats = new NodeBoneStats();
                }
                selfStats.DirectCount++;
                selfStats.SubtreeCount++;
                statsCache[id] = selfStats;

                Transform parent = go.transform.parent;
                while (parent != null)
                {
                    int pId = parent.gameObject.GetInstanceID();
                    if (!statsCache.TryGetValue(pId, out NodeBoneStats pStats))
                    {
                        pStats = new NodeBoneStats();
                    }
                    pStats.SubtreeCount++;
                    statsCache[pId] = pStats;
                    parent = parent.parent;
                }
            }
        }

        private static void OnHierarchyItemGUI(int instanceID, Rect selectionRect)
        {
            if (!isEnabled) return;

            RebuildCacheIfNeeded();

            if (!statsCache.TryGetValue(instanceID, out NodeBoneStats stats)) return;
            if (stats.SubtreeCount <= 0) return;

            InitStyles();

            bool isDirectLeaf = (stats.DirectCount > 0 && stats.SubtreeCount == stats.DirectCount);
            string badgeText;
            string tooltip;
            Color bgColor;
            Color borderColor;
            GUIStyle style;

            if (isDirectLeaf)
            {
                badgeText = stats.DirectCount > 1 ? $"PB ×{stats.DirectCount}" : "PB";
                tooltip = PhysBoneLocalization.Tr(
                    $"【挂载动骨】\n此对象直接挂载了 {stats.DirectCount} 个 PhysBone 组件",
                    $"[PhysBone Component]\nThis GameObject directly has {stats.DirectCount} PhysBone component(s)");
                bgColor = new Color(0.06f, 0.22f, 0.32f, 0.85f);
                borderColor = new Color(0f, 0.85f, 1f, 0.55f);
                style = directBadgeStyle;
            }
            else
            {
                badgeText = $"PB: {stats.SubtreeCount}";
                tooltip = stats.DirectCount > 0
                    ? PhysBoneLocalization.Tr(
                        $"【动骨统计】\n子层级包含动骨总数: {stats.SubtreeCount} 个 (本对象挂载: {stats.DirectCount} 个)",
                        $"[PhysBone Stats]\nTotal subtree PhysBones: {stats.SubtreeCount} (Direct on self: {stats.DirectCount})")
                    : PhysBoneLocalization.Tr(
                        $"【动骨统计】\n子层级包含动骨总数: {stats.SubtreeCount} 个",
                        $"[PhysBone Stats]\nTotal subtree PhysBones: {stats.SubtreeCount}");
                bgColor = new Color(0.08f, 0.22f, 0.16f, 0.85f);
                borderColor = new Color(0.2f, 0.9f, 0.5f, 0.55f);
                style = containerBadgeStyle;
            }

            float textWidth = style.CalcSize(new GUIContent(badgeText)).x;
            float badgeWidth = textWidth + 8f;
            float badgeHeight = 15f;

            float rightMargin = 4f;
            Rect badgeRect = new Rect(selectionRect.xMax - badgeWidth - rightMargin, selectionRect.y + (selectionRect.height - badgeHeight) * 0.5f, badgeWidth, badgeHeight);

            // Draw Background Pill
            EditorGUI.DrawRect(badgeRect, borderColor);
            Rect innerRect = new Rect(badgeRect.x + 1, badgeRect.y + 1, badgeRect.width - 2, badgeRect.height - 2);
            EditorGUI.DrawRect(innerRect, bgColor);

            // Draw Text with Tooltip
            GUI.Label(badgeRect, new GUIContent(badgeText, tooltip), style);
        }
    }
}
#endif
