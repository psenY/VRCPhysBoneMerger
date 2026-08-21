#if UNITY_EDITOR
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace PsenY7.VRCPhysBoneMerger
{
    public class PhysBoneBuildProgressWindow : EditorWindow
    {
        public static PhysBoneBuildProgressWindow Instance { get; private set; }

        private string _title = "VRC PhysBone Merger";
        private string _currentInfo = "正在准备非破坏性构建...";
        private float _progress = 0f;
        private bool _isDone = false;

        public static void ShowProgress(string title, string info, float progress)
        {
            if (Instance == null)
            {
                Instance = CreateInstance<PhysBoneBuildProgressWindow>();
                Instance.titleContent = new GUIContent("VRC PhysBone Merger Build");
                Instance.minSize = new Vector2(500, 115);
                Instance.maxSize = new Vector2(500, 115);

                // Center near top-middle of the screen
                var mainPos = EditorGUIUtility.GetMainWindowPosition();
                float x = mainPos.x + (mainPos.width - 500) * 0.5f;
                float y = mainPos.y + 120;
                Instance.position = new Rect(x, y, 500, 115);
                Instance.ShowPopup();
            }

            Instance._title = title;
            Instance._currentInfo = info;
            Instance._progress = Mathf.Clamp01(progress);
            Instance._isDone = false;
            Instance.Repaint();

            // Small delay so Unity renders the frame smoothly
            System.Threading.Thread.Sleep(15);
        }

        public static void Finish(int totalOriginal, int mergedCount)
        {
            if (Instance != null)
            {
                int reduced = totalOriginal - mergedCount;
                Instance._progress = 1f;
                Instance._isDone = true;
                Instance._currentInfo = $"🎉 构建完成！共将 {totalOriginal} 个动骨压缩为 {mergedCount} 个组件 (消减 {reduced} 个)";
                Instance.Repaint();

                // Keep result visible for 1.2 seconds so user can see it
                EditorApplication.delayCall += async () =>
                {
                    await Task.Delay(1200);
                    if (Instance != null)
                    {
                        Instance.Close();
                        Instance = null;
                    }
                };
            }
        }

        private void OnGUI()
        {
            // Dark modern background box
            Rect fullRect = new Rect(0, 0, position.width, position.height);
            EditorGUI.DrawRect(fullRect, new Color(0.08f, 0.12f, 0.18f, 1f));

            // Cyan border outline
            Handles.color = new Color(0.22f, 0.74f, 0.97f, 0.8f);
            Handles.DrawLine(new Vector3(0, 0), new Vector3(position.width, 0));
            Handles.DrawLine(new Vector3(0, position.height - 1), new Vector3(position.width, position.height - 1));
            Handles.DrawLine(new Vector3(0, 0), new Vector3(0, position.height));
            Handles.DrawLine(new Vector3(position.width - 1, 0), new Vector3(position.width - 1, position.height));

            GUILayout.BeginArea(new Rect(16, 12, position.width - 32, position.height - 24));

            // Top Header: Title + Percentage
            GUILayout.BeginHorizontal();
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.22f, 0.74f, 0.97f) }
            };
            GUILayout.Label($"⚡ {_title}", titleStyle);

            GUIStyle pctStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = _isDone ? new Color(0.2f, 0.9f, 0.4f) : Color.white }
            };
            GUILayout.Label($"{Mathf.RoundToInt(_progress * 100)}%", pctStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Progress Bar Track & Fill
            Rect barBg = GUILayoutUtility.GetRect(position.width - 32, 12);
            EditorGUI.DrawRect(barBg, new Color(0.15f, 0.22f, 0.32f, 1f));

            if (_progress > 0.001f)
            {
                Rect barFill = new Rect(barBg.x, barBg.y, barBg.width * _progress, barBg.height);
                Color fillColor = _isDone ? new Color(0.15f, 0.75f, 0.4f) : new Color(0.06f, 0.72f, 0.51f);
                EditorGUI.DrawRect(barFill, fillColor);
            }

            GUILayout.Space(6);

            // Subtitle info
            GUIStyle infoStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.85f, 0.9f, 0.95f) },
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            GUILayout.Label(_currentInfo, infoStyle);

            GUILayout.EndArea();
        }
    }
}
#endif