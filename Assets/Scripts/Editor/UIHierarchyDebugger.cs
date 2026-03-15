using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace DonGame2D.Editor
{
    public static class UIHierarchyDebugger
    {
        [MenuItem("Tools/DonGame/Debug Title UI Hierarchy")]
        public static void DebugHierarchy()
        {
            GameObject canvas = GameObject.Find("TitleCanvas");
            if (canvas == null)
            {
                Debug.LogError("TitleCanvas not found!");
                return;
            }

            Debug.Log($"--- TitleCanvas ({canvas.GetInstanceID()}) Hierarchy ---");
            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                Transform child = canvas.transform.GetChild(i);
                CanvasRenderer cr = child.GetComponent<CanvasRenderer>();
                Image img = child.GetComponent<Image>();
                string details = $"Index {i}: {child.name} (Active: {child.gameObject.activeSelf})";
                if (img != null) details += $", Image Sprite: {(img.sprite != null ? img.sprite.name : "null")}, Color: {img.color}";
                if (cr != null) details += $", Renderer Depth: {cr.absoluteDepth}";
                Debug.Log(details);
            }
        }

        [MenuItem("Tools/DonGame/Test Spotlight Visibility (RED)")]
        public static void TestVisibility()
        {
            GameObject obj = GameObject.Find("SpotlightEffect耳");
            if (obj != null)
            {
                Image img = obj.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = null;
                    img.color = new Color(1, 0, 0, 0.5f); // 半透明赤
                    Debug.Log("SpotlightEffect耳 color set to semi-transparent RED for testing.");
                }
            }
        }
    }
}
