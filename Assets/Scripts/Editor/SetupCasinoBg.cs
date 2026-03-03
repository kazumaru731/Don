using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SetupCasinoBg
{
    [MenuItem("Tools/Setup Casino BG")]
    public static void Run()
    {
        string path = "Assets/Sprites/casino_table_bg.png";
        
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100f;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            Debug.Log($"[{path}] Texture settings updated.");
        }
        else
        {
            Debug.LogError($"Could not load TextureImporter for {path}");
            return;
        }

        // Title SceneやGame SceneのUI上で適用されているRawImage等を探す
        // シーン上の全てのRawImageを検索し、名前に「Background」が含まれていたら設定する等
        RawImage[] rawImages = Object.FindObjectsOfType<RawImage>(true); // include inactive
        bool modified = false;
        foreach (RawImage img in rawImages)
        {
            // TitleUIController の背景として使われていそうな RawImage
            if (img.gameObject.name.ToLower().Contains("bg") || img.gameObject.name.ToLower().Contains("background"))
            {
                // Load texture and apply
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    img.texture = tex;
                    // Tilingをスケールに合わせる
                    // img.uvRect = new Rect(0, 0, 5, 5); // Example
                    Debug.Log($"Applied new texture to RawImage: {img.gameObject.name}");
                    EditorUtility.SetDirty(img);
                    modified = true;
                }
            }
        }

        if (modified)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Finished setting up Casino Table BG.");
        }
    }
}
