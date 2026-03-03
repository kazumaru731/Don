using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class AnalyzeSprites
{
    [MenuItem("Tools/DonGame/Analyze Sprites")]
    public static void Analyze()
    {
        string[] paths = {
            "Assets/Cards/A.png",
            "Assets/Cards/Club/Club_2-10.png",
            "Assets/Cards/Club/Club_J-Q-K.png"
        };

        foreach (var path in paths)
        {
            if (!File.Exists(path)) {
                Debug.LogWarning("Not found: " + path);
                continue;
            }
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null) {
                if (!importer.isReadable || importer.textureCompression != TextureImporterCompression.Uncompressed) {
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                var rects = UnityEditorInternal.InternalSpriteUtility.GenerateAutomaticSpriteRectangles(tex, 4, 0);
                Debug.Log($"{path} - {tex.width}x{tex.height} - Auto Rects: {rects.Length}");
                foreach(var r in rects) {
                    Debug.Log($"Rect: {r}");
                }
            }
        }
    }
}
