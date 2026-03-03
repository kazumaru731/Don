using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DonGame2D.Models;
using DonGame2D.Logic;
using LayoutElement = UnityEngine.UI.LayoutElement;

namespace DonGame2D.UI
{
    public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("UI Components")]
        public Image cardImage;
        public Button button;
        
        [Header("Legacy Components (Can be removed)")]
        public Text rankText;
        public Text suitText;

        [Header("Data")]
        public CardDatabase database;

        private Card cardData;
        private CardInfo cardInfoData;
        public CardInfo CardInfo => cardInfoData;
        private bool isUsingFusion = false;
        public bool isDiscarded = false; // 捨て札エリアにあるかどうかのフラグ

        // ドラッグ用の情報
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Canvas canvas;
        private Transform originalParent;
        private Vector3 originalPosition;
        private int originalSiblingIndex;
        public bool IsDragging { get; private set; } = false;
        private Vector2 dragOffset; // ドラッグ開始時のオフセット

        private void Awake()
        {
            EnsureComponents();

            // スケールの強制リセット（子が巨大化しているケースへの対策）
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                t.localScale = Vector3.one;
            }

            // カードの固定サイズ（全カード共通、比率1:1.4）
            rectTransform.sizeDelta = new Vector2(100f, 140f);

            // LayoutElementで固定サイズをレイアウトに伝える（ガタツキ防止）
            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 100f;
            layoutElement.preferredHeight = 140f;
            layoutElement.minWidth = 100f;
            layoutElement.minHeight = 140f;
            layoutElement.flexibleWidth = 0;
            layoutElement.flexibleHeight = 0;
        }

        private void EnsureComponents()
        {
            if (this == null) return; 
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null) rectTransform = gameObject.AddComponent<RectTransform>();

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (cardImage == null)
            {
                cardImage = GetComponent<Image>();
                if (cardImage == null) cardImage = GetComponentInChildren<Image>();
                if (cardImage == null) cardImage = gameObject.AddComponent<Image>();
            }

            if (database == null)
            {
                database = Resources.Load<CardDatabase>("CardDatabase");
                if (database == null)
                {
                    var uiCtrl = Object.FindObjectOfType<GameUIController>();
                    if (uiCtrl != null) database = uiCtrl.cardDatabase;
                }
            }
            
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
        }

        private Vector3 targetPosition;
        private bool isSmoothMoving = false;
        private float moveSpeed = 15f;

        private void Update()
        {
            if (isSmoothMoving)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
                if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
                {
                    transform.position = targetPosition;
                    isSmoothMoving = false;
                }
            }
        }

        public void SmoothMoveTo(Vector3 targetWorldPos)
        {
            if (IsDragging) return; // ドラッグ中は自動移動を拒否
            targetPosition = targetWorldPos;
            isSmoothMoving = true;
        }

        public void SetImmediatePosition(Vector3 worldPos)
        {
            transform.position = worldPos;
            targetPosition = worldPos;
            isSmoothMoving = false;
        }


        // 既存の DonGameManager 用
        public void Setup(Card card, bool isFaceUp)
        {
            cardData = card;
            isUsingFusion = false;
            if (rectTransform != null) rectTransform.localRotation = Quaternion.identity;

            UpdateVisuals(card.suit, card.rank, isFaceUp);
        }

        // 今回新設する Fusion2 用
        public void SetupFusion(CardInfo cardInfo, bool isFaceUp)
        {
            EnsureComponents(); // 生成直後の呼び出しに備えてコンポーネントを確保
            cardInfoData = cardInfo;
            isUsingFusion = true;
            if (rectTransform != null) rectTransform.localRotation = Quaternion.identity;

            UpdateVisuals(cardInfo.Suit, cardInfo.Rank, isFaceUp);
        }

        private void UpdateVisuals(Suit suit, int rank, bool isFaceUp)
        {
            if (cardImage == null) EnsureComponents(); // 念のため再チェック
            if (database == null)
            {
                // データベースがない場合のフォールバック（レガシーテキスト表示）
                if (isFaceUp)
                {
                    if (rankText) rankText.text = GetRankString(rank);
                    if (suitText) 
                    {
                        suitText.text = GetSuitSymbol(suit);
                        suitText.color = (suit == Suit.Hearts || suit == Suit.Diamonds) ? Color.red : Color.black;
                        if (rankText) rankText.color = suitText.color;
                    }
                    if (cardImage) cardImage.color = Color.white;
                }
                else
                {
                    if (rankText) rankText.text = "";
                    if (suitText) suitText.text = "";
                    if (cardImage) cardImage.color = Color.gray;
                }
                return;
            }

            // データベースがある場合はスプライトをセット
            if (rankText != null) rankText.gameObject.SetActive(false);
            if (suitText != null) suitText.gameObject.SetActive(false);

            if (cardImage != null)
            {
                cardImage.preserveAspect = false; // 比率を維持せず枠内いっぱいに完全一致サイズで表示する
                if (isFaceUp)
                {
                    Sprite s = database.GetCardSprite(suit, rank);
                    if (s != null)
                    {
                        cardImage.sprite = s;
                        cardImage.color = Color.white;
                    }
                    else
                    {
                        // 同期遅延などで画像が見つからない場合は裏面を表示して白塗りを防ぐ
                        cardImage.sprite = database.GetCardBack();
                        cardImage.color = Color.white;
                    }
                }
                else
                {
                    cardImage.sprite = database.GetCardBack();
                    cardImage.color = Color.white;
                }
            }
            else
            {
                Debug.LogWarning("CardUI: cardImage is not assigned!");
            }
        }

        public void ChangeSpriteWithFade(Sprite newSprite, float duration = 0.5f)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(FadeSpriteCoroutine(newSprite, duration));
            }
            else
            {
                if (cardImage != null)
                {
                    cardImage.sprite = newSprite;
                    cardImage.color = Color.white;
                }
            }
        }

        private System.Collections.IEnumerator FadeSpriteCoroutine(Sprite newSprite, float duration)
        {
            if (cardImage == null) yield break;

            Color startColor = cardImage.color;
            Color transparent = new Color(startColor.r, startColor.g, startColor.b, 0f);
            float halfDuration = duration / 2f;
            float elapsed = 0f;

            // Fade Out
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                cardImage.color = Color.Lerp(startColor, transparent, elapsed / halfDuration);
                yield return null;
            }

            cardImage.sprite = newSprite;
            elapsed = 0f;

            // Fade In
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                cardImage.color = Color.Lerp(transparent, startColor, elapsed / halfDuration);
                yield return null;
            }

            cardImage.color = startColor;
        }

        public void OnClick()
        {
            if (isUsingFusion && DonFusionManager2D.Instance != null)
            {
                DonFusionManager2D.Instance.TryPlayCard(cardInfoData);
            }
            else if (!isUsingFusion && DonGameManager.Instance != null)
            {
                DonGameManager.Instance.TryPlayCard(DonGameManager.Instance.players[0], cardData);
            }
        }

        private string GetRankString(int rank)
        {
            switch (rank)
            {
                case 1: return "A";
                case 11: return "J";
                case 12: return "Q";
                case 13: return "K";
                default: return rank.ToString();
            }
        }

        private string GetSuitSymbol(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spades: return "♠";
                case Suit.Hearts: return "♥";
                case Suit.Diamonds: return "♦";
                case Suit.Clubs: return "♣";
                default: return "";
            }
        }

        #region Drag and Drop Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isUsingFusion || isDiscarded) return; 

            IsDragging = true;
            isSmoothMoving = false; // 自動移動を停止
            
            EnsureComponents(); // ドラッグ開始時にコンポーネントを確実に確保

            originalParent = transform.parent;
            originalPosition = rectTransform.position;
            originalSiblingIndex = transform.GetSiblingIndex();

            // ドラッグ開始時のマウス位置（Canvas空間）とカードの中心位置の差分を保持する
            if (canvas != null)
            {
                RectTransform canvasRect = canvas.transform as RectTransform;
                Vector2 mouseLocalPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, 
                    eventData.position, 
                    canvas.worldCamera, 
                    out mouseLocalPoint);
                
                // 現在のカードのCanvas内でのローカル座標を取得
                // 親を変える前の世界座標をCanvasのローカル座標に変換
                Vector2 cardLocalPoint = canvasRect.InverseTransformPoint(transform.position);
                dragOffset = cardLocalPoint - mouseLocalPoint;

                transform.SetParent(canvas.transform, true);
                transform.SetAsLastSibling();

                // 座標計算を簡単にするため、アンカーとピボットを中央に合わせる
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                
                // オフセットを維持して配置
                rectTransform.anchoredPosition = mouseLocalPoint + dragOffset;
            }

            canvasGroup.blocksRaycasts = false; 
            
            Debug.Log($"[Drag] Begin: {gameObject.name}, OriginalPos: {originalPosition}, NewAnchored: {rectTransform.anchoredPosition}");
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isUsingFusion || !IsDragging || canvas == null) return;
            
            // Canvas(親)の空間におけるローカル座標系に変換
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, 
                eventData.position, 
                canvas.worldCamera, 
                out localPoint))
            {
                rectTransform.anchoredPosition = localPoint + dragOffset;
                
                // リアルタイム並べ替え: ドラッグ中にインデックスを計算し、変化があればモデルを更新
                if (DonFusionManager2D.Instance != null && !isDiscarded)
                {
                    var hand = new System.Collections.Generic.List<CardInfo>(DonFusionManager2D.Instance.myLocalHand);
                    int oldIndex = hand.FindIndex(c => c.SuitInt == cardInfoData.SuitInt && c.Rank == cardInfoData.Rank);
                    
                    if (oldIndex != -1)
                    {
                        int newIndex = CalculateNewIndex(eventData.position);
                        if (newIndex != oldIndex)
                        {
                            CardInfo card = hand[oldIndex];
                            hand.RemoveAt(oldIndex);
                            if (newIndex > hand.Count) newIndex = hand.Count;
                            hand.Insert(newIndex, card);
                            
                            // 同期的にデータを更新。これにより GameUIController.UpdateFusionHandUI -> UpdateHandWithAnimation が呼ばれる
                            DonFusionManager2D.Instance.SetLocalHand(hand);
                        }
                    }
                }

                // 異常値が計算されていたらログを出す
                if (float.IsNaN(localPoint.x) || Mathf.Abs(localPoint.x) > 10000)
                {
                    Debug.LogWarning($"[Drag] Invalid Position Detected! ScreenPos: {eventData.position}, LocalPos: {localPoint}");
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isUsingFusion) return;
            IsDragging = false; // 最初にフラグを折ることで、直後の UI 更新で正しい重なり順になるようにする
            canvasGroup.blocksRaycasts = true;

            bool isDroppedOnDiscard = false;

            var gameUI = Object.FindObjectOfType<GameUIController>();
            if (gameUI != null && gameUI.discardPileContainer != null)
            {
                RectTransform discardRect = gameUI.discardPileContainer.GetComponent<RectTransform>();
                if (RectTransformUtility.RectangleContainsScreenPoint(discardRect, eventData.position, eventData.pressEventCamera))
                {
                    isDroppedOnDiscard = true;
                }
            }

            if (isDroppedOnDiscard && DonFusionManager2D.Instance != null)
            {
                // ドロップ成功として判定処理を呼ぶ
                bool canPlay = DonFusionManager2D.Instance.TryPlayCard(cardInfoData);
                if (canPlay)
                {
                    // ルール合致。提出成功。
                    // 自分を破壊せず、UpdateFusionHandUIによる再生成に任せるため一旦手札の位置情報をリセット
                    ReturnToHand();
                    return;
                }
            }
            else if (!isDroppedOnDiscard && DonFusionManager2D.Instance != null)
            {
                // 手札コンテナ内での並べ替え判定
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)originalParent, eventData.position, eventData.pressEventCamera))
                {
                    var hand = new System.Collections.Generic.List<CardInfo>(DonFusionManager2D.Instance.myLocalHand);
                    
                    int oldIndex = hand.FindIndex(c => c.SuitInt == cardInfoData.SuitInt && c.Rank == cardInfoData.Rank);
                    if (oldIndex == -1) oldIndex = originalSiblingIndex;

                    if (oldIndex >= 0 && oldIndex < hand.Count)
                    {
                        int newIndex = CalculateNewIndex(eventData.position);
                        CardInfo card = hand[oldIndex];
                        hand.RemoveAt(oldIndex);
                        
                        if (newIndex > hand.Count) newIndex = hand.Count;
                        
                        hand.Insert(newIndex, card);
                        DonFusionManager2D.Instance.SetLocalHand(hand);
                        
                        // 並べ替え成功。
                        // 追って呼ばれる GameUIController の ApplyHandPositionsAfterLayout で位置を確定させる
                        return;
                    }
                }
            }

            // ルールに合わない、あるいはエリア外にドロップした場合は元の手札の位置に戻す
            ReturnToHand();

            // 最後にUIを強制更新して位置と重なり順を同期させる
            if (gameUI != null) gameUI.UpdateFusionHandUI(true);
        }

        private int CalculateNewIndex(Vector2 screenPosition)
        {
            int index = 0;
            var gameUI = Object.FindObjectOfType<GameUIController>();
            if (gameUI == null || gameUI.playerHandContainer == null) return index;

            foreach (Transform child in gameUI.playerHandContainer)
            {
                // ダミースロットのスクリーン座標を取得
                Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, child.position);
                
                // カードの横並び順を判定
                if (screenPosition.x > slotScreenPos.x)
                {
                    index++;
                }
            }
            return index;
        }

        public void ReturnToHand()
        {
            var gameUI = Object.FindObjectOfType<GameUIController>();
            // 親を手札ビジュアルコンテナに戻す
            if (gameUI != null && gameUI.handVisualParent != null)
            {
                transform.SetParent(gameUI.handVisualParent, true);
            }
            else
            {
                transform.SetParent(originalParent, true);
            }
            
            // 固定のインデックスに戻すのではなく、IsDragging を解除して
            // 次の UpdateHierarchy で正しい順序になるようにする
            IsDragging = false; 
        }

        #endregion
    }
}
