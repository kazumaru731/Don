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
            UpdateFusionHandUI();
        }

        private void UpdateFusionUIState()
        {
            var fm = DonFusionManager2D.Instance;

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
            
            // Round UI logic: Use roundText (card in corner) if available
            if (roundText != null)
            {
                roundText.text = $"{fm.CurrentRound}/5";
                if (roundText.transform.parent != null && !roundText.transform.parent.gameObject.activeSelf)
                {
                    roundText.transform.parent.gameObject.SetActive(true);
                }

                string scoreInfo = $"{credits} Credits";
                if (fm.DrawPenaltyCount > 0) scoreInfo += $" | Penalty: +{fm.DrawPenaltyCount}";
                penaltyText.text = scoreInfo;
            }
            else
            {
                string scoreInfo = $"RD {fm.CurrentRound}/5 | {credits} Credits";
                if (fm.DrawPenaltyCount > 0) scoreInfo += $" | Penalty: +{fm.DrawPenaltyCount}";
                penaltyText.text = scoreInfo;
            }

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
            for (int i = 0; i < 4; i++)
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
                    
                    if (!isAnimating)
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
            
            if (!skipAnimation && playerHandUI.Count > 0 && deckPileContainer != null)
            {
                int currentTotalShowing = playerHandUI.Count + inFlightAnimationCount + pendingDrawAnimations.Count;
                if (fm.myLocalHand.Count > currentTotalShowing)
                {
                    for (int i = currentTotalShowing; i < fm.myLocalHand.Count; i++)
                    {
                        pendingDrawAnimations.Enqueue(fm.myLocalHand[i]);
                    }

                    if (!isDealingAnimationRunning)
                    {
                        StartCoroutine(ProcessDrawAnimationQueue());
                    }
                }
            }
            
            if (!skipAnimation && (isDealingAnimationRunning || pendingDrawAnimations.Count > 0))
            {
                return;
            }

            if (!skipAnimation && playerHandUI.Count == 0 && fm.myLocalHand.Count > 0)
            {
                if (fm.myLocalHand.Count < fm.initialHandCount) return;
                
                if (deckPileContainer != null)
                {
                    StartCoroutine(DealCardsAnimationCoroutine(new List<CardInfo>(fm.myLocalHand)));
                    return;
                }
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
            float fanAngleSpan = Mathf.Min(count * 6f, 45f); // Increased from 5f/30f to spread more
            float startAngle = fanAngleSpan / 2f;
            float angleStep = count > 1 ? fanAngleSpan / (count - 1) : 0f;
            float radius = 800f; // Increased from 500f for a gentler curve

            for (int i = 0; i < playerHandUI.Count; i++)
            {
                if (i < dummySlots.Count && dummySlots[i] != null)
                {
                    if (!playerHandUI[i].IsDragging)
                    {
                        Vector3 basePos = dummySlots[i].transform.position;
                        float currentAngle = startAngle - (i * angleStep);
                        float rad = currentAngle * Mathf.Deg2Rad;
                        float yOffsetLocal = Mathf.Cos(rad) * radius - radius;
                        float yOffsetWorld = 0f;
                        if (playerHandContainer != null) {
                            yOffsetWorld = yOffsetLocal * playerHandContainer.lossyScale.y;
                        }

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

        private float CalcSlotWidth(int cardCount)
        {
            const float containerWidth = 950f; // Increased from 700f to utilize ~80% of width
            const float cardVisualWidth = 100f;
            const float maxSlotWidth = 180f; // Increased from 160f for more space with few cards
            const float minSlotWidth = 65f; // Slightly increased from 55f
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


        private System.Collections.IEnumerator DealCardsAnimationCoroutine(List<CardInfo> handToDeal)
        {
            isDealingAnimationRunning = true;
            foreach (Transform child in playerHandContainer) Destroy(child.gameObject);
            playerHandUI.Clear();

            List<CardUI> animatingCards = new List<CardUI>();
            List<GameObject> tempDummySlots = new List<GameObject>();

            for (int i = 0; i < handToDeal.Count; i++)
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
            
            for (int i = 0; i < handToDeal.Count; i++)
            {
                GameObject go = Instantiate(cardPrefab, canvasTransform);
                CardUI ui = go.GetComponent<CardUI>();
                ui.SetupFusion(handToDeal[i], false); 
                Vector3 startPos = deckPileContainer.position;
                ui.transform.position = startPos;
                ui.transform.localScale = Vector3.one;
                if (ui.button != null) ui.button.interactable = false;
                animatingCards.Add(ui);

                float duration = 0.25f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float easeOutT = 1f - Mathf.Pow(1f - t, 3f);

                    if (tempDummySlots[i] != null)
                    {
                        Vector3 targetPos = tempDummySlots[i].transform.position;
                        ui.transform.position = Vector3.Lerp(startPos, targetPos, easeOutT);
                    }

                    if (t > 0.5f && ui.cardImage.sprite == cardDatabase.GetCardBack())
                    {
                        ui.SetupFusion(handToDeal[i], true);
                    }
                    yield return null;
                }

                if (tempDummySlots[i] != null) ui.transform.position = tempDummySlots[i].transform.position;
                ui.SetupFusion(handToDeal[i], true);
                yield return new WaitForSeconds(0.1f);
            }

            foreach (var d in tempDummySlots) { if (d != null) Destroy(d); }
            foreach (var c in animatingCards) { if (c != null) Destroy(c.gameObject); }

            isDealingAnimationRunning = false;
            UpdateFusionHandUI(true);
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
                    if (dummyObj != null) ui.transform.position = Vector3.Lerp(startPos, dummyObj.transform.position, easeOutT);
                    if (t > 0.5f && ui.cardImage.sprite == cardDatabase.GetCardBack()) ui.SetupFusion(cardToAnimate, true);
                    yield return null;
                }

                if (dummyObj != null) Destroy(dummyObj);
                playerHandUI.Add(ui);
                ui.transform.SetAsLastSibling();
                inFlightAnimationCount--;
                yield return new WaitForSeconds(0.1f);
            }

            isDealingAnimationRunning = false;
            UpdateFusionHandUI(true);
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

            Destroy(go);
            isOpponentCardAnimationRunning = false;
            opponentAnimatingFlags[actorId] = false;
            if (pendingOpponentHandCounts.TryGetValue(actorId, out int pendingCount) && opponentUIs.TryGetValue(actorId, out OpponentUIInfo opUI))
            {
                opUI.Setup(actorId, pendingCount, pendingCount == 1);
                pendingOpponentHandCounts.Remove(actorId);
            }
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
        private bool isOpponentCardAnimationRunning = false;
        private CardInfo pendingDiscardCard = default;
        private Dictionary<int, bool> opponentAnimatingFlags = new Dictionary<int, bool>();
        private Dictionary<int, int> pendingOpponentHandCounts = new Dictionary<int, int>();

        public void OnDiscardPileChanged(CardInfo topCard)
        {
            if (topCard.Rank == 0) return;
            if (isOpponentCardAnimationRunning) { pendingDiscardCard = topCard; return; }
            CardUI ui = null;
            if (discardPileContainer != null && discardPileContainer.childCount > 0)
            {
                foreach (Transform child in discardPileContainer) { if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue; ui = child.GetComponent<CardUI>(); break; }
            }
            if (ui == null || ui.gameObject.name != "DiscardPileCard")
            {
                if (discardPileContainer != null)
                {
                    foreach (Transform child in discardPileContainer) { if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue; Destroy(child.gameObject); }
                    GameObject go = Instantiate(cardPrefab, discardPileContainer);
                    go.name = "DiscardPileCard"; go.transform.SetAsFirstSibling(); ui = go.GetComponent<CardUI>(); lastDiscardPileCount = -1;
                }
            }
            if (ui != null)
            {
                ui.transform.localPosition = Vector3.zero; ui.isDiscarded = true; ui.SetupFusion(topCard, true);
                var fm = DonFusionManager2D.Instance;
                if (fm != null) { lastDiscardPileCount = fm.DiscardCount; lastTopCardSuit = topCard.Suit; lastTopCardRank = topCard.Rank; lastActiveSuitInt = fm.ActiveSuitInt; }
            }
        }

        private void UpdateFusionDiscardPileUI()
        {
            if (isOpponentCardAnimationRunning) return;
            var fm = DonFusionManager2D.Instance;
            if (fm.DiscardCount > 0)
            {
                var topCard = fm.DiscardPile.Get(fm.DiscardCount - 1);
                if (topCard.Rank == 0) { lastDiscardPileCount = -1; return; }
                CardUI ui = null;
                if (discardPileContainer.childCount > 0) { foreach (Transform child in discardPileContainer) { if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue; ui = child.GetComponent<CardUI>(); break; } }
                if (ui == null || ui.gameObject.name != "DiscardPileCard") { foreach (Transform child in discardPileContainer) { if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue; Destroy(child.gameObject); } GameObject go = Instantiate(cardPrefab, discardPileContainer); go.name = "DiscardPileCard"; go.transform.SetAsFirstSibling(); ui = go.GetComponent<CardUI>(); lastDiscardPileCount = -1; }
                bool cardChanged = (fm.DiscardCount != lastDiscardPileCount || topCard.Suit != lastTopCardSuit || topCard.Rank != lastTopCardRank);
                bool activeSuitChanged = (fm.ActiveSuitInt != lastActiveSuitInt);
                if (cardChanged || activeSuitChanged)
                {
                    ui.transform.localPosition = Vector3.zero; ui.isDiscarded = true;
                    if (cardChanged) ui.SetupFusion(topCard, true);
                    else if (activeSuitChanged && topCard.Rank == 8 && fm.ActiveSuitInt != -1) { Sprite newSprite = ui.database.GetCardSprite((Suit)fm.ActiveSuitInt, 8); if (newSprite != null) ui.ChangeSpriteWithFade(newSprite, 0.5f); }
                    lastDiscardPileCount = fm.DiscardCount; lastTopCardSuit = topCard.Suit; lastTopCardRank = topCard.Rank; lastActiveSuitInt = fm.ActiveSuitInt;
                }
            }
            else
            {
                foreach (Transform child in discardPileContainer) { if (discardDonButton != null && child.gameObject == discardDonButton.gameObject) continue; Destroy(child.gameObject); }
                lastDiscardPileCount = -1; lastActiveSuitInt = -1;
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

        public void ShowRoundResult(string msg, bool isFinal)
        {
            if (resultPanel == null || resultText == null) return;
            resultText.text = msg; resultPanel.SetActive(true);
            var btn = resultPanel.GetComponentInChildren<Button>();
            if (btn != null)
            {
                var btnText = btn.GetComponentInChildren<Text>();
                if (btnText != null) btnText.text = isFinal ? "GAME OVER (EXIT)" : "START NEXT ROUND";
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    if (useFusion && DonFusionManager2D.Instance != null) {
                        if (isFinal) UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                        else { DonFusionManager2D.Instance.RPC_RequestNextRound(); resultPanel.SetActive(false); if (revealedHandContainer != null) foreach (Transform child in revealedHandContainer) Destroy(child.gameObject); }
                    }
                });
            }
        }

        public void PlayRoundEndAnimation(int winType, int winnerId, int loserId, int donValue, string loserHandStr, int totalPenalty, string resultMsg, bool isFinal, string winnerNames = "")
        {
            if (scoreAnimationController != null) scoreAnimationController.PlayRoundEndAnimation(winType, winnerId, loserId, donValue, loserHandStr, totalPenalty, resultMsg, isFinal, winnerNames);
            else { var animCtrl = GetComponent<ScoreAnimationController>(); if (animCtrl == null) animCtrl = gameObject.AddComponent<ScoreAnimationController>(); animCtrl.uiController = this; animCtrl.PlayRoundEndAnimation(winType, winnerId, loserId, donValue, loserHandStr, totalPenalty, resultMsg, isFinal, winnerNames); }
        }

        public void ClearHandUI(int actorId)
        {
            if (actorId == GetLocalActorId()) { foreach (var cUI in playerHandUI) if (cUI != null) Destroy(cUI.gameObject); playerHandUI.Clear(); foreach (var d in dummySlots) if (d != null) Destroy(d); dummySlots.Clear(); }
            else if (opponentUIs.TryGetValue(actorId, out var info) && info.cardIconContainer != null) foreach (Transform child in info.cardIconContainer) Destroy(child.gameObject);
        }

        public void ShowDonAnimation(int actorId, string handData) => Debug.Log($"[UI] ShowDonAnimation actorId:{actorId}");

        private void UpdateDiscardPileUI()
        {
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
            if (btnObj == null) { btnObj = new GameObject("ContextDonButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)); btnObj.transform.SetParent(discardPileContainer, false); var brt = btnObj.GetComponent<RectTransform>(); brt.anchorMin = new Vector2(0.5f, 1f); brt.anchorMax = new Vector2(0.5f, 1f); brt.pivot = new Vector2(0.5f, 0.5f); brt.anchoredPosition = new Vector2(0, 80f); brt.sizeDelta = new Vector2(160, 60); var img = btnObj.GetComponent<Image>(); img.color = new Color(0.8f, 0.2f, 0.2f, 1f); GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); textObj.transform.SetParent(btnObj.transform, false); var txt = textObj.GetComponent<Text>(); txt.text = "Don!"; txt.alignment = TextAnchor.MiddleCenter; txt.fontSize = 28; txt.color = Color.white; txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); var textRt = textObj.GetComponent<Text>().GetComponent<RectTransform>(); textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one; textRt.offsetMin = Vector2.zero; textRt.offsetMax = Vector2.zero; }
            discardDonButton = btnObj.GetComponent<Button>();
        }

        public bool IsScatterAnimationRunning { get; set; } = false;
        public System.Collections.IEnumerator Co_RecallScatteredCards(Vector3 targetPos) { while (IsScatterAnimationRunning) yield return null; }

        public int GetLocalActorId()
        {
            var fm = DonFusionManager2D.Instance;
            if (fm == null || fm.Runner == null) return -1;
            var localObj = fm.Runner.GetPlayerObject(fm.Runner.LocalPlayer);
            if (localObj != null) { var netObj = localObj.GetComponent<Fusion.NetworkObject>(); if (netObj != null) return netObj.StateAuthority.PlayerId; }
            foreach (var ac in fm.Runner.ActivePlayers) { var fObj = fm.Runner.GetPlayerObject(ac); if (fObj != null && fObj.HasInputAuthority) return fObj.StateAuthority.PlayerId; }
            return -1;
        }
        public List<CardUI> GetPlayerHandUI() => playerHandUI;
        public void RemoveFromPlayerHandUI(CardUI card) => playerHandUI.Remove(card);
        public Transform GetOpponentCardContainer(int actorId) => opponentUIs.TryGetValue(actorId, out var info) ? info.cardIconContainer : null;
        public void PlayLocalDiscardAnimation(CardUI card, CardInfo cardInfo) { if (discardPileContainer != null && card != null) { card.transform.SetParent(discardPileContainer, true); card.transform.SetAsLastSibling(); } }
    }
}
