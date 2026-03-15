using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using DonGame2D.UI;
using DonGame2D.Logic;
using DonGame2D.Network;
using UnityEngine.SceneManagement;

namespace DonGame2D.Editor
{
    public static class GameUIBuilder
    {
        [MenuItem("Tools/DonGame/Create Basic Game UI")]
        public static void CreateBasicUI()
        {
            // 既存のすべてのキャンバス（非アクティブ含む）を検索して削除
            var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (var c in canvases)
            {
                if (c.gameObject.scene.isLoaded) // シーン内オブジェクトであることを確認
                {
                    if (c.gameObject.name == "GameCanvas" || c.gameObject.name == "TitleCanvas")
                    {
                        Object.DestroyImmediate(c.gameObject);
                    }
                }
            }

            // Canvasの作成
            GameObject gameCanvasObj = new GameObject("GameCanvas");
            Canvas canvas = gameCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameCanvasObj.AddComponent<GraphicRaycaster>();
            
            // TitleCanvasの作成
            GameObject titleCanvasObj = new GameObject("TitleCanvas");
            Canvas tCanvas = titleCanvasObj.AddComponent<Canvas>();
            tCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var tScaler = titleCanvasObj.AddComponent<CanvasScaler>();
            tScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            tScaler.referenceResolution = new Vector2(1920, 1080);
            tScaler.matchWidthOrHeight = 0.5f;
            titleCanvasObj.AddComponent<GraphicRaycaster>();

            // Event Systemの作成（なければ）
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // GameManager / タイトルコントローラーの取得または作成
            var gameUIController = Object.FindObjectOfType<GameUIController>();
            GameObject gmObj = gameUIController != null ? gameUIController.gameObject : new GameObject("GameManager");
            
            if (gameUIController == null)
            {
                gameUIController = gmObj.AddComponent<GameUIController>();
                gmObj.AddComponent<DonFusionManager2D>();
                gmObj.AddComponent<DonGameManager>(); // レガシー用
            }

            var titleUIController = gmObj.GetComponent<TitleUIController>();
            if (titleUIController == null)
            {
                titleUIController = gmObj.AddComponent<TitleUIController>();
            }

            titleUIController.titleCanvasObj = titleCanvasObj;
            titleUIController.gameCanvasObj = gameCanvasObj;

            // =========================
            // カジノ風背景を適用
            // =========================
            string playBgPath = "Assets/Sprites/PlayBackground.jpg";
            string titleBgPath = "Assets/Sprites/TitleBackground.jpg";
            
            // スプライトとしてインポート設定（プレイ画面用）
            TextureImporter playImporter = AssetImporter.GetAtPath(playBgPath) as TextureImporter;
            if (playImporter != null && playImporter.textureType != TextureImporterType.Sprite)
            {
                playImporter.textureType = TextureImporterType.Sprite;
                playImporter.spriteImportMode = SpriteImportMode.Single;
                playImporter.wrapMode = TextureWrapMode.Clamp; // 1枚表示のためにClamp設定
                playImporter.SaveAndReimport();
            }

            // スプライトとしてインポート設定（タイトル画面用）
            TextureImporter titleImporter = AssetImporter.GetAtPath(titleBgPath) as TextureImporter;
            if (titleImporter != null && titleImporter.textureType != TextureImporterType.Sprite)
            {
                titleImporter.textureType = TextureImporterType.Sprite;
                titleImporter.spriteImportMode = SpriteImportMode.Single;
                titleImporter.wrapMode = TextureWrapMode.Clamp; // 1枚表示のためにClamp設定
                titleImporter.SaveAndReimport();
            }

            Sprite playSprite = AssetDatabase.LoadAssetAtPath<Sprite>(playBgPath);
            Sprite titleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(titleBgPath);

            // プレイ画面背景
            var gameBg = CreatePanel(gameCanvasObj.transform, "CasinoBackground", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var gameBgRT = gameBg.GetComponent<RectTransform>();
            gameBgRT.anchorMin = Vector2.zero;
            gameBgRT.anchorMax = Vector2.one;
            gameBgRT.offsetMin = Vector2.zero;
            gameBgRT.offsetMax = Vector2.zero;
            Image gameBgImg = gameBg.GetComponent<Image>();
            gameBgImg.type = Image.Type.Simple; // 1枚表示に設定
            gameBgImg.color = Color.white;
            if (playSprite != null) gameBgImg.sprite = playSprite;
            gameBg.transform.SetAsFirstSibling();

            // タイトル画面背景
            var titleBg = CreatePanel(titleCanvasObj.transform, "CasinoBackground", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var titleBgRT = titleBg.GetComponent<RectTransform>();
            titleBgRT.anchorMin = Vector2.zero;
            titleBgRT.anchorMax = Vector2.one;
            titleBgRT.offsetMin = Vector2.zero;
            titleBgRT.offsetMax = Vector2.zero;
            Image titleBgImg = titleBg.GetComponent<Image>();
            titleBgImg.type = Image.Type.Simple; // 1枚表示に設定
            titleBgImg.color = Color.white;
            if (titleSprite != null) titleBgImg.sprite = titleSprite;
            titleBg.transform.SetAsFirstSibling();

            // カードプレハブの取得
            string prefabPath = "Assets/Prefabs/CardUI.prefab";
            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (cardPrefab == null)
            {
                Debug.LogWarning($"CardUI prefab not found at {prefabPath}. Please assign it manually.");
            }
            gameUIController.cardPrefab = cardPrefab;

            // 各種UIパーツの作成
            // 手札パネル（下部固定）
            var handObj = CreatePanel(gameCanvasObj.transform, "PlayerHandPanel", new Vector2(0, 300), new Vector2(1000, 250), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            handObj.GetComponent<Image>().color = new Color(0, 0, 0, 0f); // 透明に設定
            var horizontalLayout = handObj.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
            horizontalLayout.spacing = -30f; // カードを少し重ねる
            horizontalLayout.childControlWidth = true;  // LayoutElementのサイズを強制
            horizontalLayout.childControlHeight = true; // LayoutElementのサイズを強制
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;
            gameUIController.playerHandContainer = handObj.transform;

            // 黄色い枠線（角丸・ネオン風）の追加 - ディスカードパネルの背面に配置するため、先に生成してCanvasの子にする
            GameObject borderObj = new GameObject("DiscardBorder");
            borderObj.transform.SetParent(gameCanvasObj.transform, false);
            RectTransform borderRT = borderObj.AddComponent<RectTransform>();
            borderRT.sizeDelta = new Vector2(120, 160); // ディスカードパネルと同じサイズ
            borderRT.anchoredPosition = new Vector2(0, 0); // 中央配置
            borderRT.anchorMin = new Vector2(0.5f, 0.5f);
            borderRT.anchorMax = new Vector2(0.5f, 0.5f);
            borderRT.pivot = new Vector2(0.5f, 0.5f);

            Image borderImg = borderObj.AddComponent<Image>();
            string framePath = "Assets/Sprites/NeonRoundedFrame.png";
            Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
            if (frameSprite != null)
            {
                borderImg.sprite = frameSprite;
                borderImg.type = Image.Type.Sliced;
                borderImg.color = Color.yellow;
            }

            GameObject discardObj = new GameObject("DiscardPilePanel");
            discardObj.transform.SetParent(gameCanvasObj.transform, false);
            RectTransform discardRT = discardObj.AddComponent<RectTransform>();
            discardRT.sizeDelta = new Vector2(120, 160); // 通常のカードより一回り大きいサイズ
            discardRT.anchoredPosition = new Vector2(0, 0); // 中央配置
            discardRT.anchorMin = new Vector2(0.5f, 0.5f);
            discardRT.anchorMax = new Vector2(0.5f, 0.5f);
            discardRT.pivot = new Vector2(0.5f, 0.5f);

            Image discardImg = discardObj.AddComponent<Image>();
            // パネル自体は透明に
            discardImg.color = new Color(0, 0, 0, 0f);
            
            gameUIController.discardPileContainer = discardObj.transform;

            // ボタン類（右側中央）
            var drawBtnObj = CreateButton(gameCanvasObj.transform, "DrawButton", "Draw Card", new Vector2(-150, 100), new Vector2(1, 0.5f));
            gameUIController.drawButton = drawBtnObj.GetComponent<Button>();

            var donBtnObj = CreateButton(gameCanvasObj.transform, "DonButton", "DON!", new Vector2(-150, -100), new Vector2(1, 0.5f));
            gameUIController.donButton = donBtnObj.GetComponent<Button>();

            // ソートボタン（手札の下部に配置）
            var sortRankBtnObj = CreateButton(gameCanvasObj.transform, "SortRankButton", "数字順", new Vector2(250, 45), new Vector2(0.5f, 0f));
            sortRankBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 50);
            sortRankBtnObj.GetComponentInChildren<Text>().fontSize = 24;
            gameUIController.sortRankButton = sortRankBtnObj.GetComponent<Button>();

            var sortSuitBtnObj = CreateButton(gameCanvasObj.transform, "SortSuitButton", "マーク順", new Vector2(400, 45), new Vector2(0.5f, 0f));
            sortSuitBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 50);
            sortSuitBtnObj.GetComponentInChildren<Text>().fontSize = 24;
            gameUIController.sortSuitButton = sortSuitBtnObj.GetComponent<Button>();

            // 相手情報コンテナ（左側固定）
            var oppContainerObj = new GameObject("OpponentInfoContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            oppContainerObj.transform.SetParent(gameCanvasObj.transform, false);
            var oppRT = oppContainerObj.GetComponent<RectTransform>();
            oppRT.anchorMin = new Vector2(0, 0);
            oppRT.anchorMax = new Vector2(0, 1);
            oppRT.pivot = new Vector2(0, 0.5f);
            oppRT.sizeDelta = new Vector2(300, 0);
            oppRT.anchoredPosition = new Vector2(20, 0);
            
            var oppVlg = oppContainerObj.GetComponent<VerticalLayoutGroup>();
            oppVlg.padding = new RectOffset(10, 10, 10, 10);
            oppVlg.spacing = 20;
            oppVlg.childAlignment = TextAnchor.UpperLeft;
            oppVlg.childControlHeight = false;
            oppVlg.childControlWidth = true;
            oppVlg.childForceExpandHeight = false;
            oppVlg.childForceExpandWidth = true;

            gameUIController.opponentInfoContainer = oppContainerObj.transform;

            string oppPrefabPath = "Assets/Prefabs/OpponentUIInfo.prefab";
            GameObject oppPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(oppPrefabPath);
            if (oppPrefab == null)
            {
                Debug.LogWarning($"OpponentUIInfo prefab not found at {oppPrefabPath}. Please assign it manually.");
            }
            gameUIController.opponentInfoPrefab = oppPrefab;

            // テキスト群（上部）
            gameUIController.statusText = CreateText(gameCanvasObj.transform, "StatusText", "Status", new Vector2(0, -120), 48, new Vector2(0.5f, 1f)).GetComponent<Text>();
            gameUIController.penaltyText = CreateText(gameCanvasObj.transform, "PenaltyText", "", new Vector2(0, -60), 36, new Vector2(0.5f, 1f)).GetComponent<Text>();
            gameUIController.penaltyText.color = Color.white;

            // リザルトパネル（中央全体を少し覆う）
            var resultPanelObj = CreatePanel(gameCanvasObj.transform, "ResultPanel", Vector2.zero, new Vector2(600, 400), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            resultPanelObj.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);
            gameUIController.resultText = CreateText(resultPanelObj.transform, "ResultText", "Result", Vector2.zero, 48, new Vector2(0.5f, 0.5f)).GetComponent<Text>();
            gameUIController.resultPanel = resultPanelObj;
            resultPanelObj.SetActive(false);
 
            // =========================
            // スート選択UIの作成（8を出した時用）
            // =========================
            var suitPanelObj = CreatePanel(gameCanvasObj.transform, "SuitSelectionPanel", Vector2.zero, new Vector2(500, 200), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            suitPanelObj.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            var suitTitle = CreateText(suitPanelObj.transform, "SuitTitle", "マークを選択", new Vector2(0, 50), 32, new Vector2(0.5f, 0.5f));
            suitTitle.GetComponent<Text>().color = Color.white;
 
            string[] suits = { "Spades", "Hearts", "Diamonds", "Clubs" };
            string[] symbols = { "♠", "♥", "♦", "♣" };
            Vector2[] btnPos = { new Vector2(-150, -40), new Vector2(-50, -40), new Vector2(50, -40), new Vector2(150, -40) };
            gameUIController.suitButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var sBtnObj = CreateButton(suitPanelObj.transform, $"Btn_{suits[i]}", symbols[i], btnPos[i], new Vector2(0.5f, 0.5f));
                sBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(90, 80);
                gameUIController.suitButtons[i] = sBtnObj.GetComponent<Button>();
                var textCmp = sBtnObj.GetComponentInChildren<Text>();
                if (textCmp != null) {
                    textCmp.color = (i == 1 || i == 2) ? Color.red : Color.black; 
                    textCmp.fontSize = 48;
                }
            }
            gameUIController.suitSelectionPanel = suitPanelObj;
            suitPanelObj.SetActive(false);

            // =========================
            // タイトルUIの作成
            // =========================
            // タイトルをもっと上に上げる
            var titleTextObj = CreateText(titleCanvasObj.transform, "GameTitle", "DON! ONLINE", new Vector2(0, 420), 72, new Vector2(0.5f, 0.5f));
            titleTextObj.GetComponent<Text>().color = Color.white;
            
            // 人数表示テキストもフレンドマッチタイトルの上に配置
            var playersCountObj = CreateText(titleCanvasObj.transform, "PlayersCountText", "誰もいません", new Vector2(0, 320), 36, new Vector2(0.5f, 0.5f));
            titleUIController.playersCountText = playersCountObj.GetComponent<Text>();
            titleUIController.playersCountText.color = Color.yellow;

            // ランダムマッチボタン
            var randomMatchBtnObj = CreateButton(titleCanvasObj.transform, "RandomMatchButton", "ランダムマッチ", new Vector2(-140, -50), new Vector2(0.5f, 0.5f));
            randomMatchBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 90);
            titleUIController.randomMatchButton = randomMatchBtnObj.GetComponent<Button>();

            // フレンドマッチボタン
            var friendMatchBtnObj = CreateButton(titleCanvasObj.transform, "FriendMatchButton", "フレンドマッチ", new Vector2(140, -50), new Vector2(0.5f, 0.5f));
            friendMatchBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 90);
            titleUIController.friendMatchButton = friendMatchBtnObj.GetComponent<Button>();

            // ゲスト用 Readyボタン
            var readyBtnObj = CreateButton(titleCanvasObj.transform, "ReadyButton", "READY", new Vector2(0, -420), new Vector2(0.5f, 0.5f));
            readyBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 100);
            titleUIController.readyButton = readyBtnObj.GetComponent<Button>();

            // ホスト用 Startボタン (新規)
            var startBtnObj = CreateButton(titleCanvasObj.transform, "HostStartButton", "ゲーム開始", new Vector2(0, -420), new Vector2(0.5f, 0.5f));
            startBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 100);
            startBtnObj.GetComponentInChildren<Text>().color = Color.white;
            startBtnObj.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 1f); // 赤色
            titleUIController.hostStartButton = startBtnObj.GetComponent<Button>();

            // =============================================
            // フレンドマッチパネルの作成
            // =============================================
            var fmPanel = CreatePanel(titleCanvasObj.transform, "FriendMatchPanel", Vector2.zero, new Vector2(700, 550), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            fmPanel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.15f, 0.95f);
            titleUIController.friendMatchPanel = fmPanel;

            // タイトルを上部に
            var fmTitle = CreateText(fmPanel.transform, "FMTitle", "フレンドマッチ", new Vector2(0, 220f), 48, new Vector2(0.5f, 0.5f));
            fmTitle.GetComponent<Text>().color = Color.white;
 
            // ホスト・ゲストボタンの位置も少し下げる
            var hostBtnObj = CreateButton(fmPanel.transform, "HostButton", "ホスト\n（ルームを作る）", new Vector2(-150, -30), new Vector2(0.5f, 0.5f));
            hostBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 100);
            titleUIController.hostButton = hostBtnObj.GetComponent<Button>();
 
            // ゲストボタン
            var guestBtnObj = CreateButton(fmPanel.transform, "GuestButton", "ゲスト\n（ルームに参加）", new Vector2(150, -30), new Vector2(0.5f, 0.5f));
            guestBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 100);
            titleUIController.guestButton = guestBtnObj.GetComponent<Button>();
 
            // 戻るボタン
            // --- ホスト用パネル ---
            var hostPanel = CreatePanel(fmPanel.transform, "HostPanel", new Vector2(0, -110), new Vector2(500, 70), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            hostPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);
            titleUIController.hostPanel = hostPanel;

            var roomIdLabel = CreateText(hostPanel.transform, "RoomIdLabel", "ルームID: ------", new Vector2(-60, 0), 36, new Vector2(0.5f, 0.5f));
            roomIdLabel.GetComponent<Text>().color = Color.cyan;
            titleUIController.roomIdDisplayText = roomIdLabel.GetComponent<Text>();

            // IDコピーボタン
            var copyBtnObj = CreateButton(hostPanel.transform, "CopyIdButton", "コピー", new Vector2(160, 0), new Vector2(0.5f, 0.5f));
            copyBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 50);
            titleUIController.copyIdButton = copyBtnObj.GetComponent<Button>();

            // --- ゲスト用パネル ---
            var guestPanel = CreatePanel(fmPanel.transform, "GuestPanel", new Vector2(0, -110), new Vector2(500, 80), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            guestPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);
            titleUIController.guestPanel = guestPanel;

            // InputField (TMP) を作成
            var inputObj = new GameObject("RoomIdInputField");
            inputObj.transform.SetParent(guestPanel.transform, false);
            var inputRT = inputObj.AddComponent<RectTransform>();
            inputRT.anchorMin = new Vector2(0.5f, 0.5f);
            inputRT.anchorMax = new Vector2(0.5f, 0.5f);
            inputRT.pivot = new Vector2(0.5f, 0.5f);
            inputRT.sizeDelta = new Vector2(320, 60);
            inputRT.anchoredPosition = new Vector2(-85, 0);
            var inputBg = inputObj.AddComponent<Image>();
            inputBg.color = Color.white;
            var inputField = inputObj.AddComponent<InputField>();
            
            var inputText = CreateText(inputObj.transform, "Text", "", Vector2.zero, 32, new Vector2(0.5f, 0.5f));
            var inputTextCmp = inputText.GetComponent<Text>();
            inputTextCmp.color = Color.black;
            inputTextCmp.alignment = TextAnchor.MiddleCenter;
            inputField.textComponent = inputTextCmp;

            var placeholderObj = CreateText(inputObj.transform, "Placeholder", "ルームIDを入力", Vector2.zero, 32, new Vector2(0.5f, 0.5f));
            var placeholderCmp = placeholderObj.GetComponent<Text>();
            placeholderCmp.color = Color.gray;
            placeholderCmp.alignment = TextAnchor.MiddleCenter;
            inputField.placeholder = placeholderCmp;
            inputField.characterLimit = 6;
            titleUIController.roomIdInputField = inputField;

            // 参加ボタン
            var joinBtnObj = CreateButton(guestPanel.transform, "JoinButton", "参加", new Vector2(180, 0), new Vector2(0.5f, 0.5f));
            joinBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(130, 60);
            titleUIController.joinButton = joinBtnObj.GetComponent<Button>();

            // 初期状態を整える
            hostPanel.SetActive(false);
            guestPanel.SetActive(false);
            fmPanel.SetActive(false);

            gameUIController.useFusion = true;
            EditorUtility.SetDirty(gameUIController);
            EditorUtility.SetDirty(titleUIController);

            // 初期状態はタイトルを表示し、ゲーム画面を隠す
            gameCanvasObj.SetActive(false);
            titleCanvasObj.SetActive(true);

            Debug.Log("<color=green>タイトルUI（フレンドマッチ対応）の作成が完了しました！</color>");
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMin.y); // アンカーにピボットを合わせる
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            obj.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
            return obj;
        }

        private static GameObject CreateText(Transform parent, string name, string text, Vector2 anchoredPosition, float fontSize, Vector2 anchor)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(600, 100);
            
            Text txtcmp = obj.AddComponent<Text>();
            txtcmp.text = text;
            txtcmp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txtcmp.fontSize = (int)fontSize;
            txtcmp.alignment = TextAnchor.MiddleCenter;
            txtcmp.color = Color.white;
            txtcmp.raycastTarget = false; 
            return obj;
        }

        private static GameObject CreateButton(Transform parent, string name, string text, Vector2 anchoredPosition, Vector2 anchor)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(250, 80);

            Image img = obj.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0); // 枠を透明にする
            
            Button btn = obj.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; // 標準のトランジションはオフ

            // ホバーエフェクトを追加
            obj.AddComponent<HoverEffect>();

            var textObj = CreateText(obj.transform, "Text", text, Vector2.zero, 36, new Vector2(0.5f, 0.5f));
            var textCmp = textObj.GetComponent<Text>();
            textCmp.color = Color.white; // 文字のみなので白にする
            textCmp.resizeTextForBestFit = true;
            textCmp.resizeTextMinSize = 14;
            textCmp.resizeTextMaxSize = 36;
            
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            return obj;
        }
    }
}
