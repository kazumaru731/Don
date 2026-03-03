using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SetupPlayBgSingle
{
    [MenuItem("Tools/Apply Play BG Single")]
    public static void Run()
    {
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
                    if (img.transform.parent == gameCanvas.transform)
                    {
                        // タイリング表記をやめ、一枚絵としてフィットさせる
                        img.type = Image.Type.Simple;
                        
                        // 画像の縦横比を維持したい場合は true にしますが、
                        // 画面全体に枠ごと伸ばして敷き詰めたい場合は false にします
                        img.preserveAspect = false;

                        Debug.Log($"Applied Simple Image Type to play bg Image: {img.gameObject.name}");
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
            Debug.Log("Finished applying Single Play BG configuration.");
        }
    }
}