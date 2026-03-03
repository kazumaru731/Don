using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using DonGame2D.UI;

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
        if (titleController == null)
        {
            Debug.LogError("TitleUIController not found in the scene.");
            return;
        }

        GameObject titleCanvasObj = titleController.titleCanvasObj;
        if (titleCanvasObj == null)
        {
            Debug.LogError("TitleCanvasObj is not set in TitleUIController.");
            return;
        }

        // 1. CPUマッチボタンの作成 (既存のボタンの並びに配置)
        // Titleキャンバスのルートを探してボタンを追加
        GameObject cpuMatchBtnObj = new GameObject("CpuMatchButton");
        cpuMatchBtnObj.transform.SetParent(titleCanvasObj.transform, false);
        var cpuMatchRect = cpuMatchBtnObj.AddComponent<RectTransform>();
        cpuMatchRect.anchoredPosition = new Vector2(0, -90); // 既存ボタン（ランダム, フレンドなど）の下に配置
        cpuMatchRect.sizeDelta = new Vector2(250, 60);

        var cpuMatchImg = cpuMatchBtnObj.AddComponent<Image>();
        cpuMatchImg.color = new Color(0.8f, 0.4f, 0.2f); // オレンジっぽくする
        var cpuMatchBtn = cpuMatchBtnObj.AddComponent<Button>();
        cpuMatchBtn.targetGraphic = cpuMatchImg;

        GameObject cpuMatchTextObj = new GameObject("Text");
        cpuMatchTextObj.transform.SetParent(cpuMatchBtnObj.transform, false);
        var cpuMatchTextRect = cpuMatchTextObj.AddComponent<RectTransform>();
        cpuMatchTextRect.anchorMin = Vector2.zero;
        cpuMatchTextRect.anchorMax = Vector2.one;
        cpuMatchTextRect.sizeDelta = Vector2.zero;
        var cpuMatchText = cpuMatchTextObj.AddComponent<Text>();
        cpuMatchText.text = "CPUマッチ";
        cpuMatchText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cpuMatchText.fontSize = 28;
        cpuMatchText.alignment = TextAnchor.MiddleCenter;
        cpuMatchText.color = Color.white;

        titleController.cpuMatchButton = cpuMatchBtn;

        // 2. CPUマッチパネルの作成
        GameObject cpuPanelObj = new GameObject("CpuMatchPanel");
        cpuPanelObj.transform.SetParent(titleCanvasObj.transform, false);
        var cpuPanelRect = cpuPanelObj.AddComponent<RectTransform>();
        cpuPanelRect.anchoredPosition = new Vector2(0, -30);
        cpuPanelRect.sizeDelta = new Vector2(400, 300);
        var cpuPanelImg = cpuPanelObj.AddComponent<Image>();
        cpuPanelImg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        cpuPanelObj.SetActive(false); // 初期非表示

        titleController.cpuMatchPanel = cpuPanelObj;

        // パネルタイトル
        GameObject panelTitleObj = new GameObject("TitleText");
        panelTitleObj.transform.SetParent(cpuPanelObj.transform, false);
        var panelTitleRect = panelTitleObj.AddComponent<RectTransform>();
        panelTitleRect.anchoredPosition = new Vector2(0, 110);
        panelTitleRect.sizeDelta = new Vector2(300, 40);
        var panelTitleText = panelTitleObj.AddComponent<Text>();
        panelTitleText.text = "プレイする人数を選択";
        panelTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        panelTitleText.fontSize = 24;
        panelTitleText.alignment = TextAnchor.MiddleCenter;
        panelTitleText.color = Color.white;

        // ボタン作成ヘルパー
        Button CreatePanelButton(string name, string text, float yPos)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(cpuPanelObj.transform, false);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, yPos);
            rect.sizeDelta = new Vector2(250, 50);

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f);
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;
            var txt = txtObj.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 24;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            return btn;
        }

        titleController.cpu2PlayerButton = CreatePanelButton("Cpu2PlayerButton", "2人プレイ", 40);
        titleController.cpu3PlayerButton = CreatePanelButton("Cpu3PlayerButton", "3人プレイ", -20);
        titleController.cpu4PlayerButton = CreatePanelButton("Cpu4PlayerButton", "4人プレイ", -80);
        titleController.cpuBackButton = CreatePanelButton("CpuBackButton", "戻る", -140);
        
        // 戻るボタンだけ色をマイルドに
        titleController.cpuBackButton.GetComponent<Image>().color = new Color(0.5f, 0.2f, 0.2f);

        EditorUtility.SetDirty(titleController);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("CPU Match UI dynamically created and assigned successfully!");
    }
}
