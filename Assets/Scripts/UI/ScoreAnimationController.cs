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

        [Header("New Assets")]
        public Sprite chipSprite;

        private class ActorCreditUI
        {
            public GameObject root;
            public Text creditText;
            public Text nameText;
            public int currentDisplayValue;
            public RectTransform rectTransform;
        }

        private Dictionary<int, ActorCreditUI> _sessionActorsUI = new Dictionary<int, ActorCreditUI>();
        private GameObject _uiContainer;
        private RectTransform _creditRoot; // [NEW] スコアUIを他の演出物から隔離するための親コンテナ

        public bool IsAnimating { get; private set; }

        private void Awake()
        {
            if (uiController == null)
                uiController = GetComponent<GameUIController>();
        }

        public void PlayRoundEndAnimation(int winType, int winnerId, int loserId, int donValue,
            string loserHandStr, int totalPenalty, string resultMsg, bool isFinal, string winnerNames = "", string winnerHandStr = "")
        {
            if (IsAnimating) return;

            // [FIX] 演出フラグが立つ「前」に、現在の座席位置と向きを確定させる
            if (uiController != null) uiController.UpdateOpponentsUI();

            if (winType == 2) StartCoroutine(Co_PlayOutWinAnimation(winnerId, loserHandStr, totalPenalty, isFinal));
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
            SetupUIContainer();
            _sessionActorsUI.Clear();

            SetOverlay(true);

            // 1. クレジットカードUIの配置
            var fm = DonFusionManager2D.Instance;
            int winnerCurrentCredits = (fm != null && fm.PlayerCredits.ContainsKey(winnerId)) ? fm.PlayerCredits[winnerId] : 0;
            int loserCurrentCredits = (fm != null && fm.PlayerCredits.ContainsKey(loserId)) ? fm.PlayerCredits[loserId] : 0;

            // [FIX] 表示上の開始値を決定
            // 敗者はサーバーで既に減算済みなので、表示は「減算前」から開始する
            int loserStartVal = loserCurrentCredits + totalPenalty;
            
            // 勝者はサーバーでまだ加算されていない（演出後に加算される）ので、表示は「現在の値（加算前）」から開始する
            int winnerStartVal = winnerCurrentCredits;

            // クレジット表示をアニメーション開始時の値でロック
            uiController.SetDisplayedCredit(winnerId, winnerStartVal);
            uiController.SetDisplayedCredit(loserId, loserStartVal);

            // 勝者を上部に配置
            var wUI = CreateActorCreditCardUI(winnerId, new Vector2(0, 200), true, winnerStartVal);
            if (!string.IsNullOrEmpty(winnerNames)) wUI.nameText.text = winnerNames;

            // 敗者を下部に配置 (Don勝利は1人の敗者)
            var lUI = CreateActorCreditCardUI(loserId, new Vector2(0, -100), false, loserStartVal);

            // [FIX] 生成・配置直後のワールド座標を確定させるため、レイアウトを強制更新する
            if (_uiContainer != null) {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_uiContainer.GetComponent<RectTransform>());
            }

            yield return new WaitForSeconds(0.5f);

            // [FIX] ドン返しの場合（winType == 1）、まず最初に元のDonプレイヤー（このラウンドの敗者）の手札を「DON!」として公開する演出を追加
            if (winType == 1 && !string.IsNullOrEmpty(loserHandStr))
            {
                var parts = loserHandStr.Split('|');
                string targetHandStr = "";
                foreach (var p in parts) {
                    var items = p.Split(':');
                    if (items.Length > 1 && items[0] == loserId.ToString()) {
                        targetHandStr = items[1]; break;
                    }
                }
                if (!string.IsNullOrEmpty(targetHandStr)) {
                    yield return StartCoroutine(Co_AnimateWinnerReveal(loserId, targetHandStr, "DON!"));
                    yield return new WaitForSeconds(0.4f);
                }
            }

            // 2. 勝者の手札表示（演出用）: ドン返しなら専用のテキストを表示
            string winPrefix = (winType == 1) ? "DON-GAESHI!" : "DON!";
            yield return StartCoroutine(Co_AnimateWinnerReveal(winnerId, winnerHandStr, winPrefix));
            yield return new WaitForSeconds(0.2f);

            int multiplier = (winType == 1) ? 10 : 2;
            string multLabel = (winType == 1) ? "DON-GAESHI x10" : "DON x2";
            int baseVal = donValue * 10;
            int mainPenalty = baseVal * multiplier;
            int totalHandPenalty = 0;

            // 3. Donされた捨て札の計算演出 -> 敗者のカードへ
            if (uiController.discardPileContainer.childCount > 0) {
                RectTransform donorCard = null;
                for (int i = uiController.discardPileContainer.childCount - 1; i >= 0; i--) {
                    Transform c = uiController.discardPileContainer.GetChild(i);
                    if (c.GetComponent<CardUI>() != null) {
                        donorCard = c.GetComponent<RectTransform>();
                        break;
                    }
                }
                
                if (donorCard != null) {
                    if (_uiContainer == null) SetupUIContainer();
                    
                    donorCard.SetParent(_uiContainer.transform, true);
                    donorCard.anchorMin = new Vector2(0.5f, 0.5f);
                    donorCard.anchorMax = new Vector2(0.5f, 0.5f);
                    donorCard.pivot = new Vector2(0.5f, 0.5f);
                    donorCard.localRotation = Quaternion.identity;
                    donorCard.localScale = Vector3.one;
                    donorCard.SetAsLastSibling();

                    // 3. 中央への強調移動
                    yield return StartCoroutine(Co_AnimateDonCalculation(baseVal, multiplier, "", donorCard));
                    yield return new WaitForSeconds(0.15f);

                    // 4. 吸い込みフェーズ（敗者へ）
                    if (lUI != null && lUI.rectTransform != null) {
                        donorCard.SetAsLastSibling();
                        yield return StartCoroutine(Co_SuckElementToTarget(donorCard, lUI.rectTransform.position, 0.5f));
                        
                        // 敗者側のスコアを減らす（勝者への転送は最後に一括）
                        int curL = lUI.currentDisplayValue;
                        yield return StartCoroutine(Co_UpdateActorCredit(loserId, curL - mainPenalty, 0.25f));
                        ShowFloatingText(lUI.rectTransform, $"-{mainPenalty}", Color.red, 40);
                    }
                    
                    Destroy(donorCard.gameObject);
                }
            }

            // 4. 敗者の手札があればそれも同様に処理
            if (!string.IsNullOrEmpty(loserHandStr)) {
                var playersData = loserHandStr.Split('|');
                foreach (var playerData in playersData) {
                    if (string.IsNullOrEmpty(playerData)) continue;
                    var parts = playerData.Split(':');
                    if (parts.Length < 2) continue;
                    int actor = int.Parse(parts[0]);
                    if (actor != loserId) continue;

                    var cards = ParseHandStr(parts[1]);
                    List<RectTransform> rects = new List<RectTransform>();
                    yield return StartCoroutine(Co_CaptureHandCards(actor, cards, rects));

                    if (rects.Count > 0)
                    {
                        yield return StartCoroutine(Co_AnimateCardsToCenter(rects, lUI.rectTransform.anchoredPosition + new Vector2(0, -200)));
                        
                        foreach (var rt in rects)
                        {
                            if (rt == null) continue;
                            var cui = rt.GetComponent<CardUI>();
                            int rank = (cui != null) ? cui.CardInfo.Rank : 0;
                            int cardPenalty = (winType == 1) ? 0 : rank * 10;

                            yield return StartCoroutine(Co_SuckElementToTarget(rt, lUI.rectTransform.position, 0.3f));
                            Destroy(rt.gameObject);

                            if (cardPenalty > 0)
                            {
                                totalHandPenalty += cardPenalty;
                                int cur = lUI.currentDisplayValue;
                                yield return StartCoroutine(Co_UpdateActorCredit(loserId, cur - cardPenalty, 0.2f));
                                ShowFloatingText(lUI.rectTransform, $"-{cardPenalty}", Color.red, 40);
                            }
                        }
                    }
                }
            }

            // 5. 最後に「すべての合計分（Don分 + 手札分）」を一括で勝者へ飛ばす
            int grandTotalToWinner = mainPenalty + totalHandPenalty;
            if (grandTotalToWinner > 0)
            {
                yield return StartCoroutine(Co_SpawnChipAndSuck(loserId, winnerId, grandTotalToWinner));
                // 勝者の表示値を計算上の最終値に更新して固定（サーバー同期前の不整合防止）
                yield return StartCoroutine(Co_UpdateActorCredit(winnerId, winnerStartVal + grandTotalToWinner, 0.4f, false));
            }

            yield return new WaitForSeconds(0.5f);
            ShowCenterText($"{winnerNames ?? ("Player " + winnerId)}  WIN!", Color.cyan);
            yield return new WaitForSeconds(0.5f);

            // [FIX] 吸い込み演出を廃止し、数値の最終同期とフェードアウトのみを行う
            if (_sessionActorsUI.ContainsKey(winnerId))
            {
                wUI = _sessionActorsUI[winnerId];
                int localActorId = uiController.GetLocalActorId();
                
                // 数値を最終同期（サーバーとの不整合防止のため、計算値を優先）
                int finalValue = winnerStartVal + grandTotalToWinner;
                uiController.UpdateDisplayedCredit(winnerId, finalValue);
                
                if (DonFusionManager2D.Instance != null && DonFusionManager2D.Instance.PlayerCredits.ContainsKey(winnerId)) {
                    int serverVal = DonFusionManager2D.Instance.PlayerCredits[winnerId];
                    if (serverVal != 0) uiController.UpdateDisplayedCredit(winnerId, serverVal);
                }
                
                // 全員フェードアウトして消去
                yield return StartCoroutine(Co_FadeOutAndDestroy(wUI.root, 0.4f));
            }

            Cleanup();

            uiController.ShowRoundResult(resultMsg, isFinal);
            if (DonFusionManager2D.Instance != null) DonFusionManager2D.Instance.RPC_ReportAnimationFinished();
            }
            finally { IsAnimating = false; if (uiController != null) uiController.SetGameMainUIActive(true); }
        }

        private IEnumerator Co_AnimateWinnerReveal(int winnerId, string winnerHandStr, string prefixText = null)
        {
            int localId = uiController.GetLocalActorId();

            // 2026-03-30 改善: LayoutGroup の干渉を完全に防ぐため無効化
            if (uiController.revealedHandContainer != null)
            {
                var lg = uiController.revealedHandContainer.GetComponent<UnityEngine.UI.LayoutGroup>();
                if (lg != null) lg.enabled = false;
            }

            UnityEngine.Vector2 revealPos = UnityEngine.Vector2.zero;
            float baseRot = 0f;

            if (winnerId == localId)
            {
                revealPos = new Vector2(0f, -250f); // 手前（下）
                baseRot = 0f;
            }
            else
            {
                var container = uiController.GetOpponentCardContainer(winnerId);
                if (container != null)
                {
                    // [FIX] 親を _uiContainer に変更するため、そちら基準で座標取得
                    RectTransform rootRT = _uiContainer.GetComponent<RectTransform>();
                    revealPos = rootRT.InverseTransformPoint(container.position);

                    // オフセット：画面の中心へ寄せる
                    Vector2 overlayCenter = rootRT.rect.center;
                    Vector2 toCenter = (overlayCenter - revealPos).normalized;
                    revealPos += toCenter * 250f;

                    // 相手の向きを取得
                    var oppUI = container.GetComponentInParent<OpponentUIInfo>();
                    if (oppUI != null)
                    {
                        baseRot = oppUI.transform.localRotation.eulerAngles.z;
                    }
                    else
                    {
                        baseRot = GuessRotationByActorId(winnerId);
                    }
                }
                else
                {
                    // フォールバック：座席位置から推測
                    revealPos = GuessRevealPositionByActorId(winnerId);
                    baseRot = GuessRotationByActorId(winnerId);
                }
            }

            // リモートプレイヤーの場合は、座標確定後に既存のアイコン等を非表示にする
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
                    foreach (var c in myHand)
                    {
                        if (c != null)
                        {
                            c.transform.SetParent(_uiContainer.transform, true);
                            handRects.Add(c.GetComponent<RectTransform>());
                        }
                    }
                    myHand.Clear();
                }

                // アニメーション中のカード（ドロー演出中など）も捕捉
                var animating = uiController.GetAnimatingDrawCards();
                if (animating != null && animating.Count > 0)
                {
                    foreach (var c in animating)
                    {
                        if (c != null)
                        {
                            c.transform.SetParent(_uiContainer.transform, true);
                            handRects.Add(c.GetComponent<RectTransform>());
                        }
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
                            rt.SetParent(_uiContainer.transform, true);
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
                        cardObj.transform.SetParent(_uiContainer.transform, false);
                        cardObj.SetActive(false); // 初期は非表示
                        var cui = cardObj.GetComponent<CardUI>();
                        if (cui != null) cui.SetFacing(false); // 最初は裏向き
                        handRects.Add(cardObj.GetComponent<RectTransform>());
                    }
                }
            }

            // 重要: 勝者の既存の裏向き手札UI（アイコンやダミースロット等）をここでクリアする。
            uiController.ClearHandUI(winnerId);

            if (handRects.Count == 0) yield break;

            // ラベルのオフセットも回転に合わせる (プレイヤーから見て「上」へ)
            Vector2 labelOffset = Quaternion.Euler(0, 0, baseRot) * new Vector2(0, 120f);
            
            // カスタムのテキストがあればそれを利用する
            string labelTxt = string.IsNullOrEmpty(prefixText) ? $"Winner: Player {winnerId}" : $"{prefixText} Player {winnerId}";
            GameObject nameLabel = ShowCenterTextPersistent(labelTxt, Color.cyan, 60, revealPos + labelOffset);
            if (nameLabel != null) nameLabel.transform.localRotation = Quaternion.Euler(0, 0, baseRot);

            // 1. 指定の位置・角度に移動
            foreach (var rt in handRects)
            {
                if (rt == null) continue;
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

        private IEnumerator Co_PlayOutWinAnimation(int winnerId, string loserHandStr, int totalPenalty, bool isFinal)
        {
            IsAnimating = true;
            try {
                if (uiController.drawButton != null) uiController.drawButton.interactable = false;
            if (uiController.donButton != null) uiController.donButton.interactable = false;
            
            ClearRevealedHand();
            SetupUIContainer();
            _sessionActorsUI.Clear();

            SetOverlay(true);

            // 1. クレジットカードUIの配置
            var fm = DonFusionManager2D.Instance;
            int winnerCurrentCredits = (fm != null && fm.PlayerCredits.ContainsKey(winnerId)) ? fm.PlayerCredits[winnerId] : 0;
            // [FIX] 表示上の開始値を決定
            // 勝者はまだサーバーで加算されていないので、現在の値を開始値とする（逆算によるマイナス表示を防ぐ）
            int winnerStartVal = winnerCurrentCredits;

            // 勝者を上部に配置
            var wUI = CreateActorCreditCardUI(winnerId, new Vector2(0, 200), true, winnerStartVal);

            // 敗者を抽出
            List<int> loserIds = new List<int>();
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
                    loserIds.Add(actorId);
                    var cards = ParseHandStr(parts[1]);
                    List<RectTransform> rects = new List<RectTransform>();
                    yield return StartCoroutine(Co_CaptureHandCards(actorId, cards, rects));
                    preCapturedRects[actorId] = rects;
                }
            }

            // 敗者カードのレイアウト計算
            int loserCount = loserIds.Count;
            for (int i = 0; i < loserCount; i++)
            {
                int actorId = loserIds[i];
                float x = 0;
                float y = 0;
                
                if (loserCount <= 4)
                {
                    // 1段配置
                    float totalWidth = (loserCount - 1) * 220f;
                    x = -totalWidth / 2f + i * 220f;
                    y = -80f;
                }
                else
                {
                    // 2段配置
                    int row = i / 4;
                    int col = i % 4;
                    int countInRow = (row == 0) ? 4 : (loserCount - 4);
                    float totalWidth = (countInRow - 1) * 220f;
                    x = -totalWidth / 2f + col * 220f;
                    y = (row == 0) ? 20f : -180f;
                }

                // 敗者のペナルティを逆算して初期値を設定
                int loserStartVal = 0;
                if (fm != null && fm.PlayerCredits.ContainsKey(actorId)) {
                    int currentCredits = fm.PlayerCredits[actorId];
                    // その敗者の手札文字列を探してペナルティを計算
                    int penalty = 0;
                    var parts = loserHandStr.Split('|');
                    foreach (var p in parts) {
                        var sub = p.Split(':');
                        if (sub.Length >= 2 && int.Parse(sub[0]) == actorId) {
                            penalty = ParseHandStr(sub[1]).Sum(c => c.Rank) * 10;
                            break;
                        }
                    }
                    loserStartVal = currentCredits + penalty;
                }

                // [FIX] クレジット表示を計算前の値でロックする
                uiController.SetDisplayedCredit(actorId, loserStartVal);

                CreateActorCreditCardUI(actorId, new Vector2(x, y), false, loserStartVal);
            }

            // [FIX] 生成・配置直後のワールド座標を確定させるため、レイアウトを強制更新する
            if (_uiContainer != null) {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_uiContainer.GetComponent<RectTransform>());
            }

            // 勝者の表示も固定
            if (fm != null && fm.PlayerCredits.ContainsKey(winnerId))
            {
                uiController.SetDisplayedCredit(winnerId, winnerStartVal);
            }

            if (uiController != null) uiController.SetGameMainUIActive(false);
            yield return new WaitForSeconds(0.5f);

            // [FIX] 勝者の数値を逐次加算するために現在の表示値を管理
            int runningWinnerValue = winnerStartVal;

            // 2. 勝者の演出（ここではOut勝利の名前を表示）
            string winnerName = _sessionActorsUI[winnerId].nameText.text;
            yield return StartCoroutine(Co_AnimateWinnerReveal(winnerId, "", "WINNER!")); // Out勝利時は手札公開済み

            // 3. 各敗者の手札を自分のカードへ
            foreach (var actorId in loserIds)
            {
                if (!preCapturedRects.ContainsKey(actorId)) continue;
                var lUI = _sessionActorsUI[actorId];
                var cardRects = preCapturedRects[actorId];

                // 敗者の手札を自分のカードの近くに集める
                yield return StartCoroutine(Co_AnimateCardsToCenter(cardRects, lUI.rectTransform.anchoredPosition + new Vector2(0, -180)));
                
                int totalLCCPenalty = 0;
                foreach (var rt in cardRects)
                {
                    if (rt == null) continue;
                    var cui = rt.GetComponent<CardUI>();
                    int rank = (cui != null) ? cui.CardInfo.Rank : 0;
                    int penalty = rank * 10;
                    totalLCCPenalty += penalty;

                    // 吸い込みとチップ生成
                    yield return StartCoroutine(Co_SuckElementToTarget(rt, lUI.rectTransform.position, 0.3f));
                    Destroy(rt.gameObject);
                    
                    // 個別のカードごとにチップを飛ばさず、一旦数値を減らす演出のみ
                    int currentFrom = lUI.currentDisplayValue;
                    yield return StartCoroutine(Co_UpdateActorCredit(actorId, currentFrom - penalty, 0.15f));
                    ShowFloatingText(lUI.rectTransform, $"-{penalty}", Color.red, 35);
                }
                // 合計額に加算
                runningWinnerValue += totalLCCPenalty;
            } // END of foreach (var actorId in loserIds)
 
            // 4. すべての敗者からの減算が完了した後、一括加算（勝者へ）
            if (runningWinnerValue > winnerStartVal)
            {
                // 各敗者から少しずつチップを飛ばす、もしくは合計分を代表して1つ飛ばす
                // ユーザーの要望「まとめて加算」に合わせ、最後に代表して1回（または各プレイヤーから流れるように）加算
                foreach (var actorId in loserIds)
                {
                    // その敗者が支払った分を計算（本来は保持しておくべきだが、簡易的に再計算または全額を代表者から）
                    // ここでは各敗者のコンテナからチップを発生させる
                    yield return StartCoroutine(Co_SpawnChipAndSuck(actorId, winnerId, 0)); // 0指定で演出のみ
                }
                
                // 数値を最終値へ
                yield return StartCoroutine(Co_UpdateActorCredit(winnerId, runningWinnerValue, 0.5f, false));
            }

            yield return new WaitForSeconds(0.5f);
            ShowCenterText($"{winnerName}  OUT  WIN!", Color.cyan);
            yield return new WaitForSeconds(0.5f);
            
            // [FIX] 吸い込み演出を廃止し、数値の最終同期とフェードアウトのみを行う
            if (_sessionActorsUI.ContainsKey(winnerId))
            {
                wUI = _sessionActorsUI[winnerId];
                int localActorId = uiController.GetLocalActorId();
                
                // 数値を最終同期（バッファ更新）
                // 数値を最終同期（計算値を優先）
                uiController.UpdateDisplayedCredit(winnerId, runningWinnerValue);
                
                if (DonFusionManager2D.Instance != null && DonFusionManager2D.Instance.PlayerCredits.ContainsKey(winnerId)) {
                    int serverVal = DonFusionManager2D.Instance.PlayerCredits[winnerId];
                    if (serverVal != 0) uiController.UpdateDisplayedCredit(winnerId, serverVal);
                }
                
                // フェードアウト
                yield return StartCoroutine(Co_FadeOutAndDestroy(wUI.root, 0.4f));
            }

            Cleanup();
            uiController.ShowRoundResult($"{winnerName}  OUT  WIN!", isFinal);
            
            } // END of try
            finally {
                IsAnimating = false;
                if (DonFusionManager2D.Instance != null) DonFusionManager2D.Instance.RPC_ReportAnimationFinished();
                if (uiController != null) uiController.SetGameMainUIActive(true);
            }
        }

        private IEnumerator Co_AnimateDonCalculation(int baseVal, int multiplier, string label, RectTransform overrideCard = null)
        {
            RectTransform topCard = overrideCard;
            if (topCard == null) yield break;

            // overrideCardが渡された場合はすでに_uiContainer下に配置済みのため
            // SetParentは行わず、そのまま現在位置から中央へアニメーションする
            if (overrideCard == null && _uiContainer != null) {
                topCard.SetParent(_uiContainer.transform, true);
                
                // 座標計算の狂いを防ぐため、アンカーとピボットを中央にリセット
                topCard.anchorMin = new Vector2(0.5f, 0.5f);
                topCard.anchorMax = new Vector2(0.5f, 0.5f);
                topCard.pivot = new Vector2(0.5f, 0.5f);
                
                topCard.SetAsLastSibling();
                topCard.gameObject.SetActive(true);
                // レイアウト確定
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_uiContainer.GetComponent<RectTransform>());
            }

            // [IMPROVED] 現在位置から中央（anchoredPosition = 0,0）へ移動させる
            Vector2 centralLocalPos = new Vector2(0f, 0f); // 画面中央ど真ん中
            Vector3 targetScale = Vector3.one * 2.0f;     // 大きく表示

            // 移動と拡大
            yield return StartCoroutine(Co_MoveLocalAndScale(topCard, topCard.anchoredPosition, centralLocalPos, topCard.localScale, targetScale, 0.4f));
            
            // 強調時間
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator Co_AnimateDiscardCard(int baseVal) { yield return StartCoroutine(Co_AnimateDonCalculation(baseVal, 2, "DON x2")); }
        private IEnumerator Co_AnimateDiscardCardDonGaeshi(int baseVal) { yield return StartCoroutine(Co_AnimateDonCalculation(baseVal, 10, "DON-GAESHI x10")); }

        // --- Utility methods below ---
private IEnumerator Co_CaptureHandCards(int loserId, IList<CardInfo> cards, List<RectTransform> outRects)
        {
            if (uiController == null) yield break;

            if (uiController != null) uiController.SetPlayerPeripheralActive(loserId, false);
            // [FIXED] 個別コンテナではなく、演出用オーバーレイ（_uiContainer）を最前面へ
            if (_uiContainer != null) _uiContainer.transform.SetAsLastSibling();

            // [FIXED] レイアウトグループの干渉を防ぐ（念のため個別コンテナ側も）
            if (uiController != null && uiController.revealedHandContainer != null) {
                var lg = uiController.revealedHandContainer.GetComponent<LayoutGroup>();
                if (lg != null) lg.enabled = false;
            }

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
                        // [FIXED] 親を _uiContainer に変更
                        GameObject go = Instantiate(uiController.cardPrefab, _uiContainer.transform);
                        rt = go.GetComponent<RectTransform>();
                        rt.position = startWorldPos;
                        rt.rotation = startWorldRot;
                        Destroy(existing.gameObject);
                        cui = rt.GetComponent<CardUI>();
                    }
                    else
                    {
                        rt = existing.GetComponent<RectTransform>();
                        // [FIXED] 親を _uiContainer に変更
                        rt.SetParent(_uiContainer.transform, true);
                    }
                    rt.gameObject.SetActive(true);
                    rt.localScale = Vector3.one;
                    if (cui != null) cui.SetupFusion(cards[i], true);
                }
                else
                {
                    // [FIXED] 親を _uiContainer に変更
                    GameObject go = Instantiate(uiController.cardPrefab, _uiContainer.transform);
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
            float spacing = Mathf.Min(110f, 750f / Mathf.Max(1, count)); // 少し間隔を広げる
            float startX = -((count - 1) * spacing) / 2f;
            float arcHeight = 85f; // より強調された扇状の高さ（弧を深める）
            
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
                
                // カードの重なり順を調整（端から中心に向かって重なるように）
                rt.SetAsLastSibling();
                
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
        private IEnumerator Co_MoveLocalAndScale(RectTransform rt, Vector2 fromPos, Vector2 toPos, Vector3 fromScale, Vector3 toScale, float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur) {
                if (rt == null) yield break;
                elapsed += Time.deltaTime; 
                float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
                rt.anchoredPosition = Vector2.Lerp(fromPos, toPos, t);
                rt.localScale = Vector3.Lerp(fromScale, toScale, t);
                yield return null;
            }
            if (rt != null) { rt.anchoredPosition = toPos; rt.localScale = toScale; }
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
        private void ClearRevealedHand()
        {
            if (uiController != null && uiController.revealedHandContainer != null)
            {
                foreach (Transform c in uiController.revealedHandContainer)
                {
                    if (c != null) Destroy(c.gameObject);
                }
            }

            // [FIXED] 構造的な保護: _creditRoot 以外のすべての子要素を削除する
            if (_uiContainer != null)
            {
                foreach (Transform child in _uiContainer.transform)
                {
                    if (child == null) continue;
                    
                    // スコア表示のルート（_creditRoot）は絶対に消さない
                    if (_creditRoot != null && child == _creditRoot) continue;

                    // それ以外（カード、演出テキスト、チップ等）はすべて削除
                    Destroy(child.gameObject);
                }
            }
        }
        private void Cleanup()
        {
            ClearRevealedHand(); SetOverlay(false);
            if (uiController.animationOverlay != null) { foreach (Transform c in uiController.animationOverlay.transform) Destroy(c.gameObject); }
            if (_totalScoreLabelObj != null) { Destroy(_totalScoreLabelObj); _totalScoreLabelObj = null; }
            if (_subTotalLabelObj != null) { Destroy(_subTotalLabelObj); _subTotalLabelObj = null; }
            if (_winnerNameLabelObj != null) { Destroy(_winnerNameLabelObj); _winnerNameLabelObj = null; }
            if (_uiContainer != null) { Destroy(_uiContainer); _uiContainer = null; }
            _sessionActorsUI.Clear();
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
        public void ShowFloatingText(Transform target, string msg, Color color, int fontSize = 52) {
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

        private void SetupUIContainer()
        {
            if (_uiContainer != null) return;
            _uiContainer = new GameObject("ScoreAnimationUIContainer", typeof(RectTransform));
            _uiContainer.transform.SetParent(uiController.animationOverlay != null ? uiController.animationOverlay.transform : uiController.transform, false);
            var rt = _uiContainer.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // [NEW] スコアUI専用の保護コンテナを作成
            GameObject cr = new GameObject("CreditRoot", typeof(RectTransform));
            _creditRoot = cr.GetComponent<RectTransform>();
            _creditRoot.SetParent(_uiContainer.transform, false);
            _creditRoot.anchorMin = Vector2.zero; _creditRoot.anchorMax = Vector2.one;
            _creditRoot.offsetMin = Vector2.zero; _creditRoot.offsetMax = Vector2.zero;
        }

        private ActorCreditUI CreateActorCreditCardUI(int actorId, Vector2 anchoredPos, bool isWinner, int customStartValue = -1)
        {
            if (uiController.creditCardObject == null) return null;
            
            // [PROTECTED] 親を _creditRoot に設定
            GameObject cardObj = Instantiate(uiController.creditCardObject, (_creditRoot != null ? _creditRoot : _uiContainer.transform));
            cardObj.SetActive(true);
            var rt = cardObj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            cardObj.transform.localScale = Vector3.one * 0.8f; 
            
            // デザインをそのまま使用するため、中のTextを探す
            Text[] allTexts = cardObj.GetComponentsInChildren<Text>(true);
            Text valTxt = null;
            Text nTxt = null;

            if (allTexts.Length >= 1) valTxt = allTexts[0]; 
            if (allTexts.Length >= 2) nTxt = allTexts[1];

            if (nTxt == null)
            {
                GameObject nameObj = new GameObject("PlayerName", typeof(RectTransform), typeof(Text));
                nameObj.transform.SetParent(cardObj.transform, false);
                var nameRt = nameObj.GetComponent<RectTransform>();
                nameRt.anchoredPosition = new Vector2(0, 40); 
                nTxt = nameObj.GetComponent<Text>();
                nTxt.font = uiController.mainFontBold ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                nTxt.fontSize = 24;
                nTxt.alignment = TextAnchor.MiddleCenter;
                nTxt.color = Color.white;
            }

            var fm = DonFusionManager2D.Instance;
            int startVal = customStartValue;
            if (startVal == -1) // 指定がない場合のみ現在値を読み込む
            {
                startVal = (fm != null && fm.PlayerCredits.ContainsKey(actorId)) ? fm.PlayerCredits[actorId] : 0;
            }

            nTxt.text = (actorId == -1) ? "Winner" : $"Player {actorId}";
            valTxt.text = startVal.ToString();

            ActorCreditUI ui = new ActorCreditUI {
                root = cardObj,
                creditText = valTxt,
                nameText = nTxt,
                currentDisplayValue = startVal,
                rectTransform = rt
            };
            _sessionActorsUI[actorId] = ui;
            return ui;
        }

        private IEnumerator Co_UpdateActorCredit(int actorId, int targetValue, float duration = 0.5f, bool updateGlobalUI = true)
        {
            if (!_sessionActorsUI.ContainsKey(actorId)) yield break;
            var ui = _sessionActorsUI[actorId];
            int startVal = ui.currentDisplayValue;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // [IMPROVED] MissingReferenceException 防止のための厳格な Null チェック
                if (ui == null || ui.root == null || ui.creditText == null) yield break;

                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                int current = (int)Mathf.Lerp(startVal, targetValue, t);
                
                // 再度チェック（1フレーム待機後の安全性）
                if (ui.creditText != null) {
                    ui.creditText.text = current.ToString();
                }
                yield return null;
            }
            if (ui != null && ui.creditText != null) {
                ui.creditText.text = targetValue.ToString();
                ui.currentDisplayValue = targetValue;
            }
            
            // [FIX] グローバル表示 (右上のメインクレジット) への反映を制御
            if (updateGlobalUI && uiController != null) {
                uiController.UpdateDisplayedCredit(actorId, targetValue);
            }
        }

        private IEnumerator Co_SpawnChipAndSuck(int fromActorId, int toActorId, int valueAmount)
        {
            if (!_sessionActorsUI.ContainsKey(fromActorId) || !_sessionActorsUI.ContainsKey(toActorId)) yield break;
            if (chipSprite == null) yield break;

            float yOffset = -80f; 
            Vector3 startPos = _sessionActorsUI[fromActorId].rectTransform.position + new Vector3(0, yOffset, 0);
            
            Vector3 endPos = _sessionActorsUI[toActorId].rectTransform.position + new Vector3(0, yOffset, 0);

            GameObject chip = new GameObject("Chip", typeof(RectTransform), typeof(Image));
            chip.transform.SetParent(_uiContainer.transform, false);
            chip.transform.position = startPos;
            var img = chip.GetComponent<Image>();
            img.sprite = chipSprite;
            img.SetNativeSize();
            chip.transform.localScale = Vector3.one * 0.25f;

            // チップの下に数値Labelを追加
            GameObject valObj = new GameObject("Value", typeof(RectTransform), typeof(Text));
            valObj.transform.SetParent(chip.transform, false);
            var valRt = valObj.GetComponent<RectTransform>();
            valRt.anchoredPosition = new Vector2(0, -60); // チップの下側
            var valTxt = valObj.GetComponent<Text>();
            valTxt.text = valueAmount.ToString();
            valTxt.font = uiController.mainFontBold ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valTxt.fontSize = 60; // チップ自体を小さくしているので大きめに設定
            valTxt.alignment = TextAnchor.MiddleCenter;
            valTxt.color = Color.white;
            valTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            valTxt.verticalOverflow = VerticalWrapMode.Overflow;

            // チップが勝者へ飛ぶ
            yield return StartCoroutine(Co_SuckElementToTarget(chip.GetComponent<RectTransform>(), endPos, 0.5f));

            // 勝者のクレジットを増やす演出
            int currentTo = _sessionActorsUI[toActorId].currentDisplayValue;
            StartCoroutine(Co_UpdateActorCredit(toActorId, currentTo + valueAmount, 0.2f));
            
            // 勝者側にプラス数値を表示
            ShowFloatingText(_sessionActorsUI[toActorId].rectTransform, $"+{valueAmount}", Color.green, 40);

            Destroy(chip);
        }
        private Vector2 GuessRevealPositionByActorId(int targetActorId)
        {
            var fm = DonFusionManager2D.Instance;
            if (fm == null) return new Vector2(0, 300);

            int localId = uiController.GetLocalActorId();
            int total = fm.Actors.Length;
            int myIdx = fm.GetActorIndex(localId);
            int targetIdx = fm.GetActorIndex(targetActorId);

            if (myIdx < 0 || targetIdx < 0) return new Vector2(0, 300);

            int relIdx = (targetIdx - myIdx + total) % total;
            
            // 4人対戦を想定した簡易配置（画面端から内側へ）
            if (total == 4) {
                switch (relIdx) {
                    case 1: return new Vector2(-400, 0); // 左
                    case 2: return new Vector2(0, 300);   // 上
                    case 3: return new Vector2(400, 0);  // 右
                }
            } else {
                // 8人などの場合は角度から計算
                float angle = 270f - (360f / total) * relIdx;
                float rad = angle * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(rad) * 400f, Mathf.Sin(rad) * 300f);
            }
            return new Vector2(0, 300);
        }

        private float GuessRotationByActorId(int targetActorId)
        {
            var fm = DonFusionManager2D.Instance;
            if (fm == null) return 180f;

            int localId = uiController.GetLocalActorId();
            int total = fm.Actors.Length;
            int myIdx = fm.GetActorIndex(localId);
            int targetIdx = fm.GetActorIndex(targetActorId);

            if (myIdx < 0 || targetIdx < 0) return 180f;

            int relIdx = (targetIdx - myIdx + total) % total;
            
            // プレイヤーの座席位置（270, 180, 90, 0）に +90 した値をベースとする
            float seatAngle = 270f - (360f / total) * relIdx;
            return seatAngle + 90f;
        }
    }
}
