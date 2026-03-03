using UnityEngine;
using UnityEditor;
using System.IO;

public class FeltTextureGenerator
{
    [MenuItem("Tools/DonGame/Generate Casino Felt")]
    public static void GenerateFelt()
    {
        int size = 1024;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        
        // Base casino green colors mimicking felt
        Color baseColor1 = new Color(0.04f, 0.38f, 0.18f); // Darker
        Color baseColor2 = new Color(0.12f, 0.48f, 0.24f); // Lighter
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Soft, wide variations
                float n1 = Mathf.PerlinNoise(x * 0.005f, y * 0.005f);
                // Medium variations
                float n2 = Mathf.PerlinNoise(x * 0.02f + 100f, y * 0.02f + 100f);
                
                float combined = (n1 * 0.5f + n2 * 0.5f);
                
                // Fine static fabric noise
                float staticNoise = Random.Range(-0.04f, 0.04f);
                
                Color finalColor = Color.Lerp(baseColor1, baseColor2, combined) + new Color(staticNoise, staticNoise, staticNoise);
                
                tex.SetPixel(x, y, finalColor);
            }
        }
        
        tex.Apply();
        
        byte[] bytes = tex.EncodeToPNG();
        
        if (!Directory.Exists("Assets/Sprites"))
        {
            Directory.CreateDirectory("Assets/Sprites");
        }
        
        string path = "Assets/Sprites/CasinoFelt.png";
        File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();
        
        // Import as Sprite
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        
        Debug.Log("Felt texture generated successfully at " + path);
    }
}
