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
        private Dictionary<int, OpponentUIInfo> opponentUIs = new Dictionary<int, OpponentUIInfo>();

        [Header("System")]
        public bool useFusion = true; // Fusion2を利用するかどうか

        private List<CardUI> playerHandUI = new List<CardUI>();
        private List<GameObject> dummySlots = new List<GameObject>();
        public Transform handVisualParent; // ビジュアル用の実体カードの親（自由配置用）
        private Coroutine applyHandPositionsCoroutine;
        
        private string temporaryNotification = "";
        private float notificationTimer = 0f;

        private bool hasSubscribedToFusion = false;
        private bool isDonButtonSetup = false; // Donボタンの初期化完了フラグ
        
private bool isDealingAnimationRunning = false; // 配布アニメーション中フラグ
        private Queue<CardInfo> pendingDrawAnimations = new Queue<CardInfo>(); // アニメーション待ちキュー
        private int inFlightAnimationCount = 0; // Dequeue済みでアニメーション中のカード枚数

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
                        int localId = DonFusionManager2D.Instance.Runner.LocalPlayer.PlayerId;
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
            if (suitSelectionPanel != null) suitSelectionPanel.SetActive(true);
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
            // Fusion側の手札が更新された際に呼ばれる
            UpdateFusionHandUI();
        }

private void UpdateFusionUIState()
        {
            var fm = DonFusionManager2D.Instance;

            // ===== Donボタンの遅延初期化 =====
            // Start()では GameCanvas が非アクティブのため失敗するが、
            // ここはゲーム実行中に必ず呼ばれるので安全に作成できる
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
                            if (DonFusionManager2D.Instance.IsWaitingForDonGaeshi)
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

            bool isMyTurn = fm.Runner != null && fm.CurrentTurnPlayerActorId == fm.Runner.LocalPlayer.PlayerId;
            int localPlayerId = fm.Runner.LocalPlayer.PlayerId;

            // ローカルプレイヤーの ActorId（1～4のゲーム内 ID）を取得
            int localActorId = -1;
            for (int i = 0; i < 4; i++)
            {
                var ac = fm.Actors.Get(i);
                if (ac.IsActive && !ac.IsCPU && ac.PlayerRef == fm.Runner.LocalPlayer)
                {
                    localActorId = ac.ActorId;
                    break;
                }
            }

            if (notificationTimer > 0)
            {
                statusText.text = temporaryNotification;
                statusText.color = Color.yellow;
                notificationTimer -= Time.deltaTime;
            }
            else
            {
                statusText.color = Color.white;
                if (fm.IsWaitingForDonGaeshi)
                {
                    if (fm.DonTargetActorId == localActorId)
                    {
                        statusText.text = "Don-Gaeshi Chance!";
                    }
                    else
                    {
                        statusText.text = "Waiting for Don-Gaeshi...";
                    }
                }
                else if (fm.RoundEndTimer.IsRunning)
                {
                    statusText.text = "Starting Next Round... (" + Mathf.CeilToInt(fm.RoundEndTimer.RemainingTime(fm.Runner) ?? 0f) + "s)";
                }
                else
                {
                    statusText.text = isMyTurn ? "Your Turn" : "Opponent's Turn";
                }
            }

            if (fm.PlayerCredits.TryGet(localPlayerId, out var credits)) { } else credits = 0;
            string scoreInfo = $"RD {fm.CurrentRound}/5 | {credits} Credits";
            if (fm.DrawPenaltyCount > 0) scoreInfo += $" | Penalty: +{fm.DrawPenaltyCount}";
            penaltyText.text = scoreInfo;

            if (fm.IsRoundOver && resultPanel != null && resultPanel.activeSelf)
            {
                var btn = resultPanel.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    var btnText = btn.GetComponentInChildren<Text>();
                    if (btnText != null && fm.RoundEndTimer.IsRunning)
                    {
                        int remain = Mathf.CeilToInt(fm.RoundEndTimer.RemainingTime(fm.Runner) ?? 0f);
                        btnText.text = fm.CurrentRound >= 5 ? "GAME OVER" : $"NEXT ROUND ({remain}s)";
                    }
                }
            }

            // Donボタンの表示制御
            if (discardDonButton != null)
            {
                bool canDon = false;
                if (!fm.IsRoundOver && fm.DiscardCount > 0 && localActorId != -1)
                {
                    var topCard = fm.DiscardPile.Get(fm.DiscardCount - 1);
                    int myTotal = 0;
                    foreach (var c in fm.myLocalHand) myTotal += c.Rank;

                    if (fm.IsWaitingForDonGaeshi)
                    {
                        // Don返し：カードを出したプレイヤーが返せるか判定
                        if (fm.DonTargetActorId == localActorId && myTotal == topCard.Rank) canDon = true;
                    }
                    else
                    {
                        // リアルタイム Don：自分が最後の打ち手でなく手札合計が一致している
                        if (fm.LastPlayedPlayerActorId != localActorId && myTotal == topCard.Rank && myTotal <= 13) canDon = true;
                    }
                }
                discardDonButton.gameObject.SetActive(canDon);
                discardDonButton.interactable = canDon;
            }

            UpdateFusionDiscardPileUI();

            if (fm.IsRoundOver)
            {
                resultPanel.SetActive(true);
                string winnerName = (fm.WinnerActorId != -1) ? $"Player {fm.WinnerActorId}" : "Someone";
                resultText.text = $"{winnerName} Wins!";
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

            UpdateOpponentsUI();
        }

private void UpdateOpponentsUI()
{
    var fm = DonFusionManager2D.Instance;
    if (fm == null || opponentInfoContainer == null || opponentInfoPrefab == null) return;

    if (fm.Runner == null || !fm.Runner.IsRunning) return;

    int localActorId = -1;
    // 自分のActorIdを特定
    for (int i = 0; i < 4; i++)
    {
        var ac = fm.Actors.Get(i);
        if (ac.IsActive && !ac.IsCPU && ac.PlayerRef == fm.Runner.LocalPlayer)
        {
            localActorId = ac.ActorId;
            break;
        }
    }

    if (localActorId == -1) return; // 自分がまだActorとして登録されていない場合

    var activeActors = new System.Collections.Generic.List<DonGame2D.Logic.ActorInfo>();
    for (int i = 0; i < 4; i++)
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
        // プレハブの元のアンカーが左下になっている等により原点がズレるのを防ぐため、毎回明示的に中央揃えにする
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
            Debug.Log($"[UI] Instantiate Opponent UI for Actor {op.ActorId}");
        }

        // --- 画面の中央（捨て札と山札の間）を基準とした楕円状配置 ---
        Vector2 centerPos = Vector2.zero;
        if (discardPileContainer != null && deckPileContainer != null)
        {
            // 捨て札と山札のワールド座標の中間点を取得
            Vector3 worldCenter = (discardPileContainer.position + deckPileContainer.position) / 2f;
            // 相手UIの親コンテナから見たローカル座標に変換する
            centerPos = opponentInfoContainer.InverseTransformPoint(worldCenter);
        }

        // 自分から時計回りに何番目か (1 ~ N-1)
        int opIdx = sortedActiveActors.FindIndex(a => a.ActorId == op.ActorId);
        int relativeIndex = (opIdx - myIdx + totalPlayers) % totalPlayers;

        // 自分（LocalPlayer）の位置を 270度（画面下中央）とする
        // 各プレイヤーを時計回りに配置するため、角度を引いていく（極座標ではマイナスが時計回り）
        // 4人の場合: My(270) -> P2(180=左) -> P3(90=上) -> P4(0=右)
        float interval = 360f / totalPlayers;
        float angle = 270f - (interval * relativeIndex);
        
        // 画面の縦横比に沿った楕円形にするため、X・Y個別に半径を計算
        var containerRT = opponentInfoContainer.GetComponent<RectTransform>();
        // 横幅の約44%, 縦幅の約42%を半径にする (テーブル淵付近に収める)
        float radiusX = containerRT.rect.width * 0.44f;
        float radiusY = containerRT.rect.height * 0.42f;
        
        float rad = angle * Mathf.Deg2Rad;
        Vector2 pos = centerPos + new Vector2(Mathf.Cos(rad) * radiusX, Mathf.Sin(rad) * radiusY);
        
        // レイアウト更新（angle + 90f：OpponentUIInfoの扇の「根元(下)」が淵側に、広がりが中央を向くように補正）
        opponentUIs[op.ActorId].UpdateLayout(pos, angle + 90f);
        
        // スケールを等倍に戻し、他（捨て札等）と大きさを合わせる
        opponentUIs[op.ActorId].transform.localScale = Vector3.one;

        // PlayerHandCounts は Actors 配列のインデックスに対応（0~3）
        int arrayIndex = -1;
        for (int i = 0; i < 4; i++)
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
            
            // アニメーション中かどうかを確認
            if (!isAnimating)
            {
                opponentUIs[op.ActorId].Setup(op.ActorId, count, count == 1);
            }
            else
            {
                // アニメーション中であっても、枚数が「減る」方向の更新であれば即座に数値とアイコンを反映する
                // これにより、カードを投げた瞬間に手元のカードが減る
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
                
                // アニメーション完了後に最終的な枚数を再反映するため、最新値を保持しておく（ドロー時など増える方向の対応）
                pendingOpponentHandCounts[op.ActorId] = count;
            }
        }


        // ターンのプレイヤーをハイライトする
        opponentUIs[op.ActorId].SetTurnActive(fm.CurrentTurnPlayerActorId == op.ActorId);
    }
}

        public void UpdateFusionHandUI(bool skipAnimation = false)
        {
            var fm = DonFusionManager2D.Instance;
            
            // ===== 1. アニメーション中の新規カード検知（skipAnimation=falseの時のみ）=====
            // この処理は isDealingAnimationRunning に関わらず、常に実行する
            if (!skipAnimation && playerHandUI.Count > 0 && deckPileContainer != null)
            {
                // 現在表示済み + アニメーション中 + キュー待機中の合計数
                int currentTotalShowing = playerHandUI.Count + inFlightAnimationCount + pendingDrawAnimations.Count;
                
                // データ上の手札が増えていれば差分をキューに積む
                if (fm.myLocalHand.Count > currentTotalShowing)
                {
                    for (int i = currentTotalShowing; i < fm.myLocalHand.Count; i++)
                    {
                        pendingDrawAnimations.Enqueue(fm.myLocalHand[i]);
                    }

                    // アニメーションが走っていなければ新たに開始
                    if (!isDealingAnimationRunning)
                    {
                        StartCoroutine(ProcessDrawAnimationQueue());
                    }
                }
            }
            
            // ===== 2. アニメーション中はUI再構築をスキップ =====
            // ただし、skipAnimation が true (内部的な並べ替えなど) の場合は許可する
            if (!skipAnimation && (isDealingAnimationRunning || pendingDrawAnimations.Count > 0))
            {
                return;
            }

            // ===== 3. 初期配布の検知（アニメーションなし・skipAnimationに関わらず） =====
            if (!skipAnimation && playerHandUI.Count == 0 && fm.myLocalHand.Count > 0)
            {
                if (fm.myLocalHand.Count < fm.initialHandCount) return;
                
                if (deckPileContainer != null)
                {
                    StartCoroutine(DealCardsAnimationCoroutine(new List<CardInfo>(fm.myLocalHand)));
                    return;
                }
            }

            // ===== 4. 手札 UI の更新（アニメーションあり・再利用） =====
            UpdateHandWithAnimation(fm.myLocalHand);
        }

        private void UpdateHandWithAnimation(List<CardInfo> myHand)
        {
            // ビジュアル用の親コンテナを確保（LayoutGroupがない親）
            if (handVisualParent == null)
            {
                GameObject go = new GameObject("HandVisualParent", typeof(RectTransform));
                go.transform.SetParent(playerHandContainer.parent, false);
                handVisualParent = go.transform;
                // playerHandContainer と同じ位置・サイズに合わせる（アンカー設定など必要なら）
                RectTransform rt = go.GetComponent<RectTransform>();
                RectTransform source = playerHandContainer.GetComponent<RectTransform>();
                rt.anchorMin = source.anchorMin;
                rt.anchorMax = source.anchorMax;
                rt.pivot = source.pivot;
                rt.anchoredPosition = source.anchoredPosition;
                rt.sizeDelta = source.sizeDelta;
            }

            // 1. ダミースロットの数を合わせる (playerHandContainer は LayoutGroup を持つ)
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

            // 2. CardUI (実体) の管理 (CardInfo に基づいて個体を維持)
            playerHandUI.RemoveAll(item => item == null);
            
            List<CardUI> nextHandUI = new List<CardUI>();
            List<CardUI> pool = new List<CardUI>(playerHandUI);

            for (int i = 0; i < myHand.Count; i++)
            {
                CardInfo targetData = myHand[i];
                // 既存のオブジェクトからデータが一致するものを探す
                CardUI existing = pool.Find(ui => ui != null && 
                                                 ui.CardInfo.SuitInt == targetData.SuitInt && 
                                                 ui.CardInfo.Rank == targetData.Rank);
                
                if (existing != null)
                {
                    nextHandUI.Add(existing);
                    pool.Remove(existing);
                    // データの再セットアップ（念のため）
                    existing.SetupFusion(targetData, true);
                }
                else
                {
                    // 見つからなければ新規生成
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

            // 余ったオブジェクト（手札から消えたカード）を破壊
            foreach (var remaining in pool)
            {
                if (remaining != null) Destroy(remaining.gameObject);
            }

            playerHandUI = nextHandUI;

            // 実体の階層順序をリスト（データの並び）と一致させる
            // リストの先頭（左側）から順に「最前面に送る」を繰り返すことで、
            // リストの最後（右側）のカードが物理的に最前面（SiblingIndex最大）になることを保証する
            for (int i = 0; i < playerHandUI.Count; i++)
            {
                if (playerHandUI[i] != null)
                {
                    // ドラッグ中以外のカードは親を確認し、必要なら handVisualParent に戻す
                    // （ドラッグ終了直後のカードを確実にリスト順の管理に含めるため）
                    if (!playerHandUI[i].IsDragging && (playerHandUI[i].transform.parent != handVisualParent))
                    {
                        playerHandUI[i].transform.SetParent(handVisualParent, true);
                    }
                    
                    // 親が handVisualParent にある場合のみ最前面へ（順次重ねていく）
                    if (playerHandUI[i].transform.parent == handVisualParent)
                    {
                        playerHandUI[i].transform.SetAsLastSibling();
                    }
                }
            }

            // 4. レイアウト確定後に移動
            if (applyHandPositionsCoroutine != null) StopCoroutine(applyHandPositionsCoroutine);
            applyHandPositionsCoroutine = StartCoroutine(ApplyHandPositionsAfterLayout());
        }

        private System.Collections.IEnumerator ApplyHandPositionsAfterLayout()
        {
            yield return null; // レイアウト確定を待つ
            Canvas.ForceUpdateCanvases();

            // 扇状配置の計算（プレイヤー自身なのでカーブはなだらかに設定）
            int count = playerHandUI.Count;
            float fanAngleSpan = Mathf.Min(count * 5f, 30f); 
            float startAngle = fanAngleSpan / 2f;
            float angleStep = count > 1 ? fanAngleSpan / (count - 1) : 0f;
            float radius = 500f; // 大きな円弧

            for (int i = 0; i < playerHandUI.Count; i++)
            {
                if (i < dummySlots.Count && dummySlots[i] != null)
                {
                    // ドラッグ中のカードはレイアウト計算から除外して飛ばさない
                    if (!playerHandUI[i].IsDragging)
                    {
                        Vector3 basePos = dummySlots[i].transform.position;
                        
                        // 傾きとY軸の沈み込みを計算
                        float currentAngle = startAngle - (i * angleStep);
                        float rad = currentAngle * Mathf.Deg2Rad;
                        float yOffsetLocal = Mathf.Cos(rad) * radius - radius;
                        
                        // キャンバススケールをかけてワールド空間オフセットに変換（親コンテナのスケールを利用）
                        float yOffsetWorld = 0f;
                        if (playerHandContainer != null) {
                            yOffsetWorld = yOffsetLocal * playerHandContainer.lossyScale.y;
                        }

                        // カーブと回転を同時に滑らかにアニメーションさせる
                        if (playerHandUI[i] != null) {
                            playerHandUI[i].SmoothMoveAndRotateTo(
                                basePos + new Vector3(0, yOffsetWorld, 0),
                                Quaternion.Euler(0, 0, currentAngle)
                            );
                        }
                    }
                }
            }
        }

        private System.Collections.IEnumerator DealCardsAnimationCoroutine(List<CardInfo> handToDeal)
        {
            isDealingAnimationRunning = true;

            // コンテナを一旦クリア（"DummySlot"など一時的なものはクリアするが、他のオブジェクトがある場合は注意。現在は Hand 限定なので全破棄でOK）
            foreach (Transform child in playerHandContainer) Destroy(child.gameObject);
            playerHandUI.Clear();

            List<CardUI> animatingCards = new List<CardUI>();
            List<GameObject> dummySlots = new List<GameObject>();

            // 1. まず「透明なダミーの枠」を必要枚数分 LayoutGroup (playerHandContainer) に追加してレイアウトを確定させる
            for (int i = 0; i < handToDeal.Count; i++)
            {
                GameObject dummyObj = new GameObject("DummySlot", typeof(RectTransform), typeof(LayoutElement));
                dummyObj.transform.SetParent(playerHandContainer, false);
                LayoutElement le = dummyObj.GetComponent<LayoutElement>();
                le.preferredWidth = 100f;
                le.preferredHeight = 140f;
                dummySlots.Add(dummyObj);
            }

            // レイアウトが計算されるまで数フレーム待機
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            // 2. 実際のカード画像を DeckPileContainer (山札) の位置から生成し、 Canvas 直下等の自由移動可能な階層へ
            Transform canvasTransform = playerHandContainer.GetComponentInParent<Canvas>().transform;
            
            for (int i = 0; i < handToDeal.Count; i++)
            {
                GameObject go = Instantiate(cardPrefab, canvasTransform);
                CardUI ui = go.GetComponent<CardUI>();
                
                // 初めは裏面で表示
                ui.SetupFusion(handToDeal[i], false); 
                
                // 初期位置は山札。Screen/World Spaceどちらでも対応できるよう山札のpositionをそのまま使用
                Vector3 startPos = deckPileContainer.position;
                ui.transform.position = startPos;
                ui.transform.localScale = Vector3.one;
                
                // クリック判定をアニメーション中は無効化しておく
                if (ui.button != null) ui.button.interactable = false;
                
                animatingCards.Add(ui);

                float duration = 0.25f; // 1枚あたり0.25秒で移動
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float easeOutT = 1f - Mathf.Pow(1f - t, 3f);

                    if (dummySlots[i] != null)
                    {
                        Vector3 targetPos = dummySlots[i].transform.position;
                        ui.transform.position = Vector3.Lerp(startPos, targetPos, easeOutT);
                    }

                    // 移動の後半で表面にフリップする演出(簡易的)
                    if (t > 0.5f && ui.cardImage.sprite == cardDatabase.GetCardBack())
                    {
                        ui.SetupFusion(handToDeal[i], true); // 表面にする
                    }

                    yield return null;
                }

                if (dummySlots[i] != null)
                {
                    ui.transform.position = dummySlots[i].transform.position;
                }
                ui.SetupFusion(handToDeal[i], true);

                // 次のカードへ移る前に少し待つ
                yield return new WaitForSeconds(0.1f);
            }

            // 3. アニメーションがすべて完了したら、ダミーと一時カードを破棄し、本来のUIへ確実に入れ替える
            foreach (var d in dummySlots) {
                if (d != null) Destroy(d);
            }
            foreach (var c in animatingCards) {
                if (c != null) Destroy(c.gameObject);
            }

            isDealingAnimationRunning = false;

            // 本来の表示処理を呼び出して、レイアウトやイベントが完全に正常な状態を再構築する
            UpdateFusionHandUI(true);
        }

        private System.Collections.IEnumerator ProcessDrawAnimationQueue()
        {
            isDealingAnimationRunning = true;
            var fm = DonFusionManager2D.Instance;

            while (pendingDrawAnimations.Count > 0)
            {
                CardInfo cardToAnimate = pendingDrawAnimations.Dequeue();
                inFlightAnimationCount++; // Dequeue後, playerHandUI.Add前の「宙ぶらりん」をカウント

                if (deckPileContainer == null) { inFlightAnimationCount--; break; }

                // 1. ダミースロットを1つ作成してレイアウトを確保
                GameObject dummyObj = new GameObject("DummySlot", typeof(RectTransform), typeof(LayoutElement));
                dummyObj.transform.SetParent(playerHandContainer, false);
                LayoutElement le = dummyObj.GetComponent<LayoutElement>();
                le.preferredWidth = 100f;
                le.preferredHeight = 140f;

                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();

                Transform canvasTransform = playerHandContainer.GetComponentInParent<Canvas>().transform;

                // 2. カードを生成してアニメーション
                GameObject go = Instantiate(cardPrefab, canvasTransform);
                CardUI ui = go.GetComponent<CardUI>();
                ui.SetupFusion(cardToAnimate, false);

                Vector3 startPos = deckPileContainer.position;
                ui.transform.position = startPos;
                ui.transform.localScale = Vector3.one;
                if (ui.button != null) ui.button.interactable = false;

                float duration = 0.25f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float easeOutT = 1f - Mathf.Pow(1f - t, 3f);

                    if (dummyObj != null)
                    {
                        Vector3 targetPos = dummyObj.transform.position;
                        ui.transform.position = Vector3.Lerp(startPos, targetPos, easeOutT);
                    }

                    if (t > 0.5f && ui.cardImage.sprite == cardDatabase.GetCardBack())
                    {
                        ui.SetupFusion(cardToAnimate, true);
                    }

                    yield return null;
                }

                if (dummyObj != null) ui.transform.position = dummyObj.transform.position;
                ui.SetupFusion(cardToAnimate, true);
                if (ui.button != null) ui.button.interactable = true;

                // 3. 後片付け:
                //    dummyObj（レイアウト確保用の枠）は破棄
                //    go（アニメーション用）は handVisualParent に「引き渡す」（破棄しない）
                if (dummyObj != null) Destroy(dummyObj);
                playerHandUI.Add(ui); // 手札管理リストに追加
                
                // カードを追加したら、とりあえず最前面にしておく
                ui.transform.SetAsLastSibling();

                inFlightAnimationCount--; // アニメーション終了
                yield return new WaitForSeconds(0.1f); // 各カード間のインターバル
            }

            isDealingAnimationRunning = false;
            // アニメーション完了後、全体の重なり順を確定させるために強制更新
            UpdateFusionHandUI(true);
        }

        #region Opponent Animations

        public void PlayOpponentCardAnimation(int actorId, CardInfo card)
        {
            var fm = DonFusionManager2D.Instance;
            if (fm == null || fm.Runner == null) return;
            
            // 自分のアクションなら何もしない（自機はドラッグ＆ドロップで即時反映されるため）
            if (fm.GetActorId(fm.Runner.LocalPlayer) == actorId) return;

            // 誰が出したかを探す
            opponentUIs.TryGetValue(actorId, out OpponentUIInfo targetOpponent);
            if (targetOpponent != null)
            {
                Debug.Log($"[Anim] PlayOpponentCardAnimation: Actor {actorId} plays {card.Suit}_{card.Rank}");
                // アニメーション開始前にフラグを立て、UpdateOpponentsUI()での手札枚数更新を抑制する
                opponentAnimatingFlags[actorId] = true;
                StartCoroutine(OpponentPlayAnimationCoroutine(actorId, targetOpponent.transform.position, card));
            }
            else
            {
                Debug.LogWarning($"[Anim] OpponentUIInfo not found for Actor {actorId}!");
            }
        }

        private System.Collections.IEnumerator OpponentPlayAnimationCoroutine(int actorId, Vector3 startPos, CardInfo card)
        {
            isOpponentCardAnimationRunning = true;
            pendingDiscardCard = default;
            
            // キャンバス直下にアニメーション用の一時カードを生成
            Transform canvasTransform = discardPileContainer.GetComponentInParent<Canvas>().transform;
            GameObject go = Instantiate(cardPrefab, canvasTransform);
            
            // 一番手前に表示
            go.transform.SetAsLastSibling();

            CardUI ui = go.GetComponent<CardUI>();
            ui.SetupFusion(card, true); // 表面で飛ばす
            ui.transform.position = startPos;
            ui.transform.localScale = Vector3.one;

            // アニメーション
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

            // 到着後に破棄（実際の捨て札UIはRPC_NotifyDiscardChangedによって別途更新される）
            Destroy(go);
            
            // アニメーション完了 → フラグを解除し、保留していた手札枚数を反映する
            isOpponentCardAnimationRunning = false;
            opponentAnimatingFlags[actorId] = false;

            // アニメーション中に保留されていた手札枚数を今すぐ反映する
            if (pendingOpponentHandCounts.TryGetValue(actorId, out int pendingCount) &&
                opponentUIs.TryGetValue(actorId, out OpponentUIInfo opUI))
            {
                opUI.Setup(actorId, pendingCount, pendingCount == 1);
                pendingOpponentHandCounts.Remove(actorId);
            }

            // 保留していた捨て札を今すぐ反映する
            if (pendingDiscardCard.Rank != 0)
            {
                OnDiscardPileChanged(pendingDiscardCard);
                pendingDiscardCard = default;
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
                Debug.Log($"[Anim] PlayOpponentDrawAnimation: Actor {actorId} draws {count} cards");
                // アニメーション開始前にフラグを立て、UpdateOpponentsUI()での手札枚数更新を抑制する
                opponentAnimatingFlags[actorId] = true;
                StartCoroutine(OpponentDrawAnimationCoroutine(actorId, targetOpponent.transform.position, count));
            }
            else
            {
                Debug.LogWarning($"[Anim] OpponentUIInfo not found for Actor {actorId}!");
            }
        }

        private System.Collections.IEnumerator OpponentDrawAnimationCoroutine(int actorId, Vector3 targetPos, int count)
        {
            Transform canvasTransform = discardPileContainer.GetComponentInParent<Canvas>().transform;
            Vector3 startPos = deckPileContainer.position; // 山札から出発

            // 最後のカードのアニメーション完了を待つために自前でトラッキングする
            float lastCardDuration = 0.3f;
            float lastCardDelay = (count - 1) * 0.1f; // 最後のカードが発射されるまでの遅延

            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(cardPrefab, canvasTransform);
                go.transform.SetAsLastSibling();

                CardUI ui = go.GetComponent<CardUI>();
                // ドロー時は裏面
                ui.SetupFusion(new CardInfo(Suit.Hearts, 1), false); // ダミーデータで裏面生成
                ui.transform.position = startPos;
                ui.transform.localScale = Vector3.one;

                StartCoroutine(AnimateSingleOpponentDraw(go, startPos, targetPos));

                // 複数枚ある場合は少しずらして発射
                yield return new WaitForSeconds(0.1f);
            }

            // 最後のカードのアニメーション（0.3秒）が完了するまで待機
            yield return new WaitForSeconds(lastCardDuration);

            // 全アニメーション完了 → フラグを解除し、保留していた手札枚数を反映する
            opponentAnimatingFlags[actorId] = false;

            // アニメーション中に保留されていた手札枚数を今すぐ反映する
            if (pendingOpponentHandCounts.TryGetValue(actorId, out int pendingCount) &&
                opponentUIs.TryGetValue(actorId, out OpponentUIInfo opUI))
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

            if (cardObj != null)
            {
                Destroy(cardObj);
            }
        }

        #endregion

        private int lastDiscardPileCount = -1;
        private Suit lastTopCardSuit = (Suit)(-1);
        private int lastTopCardRank = -1;
        
        private bool isLocalPlayerDiscarding = false;

        public void SetLocalPlayerDiscarding(bool value)
        {
            isLocalPlayerDiscarding = value;
        }
private int lastActiveSuitInt = -1;
        
        // 相手のカード提出アニメーション実行中フラグ
        private bool isOpponentCardAnimationRunning = false;
        private CardInfo pendingDiscardCard = default;  // アニメーション完了待ちの捨て札情報

        // 各相手アクターのアニメーション実行中フラグ（actorId → true=アニメーション中）
        // アニメーション完了前に手札枚数UIが更新されないようにするための制御用
        private Dictionary<int, bool> opponentAnimatingFlags = new Dictionary<int, bool>();
        // アニメーション完了後に反映すべき手札枚数（actorId → 枚数）
        private Dictionary<int, int> pendingOpponentHandCounts = new Dictionary<int, int>();

        /// <summary>
        /// DonFusionManager2D から RPC 経由で呼ばれる捨て札通知。
        /// NetworkArray の同期遅延にかかわらず、捨て札UIを必ず更新する。
        /// </summary>
public void OnDiscardPileChanged(DonGame2D.Models.CardInfo topCard)
        {
            if (topCard.Rank == 0) return;
            
            // アニメーション実行中は更新を延龐する
            if (isOpponentCardAnimationRunning)
            {
                pendingDiscardCard = topCard;
                return;
            }

            CardUI ui = null;
            if (discardPileContainer != null && discardPileContainer.childCount > 0)
            {
                // Donボタン以外の先頭の子を検索
                foreach (Transform child in discardPileContainer)
                {
                    if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue;
                    ui = child.GetComponent<CardUI>();
                    break;
                }
            }

            if (ui == null || ui.gameObject.name != "DiscardPileCard")
            {
                if (discardPileContainer != null)
                {
                    // Donボタン以外の子を全除去
                    foreach (Transform child in discardPileContainer)
                    {
                        if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue;
                        Destroy(child.gameObject);
                    }
                    GameObject go = Instantiate(cardPrefab, discardPileContainer);
                    go.name = "DiscardPileCard";
                    go.transform.SetAsFirstSibling(); // Donボタンの手前に配置
                    ui = go.GetComponent<CardUI>();
                    lastDiscardPileCount = -1;
                }
            }

            if (ui != null)
            {
                ui.transform.localPosition = Vector3.zero;
                ui.isDiscarded = true;
                ui.SetupFusion(topCard, true);

                var fm = DonFusionManager2D.Instance;
                if (fm != null)
                {
                    lastDiscardPileCount = fm.DiscardCount;
                    lastTopCardSuit = topCard.Suit;
                    lastTopCardRank = topCard.Rank;
                    lastActiveSuitInt = fm.ActiveSuitInt;
                }
            }
        }

        

                    private void UpdateFusionDiscardPileUI()
        {
            // 相手のカード提出アニメーション実行中は、ポーリングによる即時更新を停止する
            if (isOpponentCardAnimationRunning) return;

            var fm = DonFusionManager2D.Instance;
            if (fm.DiscardCount > 0)
            {
                var topCard = fm.DiscardPile.Get(fm.DiscardCount - 1);

                if (topCard.Rank == 0)
                {
                    lastDiscardPileCount = -1;
                    return;
                }

                CardUI ui = null;
                if (discardPileContainer.childCount > 0)
                {
                    // Donボタン以外の先頭の子を検索
                    foreach (Transform child in discardPileContainer)
                    {
                        if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue;
                        ui = child.GetComponent<CardUI>();
                        break;
                    }
                }

                if (ui == null || ui.gameObject.name != "DiscardPileCard")
                {
                    // Donボタン以外の子を全除去して再生成
                    foreach (Transform child in discardPileContainer)
                    {
                        if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue;
                        Destroy(child.gameObject);
                    }

                    GameObject go = Instantiate(cardPrefab, discardPileContainer);
                    go.name = "DiscardPileCard";
                    // Donボタンの手前に持ってくる（常に最前列をキープ）
                    go.transform.SetAsFirstSibling();
                    ui = go.GetComponent<CardUI>();
                    lastDiscardPileCount = -1;
                }

                bool cardChanged = (fm.DiscardCount != lastDiscardPileCount || topCard.Suit != lastTopCardSuit || topCard.Rank != lastTopCardRank);
                bool activeSuitChanged = (fm.ActiveSuitInt != lastActiveSuitInt);

                if (cardChanged || activeSuitChanged)
                {
                    ui.transform.localPosition = Vector3.zero;
                    ui.isDiscarded = true;

                    if (cardChanged)
                    {
                        ui.SetupFusion(topCard, true);
                    }
                    else if (activeSuitChanged && topCard.Rank == 8 && fm.ActiveSuitInt != -1)
                    {
                        Sprite newSprite = ui.database.GetCardSprite((Suit)fm.ActiveSuitInt, 8);
                        if (newSprite != null)
                        {
                            ui.ChangeSpriteWithFade(newSprite, 0.5f);
                        }
                    }

                    lastDiscardPileCount = fm.DiscardCount;
                    lastTopCardSuit = topCard.Suit;
                    lastTopCardRank = topCard.Rank;
                    lastActiveSuitInt = fm.ActiveSuitInt;
                }
            }
            else
            {
                // Donボタン以外の子を破棄
                foreach (Transform child in discardPileContainer)
                {
                    if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue;
                    Destroy(child.gameObject);
                }
                lastDiscardPileCount = -1;
                lastActiveSuitInt = -1;
            }
        }

        private void UpdateUIState()
        {
            DonPlayer localPlayer = DonGameManager.Instance.players[0];
            bool isMyTurn = DonGameManager.Instance.currentPlayerIndex == 0;

            statusText.text = isMyTurn ? "Your Turn" : $"{DonGameManager.Instance.players[DonGameManager.Instance.currentPlayerIndex].name}'s Turn";
            penaltyText.text = $"RD 1/5 | {localPlayer.credits} Credits";
            if (DonGameManager.Instance.drawPenaltyCount > 0) penaltyText.text += $" | Penalty: +{DonGameManager.Instance.drawPenaltyCount}";

            drawButton.interactable = isMyTurn && !DonGameManager.Instance.isRoundOver;

            Card top = DonGameManager.Instance.GetTopDiscard();
            donButton.interactable = !DonGameManager.Instance.isRoundOver && top != null && localPlayer.GetHandTotal() == top.rank && localPlayer.GetHandTotal() <= 13;

            UpdateHandUI(localPlayer);
            UpdateDiscardPileUI();

            if (DonGameManager.Instance.isRoundOver)
            {
                resultPanel.SetActive(true);
                resultText.text = "Round Finished!\nCheck console for scores.";
            }
            else
            {
                resultPanel.SetActive(false);
            }
        }

        private void UpdateHandUI(DonPlayer player)
        {
            if (playerHandUI.Count != player.hand.Count)
            {
                foreach (Transform child in playerHandContainer) Destroy(child.gameObject);
                playerHandUI.Clear();

                foreach (var card in player.hand)
                {
                    GameObject go = Instantiate(cardPrefab, playerHandContainer);
                    CardUI ui = go.GetComponent<CardUI>();
                    ui.Setup(card, true);
                    playerHandUI.Add(ui);
                }
            }
        }

        public void ShowReach(int actorId)
        {
            temporaryNotification = $"PLAYER {actorId}: REACH!!!";
            notificationTimer = 4.0f; // 4秒間表示
        }

        public void ShowRoundResult(string msg, bool isFinal)
        {
            if (resultPanel == null || resultText == null) return;

            resultText.text = msg;
            resultPanel.SetActive(true);

            // リザルトパネルに「次へ」ボタンがある場合はテキストを変えるなどの演出が可能
            var btn = resultPanel.GetComponentInChildren<Button>();
            if (btn != null)
            {
                var btnText = btn.GetComponentInChildren<Text>();
                if (btnText != null) btnText.text = isFinal ? "GAME OVER (EXIT)" : "START NEXT ROUND";
                
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    if (useFusion && DonFusionManager2D.Instance != null)
                    {
                        if (isFinal)
                        {
                            // 終了処理（タイトルへ戻る等）
                            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                        }
                        else
                        {
                            DonFusionManager2D.Instance.RPC_RequestNextRound();
                            resultPanel.SetActive(false);
                            
                            // アニメーション用の中央コンテナをクリア
                            if (revealedHandContainer != null)
                            {
                                foreach (Transform child in revealedHandContainer)
                                    Destroy(child.gameObject);
                            }
                        }
                    }
                });
            }
        }

        public void PlayRoundEndAnimation(int winType, int winnerId, int loserId, int donValue, string loserHandStr, int totalPenalty, string resultMsg, bool isFinal)
        {
            var animCtrl = GetComponent<ScoreAnimationController>();
            if (animCtrl == null) animCtrl = gameObject.AddComponent<ScoreAnimationController>();
            
            animCtrl.uiController = this;
            animCtrl.PlayWinAnimation(winType, winnerId, loserId, donValue, loserHandStr, totalPenalty, resultMsg, isFinal);
        }

        private void UpdateDiscardPileUI()
        {
            if (DonGameManager.Instance == null) return;
            Card topCard = DonGameManager.Instance.GetTopDiscard();
            if (topCard != null)
            {
                CardUI ui = null;
                if (discardPileContainer.childCount > 0)
                {
                    ui = discardPileContainer.GetChild(0).GetComponent<CardUI>();
                }

                if (ui == null || ui.gameObject.name != "DiscardPileCard")
                {
                    foreach (Transform child in discardPileContainer) Destroy(child.gameObject);
                    GameObject go = Instantiate(cardPrefab, discardPileContainer);
                    go.name = "DiscardPileCard";
                    ui = go.GetComponent<CardUI>();
                }

                ui.transform.localPosition = Vector3.zero;
                ui.isDiscarded = true;
                ui.Setup(topCard, true);
            }
        }
private void CreateContextDonButton()
        {
            // 1. まず既存の DonButton (元からシーンにある固定ボタン) を流用する
            if (donButton != null)
            {
                discardDonButton = donButton;
                Debug.Log("[Don] 既存の donButton を discardDonButton として流用します。");

                // 捕れ札パネルの上部中央に移動する
                var rt = discardDonButton.GetComponent<RectTransform>();
                if (rt != null && discardPileContainer != null)
                {
                    discardDonButton.transform.SetParent(discardPileContainer, false);
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot     = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0, 80f);
                    rt.sizeDelta = new Vector2(160, 60);
                }
                return;
            }

            // 2. donButton もない場合はコンテナから直接新規生成
            Transform discardPile = discardPileContainer;
            if (discardPile == null)
            {
                Debug.LogWarning("[Don] discardPileContainer が null のためボタンを作成できません。");
                return;
            }

            var btnTrans = discardPile.Find("ContextDonButton");
            GameObject btnObj;
            if (btnTrans != null)
            {
                btnObj = btnTrans.gameObject;
            }
            else
            {
                btnObj = new GameObject("ContextDonButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(discardPile, false);

                var brt = btnObj.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.5f, 1f);
                brt.anchorMax = new Vector2(0.5f, 1f);
                brt.pivot = new Vector2(0.5f, 0.5f);
                brt.anchoredPosition = new Vector2(0, 80f);
                brt.sizeDelta = new Vector2(160, 60);

                var img = btnObj.GetComponent<Image>();
                img.color = new Color(0.8f, 0.2f, 0.2f, 1f);

                GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObj.transform.SetParent(btnObj.transform, false);
                var txt = textObj.GetComponent<Text>();
                txt.text = "Don!";
                txt.alignment = TextAnchor.MiddleCenter;
                txt.fontSize = 28;
                txt.color = Color.white;
                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

                var textRt = textObj.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;
            }

            discardDonButton = btnObj.GetComponent<Button>();
            Debug.Log($"[Don] 新規 Don ボタンを作成しました: {discardDonButton != null}");
        }
    }
}
