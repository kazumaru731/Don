using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.U2D.Sprites;

public class ForceTrimmer {
    [MenuItem("Tools/DonGame/Force Trim All Cards")]
    public static void TrimAll() {
        string[] paths = {
            "Assets/Cards/A.png",
            "Assets/Cards/Club/Club_2-10.png",
            "Assets/Cards/Diamond/Diamond_2-10.png",
            "Assets/Cards/Heart/Heart_2-10.png",
            "Assets/Cards/Spade/Spade_2-10.png"
        };

        foreach (var path in paths) {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            if (!importer.isReadable || importer.textureCompression != TextureImporterCompression.Uncompressed) {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            var rects = dp.GetSpriteRects();
            bool changed = false;

            for (int i=0; i < rects.Length; i++) {
                var r = rects[i];
                Rect original = r.rect;
                Rect trimmed = GetTrimmedRect(tex, original);
                if (original != trimmed) {
                    r.rect = trimmed;
                    rects[i] = r;
                    changed = true;
                    Debug.Log($"Trimmed {r.name}: {original} -> {trimmed}");
                }
            }

            if (changed) {
                dp.SetSpriteRects(rects);
                dp.Apply();
                importer.SaveAndReimport();
                Debug.Log($"Applied trim to {path}");
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Trim All Complete!");
    }

    private static Rect GetTrimmedRect(Texture2D tex, Rect originalRect) {
        int xMin = Mathf.RoundToInt(originalRect.x);
        int yMin = Mathf.RoundToInt(originalRect.y);
        int width = Mathf.RoundToInt(originalRect.width);
        int height = Mathf.RoundToInt(originalRect.height);

        Color32[] pixels = tex.GetPixels32();
        int left = xMin + width;
        int right = xMin;
        int bottom = yMin + height;
        int top = yMin;
        bool found = false;

        for (int y = yMin; y < yMin + height; y++) {
            for (int x = xMin; x < xMin + width; x++) {
                if (x >= tex.width || y >= tex.height || x < 0 || y < 0) continue;
                if (pixels[y * tex.width + x].a > 10) {
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < bottom) bottom = y;
                    if (y > top) top = y;
                    found = true;
                }
            }
        }

        if (!found) return originalRect;
        
        left = Mathf.Max(xMin, left - 1);
        bottom = Mathf.Max(yMin, bottom - 1);
        right = Mathf.Min(xMin + width - 1, right + 1);
        top = Mathf.Min(yMin + height - 1, top + 1);
        
        return new Rect(left, bottom, right - left + 1, top - bottom + 1);
    }
}
