using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace DonGame2D.Editor
{
    public static class FinalVignetteDebugger
    {
        [MenuItem("Tools/DonGame/DEBUG: Test Solid Black Fullscreen")]
        public static void TestBlack()
        {
            GameObject obj = GameObject.Find("SpotlightEffect耳");
            if (obj == null) return;

            // 最前面に移動（すべてを隠すはず）
            obj.transform.SetAsLastSibling();
            
            Image img = obj.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = null;
                img.color = Color.black; // 漆黒
                img.raycastTarget = false;
            }

            RectTransform rect = obj.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-5000, -5000);
                rect.offsetMax = new Vector2(5000, 5000);
                rect.anchoredPosition = Vector2.zero;
            }
            
            Debug.Log("[DEBUG] SpotlightEffect耳 set to SOLID BLACK and moved to FRONT. Screen should be dark.");
        }

        [MenuItem("Tools/DonGame/DEBUG: Restore Spotlight with Better Texture")]
        public static void RestoreSpotlight()
        {
            GameObject obj = GameObject.Find("SpotlightEffect耳");
            if (obj == null) return;

            // 背景の直後に移動
            obj.transform.SetSiblingIndex(1);
            
            Image img = obj.GetComponent<Image>();
            if (img != null)
            {
                // テクスチャを再読み込みして適用
                string path = "Assets/Sprites/UI/SpotlightVignette.png";
                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null)
                {
                    img.sprite = sp;
                    img.color = Color.white;
                }
                else
                {
                    Debug.LogError("Vignette texture NOT found at " + path);
                }
            }
        }
    }
}
