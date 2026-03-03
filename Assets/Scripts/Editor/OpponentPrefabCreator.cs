using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using DonGame2D.UI;
using DonGame2D.Logic;
using System.IO;

public class OpponentPrefabCreator : EditorWindow
{
    private const string CardBackPath = "Assets/2d Cards Game Art Pack/Sprites/Standard 52 Cards/Standard Cards/Card Back/card_back.png";
    private const string SavePath = "Assets/Prefabs/OpponentUIInfo.prefab";

    [MenuItem("DonGame/Create Opponent UI Prefab")]
    public static void CreatePrefab()
    {
        // 1. CardDatabase の準備
        CardDatabase db = Resources.Load<CardDatabase>("CardDatabase");
        Sprite backSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CardBackPath);
        if (db != null && backSprite != null)
        {
            db.cardBackSprite = backSprite;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        // 2. 他プレイヤーUIルート
        GameObject root = new GameObject("OpponentUIInfo", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(OpponentUIInfo));
        root.layer = LayerMask.NameToLayer("UI"); // 確実にUIレイヤーに設定
        RectTransform rootRT = root.GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(280, 110); // カードがはみ出さないように少し幅を広げる

        // 親(GameUIController)のレイアウトグループによって潰されないようにサイズを保証
        LayoutElement rootLE = root.GetComponent<LayoutElement>();
        rootLE.minWidth = 280;
        rootLE.minHeight = 110;
        rootLE.preferredWidth = 280;
        rootLE.preferredHeight = 110;
        
        // アンカーとピボットを「左側の中央 (0, 0.5)」に設定する。
        // これにより、画面の左端に配置された際に右方向（画面内）に伸びるようになり、はみ出しを防ぐ。
        rootRT.anchorMin = new Vector2(0f, 0.5f);
        rootRT.anchorMax = new Vector2(0f, 0.5f);
        rootRT.pivot = new Vector2(0f, 0.5f);
        rootRT.anchoredPosition = Vector2.zero;

        Image bg = root.GetComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.5f);

        VerticalLayoutGroup vlg = root.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(15, 15, 10, 10); // 上下左右に強めの余白をとる（左端はみ出し完全防止）
        vlg.spacing = 5;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlHeight = false; 
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        OpponentUIInfo info = root.GetComponent<OpponentUIInfo>();
        info.backgroundImage = bg;

        // ハイライト用のOutlineコンポーネントを追加
        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = Color.yellow;
        outline.effectDistance = new Vector2(4, -4); // 少し太めの枠
        outline.enabled = false; // デフォルトはオフ
        info.outline = outline;

        // 名前テキスト
        GameObject nameObj = new GameObject("NameText", typeof(RectTransform), typeof(Text));
        nameObj.layer = LayerMask.NameToLayer("UI");
        nameObj.transform.SetParent(root.transform, false);
        Text nameText = nameObj.GetComponent<Text>();
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.text = "Player ID";
        nameText.fontSize = 16;
        nameText.color = Color.white;
        info.nameText = nameText;

        // 枚数テキスト
        GameObject countObj = new GameObject("CountText", typeof(RectTransform), typeof(Text));
        countObj.layer = LayerMask.NameToLayer("UI");
        countObj.transform.SetParent(root.transform, false);
        Text countText = countObj.GetComponent<Text>();
        countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        countText.text = "x0";
        countText.fontSize = 14;
        countText.color = Color.yellow;
        info.countText = countText;

        // カードアイコンコンテナ（水平リスト）
        GameObject iconContainer = new GameObject("CardIcons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        iconContainer.layer = LayerMask.NameToLayer("UI");
        iconContainer.transform.SetParent(root.transform, false);
        RectTransform iconRT = iconContainer.GetComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(234, 50);

        HorizontalLayoutGroup hlg = iconContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(5, 5, 5, 5); // カードを少し内側に寄せる
        hlg.spacing = -10; // カード同士の重なりを調整 (-15から-10に変更し、見やすく)
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlHeight = false; // カード自身の高さを維持する
        hlg.childControlWidth = false; // カード自身の幅(RectTransform)を維持する
        hlg.childForceExpandHeight = false; // 無理に広げない
        hlg.childForceExpandWidth = false;

        info.cardIconContainer = iconContainer.transform;

        // プレハブ保存
        if (!Directory.Exists("Assets/Prefabs")) Directory.CreateDirectory("Assets/Prefabs");
        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("OpponentUIInfo Updated.");
    }
}
