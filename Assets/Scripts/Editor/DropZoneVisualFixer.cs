using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace DonGame2D.Editor
{
    public static class DropZoneVisualFixer
    {
        [MenuItem("Tools/DonGame/Fix DropZone Frame Visuals")]
        public static void Fix()
        {
            GameObject obj = GameObject.Find("DropZoneArea");
            if (obj == null)
            {
                Debug.LogError("DropZoneArea not found!");
                return;
            }

            // 1. Image設定の調整
            Image img = obj.GetComponent<Image>();
            if (img != null)
            {
                // 枠を太くする (小さい値ほど太くなる)
                img.pixelsPerUnitMultiplier = 0.4f; 
                img.color = new Color(1f, 0.92f, 0.015f, 1f);
                img.type = Image.Type.Sliced;
                
                // スプライト自体のBorder設定を確認・修正
                if (img.sprite != null)
                {
                    string path = AssetDatabase.GetAssetPath(img.sprite);
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null)
                    {
                        // Borderが設定されているか確認 (0だとSlicedが意味をなさない)
                        // 通常はUnityマニュアル設定が必要だが、ここではインポート設定を強制
                        if (importer.spriteBorder == Vector4.zero)
                        {
                            // 典型的な枠スプライトの場合、四隅にマージンを設定
                            importer.spriteBorder = new Vector4(20, 20, 20, 20);
                            importer.SaveAndReimport();
                            Debug.Log($"Set sprite border for {path}");
                        }
                    }
                }
            }

            // 2. 表示欠け対策: サイズと位置の微調整
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (rect != null)
            {
                // スケールを1に戻し、サイズ自体で調整する (計算誤差を減らす)
                rect.localScale = Vector3.one;
                rect.sizeDelta = new Vector2(300, 450); // 一回り大きく (240x360 -> 300x450)
                rect.anchoredPosition = new Vector2(0, 50);
            }

            Debug.Log("[UI] DropZoneArea visual fixed: Thicker lines and normalized scale.");
        }
    }
}
