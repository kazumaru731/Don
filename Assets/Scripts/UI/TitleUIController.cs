using UnityEngine;
using UnityEngine.UI;
using DonGame2D.Network;
using System.Linq;
using System.Collections;

namespace DonGame2D.UI
{
    public class TitleUIController : MonoBehaviour
    {
        [Header("メイン画面")]
        public GameObject titleCanvasObj;
        public GameObject gameCanvasObj;
        public Button randomMatchButton;
        public Button friendMatchButton;   // フレンドマッチボタン
        public Button readyButton;         // ゲスト用Readyボタン
        public Sprite readyCardSprite;     // ReadyボタンのカードUI用スプライト (Assets/Sprites/UI/ReadyButton.png)
        public SelectionCard readySelectionCard; // Readyカード（Inspector から設定）
        public Button hostStartButton;     // ホスト用開始ボタン（新規追加）
        // legacy buttons removed: randomMatchBackButton, cpuBackButton
        public SelectionCard[] mainSelectionCards; // メインメニュー用カード
        public SelectionCard[] friendSelectionCards; // フレンドマッチ ホスト/ゲスト選択カード
        public SelectionCard[] hostSelectionCards;   // ホスト待機画面用カード（開始/戻る）
        public SelectionCard[] guestSelectionCards;  // ゲスト待機画面用カード（戻る）
        public SelectionCard[] randomSelectionCards; // ランダムマッチ待機画面用カード（戻る）

        public Text playersCountText;

        [Header("フレンドマッチパネル")]
        public GameObject friendMatchPanel;     // ホスト/ゲスト選択パネル
        public Button hostButton;               // ホスト選択ボタン
        public Button guestButton;              // ゲスト選択ボタン
        public GameObject hostPanel;            // ルームID表示パネル
        public GameObject guestPanel;           // ルームID入力パネル
        public Text roomIdDisplayText;          // ホスト用：生成されたルームIDを表示
        public Button copyIdButton;             // ホスト用：IDコピーボタン
        public InputField roomIdInputField;     // ゲスト用：ルームIDの入力欄
        public Button joinButton;               // ゲスト用：参加ボタン
        public Button backButton;               // 戻るボタン

        [Header("CPUマッチパネル")]
        public GameObject cpuMatchPanel;
        public Button cpuMatchButton;
        public SelectionCard[] cpuSelectionCards; // カードコンポーネントのリスト
        // legacy button removed: cpuBackButton

        [Header("フレンドマッチ CPU 追加")]
        public Text cpuCountLabel;       // "現在 X CPU" 表示ラベル
        public Button cpuAddButton;      // + ボタン
        public Button cpuRemoveButton;   // - ボタン

        [Header("ラウンド設定 (フレンドマッチ用)")]
        public GameObject roundSettingPanel;
        public Text roundCountText;
        public Button roundPlusButton;
        public Button roundMinusButton;

        private int friendCpuCount = 0;  // フレンドマッチで追加する CPU 数
        public static int pendingMaxRounds = 5; 

        [Header("State")]
        public bool isMatching = false;
        public bool isReady = false;

        public static int selectedTargetPlayers = 8;

        private string generatedRoomId = "";

        private bool isRandomMatch = false;
        private bool isCpuMatch = false; // CPUマッチかどうかを区別
        private bool hostCardsShown = false; // ホスト用カードが表示済みか
        private Coroutine animateCoroutine;  // 現在実行中のアニメーションコルーチン
        private SimpleDropZone cachedDropZone;
        private System.Collections.Generic.List<SelectionCard> selectedStack = new System.Collections.Generic.List<SelectionCard>();

        private void Start()
        {
            if (titleCanvasObj != null) titleCanvasObj.SetActive(true);
            if (gameCanvasObj != null) gameCanvasObj.SetActive(false);

            if (friendMatchPanel != null) friendMatchPanel.SetActive(false);
            if (hostPanel != null) hostPanel.SetActive(false);
            if (guestPanel != null) guestPanel.SetActive(false);
            if (cpuMatchPanel != null) cpuMatchPanel.SetActive(false);

            friendCpuCount = 0;
            // Removed manual interactable = false here to allow UpdateCpuCountLabel to control it

            if (cpuMatchButton == null)
            {
                var allButtons = FindObjectsOfType<Button>(true);
                foreach (var btn in allButtons)
                {
                    var text = btn.GetComponentInChildren<Text>();
                    if (text != null && (text.text == "CPUマッチ" || text.text == "CPU Match"))
                    {
                        cpuMatchButton = btn;
                        break;
                    }
                }
            }

            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (copyIdButton != null) copyIdButton.onClick.AddListener(OnCopyIdClicked);
            
            // Legacy Button Runtime Cleanup
            DisableLegacyButton(titleCanvasObj, "RandomMatchBackButton");
            DisableLegacyButton(titleCanvasObj, "CpuBackButton");
            DisableLegacyButton(titleCanvasObj, "BackButton");
            DisableLegacyButton(titleCanvasObj, "CancelButton");
            
            if (cpuAddButton != null) cpuAddButton.onClick.AddListener(OnCpuAddClicked);
            if (cpuRemoveButton != null) cpuRemoveButton.onClick.AddListener(OnCpuRemoveClicked);

            if (readyButton != null) { readyButton.onClick.AddListener(OnReadyClicked); readyButton.gameObject.SetActive(false); }
            if (hostStartButton != null) { hostStartButton.onClick.AddListener(OnHostStartClicked); hostStartButton.gameObject.SetActive(false); }

            // 自動取得フォールバック（Inspectorで未設定の場合）
            TryFindRoundSettingUI();

            // リスナー設定
            SetupRoundButtonListeners();

            if (roundSettingPanel != null) roundSettingPanel.SetActive(false);

            hostCardsShown = false;
            InitializeCards();
            
            if (mainSelectionCards != null && mainSelectionCards.Length > 0 && !isMatching)
            {
                if (animateCoroutine != null) StopCoroutine(animateCoroutine);
                animateCoroutine = StartCoroutine(Co_AnimateCards(mainSelectionCards));
            }
        }


        private void InitializeCards()
        {
            cachedDropZone = FindObjectOfType<SimpleDropZone>();
            var dropZone = cachedDropZone;
            
            if (mainSelectionCards != null) foreach (var c in mainSelectionCards) if (c != null) { c.gameObject.SetActive(false); SetCardCallbacks(c, dropZone); }
            if (cpuSelectionCards != null) foreach (var c in cpuSelectionCards) if (c != null) { c.gameObject.SetActive(false); SetCardCallbacks(c, dropZone); }
            if (friendSelectionCards != null) foreach (var c in friendSelectionCards) if (c != null) { c.gameObject.SetActive(false); SetCardCallbacks(c, dropZone); }
            if (hostSelectionCards != null) foreach (var c in hostSelectionCards) if (c != null) { c.gameObject.SetActive(false); SetCardCallbacks(c, dropZone); }
            if (guestSelectionCards != null) foreach (var c in guestSelectionCards) if (c != null) { c.gameObject.SetActive(false); SetCardCallbacks(c, dropZone); }
            if (randomSelectionCards != null) foreach (var c in randomSelectionCards) if (c != null) { c.gameObject.SetActive(false); SetCardCallbacks(c, dropZone); }
            // ReadyカードもSetCardCallbacksで初期化しておく（ShowReadyButton時に別途onSelectedを上書きする）
            if (readySelectionCard != null) { readySelectionCard.gameObject.SetActive(false); SetCardCallbacks(readySelectionCard, dropZone); }
            
            // 最初のアニメーションは Start で行うため、ここでは表示設定のみ
            // ただしマッチング中はメインカードを表示しない
            if (mainSelectionCards != null && !isMatching) 
            {
                foreach (var c in mainSelectionCards) if (c != null) c.gameObject.SetActive(true);
            }
        }


        private void SetCardCallbacks(SelectionCard card, SimpleDropZone dropZone)
        {
            if (card == null) return;
            card.IsInDropZone = (pos) => (dropZone != null && dropZone.IsPositionInside(pos));
            card.OnSelectionStarted = (c) => {
                string lowerId = (c.selectionId ?? "").ToLower(); // selectionId might be from callback or arg
                bool isFunctionalCard = lowerId.Contains("start") || lowerId.Contains("cancel") || lowerId.Contains("back") || lowerId == "back" || lowerId.Contains("ready");
                
                if (!isFunctionalCard)
                {
                    // Tolerant ID matching for root categories
                    if (IsSameSelectionId(lowerId, "random") || IsSameSelectionId(lowerId, "friend") || IsSameSelectionId(lowerId, "cpu"))
                    {
                        ClearNavigationStack();
                    }

                    if (!selectedStack.Contains(c) && !selectedStack.Any(s => s != null && IsSameSelectionId(s.selectionId, lowerId))) 
                    {
                        selectedStack.Add(c);
                        Debug.Log($"[Stack] Added: {lowerId} (Instance: {c.name}). Total: {selectedStack.Count}");
                        UpdateStackPositions();
                    }
                }
            };
            card.OnIdSelected = (id) => {
                HandleCardSelected(id, card.playerCount);
            };
            card.OnCardDropped = (c) => {
                Debug.Log($"[Stack] Card finished sliding: {c.selectionId}");
                UpdateStackPositions();
                StartCoroutine(Co_DelayedUpdateStack(0.1f)); // Reinforce after panel animations start
            };
        }

        private void HandleCardSelected(string id, int count)
        {
            Debug.Log($"[TitleUI] Card Selected: {id} / {count}");
            if (id == "Random") OnRandomMatchClicked();
            else if (id == "Friend") OnFriendMatchClicked();
            else if (id == "CPU") OnCpuMatchClicked();
            else if (id == "Host") OnHostSelected();
            else if (id == "Guest") OnGuestSelected();
            else if (id == "StartMatch") OnHostStartClicked();
            else if (id == "CancelMatch" || id == "HostBack") 
            {
                OnBackClicked();
            }
            else if (id == "back") OnCpuBackClicked();
            else if (count > 0 && cpuMatchPanel != null && cpuMatchPanel.activeSelf) OnCpuPlayerCountSelected(count);
        }



        private void ClearNavigationStack()
        {
            if (selectedStack != null)
            {
                foreach (var card in selectedStack)
                {
                    if (card != null) 
                    {
                        card.ResetCardState();
                        card.gameObject.SetActive(false);
                    }
                }
                selectedStack.Clear();
            }
            hostCardsShown = false;
        }

        private void HideAllCards(bool force = false)
        {
            if (animateCoroutine != null)
            {
                StopCoroutine(animateCoroutine);
                animateCoroutine = null;
            }

            // Find all SelectionCards in the scene to ensure nothing lingers
            var allCards = FindObjectsOfType<SelectionCard>(true);
            foreach (var c in allCards)
            {
                if (c != null)
                {
                    if (!force)
                    {
                        // Skip cards that are part of the active selection stack OR currently animating back (Instance-specific protection)
                        bool isInStack = selectedStack.Contains(c);
                        if (isInStack || c.IsFlyingBack) 
                        {
                            c.gameObject.SetActive(true); // Ensure stack members are NEVER hidden here
                            continue;
                        }
                    }

                    c.ResetCardState();
                    c.gameObject.SetActive(false);
                }
            }
        }


        private void UpdateStackPositions()
        {
            if (selectedStack == null) return;
            selectedStack.RemoveAll(s => s == null);
            if (selectedStack.Count == 0) return;

            if (cachedDropZone == null) cachedDropZone = FindObjectOfType<SimpleDropZone>();
            if (cachedDropZone == null) return;
            
            Transform stackParent = cachedDropZone.transform.parent;
            
            // Pass 1: Global Ghost Buster
            var allSelectionCards = FindObjectsOfType<SelectionCard>(true);
            foreach (var sc in allSelectionCards)
            {
                if (sc == null || selectedStack.Contains(sc)) continue;
                if (selectedStack.Any(s => s != null && IsSameSelectionId(s.selectionId, sc.selectionId)))
                {
                    sc.gameObject.SetActive(false);
                }
            }

            // Pass 2: Hierarchy Locking
            for (int i = 0; i < selectedStack.Count; i++)
            {
                var card = selectedStack[i];
                if (card.transform.parent != stackParent)
                {
                    card.transform.SetParent(stackParent, true); 
                }
                
                // Absolute Physical & Logical Layering
                card.rectTransform.anchorMin = card.rectTransform.anchorMax = card.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                float dOffset = i * 2f; 
                card.rectTransform.anchoredPosition = new Vector2(dOffset, 50f + dOffset);
                card.rectTransform.localRotation = Quaternion.identity;
                card.rectTransform.localScale = card.baseScale * (0.8f + (i * 0.02f));
                
                // Massive Z-Forwarding (-100 per card)
                card.rectTransform.localPosition = new Vector3(card.rectTransform.localPosition.x, card.rectTransform.localPosition.y, -100f * (i + 1));
                
                card.gameObject.SetActive(true);
                card.isInteractable = false;

                // Priority Sorting
                var cardCanvas = card.GetComponent<Canvas>();
                if (cardCanvas == null) cardCanvas = card.gameObject.AddComponent<Canvas>();
                cardCanvas.overrideSorting = true;
                cardCanvas.sortingOrder = 10000 + i; // Ultra-high 
                
                if (card.GetComponent<GraphicRaycaster>() == null) card.gameObject.AddComponent<GraphicRaycaster>();

                var cg = card.GetComponent<CanvasGroup>();
                if (cg == null) cg = card.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = (i == selectedStack.Count - 1) ? 1f : 0.9f;

                card.transform.SetAsLastSibling(); 
            }
        }

        private IEnumerator Co_DelayedUpdateStack(float delay)
        {
            yield return new WaitForSeconds(delay);
            UpdateStackPositions();
        }

        private IEnumerator Co_AnimateCards(SelectionCard[] cards, float delay = 0.3f)
        {
            if (cards == null)
            {
                Debug.LogWarning("[TitleUI] Co_AnimateCards: cards array is null!");
                yield break;
            }
            if (cards.Length == 0)
            {
                Debug.LogWarning("[TitleUI] Co_AnimateCards: cards array is empty!");
                yield break;
            }

            Debug.Log($"[TitleUI] Co_AnimateCards: Starting animation for {cards.Length} cards.");

            // 1. Prepare: Move to start and hide first
            foreach (var card in cards)
            {
                if (card == null) continue;
                
                // CRITICAL: Skip cards already in the discard pile stack (Robust ID check)
                if (selectedStack.Any(s => s != null && IsSameSelectionId(s.selectionId, card.selectionId)))
                {
                    card.gameObject.SetActive(false); // Ensure duplicate hand cards are HIDDEN
                    continue;
                }

                card.gameObject.SetActive(true);
                card.rectTransform.anchoredPosition = Vector2.zero; // Assuming centerPos is Vector2.zero
                card.rectTransform.localRotation = Quaternion.identity;
                card.rectTransform.localScale = Vector3.zero;
                var cg = card.GetComponent<CanvasGroup>() ?? card.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
            }

            if (delay > 0) yield return new WaitForSeconds(delay);
            
            int count = cards.Length;
            float angleRange = (count > 2) ? 18f : 12f; // Increased from 6f to 12f to prevent overlap

            float radius = 1900f;   
            Vector2 pivot = new Vector2(0, -2650f); 

            // 2. Animate each card selectively
            for (int i = 0; i < count; i++)
            {
                var card = cards[i];
                if (card == null) continue;
                
                // CRITICAL: Skip cards already in the discard pile stack (Robust ID check)
                if (selectedStack.Any(s => s != null && IsSameSelectionId(s.selectionId, card.selectionId)))
                {
                    // Ensure it stays at its stack position if it was accidentally modified
                    UpdateStackPositions();
                    continue; 
                }

                if (card == null || card.gameObject == null) continue;

                RectTransform rect = card.GetComponent<RectTransform>();
                if (rect != null)
                {
                    // 全カード共通: Portraitフレーム (250x375)
                    rect.sizeDelta = new Vector2(250, 375);
                    
                    // スプライト形式に応じて最適なスケールを適用
                    // 全画面（メインメニュー、CPU選択、フレンドマッチ、待機画面）で統一
                    
                    string id = (card.selectionId ?? "").ToLower();
                    
                    // 1/1 スプライト (Start/Cancel/Back/開始/キャンセル/戻る/Ready 等)
                    bool isOneByOne = id.Contains("start") || id.Contains("cancel") || id.Contains("back") || id.Contains("ready");

                    // 1/2 スプライト (Host/Guest)
                    bool isHalf = id == "host" || id == "guest";
                    
                    if (isOneByOne)
                    {
                        // 1/1スプライト: ユーザーの調整値 (1.7, 1.25)
                        card.baseScale = new Vector3(1.7f, 1.25f, 1.0f);
                    }
                    else if (isHalf)
                    {
                        // 1/2スプライト: 1.15x1.1
                        card.baseScale = new Vector3(1.15f, 1.1f, 1.0f);
                    }
                    else
                    {
                        // 1/3スプライト: 1.1x1.1
                        card.baseScale = new Vector3(1.1f, 1.1f, 1.0f);
                    }
                    
                    // アスペクト比と当たり判定の設定
                    var images = card.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                    foreach (var image in images) 
                    {
                        image.preserveAspect = false; // ユーザーのスケール調整値を活かすため false を維持

                        // 当たり判定の最適化 (Raycast Padding)
                        // 見た目を変えずに、透明部分の判定だけを内側に追い込む
                        if (isOneByOne)
                        {
                            // 上下の空き (375-250)/2 = 62.5 を無効化
                            image.raycastPadding = new Vector4(0, 62.5f, 0, 62.5f);
                        }
                        else
                        {
                            // 左右の空き (250-227.3)/2 = 11.35 を無効化
                            image.raycastPadding = new Vector4(11.3f, 0, 11.3f, 0);
                        }

                        // アルファテストは Texture の Read/Write 設定が必要でエラーの原因となるため
                        // 今回は Raycast Padding のみで当たり判定を制御する
                        // image.alphaHitTestMinimumThreshold = 0.5f;
                    }

                    rect.localScale = card.baseScale; 
                }

                card.gameObject.SetActive(true);
                
                // Enforce strict layering: higher index means in front
                Canvas canvas = card.GetComponent<Canvas>();
                if (canvas == null)
                {
                    canvas = card.gameObject.AddComponent<Canvas>();
                }
                
                if (canvas != null)
                {
                    canvas.overrideSorting = true;
                    int order = 500 + i;
                    canvas.sortingOrder = order; // Above panels, ordered left-to-right
                    card.SetBaseSortingOrder(order); // Remember for restoration after dragging
                }
                
                if (card.gameObject.GetComponent<GraphicRaycaster>() == null)
                {
                    card.gameObject.AddComponent<GraphicRaycaster>();
                }
                
                card.transform.SetAsLastSibling(); 
                card.transform.localPosition = Vector3.zero;
                card.transform.localRotation = Quaternion.identity;

                float angle = (count > 1) ? (angleRange * 0.5f - (angleRange / (count - 1)) * i) : 0f;
                float rad = (angle + 90f) * Mathf.Deg2Rad;
                Vector2 targetPos = pivot + new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
                
                card.PlayFlyIn(targetPos, Quaternion.Euler(0, 0, angle), i * 0.1f);
            }
        }

private void Update()
        {
            if (isMatching && DonFusionNetworkManager.Instance != null && DonFusionNetworkManager.Instance.Runner != null)
            {
                var runner = DonFusionNetworkManager.Instance.Runner;
                int count = runner.ActivePlayers.Count();
                
                bool isHostLocal = runner.IsServer || runner.IsSharedModeMasterClient;

                var fm = DonGame2D.Logic.DonFusionManager2D.Instance;
                
                if (!isHostLocal && fm != null && fm.Object != null && fm.Object.IsValid && fm.GameStartCountdown.IsRunning && playersCountText != null)
                {
                    float remaining = fm.GameStartCountdown.RemainingTime(fm.Runner) ?? 0f;
                    playersCountText.text = $"ホストがゲームを開始しました！ {Mathf.CeilToInt(remaining)}秒後に開始...";
                }
                else if (playersCountText != null && isMatching && !isReady)
                {
                    if (count == 0)
                    {
                        playersCountText.text = isCpuMatch ? "CPUマッチを準備中..." : "ネットワーク接続中...";
                    }
                    else if (isRandomMatch)
                    {
                        playersCountText.text = $"現在 {count} 人\n3人以上で開始できます";
                        if (readyButton != null)
                        {
                            readyButton.interactable = (count >= 3);
                        }
                        if (readySelectionCard != null)
                        {
                            // ドラッグだけは常に許可し、ドロップ時の OnReadyClicked で人数判定を行う
                            readySelectionCard.isInteractable = true;
                        }
                    }
                    else
                    {
                        if (isHostLocal)
                        {
                            string playerStr = $"準備中（{count}人";
                            if (friendCpuCount > 0) playerStr += $"＋{friendCpuCount}人（CPU）";
                            playerStr += "）";

                            playersCountText.text = $"{playerStr}\n準備ができたら開始してください";

                            bool isNetworkReady = fm != null && fm.Object != null && fm.Object.IsValid;
                            int totalForStart = count + friendCpuCount;
                            
                            if (isNetworkReady)
                            {
                                SetHostStartButtonState(totalForStart >= 3);
                            }
                            else
                            {
                                SetHostStartButtonState(false);
                                playersCountText.text = isCpuMatch ? "ゲームを初期化中..." : "ネットワーク初期化中...";
                            }
                        }
                        else
                        {
                            string playerStr = $"準備中（{count}人";
                            if (friendCpuCount > 0) playerStr += $"＋{friendCpuCount}人（CPU）";
                            playerStr += "）";
                            playersCountText.text = $"{playerStr}\nホストの開始を待っています...";
                        }
                    }

                    var rect = playersCountText.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 500f);
                    }
                    
                    // ラウンド設定UIの同期
                    UpdateRoundSettingUI();
                }
            }
            else
            {
                // マッチング終了・キャンセル時も、確実にUIを隠すために呼び出す
                UpdateRoundSettingUI();
            }
        }

        private async void OnRandomMatchClicked()
        {
            if (isMatching) return;

            isMatching = true;
            isRandomMatch = true;
            isCpuMatch = false;
            if (randomMatchButton != null) randomMatchButton.gameObject.SetActive(false);
            if (friendMatchButton != null) friendMatchButton.gameObject.SetActive(false);
            if (cpuMatchButton != null) cpuMatchButton.gameObject.SetActive(false);
            
            HideAllCards();
            // animateCoroutine = StartCoroutine(Co_AnimateCards(randomSelectionCards));

            if (playersCountText != null) playersCountText.text = "接続中...";
            if (readyButton != null) { readyButton.gameObject.SetActive(false); readyButton.interactable = false; }

            if (DonFusionNetworkManager.Instance != null)
                await DonFusionNetworkManager.Instance.StartGame(Fusion.GameMode.Shared, null, 8);

            if (!isMatching) return;
            ShowReadyButton();
        }

        private void OnFriendMatchClicked()
        {
            isRandomMatch = false; 
            isCpuMatch = false;
            HideAllCards();
            if (friendMatchPanel != null) friendMatchPanel.SetActive(true);
            
            if (randomMatchButton != null) randomMatchButton.gameObject.SetActive(false);
            if (friendMatchButton != null) friendMatchButton.gameObject.SetActive(false);
            if (cpuMatchButton != null) cpuMatchButton.gameObject.SetActive(false);

            if (hostPanel != null) hostPanel.SetActive(false);
            if (guestPanel != null) guestPanel.SetActive(false);

            friendCpuCount = 0;
            if (cpuAddButton != null) cpuAddButton.interactable = false;
            if (cpuRemoveButton != null) cpuRemoveButton.interactable = false;
            if (cpuCountLabel != null) cpuCountLabel.text = "CPU: なし";

            if (hostButton != null) hostButton.interactable = true;
            if (guestButton != null) guestButton.interactable = true;

            animateCoroutine = StartCoroutine(Co_AnimateCards(friendSelectionCards));
        }

        private async void OnHostSelected()
        {
            HideAllCards(); // Robust hide before starting host process
            if (isMatching) return;

            generatedRoomId = GenerateRoomId();
            if (roomIdDisplayText != null) roomIdDisplayText.text = $"ルームID: {generatedRoomId}";
            if (hostPanel != null) hostPanel.SetActive(true);
            if (guestPanel != null) guestPanel.SetActive(false);
            if (hostButton != null) hostButton.gameObject.SetActive(false);
            if (guestButton != null) guestButton.gameObject.SetActive(false);

            isMatching = true;
            isRandomMatch = false;
            friendCpuCount = 0; // フレンドマッチは初期0人
            UpdateCpuCountLabel();
            if (playersCountText != null) playersCountText.text = "ルーム作成中...";

            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(false);
                readyButton.interactable = false;
            }

            if (hostStartButton != null) hostStartButton.gameObject.SetActive(false);

            if (DonFusionNetworkManager.Instance != null)
                await DonFusionNetworkManager.Instance.StartGame(Fusion.GameMode.Shared, generatedRoomId, 8);

            if (!isMatching) return;

            ShowReadyButton();
        }

        private void OnCpuMatchClicked()
        {
            isRandomMatch = false; 
            isCpuMatch = true;
            friendCpuCount = 3; // CPUマッチは初期3人
            HideAllCards();
            if (cpuMatchPanel != null) cpuMatchPanel.SetActive(true);

            if (randomMatchButton != null) randomMatchButton.gameObject.SetActive(false);
            if (friendMatchButton != null) friendMatchButton.gameObject.SetActive(false);
            if (cpuMatchButton != null) cpuMatchButton.gameObject.SetActive(false);
            
            selectedTargetPlayers = 0;
            animateCoroutine = StartCoroutine(Co_AnimateCards(cpuSelectionCards));
            
            // UI設定（CPUボタンやラウンドボタン）を更新
            UpdateRoundSettingUI();
        }

        private async void OnCpuPlayerCountSelected(int count)
        {
            if (isMatching) return;

            selectedTargetPlayers = count;
            generatedRoomId = GenerateRoomId();
            isMatching = true;
            isRandomMatch = false;
            isCpuMatch = true;
            
            if (playersCountText != null) playersCountText.text = $"CPUと対戦 ({count}人プレイ) を準備中...";
            HideAllCards();

            if (DonFusionNetworkManager.Instance != null)
            {
                // CPUマッチの場合はオフライン（Single）モードを使用する
                var mode = isCpuMatch ? Fusion.GameMode.Single : Fusion.GameMode.Shared;
                await DonFusionNetworkManager.Instance.StartGame(mode, generatedRoomId, count);
            }

            if (isMatching)
            {
                ShowReadyButton();
                // マネージャーのインスタンスが有効になるのを待ってから開始命令を送る
                StartCoroutine(Co_WaitForManagerAndStart());
            }
        }

        private System.Collections.IEnumerator Co_WaitForManagerAndStart()
        {
            float timeout = 5f;
            float elapsed = 0f;
            Debug.Log("[TitleUI] Starting Co_WaitForManagerAndStart polling...");

            while (elapsed < timeout)
            {
                if (DonGame2D.Logic.DonFusionManager2D.Instance != null && 
                    DonGame2D.Logic.DonFusionManager2D.Instance.Object != null && 
                    DonGame2D.Logic.DonFusionManager2D.Instance.Object.IsValid)
                {
                    Debug.Log("[TitleUI] DonFusionManager2D found and valid. Triggering ForceStartGameByHost.");
                    DonGame2D.Logic.DonFusionManager2D.Instance.ForceStartGameByHost(selectedTargetPlayers);
                    yield break;
                }
                elapsed += 0.2f;
                yield return new WaitForSeconds(0.2f);
            }
            Debug.LogError("[TitleUI] Timeout waiting for DonFusionManager2D instance!");
        }

        private void OnCpuBackClicked()
        {
            if (cpuMatchPanel != null) cpuMatchPanel.SetActive(false);
            
            // Pop card when using explicit back button as well
            if (selectedStack.Count > 0)
            {
                var lastCard = selectedStack[selectedStack.Count - 1];
                selectedStack.RemoveAt(selectedStack.Count - 1);
                lastCard.ResetCardState(); // Reset state so it can be re-selected
                lastCard.FlyBack();
            }

            ShowMainButtons();
        }

        private void ShowMainButtons()
        {
            HideAllCards();
            animateCoroutine = StartCoroutine(Co_AnimateCards(mainSelectionCards, 0.1f));
        }

        private void OnGuestSelected()
        {
            HideAllCards();
            if (hostPanel != null) hostPanel.SetActive(false);
            if (guestPanel != null) guestPanel.SetActive(true);
            if (hostButton != null) hostButton.gameObject.SetActive(false);
            if (guestButton != null) guestButton.gameObject.SetActive(false);

            animateCoroutine = StartCoroutine(Co_AnimateCards(guestSelectionCards));
        }

        private async void OnJoinClicked()
        {
            if (isMatching) return;
            if (roomIdInputField == null || string.IsNullOrEmpty(roomIdInputField.text)) return;

            string inputId = roomIdInputField.text.Trim().ToUpper();
            isMatching = true;
            isRandomMatch = false;
            isCpuMatch = false;
            
            HideAllCards();
            
            if (joinButton != null) joinButton.interactable = false;
            if (playersCountText != null) playersCountText.text = $"ID [{inputId}] に接続中...";

            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(false);
                readyButton.interactable = false;
            }

            if (DonFusionNetworkManager.Instance != null)
            {
                bool success = await DonFusionNetworkManager.Instance.StartGame(Fusion.GameMode.Shared, inputId, 8, isHost: false);
                
                if (!success)
                {
                    isMatching = false;
                    if (joinButton != null) joinButton.interactable = true;
                    if (roomIdInputField != null) roomIdInputField.interactable = true;
                    if (playersCountText != null) playersCountText.text = "ルームが見つかりませんでした。";
                    return;
                }
            }

            if (!isMatching) return;

            ShowReadyButton();
        }

        private void OnCopyIdClicked()
        {
            if (!string.IsNullOrEmpty(generatedRoomId))
            {
                GUIUtility.systemCopyBuffer = generatedRoomId;
                if (copyIdButton != null)
                {
                    var textCmp = copyIdButton.GetComponentInChildren<Text>();
                    if (textCmp != null) textCmp.text = "コピー完了！";
                }
            }
        }

        private void OnRandomMatchBackClicked()
        {
            CancelMatchmaking();
            ShowMainButtons();
        }

private void OnBackClicked()
        {
            Debug.Log($"[Stack] OnBackClicked triggered. Current stack count: {selectedStack.Count}");
            if (selectedStack.Count > 0)
            {
                Debug.Log($"[Stack] Stack contents: {string.Join(", ", selectedStack.Select(c => c.selectionId))}");
            }

            // Always pop one card first
            if (selectedStack.Count > 0)
            {
                var lastCard = selectedStack[selectedStack.Count - 1];
                Debug.Log($"[Stack] Popping top card: {lastCard.selectionId}");
                selectedStack.RemoveAt(selectedStack.Count - 1);
                
                // CPU設定から戻る場合、フラグをリセット
                if (lastCard.selectionId == "CPU") isCpuMatch = false;
                
                lastCard.ResetCardState(); // CRITICAL: Reset state so it can be re-selected
                lastCard.FlyBack();
                
                UpdateStackPositions(); // Refresh visual stack for remaining cards
                StartCoroutine(Co_DelayedUpdateStack(0.1f));
            }

            Debug.Log($"[Stack] Remaining stack count: {selectedStack.Count}");

            // Now decide which menu to show based on what's left in the stack
            if (selectedStack.Count == 0)
            {
                Debug.Log("[Stack] No cards left. Returning to Main Menu.");
                CancelMatchmaking(true);
            }
            else
            {
                var topCard = selectedStack[selectedStack.Count - 1];
                string topId = topCard.selectionId;
                Debug.Log($"[Stack] Top card is now: {topId}. Determining target menu...");

                if (IsSameSelectionId(topId, "friend"))
                {
                    Debug.Log("[Stack] Returning to Friend Match Role Selection (Host/Guest).");
                    CancelMatchmaking(false);
                    OnFriendMatchClicked();
                }
                else if (IsSameSelectionId(topId, "cpu"))
                {
                    Debug.Log("[Stack] Returning to CPU Player Count Selection.");
                    CancelMatchmaking(false);
                    OnCpuMatchClicked();
                }
                else
                {
                    Debug.Log($"[Stack] Unrecognized top ID '{topId}'. Defaulting to Main Menu.");
                    CancelMatchmaking(true);
                }
            }
        }

        private bool IsSameSelectionId(string id1, string id2)
        {
            if (id1 == null || id2 == null) return id1 == id2;
            string s1 = id1.ToLower().Replace(" ", "");
            string s2 = id2.ToLower().Replace(" ", "");
            return s1 == s2;
        }

        private void CancelMatchmaking(bool showMainMenu = true)
        {
            Debug.Log("CancelMatchmaking: Resetting UI stack and returning to main menu.");

            // 1. 全てのカードをスタックから解放して戻す (メインメニューに戻る時だけ全リセット)
            if (showMainMenu)
            {
                foreach (var card in selectedStack)
                {
                    if (card != null)
                    {
                        card.ResetCardState();
                        card.FlyBack();
                    }
                }
                selectedStack.Clear();
            }
            else
            {
                // 一つ前の階層に戻るだけなら、スタックには残したまま手札だけ掃除する
                // (OnBackClicked で Pop 済みのカード以外は HideAllCards で場に残される)
            }

            // 2. マッチング・readyStateのリセット
            isMatching = false;
            isReady = false;
            isRandomMatch = false;
            hostCardsShown = false;

            // 3. 実行中のUI演出を全て停止
            StopAllCoroutines();
            animateCoroutine = null;

            // 4. 全てのカードを非表示 (showMainMenu なら force=true でスタックも消す)
            HideAllCards(showMainMenu);
            
            // selectedStack.Clear() を showMainMenu の場合のみにしたため、
            // ここで改めて stack 以外を確実に掃除する

            // 5. 個別のUIパネルを非表示
            if (hostPanel != null) hostPanel.SetActive(false);
            if (guestPanel != null) guestPanel.SetActive(false);
            if (cpuMatchPanel != null) cpuMatchPanel.SetActive(false);
            if (friendMatchPanel != null) friendMatchPanel.SetActive(showMainMenu); 
            if (roundSettingPanel != null) roundSettingPanel.SetActive(false); // 確実に消す

            // 6. 各種ボタン・テキストの状態リセット
            if (playersCountText != null) playersCountText.text = "";
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(false);
                var t = readyButton.GetComponentInChildren<Text>();
                if (t != null) t.text = "READY";
            }
            if (hostStartButton != null) hostStartButton.gameObject.SetActive(false);
            if (hostButton != null) hostButton.interactable = true;
            if (guestButton != null) guestButton.interactable = true;

            if (roomIdInputField != null)
            {
                roomIdInputField.interactable = true;
                roomIdInputField.text = "";
            }
            if (copyIdButton != null)
            {
                var textCmp = copyIdButton.GetComponentInChildren<Text>();
                if (textCmp != null) textCmp.text = "コピー";
            }

            // 7. ネットワーク接続の切断
            if (DonFusionNetworkManager.Instance != null && 
                DonFusionNetworkManager.Instance.Runner != null && 
                DonFusionNetworkManager.Instance.Runner.IsRunning)
            {
                var fm = DonGame2D.Logic.DonFusionManager2D.Instance;
                if (fm != null && fm.Object != null && fm.Object.IsValid)
                {
                    fm.SetPlayerReady(false);
                }
                DonFusionNetworkManager.Instance.ShutdownNetRunner();
            }

            // 8. メインメニューのメインボタンを再表示
            if (showMainMenu)
            {
                ShowMainButtons();
            }
        }



        private void ShowReadyButton()
        {
            bool isHostLocal = DonFusionNetworkManager.Instance != null && DonFusionNetworkManager.Instance.Runner != null && (DonFusionNetworkManager.Instance.Runner.IsServer || DonFusionNetworkManager.Instance.Runner.IsSharedModeMasterClient);

            if (isRandomMatch)
            {
                // 既存readyButtonは使わず非表示に
                if (readyButton != null) readyButton.gameObject.SetActive(false);

                // まだカードが表示されていない場合、またはReadyカードを新しく出す場合に実行
                if (!isReady)
                {
                    SelectionCard[] cardsToAnimate = randomSelectionCards;

                    if (readySelectionCard != null)
                    {
                        // スプライトを差し替え
                        var img = readySelectionCard.GetComponentInChildren<UnityEngine.UI.Image>();
                        if (img != null && readyCardSprite != null)
                        {
                            img.sprite = readyCardSprite;
                            img.preserveAspect = false;
                            img.color = Color.white;
                        }
                        // 選択のコールバックを設定
                        readySelectionCard.isInteractable = (DonFusionNetworkManager.Instance?.Runner?.ActivePlayers.Count() >= 2);
                        readySelectionCard.OnCardDropped = null;
                        readySelectionCard.OnCardDropped += (card) => OnReadyClicked();

                        // [Ready, Cancel] の順でアニメーション（Readyが左、Cancelが右）
                        var combined = new SelectionCard[1 + (randomSelectionCards?.Length ?? 0)];
                        combined[0] = readySelectionCard;
                        for (int i = 0; i < (randomSelectionCards?.Length ?? 0); i++)
                            combined[i + 1] = randomSelectionCards[i];
                        
                        cardsToAnimate = combined;
                    }

                    // すでに表示済みかチェック（簡易的に一枚目のActive状態で判定）
                    bool alreadyShown = cardsToAnimate != null && cardsToAnimate.Length > 0 && cardsToAnimate[0].gameObject.activeSelf;
                    
                    if (!alreadyShown)
                    {
                        if (animateCoroutine != null) StopCoroutine(animateCoroutine);
                        animateCoroutine = StartCoroutine(Co_AnimateCards(cardsToAnimate, 0f));
                    }
                }

                if (hostStartButton != null) hostStartButton.gameObject.SetActive(false);
            }
            else
            {
                if (isHostLocal)
                {
                    // 以前のボタンを確実に非表示
                    if (hostStartButton != null) hostStartButton.gameObject.SetActive(false);
                    if (readyButton != null) readyButton.gameObject.SetActive(false);
                    
                    UpdateCpuCountLabel();

                    // カードを表示（Update側でも念のためトリガーする）
                    if (!hostCardsShown && !isCpuMatch)
                    {
                        // Error prevention: ensure parent panel is active if cards are inside it
                        if (hostPanel != null) hostPanel.SetActive(true);

                        hostCardsShown = true;
                        StartCoroutine(Co_AnimateCards(hostSelectionCards));
                    }
                }
                else
                {
                    if (readyButton != null) readyButton.gameObject.SetActive(false);
                    if (hostStartButton != null) hostStartButton.gameObject.SetActive(false);
                    if (hostSelectionCards != null) foreach (var c in hostSelectionCards) if (c != null) c.gameObject.SetActive(false);
                }
            }

            if (playersCountText != null && DonFusionNetworkManager.Instance?.Runner != null)
            {
                var runner = DonFusionNetworkManager.Instance.Runner;
                int count = runner.ActivePlayers.Count();

                if (isRandomMatch)
                {
                    playersCountText.text = $"他のプレイヤーを待っています...\n現在 {count} 人 (2人以上で開始可能)";
                }
                else
                {
                    string playerStr = $"準備中（{count}人";
                    if (friendCpuCount > 0) playerStr += $"＋{friendCpuCount}人（CPU）";
                    playerStr += "）";

                    if (isHostLocal)
                        playersCountText.text = $"{playerStr}\n準備ができたら開始してください";
                    else
                    {
                        playersCountText.text = $"{playerStr}\nホストの開始を待っています...";
                        // ゲストの場合はホスト用パネルを確実に消す
                        if (hostPanel != null) hostPanel.SetActive(false);
                        if (guestPanel != null) guestPanel.SetActive(false);
                        if (cpuCountLabel != null) cpuCountLabel.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void OnHostStartClicked()
        {
            if (!isMatching && !isCpuMatch) return;

            int currentRealPlayers = DonFusionNetworkManager.Instance?.Runner?.ActivePlayers.Count() ?? 1;
            
            // CPUマッチかつマッチング開始前（一人用設定画面）の場合
            if (isCpuMatch && !isMatching)
            {
                // 自分(1人) + 設定されたCPU数
                int totalTarget = 1 + friendCpuCount;
                if (totalTarget < 2) totalTarget = 2; // 最低2人(自分+CPU1)
                
                // CPUマッチ開始
                OnCpuPlayerCountSelected(totalTarget);
                return;
            }

            // フレンドマッチ（ホスト）の場合
            int totalForStart = currentRealPlayers + friendCpuCount;
            
            if (totalForStart < 2)
            {
                Debug.LogWarning($"[TitleUI] 人数不足: {totalForStart}人");
                return;
            }

            if (hostStartButton != null)
            {
                SetHostStartButtonState(false);
                hostStartButton.GetComponentInChildren<Text>().text = "Starting...";
            }

            var fm = DonGame2D.Logic.DonFusionManager2D.Instance;
            if (fm != null && fm.Object != null && fm.Object.IsValid)
            {
                fm.RPC_FriendMatchForceStart(totalForStart, pendingMaxRounds);
            }
            else
            {
                if (hostStartButton != null) SetHostStartButtonState(true);
            }
        }

        private void SetHostStartButtonState(bool interactable)
        {
            // NOTE: Legacy button interaction is now disabled. 
            // We only use SelectionCards (HostStartCard) for starting the match.
            if (hostStartButton != null) hostStartButton.gameObject.SetActive(false);
            if (readyButton != null) readyButton.gameObject.SetActive(false);

            // ホスト用カードのうち、IDが "StartMatch" のものの活性/非活性を制御
            if (hostSelectionCards != null)
            {
                foreach (var card in hostSelectionCards)
                    if (card != null && card.selectionId == "StartMatch")
                    {
                        // SelectionCard に interactable プロパティがないため、CanvasGroup等で見た目を変えるか
                        // あるいは SelectionCard 側を拡張する必要があります。
                        // 現状は見た目（透明度）のみを hostStartButton に倣って変更します。
                        var cg = card.GetComponent<CanvasGroup>();
                        if (cg != null)
                        {
                            cg.alpha = 1.0f; // Keep opaque as per user request
                        }
                    }
            }
        }

        private void OnReadyClicked()
        {
            if (!isMatching || isReady) return;

            var runner = DonFusionNetworkManager.Instance?.Runner;
            if (runner != null)
            {
                int playerCount = runner.ActivePlayers.Count();
                if (playerCount < 2)
                {
                    Debug.Log($"[TitleUI] Cannot ready: count {playerCount} < 2");
                    // 2人未満の場合は元の位置に戻す
                    if (readySelectionCard != null) readySelectionCard.FlyBack(false);
                    return;
                }
            }

            isReady = true;
            if (readyButton != null)
            {
                readyButton.interactable = false;
                // テキスト子要素があれば非表示に
                var txt = readyButton.GetComponentInChildren<Text>();
                if (txt != null) txt.text = "";

                // カードを薄くしてWaiting状態を表現
                var img = readyButton.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    var c = img.color;
                    c.a = 0.5f;
                    img.color = c;
                }
            }

            // SelectionCard版のReady表示対応
            if (readySelectionCard != null)
            {
                readySelectionCard.isInteractable = false;
                var cg = readySelectionCard.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0.5f;
            }

            if (DonGame2D.Logic.DonFusionManager2D.Instance != null)
            {
                DonGame2D.Logic.DonFusionManager2D.Instance.SetPlayerReady(true);
            }
        }

        /// <summary>readyButtonのImageとサイズを選択カードと統一する</summary>
        private void ApplyReadyCardStyle(Button btn)
        {
            if (btn == null) return;

            // 既存テキストを非表示
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.text = "";

            // 既存ImageをReadyButtonスプライトに差し替え
            var img = btn.GetComponent<UnityEngine.UI.Image>();
            if (img != null && readyCardSprite != null)
            {
                img.sprite = readyCardSprite;
                img.preserveAspect = false;
                img.color = Color.white;
            }

            // 他の1/1選択カードと同じサイズ・スケールに統一 (250x375, scale 1.7/1.25)
            var rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(250f, 375f);
                rt.localScale = new Vector3(1.7f, 1.25f, 1.0f);
            }
        }

        public void SwitchToGameUI()
        {
            if (titleCanvasObj != null) titleCanvasObj.SetActive(false);
            if (gameCanvasObj != null) 
            {
                gameCanvasObj.SetActive(true);
                // 修正: スケールが0になって表示されない問題を解決
                gameCanvasObj.transform.localScale = Vector3.one;
            }
        }

        private void OnCpuAddClicked()
        {
            int realPlayers = (DonFusionNetworkManager.Instance?.Runner != null) ? DonFusionNetworkManager.Instance.Runner.ActivePlayers.Count() : 1;
            int maxCpu;
            if (isCpuMatch) {
                maxCpu = 7; // CPUマッチ：最大7 (自分+7=8)
            } else {
                maxCpu = Mathf.Max(0, 8 - realPlayers); // フレンドマッチ：最大 8 - リアル人数
            }
            
            if (friendCpuCount < maxCpu)
            {
                friendCpuCount++;
                UpdateCpuCountLabel();
            }
        }

        private void OnCpuRemoveClicked()
        {
            int minCpu = isCpuMatch ? 1 : 0; // CPUマッチは最小1、フレンドは0
            if (friendCpuCount > minCpu)
            {
                friendCpuCount--;
                UpdateCpuCountLabel();
            }
        }

        private void UpdateCpuCountLabel()
        {
            if (cpuCountLabel != null)
            {
                cpuCountLabel.text = friendCpuCount.ToString();
            }

            int realPlayers = (DonFusionNetworkManager.Instance?.Runner != null) ? DonFusionNetworkManager.Instance.Runner.ActivePlayers.Count() : 1;
            int maxCpu;
            int minCpu;
            if (isCpuMatch) {
                maxCpu = 7;
                minCpu = 1;
            } else {
                maxCpu = Mathf.Max(0, 8 - realPlayers);
                minCpu = 0;
            }

            if (cpuAddButton != null) cpuAddButton.interactable = (friendCpuCount < maxCpu);
            if (cpuRemoveButton != null) cpuRemoveButton.interactable = (friendCpuCount > minCpu);
        }

        private string GenerateRoomId()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; 
            var random = new System.Random();
            var result = new System.Text.StringBuilder(6);
            for (int i = 0; i < 6; i++)
                result.Append(chars[random.Next(chars.Length)]);
            return result.ToString();
        }

        private void SetMainCardsActive(bool active)
        {
            if (mainSelectionCards == null) return;
            foreach (var card in mainSelectionCards)
            {
                if (card != null) card.gameObject.SetActive(active);
            }
        }

        private void DisableLegacyButton(GameObject root, string name)
        {
            if (root == null) return;
            var t = root.transform.GetComponentsInChildren<Transform>(true);
            foreach (var child in t)
            {
                if (child.name == name)
                {
                    child.gameObject.SetActive(false);
                    Debug.Log($"[TitleUI] Disabled legacy button: {name}");
                }
            }
        }

        private void OnRoundPlusClicked()
        {
            if (!isMatching)
            {
                if (pendingMaxRounds < 99) pendingMaxRounds++;
                UpdateRoundSettingUI();
                return;
            }
            var fm = DonGame2D.Logic.DonFusionManager2D.Instance;
            if (fm == null || fm.Runner == null || !fm.Runner.IsSharedModeMasterClient) return;
            fm.RPC_UpdateRoundSettings(fm.MaxRounds + 1);
        }

        private void OnRoundMinusClicked()
        {
            if (!isMatching)
            {
                if (pendingMaxRounds > 1) pendingMaxRounds--;
                UpdateRoundSettingUI();
                return;
            }
            var fm = DonGame2D.Logic.DonFusionManager2D.Instance;
            if (fm == null || fm.Runner == null || !fm.Runner.IsSharedModeMasterClient) return;
            if (fm.MaxRounds > 1) fm.RPC_UpdateRoundSettings(fm.MaxRounds - 1);
        }



        private void UpdateRoundSettingUI()
        {
            var fm = DonGame2D.Logic.DonFusionManager2D.Instance;
            
            // 表示判定の修正: マッチング中(フレンドマッチ/CPU戦) または CPUマッチ設定画面
            bool isFriendMatchLobby = isMatching && !isRandomMatch;
            bool isCpuSelection = isCpuMatch && !isMatching;

            if (!isFriendMatchLobby && !isCpuSelection && !(isMatching && isCpuMatch))
            {
                if (roundSettingPanel != null && roundSettingPanel.activeSelf) roundSettingPanel.SetActive(false);
                return;
            }

            // 自動取得再試行
            if (roundSettingPanel == null) TryFindRoundSettingUI();
            if (roundSettingPanel == null) return;

            // ネットワーク接続前またはホストである場合に操作可能とする
            bool isHostLocal = true;
            if (fm != null && fm.Runner != null && fm.Object != null && fm.Object.IsValid)
            {
                isHostLocal = fm.Runner.IsServer || fm.Runner.IsSharedModeMasterClient;
            }
            
            // 表示切替
            roundSettingPanel.SetActive(isHostLocal);
            
            if (roundSettingPanel == null || !roundSettingPanel.activeSelf) return;

            // グループの表示制御
            var cpuGroup = roundSettingPanel.transform.Find("CpuGroup");
            if (cpuGroup != null) cpuGroup.gameObject.SetActive(isFriendMatchLobby || isCpuSelection);

            UpdateCpuCountLabel();

            int currentMaxRounds = (isMatching && fm != null && fm.Object != null && fm.Object.IsValid) ? fm.MaxRounds : pendingMaxRounds;

            if (roundPlusButton != null) roundPlusButton.interactable = true;
            if (roundMinusButton != null) roundMinusButton.interactable = currentMaxRounds > 1;

            if (roundCountText != null)
            {
                roundCountText.text = currentMaxRounds.ToString();
                roundCountText.color = Color.white;
            }
        }

        private void TryFindRoundSettingUI()
        {
            if (roundSettingPanel == null)
            {
                var canvas = (titleCanvasObj != null) ? titleCanvasObj.transform : this.transform;
                var allRects = canvas.GetComponentsInChildren<RectTransform>(true);
                foreach (var r in allRects)
                {
                    if (r.name == "RoundSettingPanel")
                    {
                        roundSettingPanel = r.gameObject;
                        break;
                    }
                }
            }

            if (roundSettingPanel != null)
            {
                var rGroup = roundSettingPanel.transform.Find("RoundGroup");
                var cGroup = roundSettingPanel.transform.Find("CpuGroup");

                if (rGroup != null)
                {
                    // RoundGroup -> RoundLabelContainer の中にあるため
                    var container = rGroup.Find("RoundLabelContainer");
                    if (container != null)
                    {
                        roundCountText = container.Find("RoundCountLabel")?.GetComponent<Text>();
                        roundPlusButton = container.Find("PlusButton")?.GetComponent<Button>();
                        roundMinusButton = container.Find("MinusButton")?.GetComponent<Button>();
                    }
                }

                if (cGroup != null)
                {
                    // CpuGroup直下にあるため
                    cpuCountLabel = cGroup.Find("CpuCountLabel")?.GetComponent<Text>();
                    cpuAddButton = cGroup.Find("CpuAddButton")?.GetComponent<Button>();
                    cpuRemoveButton = cGroup.Find("CpuRemoveButton")?.GetComponent<Button>();
                }
                
                SetupRoundButtonListeners();

                if (cpuAddButton != null)
                {
                    cpuAddButton.onClick.RemoveAllListeners();
                    cpuAddButton.onClick.AddListener(OnCpuAddClicked);
                }
                if (cpuRemoveButton != null)
                {
                    cpuRemoveButton.onClick.RemoveAllListeners();
                    cpuRemoveButton.onClick.AddListener(OnCpuRemoveClicked);
                }
            }
        }

        private void SetupRoundButtonListeners()
        {
            if (roundPlusButton != null)
            {
                roundPlusButton.onClick.RemoveAllListeners();
                var lp = roundPlusButton.gameObject.GetComponent<LongPressButton>() ?? roundPlusButton.gameObject.AddComponent<LongPressButton>();
                lp.onLongPress.RemoveAllListeners();
                lp.onLongPress.AddListener(() => OnRoundPlusClicked());
                roundPlusButton.onClick.AddListener(() => OnRoundPlusClicked());
            }
            if (roundMinusButton != null)
            {
                roundMinusButton.onClick.RemoveAllListeners();
                var lp = roundMinusButton.gameObject.GetComponent<LongPressButton>() ?? roundMinusButton.gameObject.AddComponent<LongPressButton>();
                lp.onLongPress.RemoveAllListeners();
                lp.onLongPress.AddListener(() => OnRoundMinusClicked());
                roundMinusButton.onClick.AddListener(() => OnRoundMinusClicked());
            }

        }
    }
}
