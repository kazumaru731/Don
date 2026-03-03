using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using DonGame2D.UI;

public static class SetupAnimationOverlayUI
{
    [MenuItem("Tools/Setup Animation Overlay UI")]
    public static void Run()
    {
        GameUIController uiController = Object.FindObjectOfType<GameUIController>(true);
        if (uiController == null)
        {
            Debug.LogError("GameUIController not found in the scene.");
            return;
        }

        bool modified = false;

        if (uiController.animationOverlay == null)
        {
            Canvas gameCanvas = null;
            Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
            foreach (var c in canvases)
            {
                if (c.gameObject.name == "GameCanvas")
                {
                    gameCanvas = c;
                    break;
                }
            }

            if (gameCanvas != null)
            {
                // 暗転用オーバーレイの作成
                GameObject overlayObj = new GameObject("AnimationOverlay");
                RectTransform rt = overlayObj.AddComponent<RectTransform>();
                rt.SetParent(gameCanvas.transform, false);
                
                // 全画面に広げる
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                
                Image img = overlayObj.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0.85f); // 85% 黒
                img.raycastTarget = true; // クリックブロック用

                overlayObj.SetActive(false); // 初期状態は非表示
                uiController.animationOverlay = overlayObj;

                Debug.Log("Created and assigned AnimationOverlay.");
                modified = true;
            }
            else
            {
                Debug.LogWarning("GameCanvas not found.");
            }
        }

        if (modified)
        {
            EditorUtility.SetDirty(uiController);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Animation Overlay UI setup complete.");
        }
        else
        {
            Debug.Log("Animation Overlay UI is already set up.");
        }
    }
}