using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DonGame2D.Models;
using DonGame2D.Logic;
using System.Linq;

namespace DonGame2D.UI
{
    public class ScoreAnimationController : MonoBehaviour
    {
        public GameUIController uiController;

        private Text _totalScoreLabelText;
        private GameObject _totalScoreLabelObj;
        private Text _subTotalLabelText;
        private GameObject _subTotalLabelObj;
        private Text _winnerNameLabelText;
        private GameObject _winnerNameLabelObj;

        public bool IsAnimating { get; private set; }

        private void Awake()
        {
            if (uiController == null)
                uiController = GetComponent<GameUIController>();
        }

        public void PlayRoundEndAnimation(int winType, int winnerId, int loserId, int donValue,
            string loserHandStr, int totalPenalty, string resultMsg, bool isFinal, string winnerNames = "", string winnerHandStr = "")
        {
            if (winType == 2) StartCoroutine(Co_PlayOutWinAnimation(winnerId, loserHandStr, isFinal));
            else StartCoroutine(Co_PlayDonWinAnimation(winType, winnerId, loserId, donValue, loserHandStr, totalPenalty, resultMsg, isFinal, winnerNames, winnerHandStr));
        }

        private IEnumerator Co_PlayDonWinAnimation(int winType, int winnerId, int loserId,
            int donValue, string loserHandStr, int totalPenalty, string resultMsg, bool isFinal, string winnerNames = "", string winnerHandStr = "")
        {
            IsAnimating = true;
            try {
                while (uiController.IsScatterAnimationRunning) yield return null;
            if (uiController.drawButton != null) uiController.drawButton.interactable = false;
            if (uiController.donButton != null) uiController.donButton.interactable = false;
            ClearRevealedHand();
            if (uiController.revealedHandContainer != null)
            {
                var lg = uiController.revealedHandContainer.GetComponent<HorizontalLayoutGroup>();
                if (lg != null) lg.enabled = false;
                var vlg = uiController.revealedHandContainer.GetComponent<VerticalLayoutGroup>();
                if (vlg != null) vlg.enabled = false;
                var glg = uiController.revealedHandContainer.GetComponent<GridLayoutGroup>();
                if (glg != null) glg.enabled = false;
            }
            SetOverlay(true);

            var scattered = uiController.GetScatteredCards();
            List<RectTransform> winnerScatteredRects = new List<RectTransform>();
            foreach (var go in scattered) if (go != null) { go.transform.SetParent(uiController.revealedHandContainer, true); winnerScatteredRects.Add(go.GetComponent<RectTransform>()); go.SetActive(false); }
            uiController.GetScatteredCards().Clear();

            int globalTotal = 0;
            string winnerDisplayName = string.IsNullOrEmpty(winnerNames) ? $"Player {winnerId}" : winnerNames;
            ShowTotalScore(globalTotal, winnerDisplayName);

            // 1. 勝者の手札表示と山札へ戻るアニメーション
            yield return StartCoroutine(Co_AnimateWinnerReveal(winnerId, winnerHandStr));
            yield return new WaitForSeconds(0.2f);

            int multiplier = (winType == 1) ? 10 : 2;
            string multLabel = (winType == 1) ? "DON-GAESHI x10" : "DON x2";
            int baseVal = donValue * 10;

            // 2. Donされた捨て札の計算演出
            // 勝者の演出完了後に捨て札を取得することで、ClearRevealedHand による破壊を回避する
            RectTransform donorCard = null;
            if (uiController.discardPileContainer.childCount > 0) {
                Transform lastChild = uiController.discardPileContainer.GetChild(uiController.discardPileContainer.childCount - 1);
                donorCard = lastChild.GetComponent<RectTransform>();
                if (donorCard != null) {
                    donorCard.SetParent(uiController.revealedHandContainer, true);
                    donorCard.gameObject.SetActive(true);
                    yield return StartCoroutine(Co_AnimateDonCalculation(baseVal, multiplier, multLabel, donorCard));
                    globalTotal += baseVal * multiplier;
                    UpdateTotalScore(globalTotal);
                    yield return new WaitForSeconds(0.6f);
                }
            }

            foreach (var rect in winnerScatteredRects) if (rect != null) { rect.gameObject.SetActive(true); StartCoroutine(Co_SuckElementToTarget(rect, _totalScoreLabelObj.transform.position, 0.4f)); yield return new WaitForSeconds(0.04f); }
            yield return new WaitForSeconds(0.3f);

            var preCapturedRects = new Dictionary<int, List<RectTransform>>();
            if (!string.IsNullOrEmpty(loserHandStr)) {
                var playersData = loserHandStr.Split('|');
                foreach (var playerData in playersData) {
                    if (string.IsNullOrEmpty(playerData)) continue;
                    var parts = playerData.Split(':');
                    if (parts.Length < 2) continue;
                    int actor = int.Parse(parts[0]);
                    var cards = ParseHandStr(parts[1]);
                    var rects = new List<RectTransform>();
                    yield return StartCoroutine(Co_CaptureHandCards(actor, cards, rects));
                    preCapturedRects[actor] = rects;
                }
            }
            if (uiController != null) uiController.SetGameMainUIActive(false);

            foreach (var kvp in preCapturedRects) {
                int actor = kvp.Key;
                // Don された対象プレイヤー（loserId）のみを加算対象とする
                if (actor != loserId) { Debug.Log($"[Animation] Skipping non-target actor {actor} in calculation."); continue; }
                var cardRects = kvp.Value;
                Debug.Log($"[Animation] Processing target loser actor {actor} with {cardRects.Count} cards.");
                GameObject nameLabel = ShowCenterTextPersistent($"Player {actor}", Color.white, 60);
                yield return new WaitForSeconds(0.4f);
                // 敗者の手札は中央（小計ラベルの下）に表示
                yield return StartCoroutine(Co_AnimateCardsToCenter(cardRects, new Vector2(0f, -300f)));
                yield return new WaitForSeconds(0.4f);
                int subTotal = 0;
                // 小計表示は削除
                foreach (var rt in cardRects) {
                    if (rt == null) continue;
                    var cui = rt.GetComponent<CardUI>();
                    int rank = (cui != null) ? cui.CardInfo.Rank : 0;
                    subTotal += rank * 10;
                    // カード吸い込み時に合計ラベルを更新するため累積値を渡す
                    yield return StartCoroutine(Co_AnimateOneCard(rt, rank, globalTotal + subTotal));
                }
                yield return new WaitForSeconds(0.4f);
                // 小計ラベルと数字の吸い込み演出は削除
                globalTotal += subTotal;
                UpdateTotalScore(globalTotal);
                if (nameLabel != null) Destroy(nameLabel);
                yield return new WaitForSeconds(0.2f);
            }
            yield return new WaitForSeconds(0.6f);
            ShowCenterText($"{winnerDisplayName}  DON  WIN!", Color.cyan);
                yield return new WaitForSeconds(2.5f);
                Cleanup();
                uiController.ShowRoundResult(resultMsg, isFinal);
                if (DonFusionManager2D.Instance != null) DonFusionManager2D.Instance.RPC_ReportAnimationFinished();
            } finally {
                IsAnimating = false;
                if (uiController != null) uiController.SetGameMainUIActive(true);
            }
        }

        private IEnumerator Co_AnimateWinnerReveal(int winnerId, string winnerHandStr)
        {
            int localId = uiController.GetLocalActorId();
            // リモートプレイヤーの場合は、演出開始前に既存の裏向き手札UI（アイコン等）を即座に非表示にしてダブりを防ぐ
            // ローカルプレイヤーの場合は移動させるため、この時点ではクリアしない
            if (winnerId != localId) 
            {
                var oppContainer = uiController.GetOpponentCardContainer(winnerId);
                if (oppContainer != null) oppContainer.gameObject.SetActive(false);
                uiController.ClearHandUI(winnerId);
            }
            
            List<RectTransform> handRects = new List<RectTransform>();
            
            if (winnerId == localId)
            {
                // ローカルプレイヤーの場合
                var myHand = uiController.GetPlayerHandUI();
                if (myHand != null && myHand.Count > 0)
                {
                    foreach (var c in myHand) if (c != null) {
                        c.transform.SetParent(uiController.revealedHandContainer, true);
                        handRects.Add(c.GetComponent<RectTransform>());
                    }
                    myHand.Clear();
                }
                
                // アニメーション中のカード（ドロー演出中など）も捕捉
                var animating = uiController.GetAnimatingDrawCards();
                if (animating != null && animating.Count > 0)
                {
                    foreach (var c in animating) if (c != null) {
                        c.transform.SetParent(uiController.revealedHandContainer, true);
                        handRects.Add(c.GetComponent<RectTransform>());
                    }
                    animating.Clear();
                }

                // まだ残っているコンテナ内のオブジェクトも捕捉 (念のため)
                if (uiController.playerHandContainer != null)
                {
                    foreach (Transform child in uiController.playerHandContainer)
                    {
                        var rt = child.GetComponent<RectTransform>();
                        if (rt != null && !handRects.Contains(rt))
                        {
                            rt.SetParent(uiController.revealedHandContainer, true);
                            handRects.Add(rt);
                        }
                    }
                }
            }
            else
            {
                // リモートプレイヤーの場合、手札を生成
                var cards = ParseHandStr(winnerHandStr);
                foreach (var c in cards)
                {
                    var cardObj = uiController.CreateCardUI(c);
                    if (cardObj != null)
                    {
                        cardObj.transform.SetParent(uiController.revealedHandContainer, false);
                        cardObj.SetActive(false); // 初期は非表示
                        var cui = cardObj.GetComponent<CardUI>();
                        if (cui != null) cui.SetFacing(false); // 最初は裏向き
                        handRects.Add(cardObj.GetComponent<RectTransform>());
                    }
                }
            }
            
            // 重要: 勝者の既存の裏向き手札UI（アイコンやダミースロット等）をここでクリアする。
            // 既に capture して revealedHandContainer に移動/生成したカードは親が違うため破壊されない。
            uiController.ClearHandUI(winnerId);
            
            if (handRects.Count == 0) yield break;
            
            // 勝者の前に出すための位置と角度を基準にする
            Vector2 revealPos = Vector2.zero;
            float baseRot = 0f;
            if (winnerId == localId) {
                revealPos = new Vector2(0f, -250f); // 手前（下）
                baseRot = 0f;
            } else {
                var container = uiController.GetOpponentCardContainer(winnerId);
                if (container != null) {
                    revealPos = uiController.revealedHandContainer.InverseTransformPoint(container.position);
                    // 中心方向へオフセット (画面端すぎないように、より「前に」)
                    Vector2 toCenter = (Vector2.zero - revealPos).normalized;
                    revealPos += toCenter * 300f;

                    // プレイヤーの向き（回転）を取得
                    var oppUI = container.GetComponentInParent<OpponentUIInfo>();
                    if (oppUI != null) baseRot = oppUI.transform.localRotation.eulerAngles.z;
                }
            }
            Debug.Log($"[Animation] Winner {winnerId} revealPos: {revealPos}, baseRot: {baseRot}");
            
            // ラベルのオフセットも回転に合わせる (プレイヤーから見て「上」へ)
            Vector2 labelOffset = Quaternion.Euler(0, 0, baseRot) * new Vector2(0, 120f);
            GameObject nameLabel = ShowCenterTextPersistent($"Winner: Player {winnerId}", Color.cyan, 60, revealPos + labelOffset);
            if (nameLabel != null) nameLabel.transform.localRotation = Quaternion.Euler(0, 0, baseRot);
            
            // 1. 指定の位置・角度に移動
            foreach (var rt in handRects)
            {
                if (rt == null) continue;
                // 初期位置を一旦 revealPos に飛ばしてから表示することで中央でのチラつきを防ぐ
                rt.anchoredPosition = revealPos;
                rt.gameObject.SetActive(true);
            }
            yield return StartCoroutine(Co_AnimateCardsToCenter(handRects, revealPos, baseRot));
            yield return new WaitForSeconds(0.4f);

            // 2. 表向きにする
            foreach (var rt in handRects)
            {
                if (rt == null) continue;
                var cui = rt.GetComponent<CardUI>();
                if (cui != null) cui.SetFacing(true);
            }
            yield return new WaitForSeconds(0.8f);

            // 3. 山札へ裏向きで戻る
            Vector3 deckPos = (uiController.deckPileContainer != null) ? uiController.deckPileContainer.position : Vector3.zero;
            foreach (var rt in handRects)
            {
                if (rt == null) continue;
                var cui = rt.GetComponent<CardUI>();
                if (cui != null) cui.SetFacing(false); // 裏向きに戻す
                StartCoroutine(Co_SuckElementToTarget(rt, deckPos, 0.5f));
                yield return new WaitForSeconds(0.05f);
            }
            yield return new WaitForSeconds(0.6f);
            
            foreach (var rt in handRects) if (rt != null) rt.gameObject.SetActive(false);
            if (nameLabel != null) Destroy(nameLabel);
            ClearRevealedHand(); // 確実に消去
            Debug.Log("[Animation] Co_AnimateWinnerReveal finished.");
        }

        private IEnumerator Co_PlayOutWinAnimation(int winnerId, string loserHandStr, bool isFinal)
        {
            IsAnimating = true;
            try {
                if (uiController.drawButton != null) uiController.drawButton.interactable = false;
            if (uiController.donButton != null) uiController.donButton.interactable = false;
            ClearRevealedHand();
            SetOverlay(true);

            var preCapturedRects = new Dictionary<int, List<RectTransform>>();
            if (!string.IsNullOrEmpty(loserHandStr))
            {
                var playersData = loserHandStr.Split('|');
                foreach (var playerData in playersData)
                {
                    if (string.IsNullOrEmpty(playerData)) continue;
                    var parts = playerData.Split(':');
                    if (parts.Length < 2) continue;
                    int actorId = int.Parse(parts[0]);
                    var cards = ParseHandStr(parts[1]);
                    var rects = new List<RectTransform>();
                    yield return StartCoroutine(Co_CaptureHandCards(actorId, cards, rects));
                    preCapturedRects[actorId] = rects;
                }
            }

            if (uiController != null) uiController.SetGameMainUIActive(false);

            int globalTotal = 0;
            string winnerName = "Player " + winnerId;
            if (uiController != null)
            {
                if (uiController.opponentUIs.TryGetValue(winnerId, out var info))
                    winnerName = info.nameText.text;
                else if (winnerId == uiController.GetLocalActorId())
                    winnerName = "You";
            }

            ShowTotalScore(globalTotal, winnerName);

            foreach (var kvp in preCapturedRects)
            {
                int actorId = kvp.Key;
                var cardRects = kvp.Value;
                GameObject nameLabel = ShowCenterTextPersistent($"Player {actorId}", Color.white, 60);
                yield return new WaitForSeconds(0.7f);
                yield return StartCoroutine(Co_AnimateCardsToCenter(cardRects, new Vector2(0f, -300f)));
                yield return new WaitForSeconds(0.3f);

                int subTotal = 0;
                // 小計表示は削除
                foreach (var rt in cardRects)
                {
                    if (rt == null) continue;
                    var cui = rt.GetComponent<CardUI>();
                    int rank = (cui != null) ? cui.CardInfo.Rank : 0;
                    subTotal += rank * 10;
                    // カード吸い込み時に合計ラベルを更新するため累積値を渡す
                    yield return StartCoroutine(Co_AnimateOneCard(rt, rank, globalTotal + subTotal));
                }
                yield return new WaitForSeconds(0.4f);

                // 小計ラベルと数字の吸い込み演出は削除
                globalTotal += subTotal;
                UpdateTotalScore(globalTotal);
                yield return new WaitForSeconds(0.4f);
                if (nameLabel != null) Destroy(nameLabel);
                yield return new WaitForSeconds(1.0f);
            }

            ShowCenterText($"{winnerName}  +{globalTotal} Credits!", Color.cyan, 70);
            yield return new WaitForSeconds(2.5f);
            Cleanup();
            uiController.ShowRoundResult($"{winnerName} OUT WIN!", isFinal);
            
                if (DonFusionManager2D.Instance != null)
                    DonFusionManager2D.Instance.RPC_ReportAnimationFinished();
            } finally {
                IsAnimating = false;
                if (uiController != null) uiController.SetGameMainUIActive(true);
            }
        }

        private IEnumerator Co_AnimateDonCalculation(int baseVal, int multiplier, string label, RectTransform overrideCard = null)
        {
            RectTransform topCard = overrideCard;
            if (topCard == null)
            {
                if (uiController.discardPileContainer == null || uiController.discardPileContainer.childCount == 0)
                {
                    Debug.LogWarning("[Animation] DonCalc: No card in discardPile and no overrideCard.");
                    yield return new WaitForSeconds(0.4f);
                    yield break;
                }
                uiController.discardPileContainer.SetAsLastSibling();
                topCard = uiController.discardPileContainer.GetChild(uiController.discardPileContainer.childCount - 1) as RectTransform;
            }

            if (topCard == null) yield break;

            // 確保エリアへ親替えて最前面へ
            if (uiController.revealedHandContainer != null) {
                topCard.SetParent(uiController.revealedHandContainer, true);
                topCard.gameObject.SetActive(true); // Ensure visibility
            }

            Vector2 startAnchored = topCard.anchoredPosition;
            Vector2 upAnchored    = startAnchored + new Vector2(0f, 60f);

            // 1. カードを浮かせるアニメーションは削除
            // yield return new WaitForSeconds(0.1f); // This line was part of the original animation flow.

            // 2. 中央へ移動させず、現在の位置（捨て場）で計算
            // Vector2 centralLocalPos = new Vector2(0f, 180f);
            // yield return StartCoroutine(Co_MoveLocal(topCard, topCard.anchoredPosition, centralLocalPos, 0.45f));
            yield return new WaitForSeconds(0.2f);
            
            // 3. 倍率とスコアの演出（ポップアップ数字）は削除
            int totalGain = baseVal * multiplier;
            yield return new WaitForSeconds(0.2f);

            // 4. 吸い込み
            if (_totalScoreLabelObj != null)
            {
                RectTransform targetTotalRt = _totalScoreLabelObj.GetComponent<RectTransform>();

                // 数字のみの吸い込みは削除
                StartCoroutine(Co_SuckElementToTarget(topCard, _totalScoreLabelObj.transform.position, 0.45f));
                
                // popup破棄は不要
                yield return new WaitForSeconds(0.3f);
                if (topCard != null) topCard.gameObject.SetActive(false);
                StartCoroutine(Co_PunchScale(targetTotalRt, 1.35f, 0.25f));
            }
            else
            {
                if (topCard != null) topCard.gameObject.SetActive(false);
            }
        }

        private IEnumerator Co_AnimateDiscardCard(int baseVal) { yield return StartCoroutine(Co_AnimateDonCalculation(baseVal, 2, "DON x2")); }
        private IEnumerator Co_AnimateDiscardCardDonGaeshi(int baseVal) { yield return StartCoroutine(Co_AnimateDonCalculation(baseVal, 10, "DON-GAESHI x10")); }

        // --- Utility methods below ---
private IEnumerator Co_CaptureHandCards(int loserId, IList<CardInfo> cards, List<RectTransform> outRects)
        {
            if (uiController == null) yield break;

            if (uiController != null) uiController.SetPlayerPeripheralActive(loserId, false);
            uiController.revealedHandContainer.SetAsLastSibling();

            var lg = uiController.revealedHandContainer.GetComponent<LayoutGroup>();
            if (lg != null) lg.enabled = false;

            int count = cards.Count;
            int localActorId = uiController.GetLocalActorId();
            bool isLocal = (loserId == localActorId);
            
            List<Transform> existingCards = new List<Transform>();
            var scattered = uiController.GetScatteredCards();
            if (scattered.Count > 0)
            {
                foreach (var go in scattered) if (go != null) existingCards.Add(go.transform);
                uiController.GetScatteredCards().Clear(); 
            }
            
            if (existingCards.Count == 0)
            {
                if (isLocal)
                {
                    var myHand = uiController.GetPlayerHandUI();
                    if (myHand != null && myHand.Count > 0)
                    {
                        foreach (var c in myHand) if (c != null) existingCards.Add(c.transform);
                        myHand.Clear();
                    }
                    
                    // 追加: アニメーション中のカードも取得（2ドロー等で演出中のカード）
                    var animating = uiController.GetAnimatingDrawCards();
                    if (animating != null && animating.Count > 0)
                    {
                        foreach (var c in animating) if (c != null) existingCards.Add(c.transform);
                        animating.Clear();
                    }

                    if (existingCards.Count == 0 && uiController.playerHandContainer != null)
                    {
                        foreach (Transform child in uiController.playerHandContainer) existingCards.Add(child);
                    }
                }
                else
                {
                    var oppContainer = uiController.GetOpponentCardContainer(loserId);
                    if (oppContainer != null)
                    {
                        oppContainer.gameObject.SetActive(false); // 演出中は即時非表示にしてダブりを防ぐ
                        foreach (Transform child in oppContainer) existingCards.Add(child);
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                RectTransform rt = null;
                Vector3 startWorldPos = Vector3.zero;
                Quaternion startWorldRot = Quaternion.identity;

                if (i < existingCards.Count && existingCards[i] != null)
                {
                    Transform existing = existingCards[i];
                    startWorldPos = existing.position;
                    startWorldRot = existing.rotation;
                    var cui = existing.GetComponent<CardUI>();
                    if (cui == null)
                    {
                        GameObject go = Instantiate(uiController.cardPrefab, uiController.revealedHandContainer);
                        rt = go.GetComponent<RectTransform>();
                        rt.position = startWorldPos;
                        rt.rotation = startWorldRot;
                        Destroy(existing.gameObject);
                        cui = rt.GetComponent<CardUI>();
                    }
                    else
                    {
                        rt = existing.GetComponent<RectTransform>();
                        rt.SetParent(uiController.revealedHandContainer, true);
                    }
                    rt.gameObject.SetActive(true);
                    rt.localScale = Vector3.one;
                    if (cui != null) cui.SetupFusion(cards[i], true);
                }
                else
                {
                    GameObject go = Instantiate(uiController.cardPrefab, uiController.revealedHandContainer);
                    CardUI cui = go.GetComponent<CardUI>();
                    if (cui != null) cui.SetupFusion(cards[i], true);
                    rt = go.GetComponent<RectTransform>();
                    Transform containerPos = isLocal ? uiController.playerHandContainer : uiController.GetOpponentCardContainer(loserId);
                    if (containerPos != null) startWorldPos = containerPos.position;
                    else startWorldPos = new Vector3(0, -1000f, 0);
                    rt.position = startWorldPos;
                    rt.gameObject.SetActive(true); // Ensure visibility
                    startWorldRot = Quaternion.identity;
                }

                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(130f, 130f * 1.4f);
                rt.localScale = Vector3.one;
                rt.position = startWorldPos;
                rt.rotation = startWorldRot;
                outRects.Add(rt);
            }
            if (uiController.revealedHandContainer != null) {
                uiController.revealedHandContainer.gameObject.SetActive(true);
                uiController.revealedHandContainer.localPosition = Vector3.zero;
            }
            Debug.Log($"[Animation] Co_CaptureHandCards for actor {loserId} finished. Captured: {outRects.Count}");
            // 重要: キャプチャ（中心へ移動）したカード以外に残っているゴミを掃除する
            uiController.ClearHandUI(loserId);
            yield return null;
        }
        private IEnumerator Co_AnimateCardsToCenter(List<RectTransform> cardRects, Vector2? pivot = null, float baseRotation = 0f)
        {
            int count = cardRects.Count;
            if (count == 0) yield break;
            
            Vector2 basePivot = pivot ?? Vector2.zero;
            float spacing = Mathf.Min(100f, 650f / Mathf.Max(1, count));
            float startX = -((count - 1) * spacing) / 2f;
            float arcHeight = 60f; // より強調された扇状の高さ
            
            for (int i = 0; i < count; i++) {
                RectTransform rt = cardRects[i];
                if (rt == null) continue;
                
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float normalizedX = t * 2f - 1f; // -1 to 1
                
                // 回転を考慮した座標計算
                float localX = startX + i * spacing;
                float localY = arcHeight * (1f - (normalizedX * normalizedX)); 
                Vector2 localOffset = new Vector2(localX, localY);
                Vector2 rotatedOffset = Quaternion.Euler(0, 0, baseRotation) * localOffset;
                
                float xPos = basePivot.x + rotatedOffset.x;
                float yPos = basePivot.y + rotatedOffset.y;

                // X座標に応じた回転 + ベース回転
                float angle = baseRotation - normalizedX * 20f; 
                
                Vector2 targetLocal = new Vector2(xPos, yPos);
                Quaternion targetRot = Quaternion.Euler(0, 0, angle);
                
                // 半同時並行で移動（わずかなディレイを挟む）
                StartCoroutine(Co_MoveLocalAndRotate(rt, rt.anchoredPosition, targetLocal, rt.localRotation, targetRot, 0.35f));
                yield return new WaitForSeconds(0.04f);
            }
            yield return new WaitForSeconds(0.4f); // 全体の移動完了を待つ
        }
        private IEnumerator Co_AnimateOneCard(RectTransform rt, int rank, int currentTotal)
        {
            if (rt == null) yield break;
            Vector2 origPos = rt.anchoredPosition;
            Vector2 upPos = origPos + new Vector2(0f, 55f);
            yield return StartCoroutine(Co_MoveLocal(rt, origPos, upPos, 0.18f));
            yield return new WaitForSeconds(0.1f);

            // 合計ラベルに向かって吸い込まれるように修正（小計をスキップ）
            if (_totalScoreLabelObj != null) {
                RectTransform targetRt = _totalScoreLabelObj.GetComponent<RectTransform>();
                yield return StartCoroutine(Co_SuckElementToTarget(rt.transform, targetRt.position, 0.45f));
                rt.gameObject.SetActive(false);
                UpdateTotalScore(currentTotal); // 逐次合計を更新
                StartCoroutine(Co_PunchScale(targetRt, 1.25f, 0.2f));
            } else { 
                UpdateTotalScore(currentTotal); 
                yield return StartCoroutine(Co_MoveLocal(rt, upPos, origPos, 0.15f)); 
            }
            yield return new WaitForSeconds(0.1f);
        }
private IEnumerator Co_SuckScoreToTarget(Vector3 startWorldPos, RectTransform targetRt, string msg, Color color, bool doPopup = true)
        {
            if (targetRt == null && !doPopup) yield break;
            Transform parent = (uiController.animationOverlay != null) ? uiController.animationOverlay.transform : uiController.transform;
            GameObject rootObj = new GameObject("SuckScoreRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            rootObj.transform.SetParent(parent, false);
            rootObj.transform.position = startWorldPos;
            Vector3 lp = rootObj.transform.localPosition; lp.z = 0; rootObj.transform.localPosition = lp;
            rootObj.transform.localScale = Vector3.one * 0.01f;
            Canvas rootCanvas = rootObj.GetComponent<Canvas>(); rootCanvas.overrideSorting = true; rootCanvas.sortingOrder = 30000;
            CanvasGroup cg = rootObj.GetComponent<CanvasGroup>(); cg.alpha = 1f; cg.blocksRaycasts = false;
            rootObj.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 200);
            GameObject textObj = new GameObject("TextDisplay", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(rootObj.transform, false);
            Text txt = textObj.GetComponent<Text>();
            if (txt != null) {
                txt.text = msg; txt.color = color;
                Font targetFont = uiController.mainFontBold ?? uiController.mainFontRegular;
                if (targetFont == null) targetFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.font = targetFont; txt.fontSize = 80; txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                var outline = textObj.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(3f, -3f);
            }
            textObj.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 200);
            if (doPopup) {
                float pDur = 0.3f; float pEl = 0f;
                while (pEl < pDur) { if (rootObj == null) yield break; pEl += Time.deltaTime; float scale = Mathf.Lerp(0f, 2.0f, Mathf.Sin((pEl / pDur) * Mathf.PI * 0.5f)); rootObj.transform.localScale = Vector3.one * scale; yield return null; }
                rootObj.transform.localScale = Vector3.one * 2.0f; yield return new WaitForSeconds(0.4f);
            } else rootObj.transform.localScale = Vector3.one * 1.5f;
            float sDur = 0.5f; float sEl = 0f; Vector3 startPos = rootObj.transform.position; Vector3 startScale = rootObj.transform.localScale;
            while (sEl < sDur) {
                if (rootObj == null) yield break; if (targetRt == null) break;
                sEl += Time.deltaTime; float easeIn = (sEl / sDur) * (sEl / sDur);
                rootObj.transform.position = Vector3.Lerp(startPos, targetRt.position, easeIn);
                rootObj.transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.4f, sEl / sDur);
                yield return null;
            }
            if (targetRt != null) StartCoroutine(Co_PunchScale(targetRt, 1.3f, 0.2f));
            Destroy(rootObj);
        }
private IEnumerator Co_SuckElementToTarget(Transform element, Vector3 targetWorldPos, float duration)
        {
            if (element == null) yield break;
            Vector3 startPos = element.position; Vector3 startScale = element.localScale; float elapsed = 0f;
            while (elapsed < duration) {
                if (element == null) yield break;
                elapsed += Time.deltaTime; float easeIn = (elapsed / duration) * (elapsed / duration);
                element.position = Vector3.Lerp(startPos, targetWorldPos, easeIn);
                element.localScale = Vector3.Lerp(startScale, Vector3.one * 0.1f, elapsed / duration);
                yield return null;
            }
            if (element != null) { element.position = targetWorldPos; element.localScale = Vector3.one * 0.1f; }
        }
private IEnumerator Co_MoveLocal(RectTransform rt, Vector2 from, Vector2 to, float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur) {
                if (rt == null) yield break;
                elapsed += Time.deltaTime; rt.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / dur));
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = to;
        }
private IEnumerator Co_MoveLocalAndRotate(RectTransform rt, Vector2 fromPos, Vector2 toPos, Quaternion fromRot, Quaternion toRot, float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur) {
                if (rt == null) yield break;
                elapsed += Time.deltaTime; float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
                rt.anchoredPosition = Vector2.Lerp(fromPos, toPos, t);
                rt.localRotation = Quaternion.Lerp(fromRot, toRot, t);
                yield return null;
            }
            if (rt != null) { rt.anchoredPosition = toPos; rt.localRotation = toRot; }
        }
private IEnumerator Co_PunchScale(RectTransform rt, float multiplier, float duration)
        {
            if (rt == null) yield break;
            Vector3 org = rt.localScale; float half = duration / 2f; float t = 0;
            while (t < half) { t += Time.deltaTime; rt.localScale = Vector3.Lerp(org, org * multiplier, t / half); yield return null; }
            t = 0; while (t < half) { t += Time.deltaTime; rt.localScale = Vector3.Lerp(org * multiplier, org, t / half); yield return null; }
            rt.localScale = org;
        }
private void ShowTotalScore(int value, string winnerName)
        {
            if (_totalScoreLabelObj != null) Destroy(_totalScoreLabelObj);
            if (_winnerNameLabelObj != null) Destroy(_winnerNameLabelObj);
            Transform parent = (uiController.animationOverlay != null) ? uiController.animationOverlay.transform : uiController.transform;
            _totalScoreLabelObj = new GameObject("TotalScoreLabel", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _totalScoreLabelObj.transform.SetParent(parent, false); _totalScoreLabelObj.transform.SetAsLastSibling();
            Canvas cv = _totalScoreLabelObj.GetComponent<Canvas>(); cv.overrideSorting = true; cv.sortingOrder = 30001;
            GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(_totalScoreLabelObj.transform, false);
            _totalScoreLabelText = txtObj.GetComponent<Text>(); _totalScoreLabelText.text = $"合計: {value}"; _totalScoreLabelText.color = Color.white; _totalScoreLabelText.fontSize = 60;
            Font tFont = uiController.mainFontBold ?? uiController.mainFontRegular; if (tFont == null) tFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _totalScoreLabelText.font = tFont; _totalScoreLabelText.fontStyle = FontStyle.Bold; _totalScoreLabelText.alignment = TextAnchor.MiddleCenter;
            RectTransform rt = _totalScoreLabelObj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -350f);
            rt.sizeDelta = new Vector2(800f, 100f);
            txtObj.AddComponent<Outline>().effectColor = Color.black;
            txtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 100f);
            _winnerNameLabelObj = new GameObject("WinnerNameLabel", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _winnerNameLabelObj.transform.SetParent(parent, false);
            _winnerNameLabelObj.transform.SetAsLastSibling();
            _winnerNameLabelObj.GetComponent<Canvas>().overrideSorting = true;
            _winnerNameLabelObj.GetComponent<Canvas>().sortingOrder = 30002;
            GameObject wTxtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            wTxtObj.transform.SetParent(_winnerNameLabelObj.transform, false);
            _winnerNameLabelText = wTxtObj.GetComponent<Text>();
            _winnerNameLabelText.text = $"Winner: {winnerName}";
            _winnerNameLabelText.color = Color.cyan;
            _winnerNameLabelText.fontSize = 48;
            _winnerNameLabelText.font = tFont;
            _winnerNameLabelText.fontStyle = FontStyle.Bold;
            _winnerNameLabelText.alignment = TextAnchor.MiddleCenter;
            _winnerNameLabelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _winnerNameLabelText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rtW = _winnerNameLabelObj.GetComponent<RectTransform>();
            rtW.anchorMin = rtW.anchorMax = new Vector2(0.5f, 1f);
            rtW.pivot = new Vector2(0.5f, 1f);
            rtW.anchoredPosition = new Vector2(0f, -430f);
            rtW.sizeDelta = new Vector2(800f, 80f);
            wTxtObj.AddComponent<Outline>().effectColor = Color.black;
            wTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 80f);
        }
        private void UpdateTotalScore(int val) { if (_totalScoreLabelText != null) _totalScoreLabelText.text = $"合計: {val}"; }
        private void ShowSubTotalScore(int value)
        {
            if (_subTotalLabelObj != null) Destroy(_subTotalLabelObj);
            Transform parent = (uiController.animationOverlay != null) ? uiController.animationOverlay.transform : uiController.transform;
            _subTotalLabelObj = new GameObject("SubTotalScoreLabel", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _subTotalLabelObj.transform.SetParent(parent, false);
            _subTotalLabelObj.transform.SetAsLastSibling();
            _subTotalLabelObj.GetComponent<Canvas>().overrideSorting = true;
            _subTotalLabelObj.GetComponent<Canvas>().sortingOrder = 30005;
            RectTransform rt = _subTotalLabelObj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -100f);
            rt.sizeDelta = new Vector2(500f, 100f);
            GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(_subTotalLabelObj.transform, false);
            _subTotalLabelText = txtObj.GetComponent<Text>();
            _subTotalLabelText.text = $"小計: {value}";
            _subTotalLabelText.color = Color.yellow;
            _subTotalLabelText.fontSize = 52;
            Font sFont = uiController.mainFontBold ?? uiController.mainFontRegular;
            if (sFont == null) sFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _subTotalLabelText.font = sFont;
            _subTotalLabelText.fontStyle = FontStyle.Bold;
            _subTotalLabelText.alignment = TextAnchor.MiddleCenter;
            _subTotalLabelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _subTotalLabelText.verticalOverflow = VerticalWrapMode.Overflow;
            txtObj.AddComponent<Outline>().effectColor = Color.black;
            txtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 100f);
        }
        private void HideSubTotalScore() { if (_subTotalLabelObj != null) { Destroy(_subTotalLabelObj); _subTotalLabelObj = null; _subTotalLabelText = null; } }
        private void UpdateSubTotalScore(int value) { if (_subTotalLabelText != null) _subTotalLabelText.text = $"小計: {value}"; }

        private GameObject ShowCenterTextPersistent(string msg, Color color, int fontSize = 60, Vector2? anchoredPos = null)
        {
            if (uiController.floatingTextPrefab == null) return null;
            Transform parent = (uiController.animationOverlay != null) ? uiController.animationOverlay.transform : uiController.transform;
            GameObject obj = Instantiate(uiController.floatingTextPrefab, parent);
            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = anchoredPos ?? Vector2.zero;
            Text txt = obj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = msg;
                txt.color = color;
                txt.fontSize = fontSize;
                txt.font = uiController.mainFontBold ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            return obj;
        }
        public List<CardInfo> ParseHandStr(string s) { var list = new List<CardInfo>(); if (string.IsNullOrEmpty(s)) return list; var cards = s.Split(';'); foreach (var c in cards) { var p = c.Split(','); if (p.Length == 2 && int.TryParse(p[0], out int st) && int.TryParse(p[1], out int r)) list.Add(new CardInfo((Suit)st, r)); } return list; }
        private void SetOverlay(bool a) { if (uiController.animationOverlay != null) uiController.animationOverlay.SetActive(a); if (uiController.revealedHandContainer != null) { uiController.revealedHandContainer.gameObject.SetActive(a); if (a) uiController.revealedHandContainer.SetAsLastSibling(); } }
        private void ClearRevealedHand() { if (uiController.revealedHandContainer != null) { foreach (Transform c in uiController.revealedHandContainer) Destroy(c.gameObject); } }
private void Cleanup()
        {
            ClearRevealedHand(); SetOverlay(false);
            if (uiController.animationOverlay != null) { foreach (Transform c in uiController.animationOverlay.transform) Destroy(c.gameObject); }
            if (_totalScoreLabelObj != null) { Destroy(_totalScoreLabelObj); _totalScoreLabelObj = null; }
            if (_subTotalLabelObj != null) { Destroy(_subTotalLabelObj); _subTotalLabelObj = null; }
            if (_winnerNameLabelObj != null) { Destroy(_winnerNameLabelObj); _winnerNameLabelObj = null; }
            if (uiController.drawButton != null) uiController.drawButton.interactable = true;
            if (uiController.donButton != null) uiController.donButton.interactable = true;
            if (uiController != null) {
                uiController.SetGameMainUIActive(true); uiController.SetAllPlayersPeripheralActive(true);
                foreach (var opUI in uiController.opponentUIs.Values) 
                {
                    if (opUI != null && opUI.cardIconContainer != null) 
                    {
                        opUI.cardIconContainer.gameObject.SetActive(true);
                        foreach (Transform c in opUI.cardIconContainer) 
                            if (c != null) c.gameObject.SetActive(true);
                    }
                }
            }
        }
        public void ShowDonFloatingText(Transform target) { ShowFloatingText(target, "Don!", Color.yellow, 100); }
        private void ShowFloatingText(Transform target, string msg, Color color, int fontSize = 52) {
            if (uiController.floatingTextPrefab == null) return;
            Transform parent = (uiController.animationOverlay != null) ? uiController.animationOverlay.transform : uiController.transform;
            Vector3 spawnPos = (target != null) ? target.position + new Vector3(0, 60f, 0) : Vector3.zero;
            GameObject obj = Instantiate(uiController.floatingTextPrefab, spawnPos, Quaternion.identity, parent);
            Text txt = obj.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = msg; txt.color = color; txt.fontSize = fontSize; txt.font = uiController.mainFontBold ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            StartCoroutine(Co_FloatUpAndDestroy(obj, 1.5f));
        }
        private void ShowCenterText(string msg, Color color, int fontSize = 65) {
            if (uiController.floatingTextPrefab == null) return;
            Transform parent = (uiController.animationOverlay != null) ? uiController.animationOverlay.transform : uiController.transform;
            GameObject obj = Instantiate(uiController.floatingTextPrefab, parent);
            obj.transform.localPosition = new Vector3(0f, -100f, 0f);
            Text txt = obj.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = msg; txt.color = color; txt.fontSize = fontSize; txt.font = uiController.mainFontBold ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            StartCoroutine(Co_FadeOutAndDestroy(obj, 2.5f));
        }
        private IEnumerator Co_FloatUpAndDestroy(GameObject target, float duration) {
            if (target == null) yield break;
            CanvasGroup cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.AddComponent<CanvasGroup>();
            Vector3 startPos = target.transform.position;
            float t = 0f;
            while (t < duration) {
                if (target == null || cg == null) yield break;
                t += Time.deltaTime;
                cg.alpha = 1f - (t / duration);
                target.transform.position = startPos + new Vector3(0f, 70f * (t / duration), 0f);
                yield return null;
            }
            if (target != null) Destroy(target);
        }

        private IEnumerator Co_FadeOutAndDestroy(GameObject target, float duration) {
            if (target == null) yield break;
            CanvasGroup cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.AddComponent<CanvasGroup>();
            yield return new WaitForSeconds(duration * 0.5f);
            float t = 0f; float fadeDur = duration * 0.5f;
            while (t < fadeDur) {
                if (target == null || cg == null) yield break;
                t += Time.deltaTime;
                cg.alpha = 1f - (t / fadeDur);
                yield return null;
            }
            if (target != null) Destroy(target);
        }
        private IEnumerator Co_Move(Transform t, Vector3 from, Vector3 to, float dur) {
            float elapsed = 0f;
            while (elapsed < dur) { if (t == null) yield break; elapsed += Time.deltaTime; t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / dur)); yield return null; }
            if (t != null) t.position = to;
        }
    }
}
