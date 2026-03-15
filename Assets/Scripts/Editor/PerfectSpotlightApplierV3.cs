using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

namespace DonGame2D.Editor
{
    public static class PerfectSpotlightApplierV3
    {
        [MenuItem("Tools/DonGame/Apply Perfect Spotlight V3 (FRONT)")]
        public static void Apply()
        {
            string fileName = "Spotlight_Perfect_V3.png";
            string folderPath = "Assets/Sprites/UI";
            string assetPath = folderPath + "/" + fileName;
            string fullPath = Path.Combine(Application.dataPath, "Sprites/UI", fileName);

            if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // Generate texture: 0-0.15 transparent (Hole), 0.15-0.4 transition, 0.4-1.0 solid black
            int size = 512;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxDist = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float alpha;
                    if (dist < 0.15f) alpha = 0; // 完全透明な穴
                    else if (dist < 0.45f) alpha = (dist - 0.15f) / 0.3f; // グラデーション
                    else alpha = 1.0f; // 漆黒
                    
                    tex.SetPixel(x, y, new Color(0, 0, 0, alpha));
                }
            }
            tex.Apply();
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            GameObject obj = GameObject.Find("SpotlightEffect耳");
            if (obj == null) return;

            // 階層の最前面に移動（すべてを黒で覆う）
            obj.transform.SetAsLastSibling();

            Image img = obj.GetComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            img.color = Color.white;
            img.raycastTarget = false; // クリックを邪魔しない
            img.maskable = false;

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            // サイズを 4000px に適正化（10000pxだと穴がデカすぎた）
            rect.sizeDelta = new Vector3(4000, 4000, 1);
            rect.anchoredPosition = new Vector2(0, -150); // カードと捨て場の中間付近
            rect.localScale = Vector3.one;
            
            Debug.Log("[UI] Perfect Spotlight V3 applied at FRONT with smaller hole.");
        }
    }
}
