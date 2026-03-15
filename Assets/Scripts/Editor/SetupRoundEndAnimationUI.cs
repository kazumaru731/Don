using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using DonGame2D.UI;

public static class SetupRoundEndAnimationUI
{
    [MenuItem("Tools/Setup Round End Animation UI")]
    public static void Run()
    {
        GameUIController uiController = Object.FindObjectOfType<GameUIController>(true);
        if (uiController == null)
        {
            Debug.LogError("GameUIController not found in the scene.");
            return;
        }

        bool modified = false;

        // 1. RevealedHandContainer の設定
        if (uiController.revealedHandContainer == null)
        {
            // GameCanvas を探す
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
                // 中央下部に空のオブジェクトを作成
                GameObject handContainer = new GameObject("RevealedHandContainer");
                RectTransform rt = handContainer.AddComponent<RectTransform>();
                rt.SetParent(gameCanvas.transform, false);
                rt.anchorMin = new Vector2(0.5f, 0.3f);
                rt.anchorMax = new Vector2(0.5f, 0.3f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                
                // 横並びにするために HorizontalLayoutGroup を追加
                HorizontalLayoutGroup hg = handContainer.AddComponent<HorizontalLayoutGroup>();
                hg.childAlignment = TextAnchor.MiddleCenter;
                hg.spacing = 20f;
                hg.childControlWidth = false;
                hg.childControlHeight = false;
                
                uiController.revealedHandContainer = handContainer.transform;
                Debug.Log("Created and assigned RevealedHandContainer.");
                modified = true;
            }
        }

        // 2. FloatingTextPrefab の作成/設定
        if (uiController.floatingTextPrefab == null)
        {
            // まず既存のプレハブを探す
            string[] guids = AssetDatabase.FindAssets("FloatingTextPrefab t:Prefab");
            GameObject prefab = null;
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            
            if (prefab == null)
            {
                // 新しくプレハブを作成する
                GameObject textObj = new GameObject("FloatingTextPrefab");
                RectTransform rt = textObj.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(300, 100);
                
                Text txt = textObj.AddComponent<Text>();
                // Font を適当に設定（デフォルト LegacyRuntime）
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 50;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.yellow;
                
                // シャドウかなにかをつけて見やすく
                Shadow shadow = textObj.AddComponent<Shadow>();
                shadow.effectColor = Color.black;
                shadow.effectDistance = new Vector2(2, -2);
                
                // Resources等ではなく、Assets/Prefabs/UI に保存する
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                    AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
                
                string savePath = "Assets/Prefabs/UI/FloatingTextPrefab.prefab";
                prefab = PrefabUtility.SaveAsPrefabAsset(textObj, savePath);
                GameObject.DestroyImmediate(textObj);
                Debug.Log($"Created new FloatingTextPrefab at {savePath}.");
            }
            
            uiController.floatingTextPrefab = prefab;
            Debug.Log("Assigned FloatingTextPrefab.");
            modified = true;
        }

        // 3. ScoreAnimationController のアタッチと設定
        ScoreAnimationController anim = uiController.GetComponent<ScoreAnimationController>();
        if (anim == null)
        {
            anim = uiController.gameObject.AddComponent<ScoreAnimationController>();
            anim.uiController = uiController;
            uiController.scoreAnimationController = anim;
            Debug.Log("Added and linked ScoreAnimationController to GameUIController.");
            modified = true;
        }
        else if (uiController.scoreAnimationController == null || anim.uiController == null)
        {
            uiController.scoreAnimationController = anim;
            anim.uiController = uiController;
            Debug.Log("Linked existing ScoreAnimationController and GameUIController.");
            modified = true;
        }

        if (modified)
        {
            EditorUtility.SetDirty(uiController);
            if (anim != null) EditorUtility.SetDirty(anim);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Round End Animation UI setup complete. Please save the scene.");
        }
        else
        {
            Debug.Log("Round End Animation UI is already set up correctly.");
        }
    }
}