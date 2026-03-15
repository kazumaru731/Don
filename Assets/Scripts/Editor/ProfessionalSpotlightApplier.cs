using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

namespace DonGame2D.Editor
{
    public static class ProfessionalSpotlightApplier
    {
        [MenuItem("Tools/DonGame/Apply Final Professional Spotlight V2")]
        public static void Apply()
        {
            string folderPath = "Assets/Sprites/UI";
            string fileName = "SpotlightVignette_Final.png";
            string assetPath = folderPath + "/" + fileName;
            string fullPath = Path.Combine(Application.dataPath, "Sprites/UI", fileName);

            if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // 1. 極めてコントラストの高いテクスチャを生成
            // 中心(0-0.2)は完全に透明、(0.2-0.5)で急激に暗転、(0.5以上)は完全な漆黒
            int size = 512;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / (size / 2f);
                    float alpha = Mathf.Clamp01((dist - 0.2f) / 0.3f);
                    tex.SetPixel(x, y, new Color(0, 0, 0, alpha));
                }
            }
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(fullPath, bytes);
            
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(assetPath);

            // 2. インポーター設定を強制
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; // 画質優先
                importer.SaveAndReimport();
            }

            // 3. オブジェクトに適用
            GameObject obj = GameObject.Find("SpotlightEffect耳");
            if (obj != null)
            {
                Image img = obj.GetComponent<Image>();
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.type = Image.Type.Simple;
                    img.raycastTarget = false;

                    RectTransform rect = obj.GetComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = new Vector2(-5000, -5000);
                    rect.offsetMax = new Vector2(5000, 5000);
                    rect.anchoredPosition = new Vector2(0, -175);
                    rect.localScale = Vector3.one;

                    // 背景の直後
                    obj.transform.SetSiblingIndex(1);
                    
                    Debug.Log($"[UI] Professional Spotlight V2 applied! Texture: {sprite.name}, Pos: {rect.anchoredPosition}");
                }
                else
                {
                    Debug.LogError("[UI] Failed to load the generated sprite from " + assetPath);
                }
            }
        }
    }
}
