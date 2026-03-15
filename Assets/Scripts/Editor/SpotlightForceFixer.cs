using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

namespace DonGame2D.Editor
{
    public static class SpotlightForceFixer
    {
        [MenuItem("Tools/DonGame/Force Fix Spotlight Visuals")]
        public static void Fix()
        {
            string folderPath = "Assets/Sprites/UI";
            string fileName = "SpotlightGlow.png";
            string fullPath = Path.Combine(Application.dataPath, "Sprites/UI", fileName);

            if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // 1. 強力な放射状グラデーションテクスチャを生成 (256x256)
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / (size / 2f);
                    // 0.0 ~ 0.4: 完全透明 (ライト内)
                    // 0.4 ~ 0.8: 急激に暗転
                    // 0.8 ~ 1.0: 完全不透明黒
                    float alpha = Mathf.Clamp01((dist - 0.4f) / 0.4f);
                    tex.SetPixel(x, y, new Color(0, 0, 0, alpha));
                }
            }
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(fullPath, bytes);
            AssetDatabase.ImportAsset(folderPath + "/" + fileName);

            // 2. インポート設定を強制 (Sprite / Alpha Is Transparency)
            TextureImporter importer = AssetImporter.GetAtPath(folderPath + "/" + fileName) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            // 3. オブジェクトの設定
            GameObject obj = GameObject.Find("SpotlightEffect耳");
            if (obj == null)
            {
                GameObject canvas = GameObject.Find("TitleCanvas");
                if (canvas != null)
                {
                    obj = new GameObject("SpotlightEffect耳");
                    obj.transform.SetParent(canvas.transform, false);
                    obj.AddComponent<CanvasRenderer>();
                    obj.AddComponent<Image>();
                }
            }

            if (obj != null)
            {
                Image img = obj.GetComponent<Image>();
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folderPath + "/" + fileName);
                img.sprite = sprite;
                img.color = Color.white;
                img.raycastTarget = false;

                RectTransform rect = obj.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0, -175);
                rect.sizeDelta = new Vector2(8000, 8000);
                rect.localScale = Vector3.one;

                // 背景の直後に移動
                obj.transform.SetSiblingIndex(1);
                
                Debug.Log("[UI] Spotlight Effect force-fixed with code-generated texture.");
            }
        }
    }
}
