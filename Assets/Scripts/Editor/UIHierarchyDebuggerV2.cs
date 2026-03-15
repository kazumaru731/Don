using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace DonGame2D.Editor
{
    public static class UIHierarchyDebuggerV2
    {
        [MenuItem("Tools/DonGame/Force Front and Solid RED")]
        public static void ForceFront()
        {
            GameObject obj = GameObject.Find("SpotlightEffect耳");
            if (obj != null)
            {
                // 最前面に移動
                obj.transform.SetAsLastSibling();
                
                Image img = obj.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = null;
                    img.color = Color.red; // 完全な不透明赤
                    img.raycastTarget = false;
                }
                
                RectTransform rect = obj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(500, 500); // 画面中央に小さく表示
                }
                
                Debug.Log("[UI] SpotlightEffect耳 moved to front and set to SOLID RED (500x500).");
            }
        }
    }
}
