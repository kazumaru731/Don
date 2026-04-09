using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DonGame2D.Models;
using DonGame2D.Logic;
using System.Linq; // Added for OrderBy
using Fusion;

namespace DonGame2D.UI
{
    public class GameUIController : MonoBehaviour
    {
        [Header("Data")]
        public CardDatabase cardDatabase;

        [Header("Prefabs")]
        public GameObject cardPrefab;

        [Header("Containers")]
        public Transform playerHandContainer;
        public Transform discardPileContainer;
        public Transform deckPileContainer; // 山札のアニメーション出発点として使用

        [Header("UI Elements")]
        public Text statusText;
        public Text penaltyText;
        public Button drawButton;
        public Button donButton;
        public Button discardDonButton; // 捨て札の上に表示する新しいDonボタン
        public GameObject resultPanel;
        public Text resultText;

        [Header("Fonts")]
        public Font mainFontRegular;
        public Font mainFontBold;

        [Header("Score Animation")]
        public ScoreAnimationController scoreAnimationController;

        [Header("Round UI")]
        public Text roundText; // 左上のラウンド表示用
        public GameObject creditCardObject; // 右上のクレジットカードUI用
        public Text creditText; // クレジット数値表示用


        [Header("Animation")]
        public GameObject animationOverlay;     // アニメーション中の背景暗転用
        public Transform revealedHandContainer; // 敗者の手札を中央に表示する用
        public GameObject floatingTextPrefab;   // 浮き上がる数字用テキストプレハブ

        [Header("Sorting")]
        public Button sortRankButton;
        public Button sortSuitButton;

        [Header("Suit Selection")]
        public GameObject suitSelectionPanel;
        public Button[] suitButtons;

        [Header("Opponents")]
        public Transform opponentInfoContainer;
        public GameObject opponentInfoPrefab;
        public Dictionary<int, OpponentUIInfo> opponentUIs = new Dictionary<int, OpponentUIInfo>();
        
        private Text _drawPenaltyIndicator; // 2ドロー累積表示用テキスト

        [Header("Final Result")]
        public GameObject finalResultPanel;
        public Transform finalResultContainer;
        public GameObject finalResultEntryPrefab;
        public Button finalResultBackButton;

        [Header("System")]
        public bool useFusion = true; // Fusion2を利用するかどうか

        private List<CardUI> playerHandUI = new List<CardUI>();
        private List<GameObject> dummySlots = new List<GameObject>();
        public Transform handVisualParent; // ビジュアル用の実体カードの親（自由配置用）
        private Coroutine applyHandPositionsCoroutine;
        private List<GameObject> _scatteredCards = new List<GameObject>();
        
        private string temporaryNotification = "";
        private float notificationTimer = 0f;

        private bool hasSubscribedToFusion = false;
        private bool isDonButtonSetup = false; // Donボタンの初期化完了フラグ
        
        private bool isDealingAnimationRunning = false; // [FIX] 操作ロック用（配布・ドロー共通）
        private bool isInitialDealingAnimation = false; // [NEW] ラウンド開始時の0→5枚配布中フラグ
        private Dictionary<int, int> _dealingHandCounts = new Dictionary<int, int>(); // [NEW] 手札配付用のアニメーション枚数保持
        private Dictionary<int, int> _displayedCredits = new Dictionary<int, int>();
        private bool _isCreditInitialized = false;
        private Queue<CardInfo> pendingDrawAnimations = new Queue<CardInfo>(); // アニメーション待ちキュー
        private int inFlightAnimationCount = 0; // Dequeue済みでアニメーション中のカード枚数
        private List<CardUI> animatingDrawCards = new List<CardUI>(); // 進行中のドロー演出用カード
        private bool _isExpectingInitialDeal = false; // 初期配布を待機しているかどうかのローカルフラグ
        private bool _hasInitialDealCompleted = false; // [NEW] 手札の初期配布アニメーションが完了したかどうかの論理ロック

        public bool IsInteractionBlocked => isDealingAnimationRunning || IsScatterAnimationRunning || (scoreAnimationController != null && scoreAnimationController.IsAnimating) || (DonFusionManager2D.Instance != null && DonFusionManager2D.Instance.DrawAnimationTimer.IsRunning);

        private void Awake()
        {
            // Instanceがまだ生成されていない可能性があるため、ここでのイベント購読は行わない
        }

        private void OnDestroy()
        {
            if (useFusion && hasSubscribedToFusion && DonFusionManager2D.Instance != null)
            {
                DonFusionManager2D.Instance.OnHandUpdated -= OnFusionHandUpdated;
            }
        }

        private void Start()
        {
            if (drawButton != null)
            {
                drawButton.onClick.AddListener(() => {
                    if (useFusion) DonFusionManager2D.Instance?.RequestDraw();
                    else DonGameManager.Instance?.PlayerDraw(DonGameManager.Instance.players[0]);
                });
            }

            // 山札と捨て札の位置入れ替え
            if (deckPileContainer != null && discardPileContainer != null)
            {
                Vector3 tempPos = deckPileContainer.localPosition;
                deckPileContainer.localPosition = discardPileContainer.localPosition;
                discardPileContainer.localPosition = tempPos;
            }

            // 動的に山札クリック用のボタンイベントを付与
            if (deckPileContainer != null)
            {
                // 山札が空の時でもクリックできるように、土台となる透明画像（RaycastTarget=true）を作成する
                var bgObj = new GameObject("DeckClickBackground", typeof(RectTransform), typeof(Image), typeof(Button));
                bgObj.transform.SetParent(deckPileContainer, false);
                bgObj.transform.SetAsFirstSibling();
                
                var rt = bgObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                
                var deckImg = bgObj.GetComponent<Image>();
                deckImg.color = new Color(0, 0, 0, 0); // 完全透明
                deckImg.raycastTarget = true;
                
                var deckBtn = bgObj.GetComponent<Button>();
                deckBtn.onClick.AddListener(OnDeckClicked);
            }

            if (sortRankButton != null) sortRankButton.onClick.AddListener(SortByRank);
            if (sortSuitButton != null) sortSuitButton.onClick.AddListener(SortBySuit);

            if (donButton != null)
            {
                donButton.gameObject.SetActive(false); // 固定ボタンを非表示
            }

            if (drawButton != null)
            {
                drawButton.gameObject.SetActive(false); // 固定ボタンを非表示
            }

            if (discardDonButton == null)
            {
                CreateContextDonButton();
            }

            if (discardDonButton != null)
            {
                discardDonButton.onClick.AddListener(() => {
                    if (useFusion && DonFusionManager2D.Instance != null && DonFusionManager2D.Instance.Object != null)
                    {
                        int localId = GetLocalActorId();
                        if (DonFusionManager2D.Instance.IsWaitingForDonGaeshi && DonFusionManager2D.Instance.DonTargetActorId == localId)
                        {
                            DonFusionManager2D.Instance.RPC_DeclareDonGaeshi(DonFusionManager2D.Instance.Runner.LocalPlayer);
                        }
                        else if (DonFusionManager2D.Instance.IsDonWindowOpen)
                        {
                            DonFusionManager2D.Instance.RPC_DeclareDon(DonFusionManager2D.Instance.Runner.LocalPlayer);
                        }
                    }
                });
                discardDonButton.gameObject.SetActive(false);
            }

            if (suitButtons != null && suitButtons.Length == 4)
            {
                // クロージャ(変数の共有)の問題を100%回避するため、メソッドを個別に割り当て
                if (suitButtons[0] != null) { PrepareSuitButton(suitButtons[0], OnSpadesClicked); }
                if (suitButtons[1] != null) { PrepareSuitButton(suitButtons[1], OnHeartsClicked); }
                if (suitButtons[2] != null) { PrepareSuitButton(suitButtons[2], OnDiamondsClicked); }
                if (suitButtons[3] != null) { PrepareSuitButton(suitButtons[3], OnClubsClicked); }
            }

            // --- 最終リザルトUIの自動取得 ---
            if (finalResultPanel == null) {
                // GameObject.Findは非アクティブなオブジェクトを見つけられないため、全表示オブジェクトから検索
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>()) {
                    if (go.name == "FinalResultPanel") {
                        finalResultPanel = go;
                        var t = go.transform.Find("FRP_ListContainer");
                        if (t != null) finalResultContainer = t;
                        var bt = go.transform.Find("FRP_BackButton");
                        if (bt != null) finalResultBackButton = bt.GetComponent<Button>();
                        Debug.Log("[Don] FinalResultPanel linked successfully.");
                        break;
                    }
                }
            }

            // --- レイアウトの自動調整 (扇状配置対応) ---
            if (opponentInfoContainer != null)
            {
                var vlg = opponentInfoContainer.GetComponent<VerticalLayoutGroup>();
                if (vlg != null) vlg.enabled = false;

                var rt = opponentInfoContainer.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                }
            }
        }
        private void PrepareSuitButton(Button btn, UnityEngine.Events.UnityAction action)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);

            // ボタン内のテキストが大きすぎて他のボタンを覆ってしまう問題を解決するため、テキストの判定をオフにする
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.raycastTarget = false;
        }

        public void OnDeckClicked()
        {
            if (IsInteractionBlocked) return;
            if (useFusion) DonFusionManager2D.Instance?.RequestDraw();
            else DonGameManager.Instance?.PlayerDraw(DonGameManager.Instance.players[0]);
        }

        private void OnSpadesClicked() => OnSuitButtonClicked(0);
        private void OnHeartsClicked() => OnSuitButtonClicked(1);
        private void OnDiamondsClicked() => OnSuitButtonClicked(2);
        private void OnClubsClicked() => OnSuitButtonClicked(3);

        public void SortByRank()
        {
            if (DonFusionManager2D.Instance == null) return;
            var hand = new System.Collections.Generic.List<CardInfo>(DonFusionManager2D.Instance.myLocalHand);
            hand = hand.OrderBy(c => c.Rank).ThenBy(c => c.SuitInt).ToList();
            DonFusionManager2D.Instance.SetLocalHand(hand);
        }

        public void SortBySuit()
        {
            if (DonFusionManager2D.Instance == null) return;
            var hand = new System.Collections.Generic.List<CardInfo>(DonFusionManager2D.Instance.myLocalHand);
            hand = hand.OrderBy(c => c.SuitInt).ThenBy(c => c.Rank).ToList();
            DonFusionManager2D.Instance.SetLocalHand(hand);
        }

        public void ShowSuitSelectionUI()
        {
            if (suitSelectionPanel != null)
            {
                var rt = suitSelectionPanel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // 捨て場(0,0)と手札(Y=-240付近)の間に配置を調整
                    rt.anchoredPosition = new Vector2(0, -130f);
                    rt.sizeDelta = new Vector2(500f, 160f); // 高さを200から160に少し縮小
                }
                suitSelectionPanel.SetActive(true);
            }
        }

        private void OnSuitButtonClicked(int suitInt)
        {
            if (useFusion && DonFusionManager2D.Instance != null)
            {
                DonFusionManager2D.Instance.RPC_SubmitSuitChoice(suitInt);
                if (suitSelectionPanel != null) suitSelectionPanel.SetActive(false);
            }
        }

        private void Update()
        {
            // スコア演出中は手札の再生成などを止める
            if (scoreAnimationController != null && scoreAnimationController.IsAnimating)
                return;

            if (useFusion)
            {
                if (DonFusionManager2D.Instance != null && DonFusionManager2D.Instance.Object != null && DonFusionManager2D.Instance.Object.IsValid)
                {
                    if (!hasSubscribedToFusion)
                    {
                        DonFusionManager2D.Instance.OnHandUpdated += OnFusionHandUpdated;
                        hasSubscribedToFusion = true;
                        OnFusionHandUpdated(); // 初回の画面反映
                    }
                    UpdateFusionUIState();
                }
            }
            else
            {
                if (DonGameManager.Instance == null) return;
                UpdateUIState();
            }
        }

        private void OnFusionHandUpdated()
        {
            // スコア演出中は手札の再生成を止める（二重表示防止）
            if (scoreAnimationController != null && scoreAnimationController.IsAnimating)
                return;

            UpdateFusionHandUI();
        }

        private void UpdateFusionUIState()
        {
            var fm = DonFusionManager2D.Instance;
            if (fm == null) return;

            // [FIX] 状態更新に必要な変数を定義
            int localActorId = GetLocalActorId();
            bool isMyTurn = (fm.Runner != null && fm.CurrentTurnPlayerActorId == localActorId);
            bool isAnimating = (scoreAnimationController != null && scoreAnimationController.IsAnimating);

            // [FIX] 初回カード（8）のための親判定: 配布中はターンが -1 なので、誰が親になるかを推定する
            int startingPlayerActorId = (fm.WinnerActorId > 0) ? fm.WinnerActorId : (fm.Actors.Length > 0 ? fm.Actors.Get(0).ActorId : -1);
            bool isMyStartingTurn = (fm.CurrentTurnPlayerActorId == -1 && fm.IsInitialDeal && startingPlayerActorId == localActorId);
            bool isAmIPickingSuit = isMyTurn || isMyStartingTurn;

            // [FIX] 配布アニメーション中でも相手のUI（目的地）や捨て札の状態（8のマーク変更）は更新する必要があるため、早期リターンの前に呼び出す
            UpdateOpponentsUI();
            UpdateStatusText();
            UpdateRoundDisplay();
            UpdateFusionDiscardPileUI(); // [CRITICAL] 常に監視が必要

            // [NEW] ラウンド終了時（演出フェーズ）にロックを事前にかけておく
            if (fm.IsRoundOver && _hasInitialDealCompleted)
            {
                _hasInitialDealCompleted = false;
                _isExpectingInitialDeal = true;
            }

            // [FIX] 実際に捨て札が見える様になってから効果UIを表示するため、コンテナ内のカード存在をチェックする
            bool isDiscardVisible = false;
            if (discardPileContainer != null) {
                foreach (Transform child in discardPileContainer) {
                    if (child.GetComponent<CardUI>() != null) { isDiscardVisible = true; break; }
                }
            }

            // --- 2ドロー累積表示 (+X) の動的生成と更新 ---
            // [FIX] 配布アニメーション中も +2 などの表示が必要なため、ここに移動。ただしカードが見えるまでは待つ。
            if (_drawPenaltyIndicator == null && discardPileContainer != null) {
                var obj = new GameObject("DrawPenaltyIndicator", typeof(RectTransform), typeof(Text), typeof(Shadow));
                obj.transform.SetParent(discardPileContainer, false);
                _drawPenaltyIndicator = obj.GetComponent<Text>();
                try {
                    _drawPenaltyIndicator.font = mainFontBold != null ? mainFontBold : (mainFontRegular != null ? mainFontRegular : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                } catch {
                    Debug.LogWarning("[GameUIController] Failed to load builtin font 'LegacyRuntime.ttf'.");
                }
                _drawPenaltyIndicator.fontSize = 60;
                _drawPenaltyIndicator.color = Color.white;
                _drawPenaltyIndicator.alignment = TextAnchor.MiddleCenter;
                var rt = obj.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0, 150);
                rt.sizeDelta = new Vector2(200, 100);
                rt.localScale = Vector3.one;
                var shadow = obj.GetComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.5f);
                shadow.effectDistance = new Vector2(2, -2);
            }
            if (_drawPenaltyIndicator != null) {
                if (fm.DrawPenaltyCount > 0 && isDiscardVisible) {
                    _drawPenaltyIndicator.text = "+" + fm.DrawPenaltyCount;
                    _drawPenaltyIndicator.gameObject.SetActive(true);
                    _drawPenaltyIndicator.transform.SetAsLastSibling();
                    var rt = _drawPenaltyIndicator.rectTransform;
                    rt.anchoredPosition = new Vector2(0, 150);
                    rt.localScale = Vector3.one;
                } else {
                    _drawPenaltyIndicator.gameObject.SetActive(false);
                }
            }

            // --- スート選択UIの表示トリガー ---
            // [FIX] 配布アニメーション中も「8」がめくれたら即座に選択できるように、ここに移動。ただしカードが見えるまでは待つ。
            if (fm.IsWaitingForSuitSelection && isAmIPickingSuit && isDiscardVisible)
            {
                if (suitSelectionPanel != null && !suitSelectionPanel.activeSelf)
                {
                    ShowSuitSelectionUI();
                }
            }
            else if (suitSelectionPanel != null && suitSelectionPanel.activeSelf)
            {
                suitSelectionPanel.SetActive(false);
            }
            
            if (isInitialDealingAnimation)
            {
                // [FIX] ラウンド開始時の「5枚配布中」のみメインのステータス表示を「Dealing...」で上書きし、ボタンを非表示にする
                statusText.text = "Dealing cards...";
                statusText.color = Color.white;
                // [FIX] 手札を配っている間は Don ボタンも非表示にする
                if (donButton != null) donButton.gameObject.SetActive(false);
                if (discardDonButton != null) discardDonButton.gameObject.SetActive(false);
                return;
            }
            
            // ドロー中などの一般的なアニメーション中（isDealingAnimationRunning=true）でも、
            // ステータス表示やボタン制御は通常のフロー（UpdateStatusText, UpdateRoundDisplay）に任せる。
            // 操作自体は drawButton.interactable 等で別途抑制されるため安全です。

            // [FIX] 欠落していた Don ボタンの初期化リスナー設定を復元
            if (!isDonButtonSetup)
            {
                if (discardDonButton == null)
                {
                    CreateContextDonButton();
                }

                if (discardDonButton != null)
                {
                    discardDonButton.onClick.RemoveAllListeners();
                    discardDonButton.onClick.AddListener(() =>
                    {
                        if (useFusion && DonFusionManager2D.Instance != null && DonFusionManager2D.Instance.Object != null)
                        {
                            int lId = GetLocalActorId();
                            if (DonFusionManager2D.Instance.IsWaitingForDonGaeshi && DonFusionManager2D.Instance.DonTargetActorId == lId)
                            {
                                DonFusionManager2D.Instance.RPC_DeclareDonGaeshi(DonFusionManager2D.Instance.Runner.LocalPlayer);
                            }
                            else if (DonFusionManager2D.Instance.IsDonWindowOpen)
                            {
                                DonFusionManager2D.Instance.RPC_DeclareDon(DonFusionManager2D.Instance.Runner.LocalPlayer);
                            }
                        }
                    });
                    discardDonButton.gameObject.SetActive(false);
                    isDonButtonSetup = true;
                    Debug.Log($"[Don] Donボタンの初期化完了: {discardDonButton.gameObject.name}");
                }
            }

            // InitializeDisplayedCredits と penaltyText の更新は UpdateRoundDisplay に集約済み

            // roundText.text の更新は UpdateRoundDisplay に集約済み

            drawButton.interactable = isMyTurn && !fm.IsRoundOver && !IsInteractionBlocked && !fm.IsInitialDeal && !fm.IsDrawing;

            Button targetDonButton = discardDonButton;
            if (targetDonButton != null)
            {
                bool canDon = false;
                int myTotal = 0;
                int currentTopCardRank = 0;
                if (!fm.IsRoundOver)
                {
                    bool isDonAllowedRound = true;
                    // 他の人がDonしていても自分もDon可能にする。自分が既にDon宣言済みかチェック
                    bool alreadyDonned = false;
                    for (int i = 0; i < fm.DonCallersCount; i++) {
                        if (fm.DonCallerActorIds.Get(i) == localActorId) alreadyDonned = true;
                    }

                    if (isDonAllowedRound && !alreadyDonned)
                    {
                        myTotal = 0;
                        foreach (var c in fm.myLocalHand) myTotal += c.Rank;

                        // UIアニメーションの遅延に影響されないよう、NetworkArrayの最新の捨て札を直接参照
                        if (fm.DiscardCount > 0) {
                            currentTopCardRank = fm.DiscardPile.Get(fm.DiscardCount - 1).Rank;
                        }

                        // 捨て札と手札合計が一致した場合のみ Don 可能 (Match Don)
                        // [FIX] 誰かがカードをプレイした直後であること（初期カードは不可）
                        bool canMatchDon = false;
                        if (currentTopCardRank > 0 && fm.LastPlayedPlayerActorId != -1)
                        {
                            canMatchDon = (myTotal == currentTopCardRank && myTotal <= 13);
                        }

                        if (fm.IsWaitingForDonGaeshi)
                        {
                            // ドン返し：ターゲットになっており、かつ合計値が捨て札（＝相手のあがり値）と一致
                            if (currentTopCardRank > 0)
                            {
                                if (fm.DonTargetActorId == localActorId && myTotal == currentTopCardRank) canDon = true;
                                // ターゲット以外の場合でも、通常ドン（マルチ対応）ができるなら押せるようにする
                                else if (canMatchDon && fm.LastPlayedPlayerActorId != localActorId) canDon = true;
                            }
                        }
                        else
                        {
                            // Match Don: 合計値が一致し、かつ最後に出したのが自分以外の場合のみ
                            if (canMatchDon && fm.LastPlayedPlayerActorId != localActorId) canDon = true;
                        }
                    }
                }

                if (canDon)
                {
                    var rt = targetDonButton.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        if (playerHandContainer != null && rt.parent != playerHandContainer.parent)
                        {
                            rt.SetParent(playerHandContainer.parent, true);
                        }
                        
                        rt.anchorMin = new Vector2(0.5f, 0f);
                        rt.anchorMax = new Vector2(0.5f, 0f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.anchoredPosition = new Vector2(0, 460f);
                        rt.sizeDelta = new Vector2(600, 200);
                        rt.SetAsLastSibling();
                        
                        var txt = targetDonButton.GetComponentInChildren<Text>();
                        if (txt != null)
                        {
                            txt.fontSize = 80;
                            txt.fontStyle = FontStyle.Bold;
                            txt.color = Color.white;
                            txt.text = "DON!";
                        }

                        var img = targetDonButton.GetComponent<Image>();
                        if (img != null)
                        {
                            img.color = new Color(1f, 0.84f, 0f, 1f);
                        }
                    }
                }
                
                // --- 修正：シンプルなロジックに戻し、デバッグログを追加 ---
                bool serverCanDon = fm.IsDonWindowOpen || fm.IsWaitingForDonGaeshi;
                if (serverCanDon && !canDon) {
                    // [DonDebug] サーバーはDon可能期間だと言っているが、クライアント側で条件（合計値の一致など）を満たしていない場合
                    Debug.Log($"[DonDebug] LocalTotal={myTotal}, TargetRank={currentTopCardRank}, IsOver={fm.IsRoundOver}, Discard={fm.DiscardCount}, IsOpen={fm.IsDonWindowOpen}, IsGaeshi={fm.IsWaitingForDonGaeshi}");
                }
                else if (serverCanDon && canDon && !targetDonButton.gameObject.activeSelf) {
                    // [DonDebug] 条件は満たしているが、まだボタンが表示されていない（最初のフレームなど）
                    Debug.Log($"[DonDebug] Showing DON Button! Total={myTotal}, Rank={currentTopCardRank}");
                }

                // 基本的にサーバーの窓口が開いているなら表示するが、自分が最後に出したカードにはドンできない（ドン返し以外）
                bool finalCanShow = canDon;
                int myIdx = fm.GetActorIndex(localActorId);
                if (myIdx != -1 && fm.IsDonRestricted[myIdx]) finalCanShow = false;
                if (fm.IsRoundOver) finalCanShow = false;

                targetDonButton.gameObject.SetActive(finalCanShow);
                // 他プレイヤーのDon演出中でも、自身がDon可能ならボタンを有効にする（アニメーションによるインタラクトブロックを解除）
                bool blockForDon = isDealingAnimationRunning;
                targetDonButton.interactable = canDon && !blockForDon;
            }

            // UpdateFusionDiscardPileUI(); // ここから削除

            if (fm.IsRoundOver)
            {
                // 最終リザルト表示中、または演出中は通常のパネルを表示しない
                // ※isAnimating はメソッド冒頭で定義済み
                if (!isAnimating && (finalResultPanel == null || !finalResultPanel.activeSelf))
                {
                    if (resultPanel != null) {
                        if (!resultPanel.activeSelf) {
                            resultPanel.SetActive(true);
                            resultPanel.transform.SetAsLastSibling(); // 最前面に持ってくる
                        }
                    }
                    string winnerName = (fm.WinnerActorId != -1) ? $"Player {fm.WinnerActorId}" : "Someone";
                    resultText.text = $"{winnerName} Wins!";
                }
                else
                {
                    if (resultPanel != null && resultPanel.activeSelf) resultPanel.SetActive(false);
                }
            }
            else
            {
                resultPanel.SetActive(false);
                if (revealedHandContainer != null)
                {
                    foreach (Transform child in revealedHandContainer)
                        Destroy(child.gameObject);
                }
            }
        }

        private void UpdateStatusText()
        {
            var fm = DonFusionManager2D.Instance;
            if (fm == null || statusText == null) return;

            int localActorId = GetLocalActorId();
            bool isMyTurn = (fm.Runner != null && fm.CurrentTurnPlayerActorId == localActorId);

            if (notificationTimer > 0)
            {
                statusText.text = temporaryNotification;
                statusText.color = Color.yellow;
                notificationTimer -= Time.deltaTime;
            }
            else
            {
                statusText.color = Color.white;
                if (!fm.IsRoundOver)
                {
                    if (isMyTurn) statusText.text = "Your Turn";
                    else
                    {
                        var current = fm.GetActor(fm.CurrentTurnPlayerActorId);
                        string actorName = current.IsActive ? (current.IsCPU ? "CPU " : "Player ") + current.ActorId : "Opponent";
                        statusText.text = $"{actorName}'s Turn";
                    }
                }
            }
        }

        private void UpdateRoundDisplay()
        {
            var fm = DonFusionManager2D.Instance;
            if (fm == null) return;

            int localActorId = GetLocalActorId();

            // クレジット表示の初期化（初回のみ）
            if (!_isCreditInitialized && fm.PlayerCredits.Count > 0)
            {
                InitializeDisplayedCredits();
                _isCreditInitialized = true;
            }

            // 演出中でない場合は、実データと表示バッファを同期させる
            bool isAnimating = (scoreAnimationController != null && scoreAnimationController.IsAnimating);
            bool isRoundOver = fm.IsRoundOver;
            
            if (!isAnimating && !isRoundOver)
            {
                foreach (var kvp in fm.PlayerCredits)
                {
                    _displayedCredits[kvp.Key] = kvp.Value;
                }
            }

            int myCredits = _displayedCredits.ContainsKey(localActorId) ? _displayedCredits[localActorId] : 0;
            string maxRoundStr = fm.MaxRounds.ToString();

            if (penaltyText != null)
            {
                penaltyText.text = $"RD {fm.CurrentRound}/{maxRoundStr}";
                if (fm.DrawPenaltyCount > 0) penaltyText.text += $" | Penalty: +{fm.DrawPenaltyCount}";
            }

            if (creditText != null)
            {
                creditText.text = myCredits.ToString();
            }
            if (creditCardObject != null && !creditCardObject.activeSelf)
            {
                creditCardObject.SetActive(true);
            }
            
            if (roundText != null)
            {
                if (roundText.transform.parent != null && !roundText.transform.parent.gameObject.activeSelf)
                {
                    roundText.transform.parent.gameObject.SetActive(true);
                }
                roundText.text = $"{fm.CurrentRound}/{maxRoundStr}";
            }
        }

        public void UpdateOpponentsUI()
        {
            var fm = DonFusionManager2D.Instance;
            if (fm == null || opponentInfoContainer == null || opponentInfoPrefab == null) return;
            if (fm.Runner == null || !fm.Runner.IsRunning) return;
            // アニメーション実行中は位置調整等を行わせないようにする (チラつき防止)
            if (scoreAnimationController != null && scoreAnimationController.IsAnimating) return;

            int localActorId = -1;
            for (int i = 0; i < fm.Actors.Length; i++)
            {
                var ac = fm.Actors.Get(i);
                if (ac.IsActive && !ac.IsCPU && ac.PlayerRef == fm.Runner.LocalPlayer)
                {
                    localActorId = ac.ActorId;
                    break;
                }
            }

            if (localActorId == -1) return;

            var activeActors = new List<ActorInfo>();
            for (int i = 0; i < fm.Actors.Length; i++)
            {
                var actor = fm.Actors.Get(i);
                if (actor.IsActive) activeActors.Add(actor);
            }
            
            // ソートして位置計算を安定させる
            var sortedActiveActors = activeActors.OrderBy(a => a.ActorId).ToList();

            // 自分以外を抽出
            var opponents = sortedActiveActors.Where(a => a.ActorId != localActorId).ToList();

            // 不要なUIを削除
            var currentOpponentIds = opponents.Select(a => a.ActorId).ToList();
            var idsToRemove = opponentUIs.Keys.Where(id => !currentOpponentIds.Contains(id)).ToList();
            foreach (var id in idsToRemove)
            {
                if (opponentUIs[id] != null) Destroy(opponentUIs[id].gameObject);
                opponentUIs.Remove(id);
            }

            int totalPlayers = sortedActiveActors.Count;
            int myIdx = sortedActiveActors.FindIndex(a => a.ActorId == localActorId);

            foreach (var op in opponents)
            {
                if (!opponentUIs.ContainsKey(op.ActorId))
                {
                    GameObject go = Instantiate(opponentInfoPrefab, opponentInfoContainer);
                    opponentUIs[op.ActorId] = go.GetComponent<OpponentUIInfo>();
                    
                    var rt = go.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0.5f, 0.5f);
                        rt.anchorMax = new Vector2(0.5f, 0.5f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                    }
                }

                Vector2 centerPos = Vector2.zero;
                if (discardPileContainer != null && deckPileContainer != null)
                {
                    Vector3 worldCenter = (discardPileContainer.position + deckPileContainer.position) / 2f;
                    centerPos = opponentInfoContainer.InverseTransformPoint(worldCenter);
                }

                int opIdx = sortedActiveActors.FindIndex(a => a.ActorId == op.ActorId);
                int relativeIndex = (opIdx - myIdx + totalPlayers) % totalPlayers;

                float interval = 360f / totalPlayers;
                float angle = 270f - (interval * relativeIndex);
                
                var containerRT = opponentInfoContainer.GetComponent<RectTransform>();
                float radiusX = containerRT.rect.width * 0.40f;
                float radiusY = containerRT.rect.height * 0.36f;

                float rad = angle * Mathf.Deg2Rad;
                Vector2 pos = centerPos + new Vector2(Mathf.Cos(rad) * radiusX, Mathf.Sin(rad) * radiusY);
                
                opponentUIs[op.ActorId].UpdateLayout(pos, angle + 90f);
                opponentUIs[op.ActorId].transform.localScale = Vector3.one;

                int arrayIndex = -1;
                for (int i = 0; i < fm.Actors.Length; i++)
                {
                    if (fm.Actors.Get(i).ActorId == op.ActorId)
                    {
                        arrayIndex = i;
                        break;
                    }
                }
                
                if (arrayIndex >= 0 && arrayIndex < fm.PlayerHandCounts.Length)
                {
                    int count = fm.PlayerHandCounts.Get(arrayIndex);
                    bool isAnimating = opponentAnimatingFlags.TryGetValue(op.ActorId, out bool animFlag) && animFlag;
                    
                    // [NEW] 配布完了フラグによる厳格なガード
                    if (!_hasInitialDealCompleted)
                    {
                        // まだ配布が始まっていない、または配布アニメーション中の表示
                        int dCount = 0;
                        if (isInitialDealingAnimation && _dealingHandCounts.TryGetValue(op.ActorId, out int animatedCount)) 
                        {
                            dCount = animatedCount;
                        }
                        opponentUIs[op.ActorId].Setup(op.ActorId, dCount, dCount == 1);
                    }
                    else if (!isAnimating)
                    {
                        opponentUIs[op.ActorId].Setup(op.ActorId, count, count == 1);
                    }
                    else
                    {
                        int currentDisplayCount = 0;
                        if (opponentUIs[op.ActorId].countText != null)
                        {
                            string txt = opponentUIs[op.ActorId].countText.text.Replace("x", "");
                            int.TryParse(txt, out currentDisplayCount);
                        }

                        if (count < currentDisplayCount)
                        {
                            opponentUIs[op.ActorId].Setup(op.ActorId, count, count == 1);
                        }
                        pendingOpponentHandCounts[op.ActorId] = count;
                    }
                }

                opponentUIs[op.ActorId].SetTurnActive(fm.CurrentTurnPlayerActorId == op.ActorId);
            }
        }

        public void UpdateFusionHandUI(bool skipAnimation = false)
        {
            var fm = DonFusionManager2D.Instance;
            
            // スコア演出中は完全に停止
            if (scoreAnimationController != null && scoreAnimationController.IsAnimating)
                return;

            // [NEW] 親決め演出中は手札を表示しない
            if (fm.IsDeterminingStarter)
                return;

            // 配布アニメーション中またはドローアニメーション中なら何もしない
            if (!skipAnimation && (isDealingAnimationRunning || pendingDrawAnimations.Count > 0))
                return;

            // [NEW] 配布完了フラグの自動チェック（途中参加対応）
            if (!_hasInitialDealCompleted && !fm.IsInitialDeal && !_isExpectingInitialDeal && fm.myLocalHand.Count > 0)
            {
                Debug.Log("[GameUI] Initial deal already passed or not expected. Unlocking UI.");
                _hasInitialDealCompleted = true;
            }

            // [NEW] 最初の5枚が届いた時に、アニメーションを開始するための特別なルート
            if (!_hasInitialDealCompleted && !skipAnimation && playerHandUI.Count == 0 && fm.myLocalHand.Count >= fm.initialHandCount)
            {
                if (deckPileContainer != null)
                {
                    StartCoroutine(DealCardsAnimationCoroutine(new List<CardInfo>(fm.myLocalHand)));
                    return;
                }
            }

            // [NEW] 論理ロック: 配布アニメーションが完了するまで、UpdateHandWithAnimation を呼ばせない
            if (!skipAnimation && !_hasInitialDealCompleted)
            {
                return;
            }

            UpdateHandWithAnimation(fm.myLocalHand);
        }

        private void UpdateHandWithAnimation(List<CardInfo> myHand)
        {
            if (handVisualParent == null)
            {
                GameObject go = new GameObject("HandVisualParent", typeof(RectTransform));
                go.transform.SetParent(playerHandContainer.parent, false);
                handVisualParent = go.transform;
                RectTransform rt = go.GetComponent<RectTransform>();
                RectTransform source = playerHandContainer.GetComponent<RectTransform>();
                rt.anchorMin = source.anchorMin;
                rt.anchorMax = source.anchorMax;
                rt.pivot = source.pivot;
                rt.anchoredPosition = source.anchoredPosition;
                rt.sizeDelta = source.sizeDelta;
            }

            // コンテナ幅を常にCanvas横幅の90%に強制設定（手札増枚時に縮まないための根本修正）
            {
                Canvas c = null;
                if (playerHandContainer != null) c = playerHandContainer.GetComponentInParent<Canvas>();
                if (c == null) c = FindObjectOfType<Canvas>();
                if (c != null)
                {
                    var canvasRT = c.GetComponent<RectTransform>();
                    if (canvasRT != null && canvasRT.rect.width > 0)
                    {
                        float targetW = canvasRT.rect.width * 0.90f;
                        var handRT = playerHandContainer?.GetComponent<RectTransform>();
                        if (handRT != null) { handRT.sizeDelta = new Vector2(targetW, handRT.sizeDelta.y); }
                        var hvRT = (handVisualParent as RectTransform) ?? handVisualParent?.GetComponent<RectTransform>();
                        if (hvRT != null) { hvRT.sizeDelta = new Vector2(targetW, hvRT.sizeDelta.y); }
                    }
                }
            }

            while (dummySlots.Count < myHand.Count)
            {
                GameObject dummyObj = new GameObject("DummySlot", typeof(RectTransform), typeof(LayoutElement));
                dummyObj.transform.SetParent(playerHandContainer, false);
                LayoutElement le = dummyObj.GetComponent<LayoutElement>();
                le.preferredWidth = 100f;
                le.preferredHeight = 140f;
                dummySlots.Add(dummyObj);
            }
            while (dummySlots.Count > myHand.Count)
            {
                Destroy(dummySlots[dummySlots.Count - 1]);
                dummySlots.RemoveAt(dummySlots.Count - 1);
            }

            RefreshSlotWidths(CalcSlotWidth(myHand.Count));

            playerHandUI.RemoveAll(item => item == null);
            List<CardUI> nextHandUI = new List<CardUI>();
            List<CardUI> pool = new List<CardUI>(playerHandUI);

            for (int i = 0; i < myHand.Count; i++)
            {
                CardInfo targetData = myHand[i];
                CardUI existing = pool.Find(ui => ui != null && 
                                                 ui.CardInfo.SuitInt == targetData.SuitInt && 
                                                 ui.CardInfo.Rank == targetData.Rank);
                
                if (existing != null)
                {
                    nextHandUI.Add(existing);
                    pool.Remove(existing);
                    existing.SetupFusion(targetData, true);
                }
                else
                {
                    GameObject go = Instantiate(cardPrefab, handVisualParent);
                    CardUI ui = go.GetComponent<CardUI>();
                    ui.SetupFusion(targetData, true);
                    nextHandUI.Add(ui);
                    
                    if (deckPileContainer != null)
                    {
                        ui.SetImmediatePosition(deckPileContainer.position);
                    }
                }
            }

            foreach (var remaining in pool)
            {
                if (remaining != null) Destroy(remaining.gameObject);
            }

            playerHandUI = nextHandUI;

            for (int i = 0; i < playerHandUI.Count; i++)
            {
                if (playerHandUI[i] != null)
                {
                    if (!playerHandUI[i].IsDragging && (playerHandUI[i].transform.parent != handVisualParent))
                    {
                        playerHandUI[i].transform.SetParent(handVisualParent, true);
                    }
                    if (playerHandUI[i].transform.parent == handVisualParent)
                    {
                        playerHandUI[i].transform.SetAsLastSibling();
                    }
                }
            }

            if (applyHandPositionsCoroutine != null) StopCoroutine(applyHandPositionsCoroutine);
            applyHandPositionsCoroutine = StartCoroutine(ApplyHandPositionsAfterLayout());
        }

        private System.Collections.IEnumerator ApplyHandPositionsAfterLayout()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            int count = playerHandUI.Count;
            if (count == 0) yield break;

            // Canvas横幅の90%を手札エリアの幅として使用（LayoutGroup / RectTransformサイズに依存しない）
            float totalWidth = 900f;
            Canvas canvas = null;
            if (playerHandContainer != null) canvas = playerHandContainer.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                var cRT = canvas.GetComponent<RectTransform>();
                if (cRT != null && cRT.rect.width > 0)
                {
                    // 両端に100pxずつパディング（計200px）を確保することで、端のカードの操作性向上と画面のはみ出しを防止
                    const float sidePadding = 100f;
                    totalWidth = cRT.rect.width * 0.90f - sidePadding * 2f;
                }
            }

            // 扇状配置のパラメータ（カード増加で角度がつきすぎないよう制限）
            float fanAngleSpan = Mathf.Min(count * 3f, 25f);
            float startAngle = fanAngleSpan / 2f;
            float angleStep = count > 1 ? fanAngleSpan / (count - 1) : 0f;
            float radius = 1500f;

            // カード間隔を計算：基本は固定間隔を維持し、画面を超える場合のみ縮小する
            const float defaultSpacing = 150f; // 標準の1枚あたりの間隔
            float maxSpacing = count > 1 ? totalWidth / (count - 1) : defaultSpacing;
            float spacing = Mathf.Min(defaultSpacing, maxSpacing);
            float actualSpan = spacing * (count - 1); // 実際に使う横幅

            // 基準位置: playerHandContainerのワールド座標を中心に使用
            Vector3 centerWorld = playerHandContainer != null 
                ? playerHandContainer.position 
                : Vector3.zero;
            float scale = playerHandContainer != null 
                ? playerHandContainer.lossyScale.y 
                : 1f;

            for (int i = 0; i < playerHandUI.Count; i++)
            {
                if (playerHandUI[i] == null || playerHandUI[i].IsDragging || playerHandUI[i].isDiscarded) continue;

                float currentAngle = startAngle - (i * angleStep);
                float rad = currentAngle * Mathf.Deg2Rad;
                float yOffsetLocal = Mathf.Cos(rad) * radius - radius;
                float yOffsetWorld = yOffsetLocal * scale;

                // X位置: 中心から均等に振り分け
                float xLocal = -actualSpan / 2f + spacing * i;
                float xWorld = xLocal * scale;

                Vector3 targetPos = new Vector3(
                    centerWorld.x + xWorld,
                    centerWorld.y + yOffsetWorld,
                    centerWorld.z
                );

                playerHandUI[i].SmoothMoveAndRotateTo(
                    targetPos,
                    Quaternion.Euler(0, 0, currentAngle)
                );
            }
        }

        private float CalcSlotWidth(int cardCount)
        {
            float containerWidth = 1920f * 0.9f;
            Canvas canvas = null;
            if (playerHandContainer != null) canvas = playerHandContainer.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();

            if (canvas != null)
            {
                var canvasRT = canvas.GetComponent<RectTransform>();
                if (canvasRT != null && canvasRT.rect.width > 0)
                {
                    // 必ず全幅の90%を使うようにする
                    containerWidth = canvasRT.rect.width * 0.9f;
                }
            }

            const float cardVisualWidth = 100f;
            const float maxSlotWidth = 180f; 
            // カードが増えすぎても選択できるように、最低でも45pxは間隔を確保する
            const float minSlotWidth = 45f;
            if (cardCount <= 1) return maxSlotWidth;
            float neededWidth = (containerWidth - cardVisualWidth) / (cardCount - 1);
            return Mathf.Clamp(neededWidth, minSlotWidth, maxSlotWidth);
        }

        private void RefreshSlotWidths(float slotWidth)
        {
            foreach (var d in dummySlots)
            {
                if (d != null)
                {
                    var le = d.GetComponent<LayoutElement>();
                    if (le != null) { le.preferredWidth = slotWidth; le.minWidth = slotWidth; }
                }
            }
        }


        private System.Collections.IEnumerator DealCardsAnimationCoroutine(List<CardInfo> myHand)
        {
            isDealingAnimationRunning = true;
            isInitialDealingAnimation = true; 
            _isExpectingInitialDeal = false; // [NEW] アニメーションが始まったのでフラグを戻す
            
            // 確実に古いUI（および誤って生成されたUI）をすべて消去する
            ClearHandUI(GetLocalActorId());
            if (handVisualParent != null) { foreach (Transform child in handVisualParent) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
            if (playerHandContainer != null) { foreach (Transform child in playerHandContainer) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }

            var fm = DonFusionManager2D.Instance;
            if (fm == null) yield break;

            // 有効な全プレイヤーを取得（配列インデックス順に配る）
            var activeActors = new List<ActorInfo>();
            for (int i = 0; i < fm.Actors.Length; i++)
            {
                if (fm.Actors[i].IsActive) activeActors.Add(fm.Actors[i]);
            }

            int myActorId = GetLocalActorId();
            int myHandIdx = 0;
            int initialCount = fm.initialHandCount;

            List<CardUI> localAnimatingCards = new List<CardUI>();
            List<GameObject> tempDummySlots = new List<GameObject>();

            // 自分の手札用のダミースロットを先に用意（最終的なレイアウト計算のため）
            for (int i = 0; i < myHand.Count; i++)
            {
                GameObject dummyObj = new GameObject("DummySlot", typeof(RectTransform), typeof(LayoutElement));
                dummyObj.transform.SetParent(playerHandContainer, false);
                LayoutElement le = dummyObj.GetComponent<LayoutElement>();
                le.preferredWidth = 100f;
                le.preferredHeight = 140f;
                tempDummySlots.Add(dummyObj);
            }

            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            Transform canvasTransform = playerHandContainer.GetComponentInParent<Canvas>().transform;
            Vector3 deckPos = deckPileContainer != null ? deckPileContainer.position : Vector3.zero;

            // --- 5巡の配付ループ ---
            _dealingHandCounts.Clear();
            for (int round = 0; round < initialCount; round++)
            {
                foreach (var actor in activeActors)
                {
                    if (!_dealingHandCounts.ContainsKey(actor.ActorId)) _dealingHandCounts[actor.ActorId] = 0;
                    _dealingHandCounts[actor.ActorId]++;
                    
                    bool isMe = (actor.ActorId == myActorId);
                    
                    GameObject go = Instantiate(cardPrefab, canvasTransform);
                    CardUI ui = go.GetComponent<CardUI>();
                    
                    Vector3 targetPos;
                    if (isMe && myHandIdx < myHand.Count)
                    {
                        ui.SetupFusion(myHand[myHandIdx], true); // 自分は表向き
                        targetPos = tempDummySlots[myHandIdx].transform.position;
                        localAnimatingCards.Add(ui);
                        myHandIdx++;
                    }
                    else
                    {
                        ui.SetupFusion(new CardInfo { Rank = 0 }, false); // 相手は裏向き
                        if (opponentUIs.ContainsKey(actor.ActorId))
                            targetPos = opponentUIs[actor.ActorId].transform.position;
                        else
                            targetPos = deckPos; // 万が一見つからない場合はその場
                    }

                    ui.transform.position = deckPos;
                    ui.transform.localScale = Vector3.one;
                    if (ui.button != null) ui.button.interactable = false;

                    // カードを飛ばす（並列で動かしたいが、配付の「テンポ」を出すために少し待つ）
                    StartCoroutine(AnimateSingleCardToTarget(ui, deckPos, targetPos, 0.25f, !isMe));
                    
                    yield return new WaitForSeconds(0.06f); // 次のプレイヤーへ配る間隔
                }
            }

            // 全て配り終わるのを少し待つ
            yield return new WaitForSeconds(0.4f);

            foreach (var d in tempDummySlots) { if (d != null) Destroy(d); }
            foreach (var c in localAnimatingCards) { if (c != null) Destroy(c.gameObject); }

            isDealingAnimationRunning = false;
            isInitialDealingAnimation = false;
            _hasInitialDealCompleted = true; // [NEW] ロック解除
            UpdateFusionHandUI(true);
        }

        private System.Collections.IEnumerator AnimateSingleCardToTarget(CardUI ui, Vector3 start, Vector3 target, float duration, bool destroyAtEnd)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (ui == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easeOutT = 1f - Mathf.Pow(1f - t, 3f);
                ui.transform.position = Vector3.Lerp(start, target, easeOutT);
                yield return null;
            }
            if (ui != null)
            {
                ui.transform.position = target;
                if (destroyAtEnd) Destroy(ui.gameObject);
            }
        }

        private System.Collections.IEnumerator ProcessDrawAnimationQueue()
        {
            isDealingAnimationRunning = true;
            var fm = DonFusionManager2D.Instance;

            while (pendingDrawAnimations.Count > 0)
            {
                CardInfo cardToAnimate = pendingDrawAnimations.Dequeue();
                inFlightAnimationCount++;

                if (deckPileContainer == null) { inFlightAnimationCount--; break; }

                GameObject dummyObj = new GameObject("DummySlot", typeof(RectTransform), typeof(LayoutElement));
                dummyObj.transform.SetParent(playerHandContainer, false);
                LayoutElement le = dummyObj.GetComponent<LayoutElement>();
                le.preferredWidth = 100f;
                le.preferredHeight = 140f;

                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();

                Transform canvasTransform = playerHandContainer.GetComponentInParent<Canvas>().transform;
                GameObject go = Instantiate(cardPrefab, canvasTransform);
                CardUI ui = go.GetComponent<CardUI>();
                animatingDrawCards.Add(ui);
                ui.SetupFusion(cardToAnimate, true);
                Vector3 startPos = deckPileContainer.position;
                ui.transform.position = startPos;
                ui.transform.localScale = Vector3.one;
                if (ui.button != null) ui.button.interactable = false;

                float duration = 0.25f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    // 途中でスコア演出が始まったら中止して破棄
                    if (scoreAnimationController != null && scoreAnimationController.IsAnimating)
                    {
                        if (go != null) Destroy(go);
                        animatingDrawCards.Remove(ui);
                        inFlightAnimationCount--;
                        pendingDrawAnimations.Clear();
                        isDealingAnimationRunning = false;
                        yield break;
                    }
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float easeOutT = 1f - Mathf.Pow(1f - t, 3f);
                    if (dummyObj != null) ui.transform.position = Vector3.Lerp(startPos, dummyObj.transform.position, easeOutT);
                    yield return null;
                }

                if (dummyObj != null) Destroy(dummyObj);
                if (ui != null)
                {
                    playerHandUI.Add(ui);
                    animatingDrawCards.Remove(ui);
                    ui.transform.SetAsLastSibling();
                }
                inFlightAnimationCount--;
                yield return new WaitForSeconds(0.1f);
            }

            _dealingHandCounts.Clear();
            isDealingAnimationRunning = false;
            isInitialDealingAnimation = false; // [NEW] 配布終了
            OnFusionHandUpdated();
        }

        #region Opponent Animations

        public void PlayOpponentCardAnimation(int actorId, CardInfo card)
        {
            var fm = DonFusionManager2D.Instance;
            if (fm == null || fm.Runner == null) return;
            if (fm.GetActorId(fm.Runner.LocalPlayer) == actorId) return;
            opponentUIs.TryGetValue(actorId, out OpponentUIInfo targetOpponent);
            if (targetOpponent != null)
            {
                opponentAnimatingFlags[actorId] = true;
                StartCoroutine(OpponentPlayAnimationCoroutine(actorId, targetOpponent.transform.position, card));
            }
        }

        private System.Collections.IEnumerator OpponentPlayAnimationCoroutine(int actorId, Vector3 startPos, CardInfo card)
        {
            isOpponentCardAnimationRunning = true;
            pendingDiscardCard = default;
            Transform canvasTransform = discardPileContainer.GetComponentInParent<Canvas>().transform;
            GameObject go = Instantiate(cardPrefab, canvasTransform);
            go.transform.SetAsLastSibling();
            CardUI ui = go.GetComponent<CardUI>();
            ui.SetupFusion(card, true);
            ui.transform.position = startPos;
            ui.transform.localScale = Vector3.one;

            float duration = 0.3f;
            float elapsed = 0f;
            Vector3 targetPos = discardPileContainer.position;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easeOutT = 1f - Mathf.Pow(1f - t, 3f);
                ui.transform.position = Vector3.Lerp(startPos, targetPos, easeOutT);
                yield return null;
            }

            // [FIX] チラつき防止：isOpponentCardAnimationRunning を先に折って、OnDiscardPileChanged が即座に反映されるようにする
            isOpponentCardAnimationRunning = false;
            if (pendingDiscardCard.Rank != 0)
            {
                OnDiscardPileChanged(pendingDiscardCard);
                pendingDiscardCard = default;
            }
            else
            {
                OnDiscardPileChanged(card);
            }

            Destroy(go);
            
            opponentAnimatingFlags[actorId] = false;
            if (pendingOpponentHandCounts.TryGetValue(actorId, out int pendingCount) && opponentUIs.TryGetValue(actorId, out OpponentUIInfo opUI))
            {
                opUI.Setup(actorId, pendingCount, pendingCount == 1);
                pendingOpponentHandCounts.Remove(actorId);
            }
        }

        public void PlayOpponentDrawAnimation(int actorId, int count)
        {
            var fm = DonFusionManager2D.Instance;
            if (fm == null || fm.Runner == null) return;
            if (fm.GetActorId(fm.Runner.LocalPlayer) == actorId) return;
            opponentUIs.TryGetValue(actorId, out OpponentUIInfo targetOpponent);
            if (targetOpponent != null)
            {
                opponentAnimatingFlags[actorId] = true;
                StartCoroutine(OpponentDrawAnimationCoroutine(actorId, targetOpponent.transform.position, count));
            }
        }

        private System.Collections.IEnumerator OpponentDrawAnimationCoroutine(int actorId, Vector3 targetPos, int count)
        {
            Transform canvasTransform = discardPileContainer.GetComponentInParent<Canvas>().transform;
            Vector3 startPos = deckPileContainer.position;
            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(cardPrefab, canvasTransform);
                go.transform.SetAsLastSibling();
                CardUI ui = go.GetComponent<CardUI>();
                ui.SetupFusion(new CardInfo(Suit.Hearts, 1), false);
                ui.transform.position = startPos;
                ui.transform.localScale = Vector3.one;
                StartCoroutine(AnimateSingleOpponentDraw(go, startPos, targetPos));
                yield return new WaitForSeconds(0.1f);
            }
            yield return new WaitForSeconds(0.3f);
            opponentAnimatingFlags[actorId] = false;
            if (pendingOpponentHandCounts.TryGetValue(actorId, out int pendingCount) && opponentUIs.TryGetValue(actorId, out OpponentUIInfo opUI))
            {
                opUI.Setup(actorId, pendingCount, pendingCount == 1);
                pendingOpponentHandCounts.Remove(actorId);
            }
        }

        private System.Collections.IEnumerator AnimateSingleOpponentDraw(GameObject cardObj, Vector3 start, Vector3 target)
        {
            float duration = 0.3f;
            float elapsed = 0f;
            while (elapsed < duration && cardObj != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easeOutT = 1f - Mathf.Pow(1f - t, 3f);
                cardObj.transform.position = Vector3.Lerp(start, target, easeOutT);
                yield return null;
            }
            if (cardObj != null) Destroy(cardObj);
        }

        #endregion

        private int lastDiscardPileCount = -1;
        private Suit lastTopCardSuit = (Suit)(-1);
        private int lastTopCardRank = -1;
        private bool isLocalPlayerDiscarding = false;
        public void SetLocalPlayerDiscarding(bool value) => isLocalPlayerDiscarding = value;
        private int lastActiveSuitInt = -1;
        private bool lastIsWaitingForSuitSelection = false;
        private bool isOpponentCardAnimationRunning = false;
        private CardInfo pendingDiscardCard = default;
        private Dictionary<int, bool> opponentAnimatingFlags = new Dictionary<int, bool>();
        private Dictionary<int, int> pendingOpponentHandCounts = new Dictionary<int, int>();

        public void OnDiscardPileChanged(CardInfo topCard)
        {
            if (topCard.Rank == 0) return;
            if (isOpponentCardAnimationRunning || isLocalPlayerDiscarding) { pendingDiscardCard = topCard; return; }
            
            CardUI ui = null;
            if (discardPileContainer != null && discardPileContainer.childCount > 0)
            {
                foreach (Transform child in discardPileContainer) 
                { 
                    if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue; 
                    if (_drawPenaltyIndicator != null && child.gameObject == _drawPenaltyIndicator.gameObject) continue;
                    ui = child.GetComponent<CardUI>(); 
                    if (ui != null) break;
                }
            }

            if (ui == null)
            {
                if (discardPileContainer != null)
                {
                    foreach (Transform child in discardPileContainer) 
                    { 
                        if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue; 
                        if (_drawPenaltyIndicator != null && child.gameObject == _drawPenaltyIndicator.gameObject) continue;
                        Destroy(child.gameObject); 
                    }
                    GameObject go = Instantiate(cardPrefab, discardPileContainer);
                    go.name = "DiscardPileCard"; go.transform.SetAsFirstSibling(); ui = go.GetComponent<CardUI>(); 
                }
            }

            if (ui != null)
            {
                ui.transform.localPosition = Vector3.zero; ui.isDiscarded = true; ui.SetupFusion(topCard, true);
                var fm = DonFusionManager2D.Instance;
                if (fm != null) { 
                    // [FIX] ここで lastActiveSuitInt を更新しないようにする（UpdateFusionDiscardPileUIでの演出を走らせるため）
                    // lastDiscardPileCount = fm.DiscardCount; lastTopCardSuit = topCard.Suit; lastTopCardRank = topCard.Rank; lastActiveSuitInt = fm.ActiveSuitInt; 
                    
                    // カード情報の変化をトリガーにUI状態（Donボタン表示等）を強制更新
                    UpdateFusionUIState();
                }
            }
        }

        private void UpdateFusionDiscardPileUI()
        {
            // [FIX] 相手のプレイ演出中、自分のカード出し中は表示を更新しない（配布演出中 isDealingAnimationRunning は最初の一枚を表示するためガードから外す）
            if (isOpponentCardAnimationRunning || isLocalPlayerDiscarding) return;
            var fm = DonFusionManager2D.Instance;
            if (fm.DiscardCount > 0)
            {
                var topCard = fm.DiscardPile.Get(fm.DiscardCount - 1);
                if (topCard.Rank == 0) { lastDiscardPileCount = -1; return; }
                
                CardUI ui = null;
                // 既存のカードを探す（再利用するため）
                if (discardPileContainer.childCount > 0) 
                { 
                    foreach (Transform child in discardPileContainer) 
                    { 
                        if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue; 
                        if (_drawPenaltyIndicator != null && child.gameObject == _drawPenaltyIndicator.gameObject) continue;
                        ui = child.GetComponent<CardUI>(); 
                        if (ui != null) break;
                    } 
                }

                // なければ生成
                if (ui == null) 
                { 
                    foreach (Transform child in discardPileContainer) 
                    { 
                        if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue; 
                        if (_drawPenaltyIndicator != null && child.gameObject == _drawPenaltyIndicator.gameObject) continue;
                        Destroy(child.gameObject); 
                    } 
                    GameObject go = Instantiate(cardPrefab, discardPileContainer); 
                    go.name = "DiscardPileCard"; 
                    go.transform.SetAsFirstSibling(); 
                    ui = go.GetComponent<CardUI>(); 
                    lastDiscardPileCount = -1; 
                }

                bool cardChanged = (fm.DiscardCount != lastDiscardPileCount || topCard.Suit != lastTopCardSuit || topCard.Rank != lastTopCardRank);
                bool activeSuitChanged = (fm.ActiveSuitInt != lastActiveSuitInt);
                bool selectionFinished = (lastIsWaitingForSuitSelection && !fm.IsWaitingForSuitSelection);

                if (cardChanged || activeSuitChanged || selectionFinished)
                {
                    ui.transform.localPosition = Vector3.zero; ui.isDiscarded = true;
                    if (cardChanged) ui.SetupFusion(topCard, true);
                    
                    // カード自体が変わっていなくても、スートが変わった、またはスート選択が完了した場合はフェードで更新
                    // [FIX] 内部的に変わっているが表示されない問題を防ぐため、ここでの条件判定を確実に
                    if ((activeSuitChanged || selectionFinished) && topCard.Rank == 8 && fm.ActiveSuitInt != -1) 
                    { 
                        Sprite newSprite = ui.database.GetCardSprite((Suit)fm.ActiveSuitInt, 8); 
                        if (newSprite != null) ui.ChangeSpriteWithFade(newSprite, 0.5f); 
                    }
                    
                    lastDiscardPileCount = fm.DiscardCount; 
                    lastTopCardSuit = topCard.Suit; 
                    lastTopCardRank = topCard.Rank; 
                    lastActiveSuitInt = fm.ActiveSuitInt;
                }
                lastIsWaitingForSuitSelection = fm.IsWaitingForSuitSelection;
            }
            else
            {
                // 全削除
                foreach (Transform child in discardPileContainer) 
                { 
                    if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue; 
                    if (_drawPenaltyIndicator != null && child.gameObject == _drawPenaltyIndicator.gameObject) continue;
                    Destroy(child.gameObject); 
                }
                lastDiscardPileCount = -1; lastActiveSuitInt = -1;
                lastIsWaitingForSuitSelection = false;
            }
        }

        private void UpdateUIState()
        {
            DonPlayer localPlayer = DonGameManager.Instance.players[0];
            bool isMyTurn = DonGameManager.Instance.currentPlayerIndex == 0;
            statusText.text = isMyTurn ? "Your Turn" : $"{DonGameManager.Instance.players[DonGameManager.Instance.currentPlayerIndex].name}'s Turn";
            string maxRoundStr = (DonGameManager.Instance.MaxRounds == 0) ? "∞" : DonGameManager.Instance.MaxRounds.ToString();
            penaltyText.text = $"RD 1/{maxRoundStr} | {localPlayer.credits} Credits";
            if (DonGameManager.Instance.drawPenaltyCount > 0) penaltyText.text += $" | Penalty: +{DonGameManager.Instance.drawPenaltyCount}";
            drawButton.interactable = isMyTurn && !DonGameManager.Instance.isRoundOver;
            Card top = DonGameManager.Instance.GetTopDiscard();
            donButton.interactable = !DonGameManager.Instance.isRoundOver && top != null && localPlayer.GetHandTotal() == top.rank && localPlayer.GetHandTotal() <= 13;
            UpdateHandUI(localPlayer); UpdateDiscardPileUI();
            if (DonGameManager.Instance.isRoundOver) { resultPanel.SetActive(true); resultText.text = "Round Finished!\nCheck console for scores."; }
            else resultPanel.SetActive(false);
        }

        private void UpdateHandUI(DonPlayer player)
        {
            if (playerHandUI.Count != player.hand.Count)
            {
                foreach (Transform child in playerHandContainer) Destroy(child.gameObject);
                playerHandUI.Clear();
                foreach (var card in player.hand) { GameObject go = Instantiate(cardPrefab, playerHandContainer); CardUI ui = go.GetComponent<CardUI>(); ui.Setup(card, true); playerHandUI.Add(ui); }
            }
        }

        public void ShowReach(int actorId) { temporaryNotification = $"PLAYER {actorId}: REACH!!!"; notificationTimer = 4.0f; }
        
        public void ShowDrawPenaltyEffect(int actorId, int amount)
        {
            if (scoreAnimationController == null) return;
            Transform target = (actorId == GetLocalActorId()) ? playerHandContainer : GetOpponentCardContainer(actorId);
            if (target != null)
            {
                scoreAnimationController.ShowFloatingText(target, $"+{amount}", Color.red, 60);
            }
        }

        public void ShowRoundResult(string msg, bool isFinal)
        {
            if (isFinal)
            {
                ShowFinalResult();
                return;
            }

            if (resultPanel == null || resultText == null) return;
            
            // レイアウト調整
            var rt = resultPanel.GetComponent<RectTransform>();
            if (rt != null) {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero; rt.localScale = Vector3.one;
            }

            var fontFallback = mainFontRegular ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            resultText.font = fontFallback;
            resultText.fontSize = 50;
            resultText.color = Color.white;
            resultText.alignment = TextAnchor.MiddleCenter;
            resultText.text = msg;

            // レイアウト保証: テキストコンポーネントを全画面（若干のマージン）に広げる
            var textRt = resultText.GetComponent<RectTransform>();
            if (textRt != null)
            {
                textRt.anchorMin = new Vector2(0.05f, 0.05f);
                textRt.anchorMax = new Vector2(0.95f, 0.95f);
                textRt.pivot = new Vector2(0.5f, 0.5f);
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;
            }
            resultText.horizontalOverflow = HorizontalWrapMode.Overflow;
            resultText.verticalOverflow = VerticalWrapMode.Overflow;

            // シャドウの追加（視認性向上）
            if (resultText.GetComponent<Shadow>() == null) resultText.gameObject.AddComponent<Shadow>().effectColor = new Color(0,0,0,0.5f);

            resultPanel.SetActive(true);
            
            var btn = resultPanel.GetComponentInChildren<Button>();
            if (btn != null)
            {
                var btnImg = btn.GetComponent<Image>();
                if (btnImg != null) btnImg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

                var btnText = btn.GetComponentInChildren<Text>();
                if (btnText != null) {
                    btnText.text = "START NEXT ROUND";
                    btnText.font = fontFallback;
                    btnText.fontSize = 40;
                    btnText.color = Color.white;
                }
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    if (useFusion && DonFusionManager2D.Instance != null) {
                        DonFusionManager2D.Instance.RPC_RequestNextRound(); 
                        resultPanel.SetActive(false); 
                        if (revealedHandContainer != null) foreach (Transform child in revealedHandContainer) Destroy(child.gameObject);
                    }
                });
            }
        }

        private void ShowFinalResult()
        {
            Debug.Log("[Don] ShowFinalResult called.");
            if (finalResultPanel == null)
            {
                Debug.LogError("[Don] finalResultPanel is not assigned! Attempting to find again...");
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>()) {
                    if (go.name == "FinalResultPanel") {
                        finalResultPanel = go;
                        finalResultContainer = go.transform.Find("FRP_ListContainer");
                        finalResultBackButton = go.transform.Find("FRP_BackButton")?.GetComponent<Button>();
                        break;
                    }
                }
                
                if (finalResultPanel == null) {
                    Debug.LogError("[Don] Still could not find FinalResultPanel!");
                    return;
                }
            }

            // レイアウト調整
            var panelRT = finalResultPanel.GetComponent<RectTransform>();
            if (panelRT != null) {
                panelRT.anchorMin = Vector2.zero; panelRT.anchorMax = Vector2.one;
                panelRT.sizeDelta = Vector2.zero; panelRT.localScale = Vector3.one;
            }

            // 親パネルのレイアウト制約を解除（子要素を自由に配置するため）
            var pVLG = finalResultPanel.GetComponent<LayoutGroup>();
            if (pVLG != null) Destroy(pVLG);
            var pCSF = finalResultPanel.GetComponent<ContentSizeFitter>();
            if (pCSF != null) Destroy(pCSF);

            // 通常のリザルトパネルを閉じる
            if (resultPanel != null) resultPanel.SetActive(false);
            if (revealedHandContainer != null) foreach (Transform child in revealedHandContainer) Destroy(child.gameObject);

            // リザルトエントリをクリア
            if (finalResultContainer != null)
            {
                foreach (Transform child in finalResultContainer) Destroy(child.gameObject);
                
                // コンテナのレイアウト設定
                var vlg = finalResultContainer.GetComponent<VerticalLayoutGroup>();
                if (vlg == null) vlg = finalResultContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.childControlHeight = false; vlg.childControlWidth = true;
                vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
                vlg.spacing = 20; vlg.padding = new RectOffset(50, 50, 50, 50);
                vlg.childAlignment = TextAnchor.UpperCenter;

                var csf = finalResultContainer.GetComponent<ContentSizeFitter>();
                if (csf == null) csf = finalResultContainer.gameObject.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                
                var containerRT = finalResultContainer.GetComponent<RectTransform>();
                if (containerRT != null) {
                    containerRT.anchorMin = new Vector2(0.05f, 0.2f);
                    containerRT.anchorMax = new Vector2(0.95f, 0.8f);
                    containerRT.pivot = new Vector2(0.5f, 0.5f);
                    containerRT.offsetMin = Vector2.zero;
                    containerRT.offsetMax = Vector2.zero;
                    containerRT.anchoredPosition = Vector2.zero;
                    containerRT.sizeDelta = Vector2.zero;
                }
            }

            finalResultPanel.SetActive(true);
            finalResultPanel.transform.localScale = Vector3.one;

            // 背景画像の設定 (play_background.PNG)
            var bgImage = finalResultPanel.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.sprite = Resources.Load<Sprite>("Sprites/play_background");
                if (bgImage.sprite == null) {
                    var casinoBG = GameObject.Find("CasinoBackground")?.GetComponent<Image>();
                    if (casinoBG != null) bgImage.sprite = casinoBG.sprite;
                }
                if (bgImage.sprite == null) bgImage.color = new Color(0.1f, 0.4f, 0.1f, 1f); // 濃い緑の代替色
                else bgImage.color = Color.white;
            }

            var fontFallback = mainFontRegular;
            if (fontFallback == null) fontFallback = mainFontBold;
            if (fontFallback == null)
            {
                var anyText = GameObject.FindObjectOfType<Text>();
                if (anyText != null) fontFallback = anyText.font;
            }
            if (fontFallback == null) fontFallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (finalResultContainer != null) finalResultContainer.localScale = Vector3.one;
            if (finalResultBackButton != null) {
                finalResultBackButton.transform.localScale = Vector3.one;
                var backRT = finalResultBackButton.GetComponent<RectTransform>();
                if (backRT != null) {
                    backRT.anchorMin = new Vector2(0.2f, 0.05f);
                    backRT.anchorMax = new Vector2(0.8f, 0.15f);
                    backRT.pivot = new Vector2(0.5f, 0.5f);
                    backRT.offsetMin = Vector2.zero;
                    backRT.offsetMax = Vector2.zero;
                    backRT.anchoredPosition = Vector2.zero;
                    backRT.sizeDelta = Vector2.zero;
                }
                var btnImg = finalResultBackButton.GetComponent<Image>();
                if (btnImg != null) btnImg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

                var backText = finalResultBackButton.GetComponentInChildren<Text>();
                if (backText != null) {
                    backText.color = Color.white; backText.fontSize = 45;
                    backText.font = fontFallback;
                    backText.text = "タイトルに戻る";
                    backText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    backText.verticalOverflow = VerticalWrapMode.Overflow;
                    backText.transform.localScale = Vector3.one;
                    if (backText.GetComponent<Shadow>() == null) backText.gameObject.AddComponent<Shadow>();
                }
            }

            // タイトルの設定
            var titleTrans = finalResultPanel.transform.Find("FRP_Title");
            if (titleTrans != null) {
                titleTrans.localScale = Vector3.one;
                var titleRT = titleTrans.GetComponent<RectTransform>();
                if (titleRT != null) {
                    titleRT.anchorMin = new Vector2(0.1f, 0.85f);
                    titleRT.anchorMax = new Vector2(0.9f, 0.95f);
                    titleRT.pivot = new Vector2(0.5f, 0.5f);
                    titleRT.offsetMin = Vector2.zero;
                    titleRT.offsetMax = Vector2.zero;
                    titleRT.anchoredPosition = Vector2.zero;
                    titleRT.sizeDelta = Vector2.zero;
                }
                var titleText = titleTrans.GetComponent<Text>();
                if (titleText != null) {
                    titleText.color = Color.white; titleText.fontSize = 70;
                    titleText.font = fontFallback;
                    titleText.text = "最終結果";
                    titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    titleText.verticalOverflow = VerticalWrapMode.Overflow;
                    if (titleText.GetComponent<Shadow>() == null) titleText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3,-3);
                }
            }

            var fm = DonFusionManager2D.Instance;
            if (fm == null) return;

            // プレイヤーデータの集計
            List<PlayerResultData> results = new List<PlayerResultData>();
            for (int i = 0; i < fm.Actors.Length; i++)
            {
                var actor = fm.Actors.Get(i);
                if (!actor.IsActive) continue;
                if (!fm.PlayerCredits.TryGet(actor.ActorId, out int credits)) credits = 0;

                results.Add(new PlayerResultData
                {
                    ActorId = actor.ActorId,
                    Credits = credits,
                    IsLocal = (actor.ActorId == GetLocalActorId())
                });
            }

            // クレジット順にソート（降順）
            results = results.OrderByDescending(r => r.Credits).ToList();

            // エントリ의生成
            for (int i = 0; i < results.Count; i++)
            {
                if (finalResultContainer != null)
                {
                    GameObject go = null;
                    FinalResultEntry entry = null;

                    if (finalResultEntryPrefab != null)
                    {
                        go = Instantiate(finalResultEntryPrefab, finalResultContainer);
                        entry = go.GetComponent<FinalResultEntry>();
                    }
                    else
                    {
                        // プレハブがない場合は動的に生成
                        go = new GameObject("ResultEntry", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(FinalResultEntry));
                        go.transform.SetParent(finalResultContainer, false);
                        
                        var entryBG = go.GetComponent<Image>();
                        entryBG.color = new Color(0, 0, 0, 0.6f); // 半透明の黒背景

                        var hlg = go.GetComponent<HorizontalLayoutGroup>();
                        hlg.childControlWidth = true; hlg.childForceExpandWidth = true;
                        hlg.childControlHeight = true; hlg.childForceExpandHeight = true;
                        hlg.padding = new RectOffset(40, 40, 15, 15); hlg.spacing = 30;
                        hlg.childAlignment = TextAnchor.MiddleCenter;

                        var le = go.AddComponent<LayoutElement>();
                        le.minHeight = 100f; le.preferredHeight = 120f;

                        // ランク
                        var rankObj = new GameObject("Rank", typeof(RectTransform), typeof(Text), typeof(Shadow));
                        rankObj.transform.SetParent(go.transform, false);
                        var rankTxt = rankObj.GetComponent<Text>();
                        rankTxt.alignment = TextAnchor.MiddleCenter; rankTxt.fontSize = 50; rankTxt.font = fontFallback;
                        rankTxt.color = (i == 0) ? Color.yellow : Color.white;
                        rankTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                        rankTxt.verticalOverflow = VerticalWrapMode.Overflow;
                        rankObj.AddComponent<LayoutElement>().preferredWidth = 80;
                        
                        // 名前
                        var nameObj = new GameObject("Name", typeof(RectTransform), typeof(Text), typeof(Shadow));
                        nameObj.transform.SetParent(go.transform, false);
                        var nameTxt = nameObj.GetComponent<Text>();
                        nameTxt.alignment = TextAnchor.MiddleLeft; nameTxt.fontSize = 50; nameTxt.font = fontFallback;
                        nameTxt.color = Color.white;
                        nameTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                        nameTxt.verticalOverflow = VerticalWrapMode.Overflow;
                        nameObj.AddComponent<LayoutElement>().flexibleWidth = 1;
                        
                        // スコア
                        var scoreObj = new GameObject("Score", typeof(RectTransform), typeof(Text), typeof(Shadow));
                        scoreObj.transform.SetParent(go.transform, false);
                        var scoreTxt = scoreObj.GetComponent<Text>();
                        scoreTxt.alignment = TextAnchor.MiddleRight; scoreTxt.fontSize = 50; scoreTxt.font = fontFallback;
                        scoreTxt.color = Color.white;
                        scoreTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                        scoreTxt.verticalOverflow = VerticalWrapMode.Overflow;
                        scoreObj.AddComponent<LayoutElement>().preferredWidth = 250;

                        entry = go.GetComponent<FinalResultEntry>();
                        entry.rankText = rankTxt;
                        entry.nameText = nameTxt;
                        entry.scoreText = scoreTxt;
                    }

                    if (go != null)
                    {
                        go.transform.localScale = Vector3.one;
                        if (entry == null) entry = go.GetComponent<FinalResultEntry>();
                        if (entry != null)
                        {
                            entry.Setup(i + 1, (results[i].IsLocal ? "YOU" : $"Player {results[i].ActorId}"), results[i].Credits, results[i].IsLocal);
                            foreach (var txt in go.GetComponentsInChildren<Text>(true)) {
                                txt.transform.localScale = Vector3.one;
                                txt.font = fontFallback;
                                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                                txt.verticalOverflow = VerticalWrapMode.Overflow;
                                if (txt.GetComponent<Shadow>() == null) txt.gameObject.AddComponent<Shadow>();
                            }
                        }
                    }
                }
            }

            // 戻るボタンの設定
            if (finalResultBackButton != null)
            {
                finalResultBackButton.onClick.RemoveAllListeners();
                finalResultBackButton.onClick.AddListener(() => {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                });
            }
        }

        private struct PlayerResultData
        {
            public int ActorId;
            public int Credits;
            public bool IsLocal;
        }

        public void PlayRoundEndAnimation(int winType, int winnerId, int loserId, int donValue, string loserHandStr, int totalPenalty, string resultMsg, bool isFinal, string winnerNames = "", string winnerHandStr = "")
        {
            if (scoreAnimationController != null) scoreAnimationController.PlayRoundEndAnimation(winType, winnerId, loserId, donValue, loserHandStr, totalPenalty, resultMsg, isFinal, winnerNames, winnerHandStr);
            else { var animCtrl = GetComponent<ScoreAnimationController>(); if (animCtrl == null) animCtrl = gameObject.AddComponent<ScoreAnimationController>(); animCtrl.uiController = this; animCtrl.PlayRoundEndAnimation(winType, winnerId, loserId, donValue, loserHandStr, totalPenalty, resultMsg, isFinal, winnerNames, winnerHandStr); }
        }

        public void ClearHandUI(int actorId)
        {
            if (actorId == GetLocalActorId())
            {
                foreach (var cUI in playerHandUI) 
                {
                    if (cUI != null) 
                    {
                        cUI.gameObject.SetActive(false); // 即座に非表示にする（Destroyラグ対策）
                        Destroy(cUI.gameObject);
                    }
                }
                playerHandUI.Clear();
                foreach (var d in dummySlots) 
                {
                    if (d != null) 
                    {
                        d.SetActive(false);
                        Destroy(d);
                    }
                }
                dummySlots.Clear();
                
                // アニメーション中および待機中のカードも全て非表示にして破棄
                foreach (var c in animatingDrawCards) 
                {
                    if (c != null) 
                    {
                        c.gameObject.SetActive(false);
                        Destroy(c.gameObject);
                    }
                }
                animatingDrawCards.Clear();
                pendingDrawAnimations.Clear();
                inFlightAnimationCount = 0;
                isDealingAnimationRunning = false;
            }
            else if (opponentUIs.TryGetValue(actorId, out var info) && info.cardIconContainer != null)
            {
                foreach (Transform child in info.cardIconContainer) Destroy(child.gameObject);
            }
        }

        /// <summary>
        /// 新しいラウンド開始時に、画面上のすべてのカードオブジェクトと状態をリセットします。
        /// </summary>
        public void ResetUIForNewRound()
        {
            Debug.Log("[GameUI] Resetting UI for new round...");
            
            _hasInitialDealCompleted = false; // [NEW] ラウンドの節目でロックをかける
            _isExpectingInitialDeal = true;
            
            // 自分の手札をクリア
            ClearHandUI(GetLocalActorId());
            
            // 捨て札コンテナをクリア（Donボタンやドローペナルティ表示は破棄しない）
            if (discardPileContainer != null)
            {
                foreach (Transform child in discardPileContainer)
                {
                    if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue;
                    if (_drawPenaltyIndicator != null && child.gameObject == _drawPenaltyIndicator.gameObject) continue;
                    Destroy(child.gameObject);
                }
            }
            
            // 公開手札（敗者の手札表示用）をクリア
            if (revealedHandContainer != null)
            {
                foreach (Transform child in revealedHandContainer) Destroy(child.gameObject);
            }
            
            // 全対戦相手のカード表示をリセット
            foreach (var kvp in opponentUIs)
            {
                if (kvp.Value.cardIconContainer != null)
                {
                    foreach (Transform child in kvp.Value.cardIconContainer) Destroy(child.gameObject);
                }
            }
            
            // 各種状態フラグをリセット
            isDealingAnimationRunning = false;
            _isExpectingInitialDeal = true; // [NEW] これから初期配布が来ることを示す
            IsScatterAnimationRunning = false;
            pendingDrawAnimations.Clear();
            animatingDrawCards.Clear();
            inFlightAnimationCount = 0;

            // パネル類の非表示
            if (resultPanel != null) resultPanel.SetActive(false);
            if (suitSelectionPanel != null) suitSelectionPanel.SetActive(false);
            
            // スコア演出停止
            if (scoreAnimationController != null) scoreAnimationController.StopAllCoroutines();
        }

        public List<CardUI> GetAnimatingDrawCards() => animatingDrawCards;

        public void ShowDonAnimation(int actorId, string handData) 
        {
            Debug.Log($"[UI] ShowDonAnimation actorId:{actorId} data:{handData}");
            StartCoroutine(Co_DonScatterAnimation(actorId, handData));
        }

        public GameObject CreateCardUI(CardInfo info)
        {
            if (cardPrefab == null) return null;
            GameObject go = Instantiate(cardPrefab);
            go.SetActive(true); // Ensure it's active
            var cui = go.GetComponent<CardUI>();
            if (cui != null)
            {
                cui.SetupFusion(info, true);
            }
            return go;
        }

        private System.Collections.IEnumerator Co_DonScatterAnimation(int actorId, string handData)
        {
            IsScatterAnimationRunning = true;
            
            // 既存の散らばったカードがあれば破棄（念のため）
            foreach (var c in _scatteredCards) if (c != null) Destroy(c);
            _scatteredCards.Clear();

            // 手札データのパース (suit,rank;suit,rank...)
            List<CardInfo> cards = new List<CardInfo>();
            if (!string.IsNullOrEmpty(handData)) {
                var sCards = handData.Split(';');
                foreach (var s in sCards) {
                    if (string.IsNullOrEmpty(s)) continue;
                    var parts = s.Split(',');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int suit) && int.TryParse(parts[1], out int rank))
                        cards.Add(new CardInfo((Suit)suit, rank));
                }
            }

            int localId = GetLocalActorId();
            bool isLocal = (actorId == localId);

            // 出現開始位置（DonしたプレイヤーのUI位置）
            Vector3 startPos = Vector3.zero;
            if (isLocal) startPos = playerHandContainer.position;
            else if (opponentUIs.TryGetValue(actorId, out var info) && info.cardIconContainer != null) 
            {
                startPos = info.cardIconContainer.position;
                // 相手の手札アイコン（枚数表示等）を非表示にする
                info.Setup(actorId, 0, false); 
            }

            // [FIX] Donした瞬間に中央に手札を散布する演出を無効化（ScoreAnimationController の localized reveal と重複するため）
            /* 以前の演出ロジックをスキップ */
            yield return new WaitForSeconds(0.1f);
            IsScatterAnimationRunning = false;
        }

        private void UpdateDiscardPileUI()
        {
            // スコア演出中は手札の再生成などを止める (演出用のカードが消えたり復活したりするのを防ぐ)
            if (scoreAnimationController != null && scoreAnimationController.IsAnimating)
                return;

            if (DonGameManager.Instance == null) return;
            Card topCard = DonGameManager.Instance.GetTopDiscard();
            if (topCard != null)
            {
                CardUI ui = (discardPileContainer.childCount > 0) ? discardPileContainer.GetChild(0).GetComponent<CardUI>() : null;
                if (ui == null || ui.gameObject.name != "DiscardPileCard") { foreach (Transform child in discardPileContainer) Destroy(child.gameObject); GameObject go = Instantiate(cardPrefab, discardPileContainer); go.name = "DiscardPileCard"; ui = go.GetComponent<CardUI>(); }
                ui.transform.localPosition = Vector3.zero; ui.isDiscarded = true; ui.Setup(topCard, true);
            }
        }

        private void CreateContextDonButton()
        {
            if (donButton != null) { discardDonButton = donButton; var rt = discardDonButton.GetComponent<RectTransform>(); if (rt != null && discardPileContainer != null) { discardDonButton.transform.SetParent(discardPileContainer, false); rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = new Vector2(0, 80f); rt.sizeDelta = new Vector2(160, 60); } return; }
            if (discardPileContainer == null) return;
            var btnTrans = discardPileContainer.Find("ContextDonButton");
            GameObject btnObj = (btnTrans != null) ? btnTrans.gameObject : null;
            if (btnObj == null) { btnObj = new GameObject("ContextDonButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)); btnObj.transform.SetParent(discardPileContainer, false); var brt = btnObj.GetComponent<RectTransform>(); brt.anchorMin = new Vector2(0.5f, 1f); brt.anchorMax = new Vector2(0.5f, 1f); brt.pivot = new Vector2(0.5f, 0.5f); brt.anchoredPosition = new Vector2(0, 80f); brt.sizeDelta = new Vector2(160, 60); var img = btnObj.GetComponent<Image>(); img.color = new Color(0.8f, 0.2f, 0.2f, 1f); GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); textObj.transform.SetParent(btnObj.transform, false); var txt = textObj.GetComponent<Text>(); txt.text = "Don!"; txt.alignment = TextAnchor.MiddleCenter; txt.fontSize = 28; txt.color = Color.white; txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); var textRt = textObj.GetComponent<Text>().GetComponent<RectTransform>(); textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one; textRt.offsetMin = Vector2.zero; textRt.offsetMax = Vector2.zero; }
            discardDonButton = btnObj.GetComponent<Button>();
        }

        public List<GameObject> GetScatteredCards() => _scatteredCards;
        public void ClearScatteredCards() 
        {
            foreach (var go in _scatteredCards) if (go != null) Destroy(go);
            _scatteredCards.Clear();
        }

        public bool IsScatterAnimationRunning { get; set; } = false;
        public System.Collections.IEnumerator Co_RecallScatteredCards(Vector3 targetPos) 
        { 
            // Scatter演出の完了を待機
            while (IsScatterAnimationRunning) yield return null; 

            // 散らばったカードを回収地点へ
            foreach (var go in _scatteredCards)
            {
                if (go == null) continue;
                var cui = go.GetComponent<CardUI>();
                if (cui != null) cui.SmoothMoveAndRotateTo(targetPos, Quaternion.identity);
                yield return new WaitForSeconds(0.05f);
            }

            yield return new WaitForSeconds(0.4f);

            foreach (var go in _scatteredCards) if (go != null) Destroy(go);
            _scatteredCards.Clear();
        }

        public int GetLocalActorId()
        {
            var fm = DonFusionManager2D.Instance;
            if (fm == null || fm.Runner == null) return -1;
            
            // Actors[] 配列から自分の PlayerRef に一致する ActorId を探す
            for (int i = 0; i < fm.Actors.Length; i++)
            {
                var actor = fm.Actors.Get(i);
                if (actor.IsActive && !actor.IsCPU && actor.PlayerRef == fm.Runner.LocalPlayer)
                {
                    return actor.ActorId;
                }
            }
            return -1;
        }
        public List<CardUI> GetPlayerHandUI() => playerHandUI;
        public void RemoveFromPlayerHandUI(CardUI card) => playerHandUI.Remove(card);
        public Transform GetOpponentCardContainer(int actorId) => opponentUIs.TryGetValue(actorId, out var info) ? info.cardIconContainer : null;
        public void PlayLocalDiscardAnimation(CardUI card, CardInfo cardInfo)
        {
            if (card == null || discardPileContainer == null) return;

            isLocalPlayerDiscarding = true;

            // 即座に手札リストから除外してレイアウト計算に影響させないようにする
            playerHandUI.Remove(card);

            // アニメーション用に親を一時的に Canvas 直下に移動（最前面）
            Transform canvasTransform = discardPileContainer.GetComponentInParent<Canvas>().transform;
            card.transform.SetParent(canvasTransform, true);
            card.transform.SetAsLastSibling();

            StartCoroutine(Co_FinalizeLocalDiscard(card, cardInfo));
        }

        private System.Collections.IEnumerator Co_FinalizeLocalDiscard(CardUI card, CardInfo cardInfo)
        {
            // 捨て札置き場の中央へ移動
            Vector3 targetPos = discardPileContainer.position;
            card.SmoothMoveAndRotateTo(targetPos, Quaternion.identity);

            // 移動完了（SmoothMoveAndRotateTo の duration 0.25f）を待つ
            yield return new WaitForSeconds(0.3f);

            if (card != null && discardPileContainer != null)
            {
                // 捨て札置き場の階層へ移動
                card.transform.SetParent(discardPileContainer, true);
                card.transform.SetAsLastSibling();
                card.transform.localPosition = Vector3.zero;
                card.isDiscarded = true;
                
                // 捨て札置き場の描画情報を更新（重複インスタンスを整理）
                UpdateFusionDiscardPileUI();
                
                // アニメーション用の一時オブジェクトを安全に処理
                // (UpdateFusionDiscardPileUI が新しいカードを生成するため、アニメーション用は破棄)
                Destroy(card.gameObject);
            }

            isLocalPlayerDiscarding = false;

            // 保持されていた更新があれば実行
            if (pendingDiscardCard.Rank != 0)
            {
                OnDiscardPileChanged(pendingDiscardCard);
                pendingDiscardCard = default;
            }
            else
            {
                UpdateFusionDiscardPileUI();
            }
        }
        public void SetGameMainUIActive(bool active)
        {
            if (deckPileContainer != null) deckPileContainer.gameObject.SetActive(active);
            if (discardPileContainer != null) discardPileContainer.gameObject.SetActive(active);
            if (playerHandContainer != null) playerHandContainer.gameObject.SetActive(active);
            if (opponentInfoContainer != null) opponentInfoContainer.gameObject.SetActive(active);

            // ボタン類の制御
            if (drawButton != null) drawButton.gameObject.SetActive(active);
            if (donButton != null) donButton.gameObject.SetActive(active);
            if (sortRankButton != null) sortRankButton.gameObject.SetActive(active);
            if (sortSuitButton != null) sortSuitButton.gameObject.SetActive(active);
            if (discardDonButton != null) discardDonButton.gameObject.SetActive(active);
        }

        /// <summary>
        /// ローカルプレイヤーの周辺UI（ステータス、点数等）を表示・非表示にする
        /// </summary>
        public void SetLocalPlayerPeripheralActive(bool active)
        {
            if (statusText != null) statusText.gameObject.SetActive(active);
            if (penaltyText != null) penaltyText.gameObject.SetActive(active);
            if (roundText != null && roundText.transform.parent != null) 
                roundText.transform.parent.gameObject.SetActive(active);
        }

        /// <summary>
        /// 特定プレイヤーの周辺UI（名前、枚数等）を表示・非表示にする
        /// </summary>
        public void SetPlayerPeripheralActive(int actorId, bool active)
        {
            if (actorId == GetLocalActorId())
            {
                SetLocalPlayerPeripheralActive(active);
            }
            else if (opponentUIs.TryGetValue(actorId, out var info))
            {
                info.SetPeripheralActive(active);
            }
        }

        /// <summary>
        /// 全てのプレイヤー（自分含む）の周辺UIを再表示する
        /// </summary>
        public void SetAllPlayersPeripheralActive(bool active)
        {
            SetLocalPlayerPeripheralActive(active);
            foreach (var kvp in opponentUIs)
            {
                kvp.Value.SetPeripheralActive(active);
            }
        }

        /// <summary>
        /// 現在のネットワーク上のクレジットを表示バッファにコピーします。
        /// 演出開始直前に呼ぶことで、値を固定します。
        /// </summary>
        public void InitializeDisplayedCredits()
        {
            var fm = DonGame2D.Logic.DonFusionManager2D.Instance;
            if (fm == null) return;

            foreach (var kvp in fm.PlayerCredits)
            {
                _displayedCredits[kvp.Key] = kvp.Value;
            }
            Debug.Log("[GameUI] Displayed credits initialized/captured.");
        }

        /// <summary>
        /// 表示クレジットを指定した値に強制設定します。演出開始時の「計算前」の状態を作るのに使用します。
        /// </summary>
        public void SetDisplayedCredit(int actorId, int value)
        {
            _displayedCredits[actorId] = value;
            
            // 自分自身の場合は即座にテキスト表示のみ反映
            if (actorId == GetLocalActorId() && creditText != null)
            {
                creditText.text = value.ToString();
            }
        }

        /// <summary>
        /// 指定したプレイヤーの表示クレジットのみを更新します。
        /// 吸い込み演出完了時などに呼び出し、演出によってもたらされた「最終結果」を反映させます。
        /// </summary>
        public void UpdateDisplayedCredit(int actorId, int newValue)
        {
            _displayedCredits[actorId] = newValue;
            
            // 自分の場合は即座に右上のテキストにも反映
            if (actorId == GetLocalActorId() && creditText != null)
            {
                creditText.text = newValue.ToString();
            }
            
            Debug.Log($"[GameUI] Displayed credit updated for Player {actorId}: {newValue}");
        }

        public int GetDisplayedCredit(int actorId)
        {
            if (_displayedCredits.ContainsKey(actorId)) return _displayedCredits[actorId];
            
            var fm = DonGame2D.Logic.DonFusionManager2D.Instance;
            if (fm != null && fm.PlayerCredits.ContainsKey(actorId))
            {
                return fm.PlayerCredits[actorId];
            }
            return 0;
        }
        private Dictionary<int, CardUI> _starterCards = new Dictionary<int, CardUI>();

        public void OnStarterDrawStarted(int[] actors, CardInfo[] cards)
        {
            StartCoroutine(Co_AnimateStarterPhase(actors, cards));
        }

        private System.Collections.IEnumerator Co_AnimateStarterPhase(int[] actors, CardInfo[] cards)
        {
            // 古いカードがあればクリア
            foreach (var sc in _starterCards.Values) if (sc != null) Destroy(sc.gameObject);
            _starterCards.Clear();

            // 手札コンテナを確実にクリア（1ラウンド目開始時）
            ClearHandUI(GetLocalActorId());
            if (handVisualParent != null) { foreach (Transform child in handVisualParent) Destroy(child.gameObject); }

            // 親Canvasのトランスフォームを取得（フォールバック付き）
            Transform parentTransform = null;
            if (animationOverlay != null) parentTransform = animationOverlay.transform;
            else if (playerHandContainer != null) parentTransform = playerHandContainer.parent;
            else {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null) parentTransform = canvas.transform;
            }

            // [NEW] 演出用オーバーレイをアクティブにする
            if (animationOverlay != null) {
                animationOverlay.SetActive(true);
                animationOverlay.transform.SetAsLastSibling();
            }

            if (parentTransform == null || cardPrefab == null) {
                Debug.LogError("[GameUI] Failed to start starter animation: parentTransform or cardPrefab is null.");
                yield break;
            }

            Vector3 deckPos = deckPileContainer != null ? deckPileContainer.position : Vector3.zero;

            // 1. 全員に1枚ずつ配る
            for (int i = 0; i < actors.Length; i++)
            {
                int actorId = actors[i];
                GameObject go = Instantiate(cardPrefab, parentTransform);
                go.transform.SetAsLastSibling(); // 常に最前面に
                CardUI ui = go.GetComponent<CardUI>();
                ui.SetupFusion(cards[i], false); // 裏向きで配る
                ui.SetImmediatePosition(deckPos);
                _starterCards[actorId] = ui;

                // [FIX] 均等な楕円配置に変更
                Vector3 targetPos = GetStarterTargetPosition(i, actors.Length);

                // 山札から配るアニメーション
                float dur = 0.4f;
                float el = 0;
                Vector3 start = deckPos;
                while (el < dur)
                {
                    el += Time.deltaTime;
                    ui.transform.position = Vector3.Lerp(start, targetPos, el / dur);
                    yield return null;
                }
                ui.transform.position = targetPos;
                yield return new WaitForSeconds(0.08f);
            }

            yield return new WaitForSeconds(0.4f);

            // 2. 一斉公開
            foreach (var ui in _starterCards.Values)
            {
                ui.SetFacing(true);
            }
        }

        public void OnStarterResult(int[] winners, int[] losers)
        {
            StartCoroutine(Co_AnimateStarterResult(winners, losers));
        }

        private System.Collections.IEnumerator Co_AnimateStarterResult(int[] winners, int[] losers)
        {
            Vector3 deckPos = deckPileContainer != null ? deckPileContainer.position : Vector3.zero;

            // 1. 敗者のカードを山札に戻す
            foreach (int loserId in losers)
            {
                if (_starterCards.TryGetValue(loserId, out var ui))
                {
                    StartCoroutine(Co_SuckToDeck(ui, deckPos));
                }
            }

            // 敗者回収のアニメーション時間を待つ
            yield return new WaitForSeconds(1.0f);

            // 2. 1秒待機（勝者を強調）
            yield return new WaitForSeconds(1.0f);

            // 3. 勝者のカードも山札に戻す
            foreach (int winnerId in winners)
            {
                if (_starterCards.TryGetValue(winnerId, out var ui))
                {
                    StartCoroutine(Co_SuckToDeck(ui, deckPos));
                }
            }
            
            yield return new WaitForSeconds(1.0f);
            
            // 全てクリア
            foreach (var sc in _starterCards.Values) if (sc != null) Destroy(sc.gameObject);
            _starterCards.Clear();

            // [NEW] 演出終了後、オーバーレイを非アクティブに戻す
            if (animationOverlay != null) animationOverlay.SetActive(false);
        }

        private System.Collections.IEnumerator Co_SuckToDeck(CardUI ui, Vector3 deckPos)
        {
            if (ui == null) yield break;
            ui.SetFacing(false); // 裏向きに戻す
            
            float dur = 0.5f;
            float el = 0;
            Vector3 startPos = ui.transform.position;
            Vector3 startScale = ui.transform.localScale;
            
            while (el < dur)
            {
                if (ui == null) yield break;
                el += Time.deltaTime;
                float t = el / dur;
                float easeIn = t * t;
                ui.transform.position = Vector3.Lerp(startPos, deckPos, easeIn);
                ui.transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.1f, t);
                yield return null;
            }
            if (ui != null) Destroy(ui.gameObject);
        }

        private Vector3 GetStarterTargetPosition(int index, int total)
        {
            if (animationOverlay == null) return Vector3.zero;

            // 画面中央（World座標）を取得
            Vector3 center = animationOverlay.transform.position;
            
            // キャンバスがオーバーレイであると仮定し、Rect内部でのピクセル単位の半径を計算
            // アニメーションオーバーレイを基準とした楕円配置
            float rx = Screen.width * 0.35f;
            float ry = Screen.height * 0.25f;

            // 角度の計算 (自分の位置 -90度(真下) を基準にする)
            // 昇順にソートされた actorIds が渡されるため、indexが増えるほど角度を減らすことで時計回りになる
            float angleStep = 360f / total;
            float angleDeg = -90f - (index * angleStep);
            float angleRad = angleDeg * Mathf.Deg2Rad;

            float x = center.x + Mathf.Cos(angleRad) * rx;
            float y = center.y + Mathf.Sin(angleRad) * ry;

            return new Vector3(x, y, center.z);
        }
    }
}
