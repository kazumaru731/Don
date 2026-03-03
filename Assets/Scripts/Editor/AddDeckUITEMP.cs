using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using DonGame2D.Logic;

public class AddDeckUITEMP {
    [MenuItem("Tools/DonGame/Add Deck UI")]
    public static void Add() {
        GameObject discardBorder = null;
        GameObject discardPile = null;

        var allRects = Object.FindObjectsOfType<RectTransform>(true);
        foreach (var rt in allRects) {
            if (rt.gameObject.name == "DiscardBorder") discardBorder = rt.gameObject;
            if (rt.gameObject.name == "DiscardPilePanel") discardPile = rt.gameObject;
        }

        if (discardBorder == null || discardPile == null) {
            Debug.LogError("DiscardBorder or DiscardPilePanel not found! Are you sure you ran GameUIBuilder or opened the correct scene?");
            return;
        }

        Transform parent = discardBorder.transform.parent; // GameCanvas

        GameObject deckBorder = GameObject.Find("DeckBorder");
        if (deckBorder == null) {
            deckBorder = Object.Instantiate(discardBorder, parent);
            deckBorder.name = "DeckBorder";
            Undo.RegisterCreatedObjectUndo(deckBorder, "Create DeckBorder");
        }

        GameObject deckPile = GameObject.Find("DeckPilePanel");
        if (deckPile == null) {
            deckPile = Object.Instantiate(discardPile, parent);
            deckPile.name = "DeckPilePanel";
            Undo.RegisterCreatedObjectUndo(deckPile, "Create DeckPile");
        }

        Image deckImg = deckPile.GetComponent<Image>();
        if (deckImg == null) {
            deckImg = deckPile.AddComponent<Image>();
        }
        
        CardDatabase db = Resources.Load<CardDatabase>("CardDatabase");
        if (db != null) {
            deckImg.sprite = db.GetCardBack();
            deckImg.color = Color.white; 
            deckImg.preserveAspect = false; 
        } else {
            Debug.LogWarning("CardDatabase not found in Resources!");
        }

        RectTransform discardRt = discardPile.GetComponent<RectTransform>();
        RectTransform discardBorderRt = discardBorder.GetComponent<RectTransform>();
        RectTransform deckRt = deckPile.GetComponent<RectTransform>();
        RectTransform deckBorderRt = deckBorder.GetComponent<RectTransform>();

        Undo.RecordObject(discardRt, "Move Discard");
        Undo.RecordObject(discardBorderRt, "Move Discard Border");
        Undo.RecordObject(deckRt, "Move Deck");
        Undo.RecordObject(deckBorderRt, "Move Deck Border");

        // 中央(0)から少し左右に振り分ける (-80 と 80 など)
        // カードの幅100+枠120なので、それぞれ 70px ずつずらせば重ならない
        discardRt.anchoredPosition = new Vector2(70, 80);
        discardBorderRt.anchoredPosition = new Vector2(70, 80);

        deckRt.anchoredPosition = new Vector2(-70, 80);
        deckBorderRt.anchoredPosition = new Vector2(-70, 80);

        // DiscardPileは透明になっているので(UIBuilderでそうしている)、DeckPileもサイズ調整
        deckRt.sizeDelta = new Vector2(100, 140);
        // カードの比率に合わせて枠内にピッタリ収める

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Deck Pile has been added successfully next to Discard Pile.");
    }
}
