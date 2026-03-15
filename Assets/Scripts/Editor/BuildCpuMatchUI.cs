using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using DonGame2D.UI;
using System.Linq;

/*
[UnityEditor.InitializeOnLoad]
public class BuildCpuMatchUIAutoRun
{
    static BuildCpuMatchUIAutoRun()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool("BuildCpuMatchUIV2", false)) return;
            SessionState.SetBool("BuildCpuMatchUIV2", true);
            BuildCpuMatchUI.Run();
        };
    }
}
*/

public class BuildCpuMatchUI
{
    [MenuItem("Tools/Build CPU Match UI")]
    public static void Run()
    {
        // 念のためシーンを開く
        string scenePath = "Assets/Scenes/SampleScene.unity";
        if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path != scenePath)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }

        var titleController = Object.FindObjectOfType<TitleUIController>(true);
        if (titleController == null) return;

        GameObject titleCanvasObj = titleController.titleCanvasObj;
        Transform buttonsContainer = titleCanvasObj.transform.Find("SafeAreaContainer");
        if (buttonsContainer == null) buttonsContainer = titleCanvasObj.transform;

        // --- 1. Spriteの準備 (既存のユーザー設定スライスを使用) ---
        Sprite GetSprite(string path, int index)
        {
            // インポーターの設定を強制変更しないように修正
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            var sprites = allAssets.OfType<Sprite>().OrderBy(s => s.name).ToArray();
            
            if (sprites.Length > index) return sprites[index];
            
            Debug.LogWarning($"[BuildUI] Sprite not found at {path} index {index}. Assets found: {allAssets.Length}");
            return null;
        }

        // --- 2. クリーンアップ ---
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Repair UI with Sprites");
        for (int i = titleCanvasObj.transform.childCount - 1; i >= 0; i--)
        {
            Transform t = titleCanvasObj.transform.GetChild(i);
            if (t.name == "CpuMatchPanel" || t.name.Contains("Generated")) Undo.DestroyObjectImmediate(t.gameObject);
        }
        // MainMenuCardも一旦掃除
        for (int i = buttonsContainer.childCount - 1; i >= 0; i--)
        {
            Transform t = buttonsContainer.GetChild(i);
            if (t.name.Contains("MatchCard")) Undo.DestroyObjectImmediate(t.gameObject);
        }

        // --- 3. メインメニューカードの作成 ---
        string titleSpritePath = "Assets/Sprites/UI/TitleButton.png";
        SelectionCard CreateCard(Transform parent, string name, string id, Sprite sp)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(SelectionCard), typeof(CanvasGroup));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250, 375); // 1:1.5のカード比率に固定

            var img = obj.GetComponent<Image>();
            img.sprite = sp;
            img.color = Color.white;
            img.preserveAspect = false; // 強制的に枠いっぱいに広げてサイズを完全に統一する
            var sc = obj.GetComponent<SelectionCard>();
            sc.selectionId = id;
            return sc;
        }

        var m1 = CreateCard(buttonsContainer, "RandomMatchCard", "Random", GetSprite(titleSpritePath, 0));
        var m2 = CreateCard(buttonsContainer, "FriendMatchCard", "Friend", GetSprite(titleSpritePath, 1));
        var m3 = CreateCard(buttonsContainer, "CpuMatchCard", "CPU", GetSprite(titleSpritePath, 2));
        titleController.mainSelectionCards = new SelectionCard[] { m1, m2, m3 };

        // 既存のボタンを隠す
        if (titleController.randomMatchButton != null) titleController.randomMatchButton.gameObject.SetActive(false);
        if (titleController.friendMatchButton != null) titleController.friendMatchButton.gameObject.SetActive(false);
        if (titleController.cpuMatchButton != null) titleController.cpuMatchButton.gameObject.SetActive(false);

        // --- 4. CPU人数選択パネルの作成 ---
        GameObject cpuPanel = new GameObject("CpuMatchPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        cpuPanel.transform.SetParent(titleCanvasObj.transform, false);
        var panelRect = cpuPanel.GetComponent<RectTransform>();
        panelRect.anchoredPosition = Vector2.zero; panelRect.sizeDelta = new Vector2(1000, 600);
        cpuPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0f); // 背景を完全に透明に修正
        cpuPanel.SetActive(false);
        titleController.cpuMatchPanel = cpuPanel;

        string cpuSpritePath = "Assets/Sprites/UI/CpuPlayerButton.png";
        SelectionCard CreateCpuCard(string name, int count, Sprite sp, float xPos)
        {
            var sc = CreateCard(cpuPanel.transform, name, "", sp);
            sc.playerCount = count;
            var rect = sc.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(xPos, 0); // Yは常に0
            rect.sizeDelta = new Vector2(250, 375); // メインカードとサイズを一致させる
            return sc;
        }

        var c2 = CreateCpuCard("Card2", 2, GetSprite(cpuSpritePath, 0), -300);
        var c3 = CreateCpuCard("Card3", 3, GetSprite(cpuSpritePath, 1), 0);
        var c4 = CreateCpuCard("Card4", 4, GetSprite(cpuSpritePath, 2), 300);
        titleController.cpuSelectionCards = new SelectionCard[] { c2, c3, c4 };

        // --- 5. フレンドマッチ ホスト/ゲストボタンの差し替え ---
        string friendSpritePath = "Assets/Sprites/UI/FriendMatchButton.png";
        void SetupFriendCard(Button btn, Sprite sp, float xPos)
        {
            if (btn == null) return;
            var rect = btn.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(xPos, 0); // 中央
            rect.sizeDelta = new Vector2(180, 270); // 小さめのカードサイズに調整
            
            var img = btn.GetComponent<Image>();
            if (img != null) { 
                img.sprite = sp; 
                img.color = Color.white; 
                img.preserveAspect = false; // 比率維持をオフにしてサイズを強制
            }
            
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.enabled = false;

            // 演出用のコンポーネントを追加/更新
            var sc = btn.GetComponent<SelectionCard>() ?? btn.gameObject.AddComponent<SelectionCard>();
            sc.selectionId = (btn.name.Contains("Host")) ? "CancelMatch" : "CancelMatch"; // Unify as CancelMatch for easy fanning logic overrides
            sc.hoverYOffset = 30f;
            sc.hoverScale = 1.1f;
        }

        SetupFriendCard(titleController.hostButton, GetSprite(friendSpritePath, 0), -100);
        SetupFriendCard(titleController.guestButton, GetSprite(friendSpritePath, 1), 100);

        EditorUtility.SetDirty(titleController);
        EditorUtility.SetDirty(titleController.hostButton);
        EditorUtility.SetDirty(titleController.guestButton);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("UI build complete with automatic sprite assignment.");
    }
}
