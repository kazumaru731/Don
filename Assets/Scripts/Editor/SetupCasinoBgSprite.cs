using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SetupCasinoBgSprite
{
    [MenuItem("Tools/Apply Casino BG Sprite")]
    public static void Run()
    {
        string path = "Assets/Sprites/casino_table_bg.png";
        Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        
        if (bgSprite == null)
        {
            Debug.LogError($"Could not load Sprite at {path}");
            return;
        }

        Image[] images = Object.FindObjectsOfType<Image>(true); // include inactive
        bool modified = false;
        foreach (Image img in images)
        {
            string nameLower = img.gameObject.name.ToLower();
            if (nameLower.Contains("bg") || nameLower.Contains("background"))
            {
                img.sprite = bgSprite;
                img.type = Image.Type.Tiled;
                // scale adjust for repeating
                img.pixelsPerUnitMultiplier = 0.5f; // Adjust this value as needed to make the pattern look good

                Debug.Log($"Applied new bg sprite to Image: {img.gameObject.name}");
                EditorUtility.SetDirty(img);
                modified = true;
            }
        }

        if (modified)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Finished applying Casino Table BG Sprite to Images.");
            
            // Save scene if changes made
            // UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}