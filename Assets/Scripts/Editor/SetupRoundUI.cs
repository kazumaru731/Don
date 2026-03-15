using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using DonGame2D.UI;

public class SetupRoundUI
{
    [MenuItem("Tools/Setup Round UI")]
    public static void Setup()
    {
        GameUIController uiController = Object.FindObjectOfType<GameUIController>();
        if (uiController == null)
        {
            Debug.LogError("GameUIController not found in the scene.");
            return;
        }

        // GameCanvasを確実に見つけるために、GameUIControllerの子要素が属するCanvasを取得する (非アクティブ状態も検索対象とする)
        Canvas canvas = null;
        if (uiController.statusText != null)
        {
            canvas = uiController.statusText.GetComponentInParent<Canvas>(true);
        }
        
        if (canvas == null)
        {
            Debug.LogError("GameCanvas could not be found via uiController.statusText.");
            return;
        }

        // --- 全てのCanvasから誤って配置された古い RoundUI を探して完全削除 ---
        Canvas[] allCanvases = Object.FindObjectsOfType<Canvas>(true);
        foreach (var c in allCanvases)
        {
            Transform badExisting = c.transform.Find("RoundUI");
            if (badExisting != null)
            {
                Object.DestroyImmediate(badExisting.gameObject);
                Debug.Log($"Removed incorrectly placed RoundUI from canvas: {c.gameObject.name}");
            }
        }

        // 1. ラウンド画像のロード
        Sprite roundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Round.png");
        if (roundSprite == null)
        {
            Debug.LogError("Round.png not found at Assets/Sprites/UI/Round.png");
            return;
        }

        // 2. ラウンド用UIの作成 (左上配置)

        GameObject roundObj = new GameObject("RoundUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        roundObj.transform.SetParent(canvas.transform, false);
        roundObj.transform.SetAsLastSibling();

        RectTransform rt = roundObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); // Top Left
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(20, -20); // 少しマージンを取る
        
        // 固定枠（例えば 250x250 の正方形）を作り、画像はその中で比率を崩さず収まるように設定する
        rt.sizeDelta = new Vector2(250f, 250f);
        rt.localScale = Vector3.one;

        Image img = roundObj.GetComponent<Image>();
        img.sprite = roundSprite;
        img.preserveAspect = true; // アスペクト比を維持して正方形枠に綺麗に収める
        img.color = Color.white;

        // 3. テキストの作成
        GameObject textObj = new GameObject("RoundText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObj.transform.SetParent(roundObj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        Text txt = textObj.GetComponent<Text>();
        txt.text = "1/5";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 45; // 30から45に拡大
        txt.color = Color.black; // 文字色を黒に変更
        txt.font = uiController.mainFontBold != null ? uiController.mainFontBold : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // GameUIControllerへの参照割り当て
        uiController.roundText = txt;
        EditorUtility.SetDirty(uiController);
        
        // タイトル画面表示時に最初から出てしまわないように非表示状態にしておく
        roundObj.SetActive(false);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Round UI setup complete!");
    }
}
