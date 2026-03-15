using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace DonGame2D.Editor
{
    public static class SpotlightVisualFixer
    {
        [MenuItem("Tools/DonGame/Apply Strong Spotlight")]
        public static void Apply()
        {
            GameObject spotlight = GameObject.Find("SpotlightEffect耳");
            if (spotlight == null) return;

            Image img = spotlight.GetComponent<Image>();
            if (img != null)
            {
                // アルファが正しく効くようにカラーを純粋な白にリセット
                img.color = Color.white;
                // 周辺が透けないよう Simple モードを確認
                img.type = Image.Type.Simple;
                // 他のUIをブロックしないように
                img.raycastTarget = false;
            }

            RectTransform rect = spotlight.GetComponent<RectTransform>();
            if (rect != null)
            {
                // 画面全体を覆う十分なサイズ
                rect.sizeDelta = new Vector2(8000, 8000);
                // カード(Y=-400)と捨て場(Y=50)の中間付近
                rect.anchoredPosition = new Vector2(0, -175);
            }

            // 描画順序を背景(0)の直後(1)に
            spotlight.transform.SetSiblingIndex(1);
            
            Debug.Log("[UI] Strong Spotlight applied and centered at Y=-175.");
        }
    }
}
