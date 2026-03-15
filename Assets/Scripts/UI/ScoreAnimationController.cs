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

        // 上部スコア表示用（動的生成）
        private Text _totalScoreLabelText;
        private GameObject _totalScoreLabelObj;
        
        // 小計スコア表示用（動的生成）
        private Text _subTotalLabelText;
        private GameObject _subTotalLabelObj;

        // 勝利者名表示用
        private Text _winnerNameLabelText;
        private GameObject _winnerNameLabelObj;

        public bool IsAnimating { get; private set; } // 演出実行中フラグ

        private void Awake()
        {
            if (uiController == null)
                uiController = GetComponent<GameUIController>();
        }

        public void PlayRoundEndAnimation(int winType, int winnerId, int loserId, int donValue,
            string loserHandStr, int totalPenalty, string resultMsg, bool isFinal, string winnerNames = "")
        {
            Debug.Log($"[Animation] ScoreAnimationController.PlayRoundEndAnimation: winType={winType}, winnerId={winnerId}, loserId={loserId}, handStr='{loserHandStr}'");
            if (winType == 2) // OUT WIN
            {
                Debug.Log("[Animation] Starting Co_PlayOutWinAnimation");
                StartCoroutine(Co_PlayOutWinAnimation(winnerId, loserHandStr, isFinal));
            }
            else // DON
            {
                Debug.Log($"[Animation] Starting Co_PlayDonWinAnimation (winType={winType})");
                StartCoroutine(Co_PlayDonWinAnimation(winType, winnerId, loserId, donValue,
                    loserHandStr, totalPenalty, resultMsg, isFinal, winnerNames));
            }
        }

        private IEnumerator Co_PlayDonWinAnimation(int winType, int winnerId, int loserId,
            int donValue, string loserHandStr, int totalPenalty, string resultMsg, bool isFinal, string winnerNames = "")
        {
            IsAnimating = true;
            while (uiController.IsScatterAnimationRunning)
            {
                yield return null;
            }

            // 投げられたカードを山札へ回収する演出を追加
            if (uiController.deckPileContainer != null)
            {
                yield return StartCoroutine(uiController.Co_RecallScatteredCards(uiController.deckPileContainer.position));
            }

            if (uiController.drawButton != null) uiController.drawButton.interactable = false;
            if (uiController.donButton  != null) uiController.donButton.interactable  = false;

            ClearRevealedHand();
            SetOverlay(true);

            int globalTotal = 0;
            // winnerNames があればそれを使い、なければ Player X 形式
            string winnerDisplayName = string.IsNullOrEmpty(winnerNames) ? $"Player {winnerId}" : winnerNames;
            ShowTotalScore(globalTotal, winnerDisplayName);

            if (winType == 1) // DON-GAESHI
            {
                int baseVal = donValue * 10;
                yield return StartCoroutine(Co_AnimateDonCalculation(baseVal, 10, "DON-GAESHI x10"));

                // 吸い込み後に加算
                globalTotal += baseVal * 10;
                UpdateTotalScore(globalTotal);
                yield return new WaitForSeconds(1.0f);

                string winLabel = "DON-GAESHI";
                ShowCenterText($"{winnerDisplayName}  {winLabel}  WIN!\n+{globalTotal} Credits", Color.cyan);
                yield return new WaitForSeconds(2.5f);
            }
            else // NORMAL/MULTI DON
            {
                int baseVal = donValue * 10;
                yield return StartCoroutine(Co_AnimateDonCalculation(baseVal, 2, "DON x2"));

                // 吸い込み後に加算 (ドン額分)
                globalTotal += baseVal * 2;
                UpdateTotalScore(globalTotal);
                yield return new WaitForSeconds(0.6f);

                var cards = ParseHandStr(loserHandStr);
                var cardRects = new List<RectTransform>();
                yield return StartCoroutine(Co_FlyInHandCards(loserId, cards, cardRects));
                yield return new WaitForSeconds(0.3f);

                // 中央に対象プレイヤー名を表示
                GameObject loserNameLabel = ShowCenterTextPersistent($"Player {loserId}", Color.white, 60);
                yield return new WaitForSeconds(0.3f);

                // アニメーション用に手札をめくるだけ（スコア加算は既に行い済みとするか、視覚演出として行う）
                int subTotal = 0;
                ShowSubTotalScore(subTotal);
                foreach (var rt in cardRects)
                {
                    if (rt == null) continue;
                    var cui  = rt.GetComponent<CardUI>();
                    int rank = (cui != null) ? cui.CardInfo.Rank : 0;
                    int addValue = rank * 10;
                    subTotal += addValue;
                    
                    yield return StartCoroutine(Co_AnimateOneCard(rt, rank, subTotal));
                }
                yield return new WaitForSeconds(0.4f);

                // 小計を総合計に吸い込ませる
                if (_subTotalLabelObj != null && _totalScoreLabelObj != null)
                {
                    RectTransform targetRt = _totalScoreLabelObj.GetComponent<RectTransform>();
                    Vector3 startPos = _subTotalLabelObj.transform.position;
                    HideSubTotalScore();
                    yield return StartCoroutine(Co_SuckScoreToTarget(startPos, targetRt, $"+{subTotal}", Color.yellow, false));
                    
                    // 吸い込み完了後に合計へ加算
                    globalTotal += subTotal;
                    UpdateTotalScore(globalTotal);
                }
                else
                {
                    HideSubTotalScore();
                    // 安全策として加算しておく
                    globalTotal += subTotal;
                    UpdateTotalScore(globalTotal);
                }

                if (loserNameLabel != null) Destroy(loserNameLabel);
                yield return new WaitForSeconds(0.6f);

                string winLabel = "DON";
                ShowCenterText($"{winnerDisplayName}  {winLabel}  WIN!", Color.cyan);
                yield return new WaitForSeconds(2.5f);
            }

            Cleanup();
            uiController.ShowRoundResult(resultMsg, isFinal);

            if (DonFusionManager2D.Instance != null)
                DonFusionManager2D.Instance.RPC_ReportAnimationFinished();

            IsAnimating = false;
        }

        private IEnumerator Co_PlayOutWinAnimation(int winnerId, string loserHandStr, bool isFinal)
        {
            IsAnimating = true;
            if (uiController.drawButton != null) uiController.drawButton.interactable = false;
            if (uiController.donButton  != null) uiController.donButton.interactable  = false;

            ClearRevealedHand();
            SetOverlay(true);

            int globalTotal = 0;
            ShowTotalScore(globalTotal, $"Player {winnerId}");

            if (!string.IsNullOrEmpty(loserHandStr))
            {
                // カンマ区切りの手札情報を分解して各プレイヤーの手札をめくる演出
                var playersData = loserHandStr.Split('|');
                foreach (var playerData in playersData)
                {
                    if (string.IsNullOrEmpty(playerData)) continue;
                    var parts = playerData.Split(':');
                    if (parts.Length < 2) continue;

                    int actorId = int.Parse(parts[0]);
                    string cardsData = parts[1];

                    ClearRevealedHand();

                    // プレイヤー名を表示（あがり計算中は消さない）
                    GameObject nameLabel = ShowCenterTextPersistent($"Player {actorId}", Color.white, 60);
                    yield return new WaitForSeconds(0.7f);

                    var cards    = ParseHandStr(cardsData);
                    var cardRects = new List<RectTransform>();
                    yield return StartCoroutine(Co_FlyInHandCards(actorId, cards, cardRects));
                    yield return new WaitForSeconds(0.3f);

                    int subTotal = 0;
                    ShowSubTotalScore(subTotal);

                    foreach (var rt in cardRects)
                    {
                        if (rt == null) continue;
                        var cui  = rt.GetComponent<CardUI>();
                        int rank = (cui != null) ? cui.CardInfo.Rank : 0;
                        
                        subTotal += rank * 10;
                        yield return StartCoroutine(Co_AnimateOneCard(rt, rank, subTotal));
                    }

                    yield return new WaitForSeconds(0.4f);

                    // 小計を総合計に吸い込ませる
                    if (_subTotalLabelObj != null && _totalScoreLabelObj != null)
                    {
                        RectTransform targetRt = _totalScoreLabelObj.GetComponent<RectTransform>();
                        Vector3 startPos = _subTotalLabelObj.transform.position;
                        HideSubTotalScore();
                        yield return StartCoroutine(Co_SuckScoreToTarget(startPos, targetRt, $"+{subTotal}", Color.yellow, false));
                    }
                    else
                    {
                        HideSubTotalScore();
                    }

                    // 小計を総合計に加算
                    globalTotal += subTotal;
                    UpdateTotalScore(globalTotal);

                    yield return new WaitForSeconds(0.4f);

                    // 計算が終わったら名前表示を消す
                    if (nameLabel != null) Destroy(nameLabel);
                    yield return new WaitForSeconds(1.0f);
                }

                ShowCenterText($"Player {winnerId}  +{globalTotal} Credits!", Color.cyan, 70);
                yield return new WaitForSeconds(2.5f);
            }

            Cleanup();
            uiController.ShowRoundResult($"Player {winnerId} OUT WIN!", isFinal);

            if (DonFusionManager2D.Instance != null)
                DonFusionManager2D.Instance.RPC_ReportAnimationFinished();

            IsAnimating = false;
        }

        private IEnumerator Co_AnimateDiscardCard(int baseVal)
        {
            yield return StartCoroutine(Co_AnimateDonCalculation(baseVal, 2, "DON x2"));
        }

        private IEnumerator Co_AnimateDiscardCardDonGaeshi(int baseVal)
        {
            yield return StartCoroutine(Co_AnimateDonCalculation(baseVal, 10, "DON-GAESHI x10"));
        }

        private IEnumerator Co_AnimateDonCalculation(int baseVal, int multiplier, string label)
        {
            if (uiController.discardPileContainer == null || uiController.discardPileContainer.childCount == 0)
            {
                yield return new WaitForSeconds(0.4f);
                yield break;
            }

            uiController.discardPileContainer.SetAsLastSibling();
            Transform topCard = uiController.discardPileContainer.GetChild(uiController.discardPileContainer.childCount - 1);

            Vector3 origPos = topCard.position;
            Vector3 upPos = origPos + new Vector3(0, 100f, 0);

            // 1. カードを浮かせる (数字表示は廃止)
            yield return StartCoroutine(Co_Move(topCard, origPos, upPos, 0.35f));
            yield return new WaitForSeconds(0.25f);

            // 2. カードを合計スコアへ吸い込ませる
            if (_totalScoreLabelObj != null)
            {
                RectTransform targetTotalRt = _totalScoreLabelObj.GetComponent<RectTransform>();
                yield return StartCoroutine(Co_SuckElementToTarget(topCard, targetTotalRt.position, 0.5f));
                
                // カードを非表示にする
                topCard.gameObject.SetActive(false);
                
                // スコアUIのヒット演出
                StartCoroutine(Co_PunchScale(targetTotalRt, 1.35f, 0.25f));
            }
            else
            {
                // 吸い込み先がない場合は戻す（フォールバック）
                yield return StartCoroutine(Co_Move(topCard, upPos, origPos, 0.2f));
            }
        }


        private IEnumerator Co_FlyInHandCards(int loserId, List<CardInfo> cards, List<RectTransform> outRects)
        {
            if (cards.Count == 0 || uiController.revealedHandContainer == null ||
                uiController.cardPrefab == null) yield break;

            uiController.revealedHandContainer.SetAsLastSibling();

            var lg = uiController.revealedHandContainer.GetComponent<LayoutGroup>();
            if (lg != null) lg.enabled = false;

            int count = cards.Count;
            float spacing = Mathf.Min(80f, 600f / Mathf.Max(1, count));
            float startX = -((count - 1) * spacing) / 2f;
            float arcHeight = count > 2 ? 30f : 15f;
            float flyStartY = -700f;   

            int localActorId = uiController.GetLocalActorId();
            bool isLocal = (loserId == localActorId);
            
            List<Transform> existingCards = new List<Transform>();
            if (isLocal)
            {
                var myHand = uiController.GetPlayerHandUI();
                foreach (var c in myHand) existingCards.Add(c.transform);
            }
            else
            {
                var oppContainer = uiController.GetOpponentCardContainer(loserId);
                if (oppContainer != null)
                {
                    foreach (Transform child in oppContainer) existingCards.Add(child);
                }
            }

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float normalizedX = t * 2f - 1f; // -1 to 1

                float xPos = startX + i * spacing;
                float yPos = arcHeight * (1f - (normalizedX * normalizedX)) - 30f; 
                float angle = -normalizedX * 15f; 

                Vector2 targetLocal = new Vector2(xPos, yPos);
                Quaternion targetRot = Quaternion.Euler(0, 0, angle);

                RectTransform rt;
                Vector3 startWorldPos;
                Quaternion startWorldRot;

                if (isLocal && i < existingCards.Count && existingCards[i] != null)
                {
                    var existingCard = existingCards[i].GetComponent<CardUI>();
                    if (existingCard != null)
                    {
                        uiController.RemoveFromPlayerHandUI(existingCard);
                        rt = existingCard.GetComponent<RectTransform>();
                        startWorldPos = rt.position;
                        startWorldRot = rt.rotation;
                        existingCard.transform.SetParent(uiController.revealedHandContainer, true);
                    }
                    else
                    {
                        GameObject go = Instantiate(uiController.cardPrefab, uiController.revealedHandContainer);
                        CardUI cui = go.GetComponent<CardUI>();
                        if (cui != null) cui.SetupFusion(cards[i], true);
                        rt = go.GetComponent<RectTransform>();
                        rt.anchoredPosition = new Vector2(0, flyStartY);
                        startWorldPos = rt.position;
                        startWorldRot = Quaternion.identity;
                    }
                }
                else
                {
                    GameObject go = Instantiate(uiController.cardPrefab, uiController.revealedHandContainer);
                    CardUI cui = go.GetComponent<CardUI>();
                    if (cui != null) cui.SetupFusion(cards[i], true);
                    rt = go.GetComponent<RectTransform>();

                    if (!isLocal && i < existingCards.Count && existingCards[i] != null)
                    {
                        startWorldPos = existingCards[i].position;
                        startWorldRot = existingCards[i].rotation;
                        Destroy(existingCards[i].gameObject);
                    }
                    else
                    {
                        rt.anchoredPosition = new Vector2(0, flyStartY);
                        startWorldPos = rt.position;
                        startWorldRot = Quaternion.identity;
                    }
                }

                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(130f, 130f * 1.4f);
                rt.localScale = Vector3.one;
                
                rt.position = startWorldPos;
                rt.rotation = startWorldRot;

                outRects.Add(rt);

                yield return StartCoroutine(Co_MoveLocalAndRotate(rt, rt.anchoredPosition, targetLocal, rt.localRotation, targetRot, 0.28f));
                yield return new WaitForSeconds(0.07f);
            }
        }

        private IEnumerator Co_AnimateOneCard(RectTransform rt, int rank, int newSubTotal)
        {
            if (rt == null) yield break;

            Vector2 origPos = rt.anchoredPosition;
            Vector2 upPos   = origPos + new Vector2(0f, 55f);

            // 1. 少し浮かせる
            yield return StartCoroutine(Co_MoveLocal(rt, origPos, upPos, 0.18f));
            yield return new WaitForSeconds(0.1f);

            // 2. カードを小計スコアへ吸い込ませる (数字表示は廃止)
            if (_subTotalLabelObj != null)
            {
                RectTransform targetRt = _subTotalLabelObj.GetComponent<RectTransform>();
                // カードそのものを吸い込ませる
                yield return StartCoroutine(Co_SuckElementToTarget(rt.transform, targetRt.position, 0.45f));
                
                // カードを非表示にする
                rt.gameObject.SetActive(false);

                // 吸い込み完了後にスコア更新
                UpdateSubTotalScore(newSubTotal);
                
                // ヒット演出
                StartCoroutine(Co_PunchScale(targetRt, 1.25f, 0.2f));
            }
            else
            {
                // 吸い込み先がなければ更新だけして戻す
                UpdateSubTotalScore(newSubTotal);
                yield return StartCoroutine(Co_MoveLocal(rt, upPos, origPos, 0.15f));
            }
            
            yield return new WaitForSeconds(0.1f);
        }

        private IEnumerator Co_SuckScoreToTarget(Vector3 startWorldPos, RectTransform targetRt, string msg, Color color, bool doPopup = true)
        {
            if (targetRt == null && !doPopup)
            {
                yield break;
            }

            string targetName = (targetRt != null) ? targetRt.name : "None (Popup Only)";
            Debug.Log($"[ScoreAnimation] Starting Suck animation for '{msg}' at WorldPos: {startWorldPos}. Target: {targetName}");

            Transform parent = (uiController.animationOverlay != null) ? uiController.animationOverlay.transform : uiController.transform;

            GameObject suckObj = null;
            Text txt = null;

            // --- ヒエラルキーの再構築 (Graphic衝突回避と最前面表示) ---
            // 1. ルートコンテナ (Canvas + CanvasGroup)
            GameObject rootObj = new GameObject("SuckScoreRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            rootObj.transform.SetParent(parent, false);
            rootObj.transform.position = startWorldPos;
            Vector3 lp = rootObj.transform.localPosition;
            lp.z = 0; 
            rootObj.transform.localPosition = lp;
            rootObj.transform.localScale = Vector3.one * 0.01f;

            // 最前面表示の強制
            Canvas rootCanvas = rootObj.GetComponent<Canvas>();
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 30000; // 圧倒的最前面
            
            CanvasGroup cg = rootObj.GetComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = false;

            RectTransform rootRt = rootObj.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(400, 200);

            // 3. テキスト用オブジェクト (子に配置)
            GameObject textObj = new GameObject("TextDisplay", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(rootObj.transform, false);
            txt = textObj.GetComponent<Text>();

            if (txt != null)
            {
                txt.text = msg;
                txt.color = color;
                // フォントの取得をより確実に
                Font targetFont = uiController.mainFontBold ?? uiController.mainFontRegular;
                if (targetFont == null) targetFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                
                txt.font = targetFont;
                txt.fontSize = 80;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                
                var outline = textObj.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(3f, -3f);
            }

            suckObj = rootObj; // 以降の処理は rootObj に対して行う

            if (doPopup)
            {
                float popupDuration = 0.3f;
                float popupElapsed = 0f;
                // Popup開始時のワールド座標を念のため再度ログ
                Debug.Log($"[ScoreAnimation] Popup Start. Pos: {suckObj.transform.position}");

                while (popupElapsed < popupDuration)
                {
                    if (suckObj == null) yield break;
                    popupElapsed += Time.deltaTime;
                    float t = popupElapsed / popupDuration;
                    float scale = Mathf.Lerp(0f, 2.0f, Mathf.Sin(t * Mathf.PI * 0.5f));
                    
                    suckObj.transform.localScale = Vector3.one * scale;
                    
                    if (txt != null)
                    {
                        Color c = txt.color;
                        c.a = t;
                        txt.color = c;
                    }
                    yield return null;
                }
                suckObj.transform.localScale = Vector3.one * 2.0f;
                yield return new WaitForSeconds(0.4f);
            }
            else
            {
                suckObj.transform.localScale = Vector3.one * 1.5f;
            }

            string targetPosStr = (targetRt != null) ? targetRt.position.ToString() : "None";
            Debug.Log($"[ScoreAnimation] Moving to target. CurrentPos: {suckObj.transform.position}, TargetPos: {targetPosStr}");

            float suckDuration = 0.5f;
            float suckElapsed = 0f;
            Vector3 startPos = suckObj.transform.position;
            Vector3 startScale = suckObj.transform.localScale;

            while (suckElapsed < suckDuration)
            {
                if (suckObj == null) yield break;
                // 目標がない場合はここで終了（ポップアップのみの場合）
                if (targetRt == null) break;
                
                suckElapsed += Time.deltaTime;
                float t = suckElapsed / suckDuration;
                float easeIn = t * t; 

                suckObj.transform.position = Vector3.Lerp(startPos, targetRt.position, easeIn);
                suckObj.transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.4f, t);
                
                if (txt != null)
                {
                    Color c = txt.color;
                    c.a = 1f - (t * 0.5f);
                    txt.color = c;
                }
                yield return null;
            }

            if (targetRt != null)
            {
                StartCoroutine(Co_PunchScale(targetRt, 1.3f, 0.2f));
            }
            Destroy(suckObj);
            Debug.Log("[ScoreAnimation] Suck animation sequence completed.");
        }

        private IEnumerator Co_PunchScale(RectTransform rt, float multiplier, float duration)
        {
            if (rt == null) yield break;
            Vector3 origScale = rt.localScale;
            float half = duration / 2f;
            
            float t = 0;
            while (t < half) {
                t += Time.deltaTime;
                rt.localScale = Vector3.Lerp(origScale, origScale * multiplier, t / half);
                yield return null;
            }
            t = 0;
            while (t < half) {
                t += Time.deltaTime;
                rt.localScale = Vector3.Lerp(origScale * multiplier, origScale, t / half);
                yield return null;
            }
            rt.localScale = origScale;
        }

        private void ShowTotalScore(int value, string winnerName)
        {
            if (_totalScoreLabelObj != null) Destroy(_totalScoreLabelObj);
            if (_winnerNameLabelObj != null) Destroy(_winnerNameLabelObj);

            Debug.Log($"[ScoreAnimation] Showing Total Score: {value} for {winnerName}");

            Transform parent = (uiController.animationOverlay != null)
                ? uiController.animationOverlay.transform
                : uiController.transform;

            _totalScoreLabelObj = new GameObject("TotalScoreLabel",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _totalScoreLabelObj.transform.SetParent(parent, false);
            _totalScoreLabelObj.transform.SetAsLastSibling();

            Canvas cv = _totalScoreLabelObj.GetComponent<Canvas>();
            cv.overrideSorting = true;
            cv.sortingOrder = 30001; 

            // テキストは子に作成
            GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(_totalScoreLabelObj.transform, false);
            
            _totalScoreLabelText = txtObj.GetComponent<Text>();
            _totalScoreLabelText.text      = $"合計: {value}";
            _totalScoreLabelText.color     = Color.white;
            _totalScoreLabelText.fontSize  = 60;
            
            Font tFont = uiController.mainFontBold ?? uiController.mainFontRegular;
            if (tFont == null) tFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _totalScoreLabelText.font = tFont;

            _totalScoreLabelText.fontStyle = FontStyle.Bold;
            _totalScoreLabelText.verticalOverflow = VerticalWrapMode.Overflow;
            _totalScoreLabelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _totalScoreLabelText.alignment = TextAnchor.MiddleCenter;

            RectTransform rt = _totalScoreLabelObj.GetComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -350f); // -250fからさらに-350fに下げた
            rt.sizeDelta        = new Vector2(800f, 100f);

            // 表示保証
            var tcg = _totalScoreLabelObj.GetComponent<CanvasGroup>();
            if (tcg != null) tcg.alpha = 1f;

            // 縁取り追加
            var ol1 = txtObj.AddComponent<Outline>();
            ol1.effectColor = Color.black;
            ol1.effectDistance = new Vector2(2f, -2f);

            _winnerNameLabelObj = new GameObject("WinnerNameLabel",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _winnerNameLabelObj.transform.SetParent(parent, false);
            _winnerNameLabelObj.transform.SetAsLastSibling();

            Canvas cvW = _winnerNameLabelObj.GetComponent<Canvas>();
            cvW.overrideSorting = true;
            cvW.sortingOrder = 30002;

            GameObject wTxtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            wTxtObj.transform.SetParent(_winnerNameLabelObj.transform, false);
            _winnerNameLabelText = wTxtObj.GetComponent<Text>(); // Assign _winnerNameLabelText from the child object
            _winnerNameLabelText.text      = $"Winner: {winnerName}";
            _winnerNameLabelText.color     = Color.cyan;
            _winnerNameLabelText.fontSize  = 48;
            
            Font wFont = uiController.mainFontBold ?? uiController.mainFontRegular;
            if (wFont == null) wFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _winnerNameLabelText.font = wFont;

            _winnerNameLabelText.fontStyle = FontStyle.Bold;
            _winnerNameLabelText.verticalOverflow = VerticalWrapMode.Overflow;
            _winnerNameLabelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _winnerNameLabelText.alignment = TextAnchor.MiddleCenter;

            var ol2 = wTxtObj.AddComponent<Outline>();
            ol2.effectColor = Color.black;
            ol2.effectDistance = new Vector2(2f, -2f);

            RectTransform rtW = _winnerNameLabelObj.GetComponent<RectTransform>();
            rtW.anchorMin        = rtW.anchorMax = new Vector2(0.5f, 1f);
            rtW.pivot            = new Vector2(0.5f, 1f);
            rtW.anchoredPosition = new Vector2(0f, -430f); // -330fからさらに-430fに下げた
            rtW.sizeDelta        = new Vector2(800f, 80f);

            // Winner表示の保証
            var wcg = _winnerNameLabelObj.GetComponent<CanvasGroup>();
            if (wcg != null) wcg.alpha = 1f;
        }

        private void UpdateTotalScore(int value)
        {
            if (_totalScoreLabelText != null)
                _totalScoreLabelText.text = $"合計: {value}";
        }

        private void ShowSubTotalScore(int value)
        {
            if (_subTotalLabelObj != null) Destroy(_subTotalLabelObj);

            Debug.Log($"[ScoreAnimation] Showing SubTotal Score: {value}");

            Transform parent = (uiController.animationOverlay != null)
                ? uiController.animationOverlay.transform
                : uiController.transform;

            _subTotalLabelObj = new GameObject("SubTotalScoreLabel",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            _subTotalLabelObj.transform.SetParent(parent, false);
            _subTotalLabelObj.transform.SetAsLastSibling();

            Canvas cv = _subTotalLabelObj.GetComponent<Canvas>();
            cv.overrideSorting = true;
            cv.sortingOrder = 30005;

            RectTransform rt = _subTotalLabelObj.GetComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -100f);
            rt.sizeDelta        = new Vector2(500f, 100f);

            GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(_subTotalLabelObj.transform, false);

            _subTotalLabelText           = txtObj.GetComponent<Text>();
            _subTotalLabelText.text      = $"小計: {value}";
            _subTotalLabelText.color     = Color.yellow;
            _subTotalLabelText.fontSize  = 52;
            
            Font sFont = uiController.mainFontBold ?? uiController.mainFontRegular;
            if (sFont == null) sFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _subTotalLabelText.font = sFont;

            _subTotalLabelText.fontStyle = FontStyle.Bold;
            _subTotalLabelText.verticalOverflow = VerticalWrapMode.Overflow;
            _subTotalLabelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _subTotalLabelText.alignment = TextAnchor.MiddleCenter;


            // 表示保証
            var scg = _subTotalLabelObj.GetComponent<CanvasGroup>();
            if (scg != null) scg.alpha = 1f;

            var ol = txtObj.AddComponent<Outline>();
            ol.effectColor = Color.black;
            ol.effectDistance = new Vector2(2f, -2f);
        }

        private void UpdateSubTotalScore(int value)
        {
            if (_subTotalLabelText != null)
                _subTotalLabelText.text = $"小計: {value}";
        }

        private void HideSubTotalScore()
        {
            if (_subTotalLabelObj != null)
            {
                Destroy(_subTotalLabelObj);
                _subTotalLabelObj = null;
                _subTotalLabelText = null;
            }
        }

        public List<CardInfo> ParseHandStr(string handStr)
        {
            var list = new List<CardInfo>();
            if (string.IsNullOrEmpty(handStr)) return list;

            Debug.Log($"[Animation] Parsing hand string: '{handStr}'");

            var cards = handStr.Split(';');
            foreach (var s in cards)
            {
                if (string.IsNullOrEmpty(s)) continue;
                var parts = s.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int suit) && int.TryParse(parts[1], out int rank))
                {
                    list.Add(new CardInfo((Suit)suit, rank));
                }
                else
                {
                    Debug.LogWarning($"[Animation] Failed to parse card element: '{s}' in string '{handStr}'");
                }
            }
            
            Debug.Log($"[Animation] Parse complete. Cards found: {list.Count}");
            return list;
        }

        private void SetOverlay(bool active)
        {
            if (uiController.animationOverlay == null) return;
            uiController.animationOverlay.SetActive(active);
            if (active) uiController.animationOverlay.transform.SetAsLastSibling();
        }

        private void ClearRevealedHand()
        {
            if (uiController.revealedHandContainer == null) return;
            foreach (Transform child in uiController.revealedHandContainer)
                Destroy(child.gameObject);
        }

        private void Cleanup()
        {
            SetOverlay(false);

            // animationOverlay に残っている演出用オブジェクト（ばらまきカード等）をクリア
            if (uiController.animationOverlay != null)
            {
                foreach (Transform child in uiController.animationOverlay.transform)
                    Destroy(child.gameObject);
            }

            if (_totalScoreLabelObj != null) { Destroy(_totalScoreLabelObj); _totalScoreLabelObj = null; }
            if (_subTotalLabelObj != null) { Destroy(_subTotalLabelObj); _subTotalLabelObj = null; }
            if (_winnerNameLabelObj != null) { Destroy(_winnerNameLabelObj); _winnerNameLabelObj = null; }
            if (uiController.drawButton != null) uiController.drawButton.interactable = true;
            if (uiController.donButton  != null) uiController.donButton.interactable  = true;
        }

        private IEnumerator Co_SuckElementToTarget(Transform element, Vector3 targetWorldPos, float duration)
        {
            if (element == null) yield break;
            
            Vector3 startPos = element.position;
            Vector3 startScale = element.localScale;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                if (element == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // 加速しながら吸い込まれるように easeIn を適用
                float easeIn = t * t; 
                element.position = Vector3.Lerp(startPos, targetWorldPos, easeIn);
                
                // 徐々に小さく、透明度がある場合は下げていく
                element.localScale = Vector3.Lerp(startScale, Vector3.one * 0.1f, t);
                
                yield return null;
            }
            
            if (element != null)
            {
                element.position = targetWorldPos;
                element.localScale = Vector3.one * 0.1f;
            }
        }

        private IEnumerator Co_Move(Transform t, Vector3 from, Vector3 to, float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (t == null) yield break;
                elapsed += Time.deltaTime;
                t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / dur));
                yield return null;
            }
            if (t != null) t.position = to;
        }

        private IEnumerator Co_MoveLocal(RectTransform rt, Vector2 from, Vector2 to, float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                rt.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / dur));
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = to;
        }

        private IEnumerator Co_MoveLocalAndRotate(RectTransform rt, Vector2 fromPos, Vector2 toPos, Quaternion fromRot, Quaternion toRot, float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
                rt.anchoredPosition = Vector2.Lerp(fromPos, toPos, t);
                rt.localRotation = Quaternion.Lerp(fromRot, toRot, t);
                yield return null;
            }
            if (rt != null) 
            {
                rt.anchoredPosition = toPos;
                rt.localRotation = toRot;
            }
        }

        public void ShowDonFloatingText(Transform target)
        {
            // 大きな黄色い文字で「Don!」を表示
            ShowFloatingText(target, "Don!", Color.yellow, 100);
        }

        private void ShowFloatingText(Transform target, string msg, Color color, int fontSize = 52)
        {
            if (uiController.floatingTextPrefab == null) return;

            Transform parent = (uiController.animationOverlay != null)
                ? uiController.animationOverlay.transform
                : uiController.transform;

            Vector3 spawnPos = (target != null)
                ? target.position + new Vector3(0, 60f, 0)
                : Vector3.zero;

            GameObject obj = Instantiate(uiController.floatingTextPrefab, spawnPos,
                Quaternion.identity, parent);
            obj.transform.SetAsLastSibling();

            Text txt = obj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text     = msg;
                txt.color    = color;
                txt.fontSize = fontSize;
                txt.font     = uiController.mainFontBold ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            StartCoroutine(Co_FloatUpAndDestroy(obj, 1.5f));
        }

        private void ShowCenterText(string msg, Color color, int fontSize = 65)
        {
            if (uiController.floatingTextPrefab == null) return;

            Transform parent = (uiController.animationOverlay != null)
                ? uiController.animationOverlay.transform
                : uiController.transform;

            GameObject obj = Instantiate(uiController.floatingTextPrefab, parent);
            obj.transform.localPosition = new Vector3(0f, -100f, 0f);
            obj.transform.SetAsLastSibling();

            Text txt = obj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text     = msg;
                txt.color    = color;
                txt.fontSize = fontSize;
                txt.font     = uiController.mainFontBold ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            StartCoroutine(Co_FadeOutAndDestroy(obj, 2.5f));
        }

        private GameObject ShowCenterTextPersistent(string msg, Color color, int fontSize = 65)
        {
            if (uiController.floatingTextPrefab == null) return null;

            Transform parent = (uiController.animationOverlay != null)
                ? uiController.animationOverlay.transform
                : uiController.transform;

            GameObject obj = Instantiate(uiController.floatingTextPrefab, parent);
            obj.transform.localPosition = new Vector3(0f, -100f, 0f);
            obj.transform.SetAsLastSibling();

            Text txt = obj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text     = msg;
                txt.color    = color;
                txt.fontSize = fontSize;
                txt.font     = uiController.mainFontBold ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            return obj;
        }

        private IEnumerator Co_FloatUpAndDestroy(GameObject target, float duration)
        {
            if (target == null) yield break;
            CanvasGroup cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.AddComponent<CanvasGroup>();
            if (cg == null) yield break;

            Vector3 startPos = target.transform.position;
            float t = 0f;
            while (t < duration)
            {
                if (target == null || cg == null) yield break;
                t += Time.deltaTime;
                float ratio = t / duration;
                cg.alpha = 1f - ratio;
                target.transform.position = startPos + new Vector3(0f, 70f * ratio, 0f);
                yield return null;
            }
            if (target != null) Destroy(target);
        }

        private IEnumerator Co_FadeOutAndDestroy(GameObject target, float duration)
        {
            if (target == null) yield break;
            CanvasGroup cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.AddComponent<CanvasGroup>();
            if (cg == null) yield break;

            yield return new WaitForSeconds(duration * 0.5f);

            float t = 0f;
            float fadeDur = duration * 0.5f;
            while (t < fadeDur)
            {
                if (target == null || cg == null) yield break;
                t += Time.deltaTime;
                cg.alpha = 1f - (t / fadeDur);
                yield return null;
            }
            if (target != null) Destroy(target);
        }
    }
}
