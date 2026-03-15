using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

namespace DonGame2D.Editor
{
    public static class FinalSpotlightFixer
    {
        [MenuItem("Tools/DonGame/Apply Final Professional Spotlight")]
        public static void Apply()
        {
            string folderPath = "Assets/Sprites/UI";
            string fileName = "SpotlightVignette.png";
            string assetPath = folderPath + "/" + fileName;
            string fullPath = Path.Combine(Application.dataPath, "Sprites/UI", fileName);

            if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // 1. 高解像度グラデーションテクスチャ生成 (512x512)
            int size = 512;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / (size / 2f);
                    // 0.0 ~ 0.3: 完全透明 (照らされる範囲)
                    // 0.3 ~ 0.7: 滑らかに暗転
                    // 0.7 ~ 1.0: 完全な漆黒
                    float alpha = Mathf.Clamp01((dist - 0.3f) / 0.4f);
                    tex.SetPixel(x, y, new Color(0, 0, 0, alpha));
                }
            }
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(fullPath, bytes);
            AssetDatabase.ImportAsset(assetPath);

            // 2. インポーター設定
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.SaveAndReimport();
            }

            // 3. オブジェクト再設定
            GameObject obj = GameObject.Find("SpotlightEffect耳");
            if (obj != null)
            {
                Image img = obj.GetComponent<Image>();
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                
                img.sprite = sprite;
                img.color = Color.white;
                img.type = Image.Type.Simple;
                img.raycastTarget = false;

                RectTransform rect = obj.GetComponent<RectTransform>();
                // カード群と捨て場の位置関係に合わせて調整
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0, -175);
                rect.sizeDelta = new Vector2(10000, 10000); // 巨大化して隅々まで暗くする
                rect.localScale = Vector3.one;

                // 描画順序: 背景(0)の直後(1)へ
                obj.transform.SetSiblingIndex(1);
                
                Debug.Log("[UI] Final Professional Spotlight applied. Center at (0, -175), size 10000x10000.");
            }
        }
    }
}
