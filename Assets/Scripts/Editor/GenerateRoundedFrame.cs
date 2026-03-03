using UnityEngine;
using UnityEditor;
using System.IO;

public static class GenerateRoundedFrame
{
    [MenuItem("Tools/DonGame/Generate Neon Frame")]
    public static void Generate()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        
        float r = 24f;        // skeleton corner radius
        float t = 4f;         // core solid thickness
        float glowWidth = 0f; // ★ ぼかし無し
        float halfSize = size / 2f;
        float bx = halfSize - (t / 2f) - glowWidth - r;
        float by = halfSize - (t / 2f) - glowWidth - r;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x - halfSize + 0.5f);
                float py = Mathf.Abs(y - halfSize + 0.5f);
                
                float qx = Mathf.Max(px - bx, 0);
                float qy = Mathf.Max(py - by, 0);
                
                float dist = Mathf.Sqrt(qx * qx + qy * qy) + Mathf.Min(Mathf.Max(px - bx, py - by), 0f) - r;
                
                float dAbs = Mathf.Abs(dist);
                float alpha = 0f;

                if (dAbs < t / 2f)
                {
                    alpha = 1f; // Solid core
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        
        string dirInfo = "Assets/Sprites";
        if (!Directory.Exists(dirInfo))
        {
            Directory.CreateDirectory(dirInfo);
        }
        
        string path = "Assets/Sprites/NeonRoundedFrame.png";
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            // The border must encapsulate the entire rounded corner and glow to stretch properly on straight edges
            int border = Mathf.CeilToInt(r + (t / 2f) + glowWidth) + 4; // Add small padding
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        
        Debug.Log("NeonRoundedFrame.png generated at " + path);
    }
}
