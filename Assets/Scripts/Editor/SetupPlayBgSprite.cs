using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SetupPlayBgSprite
{
    [MenuItem("Tools/Apply Play BG Sprite")]
    public static void Run()
    {
        string path = "Assets/Sprites/play_background.png";
        
        // 1. スプライトとしてのインポート設定を確実にする
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            // importer.spritePixelsPerUnit = 100f;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (bgSprite == null)
        {
            Debug.LogError($"Could not load Sprite at {path}");
            return;
        }

        // 2. GameCanvas以下の背景を探す
        // シーン内のすべてのCanvasを検索し、"GameCanvas"という名前のものから探す
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        Canvas gameCanvas = null;
        foreach(var c in canvases)
        {
            if(c.gameObject.name == "GameCanvas")
            {
                gameCanvas = c;
                break;
            }
        }

        bool modified = false;

        if (gameCanvas != null)
        {
            Image[] images = gameCanvas.GetComponentsInChildren<Image>(true); // include inactive
            foreach (Image img in images)
            {
                string nameLower = img.gameObject.name.ToLower();
                // GameCanvasの直下や背景っぽい名前のものを対象とする
                if (nameLower.Contains("bg") || nameLower.Contains("background") || nameLower == "gamecanvas")
                {
                    // ボタン等の背景を誤爆しないよう注意
                    // （通常、大元の背景はBackground等の名前になっているか、階層が浅い）
                    if (img.transform.parent == gameCanvas.transform)
                    {
                        img.sprite = bgSprite;
                        img.type = Image.Type.Tiled;
                        // scale adjust for repeating
                        img.pixelsPerUnitMultiplier = 1f; // Adjust this value as needed

                        Debug.Log($"Applied new play bg sprite to Image: {img.gameObject.name}");
                        EditorUtility.SetDirty(img);
                        modified = true;
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("GameCanvas not found.");
        }

        if (modified)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Finished applying Play BG Sprite to GameCanvas.");
        }
    }
}