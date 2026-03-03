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

        private void Awake()
        {
            if (uiController == null)
                uiController = GetComponent<GameUIController>();
        }

        public void PlayWinAnimation(int winType, int winnerId, int loserId, int donValue, string loserHandStr, int totalPenalty, string resultMsg, bool isFinal)
        {
            StartCoroutine(Co_PlayDonWinAnimation(winType, winnerId, loserId, donValue, loserHandStr, totalPenalty, resultMsg, isFinal));
        }

        private IEnumerator Co_PlayDonWinAnimation(int winType, int winnerId, int loserId, int donValue, string loserHandStr, int totalPenalty, string resultMsg, bool isFinal)
        {
            // アニメーション用にUIの入力を一時遮断
            uiController.drawButton.interactable = false;
            uiController.donButton.interactable = false;

            // 描画順の記録
            int originalDiscardIndex = uiController.discardPileContainer.GetSiblingIndex();
            int originalHandContainerIndex = -1;
            if (uiController.revealedHandContainer != null)
                originalHandContainerIndex = uiController.revealedHandContainer.GetSiblingIndex();

            // 背景を暗くするオーバーレイを表示
            if (uiController.animationOverlay != null)
            {
                uiController.animationOverlay.SetActive(true);
                uiController.animationOverlay.transform.SetAsLastSibling(); // 最前面に（他を隠す）
            }

            // Don決着の場合は捨て札をオーバーレイの前に持ってくる
            if (winType == 0 || winType == 1) // Don or DonGaeshi
            {
                uiController.discardPileContainer.SetAsLastSibling();

                if (uiController.discardPileContainer.childCount > 0)
                {
                    Transform topCard = uiController.discardPileContainer.GetChild(0);
                    Vector3 startPos = topCard.position;
                    Vector3 endPos = startPos + new Vector3(0, 50f, 0); // 上に少し浮く

                    // 叩かれたようなバウンド動作
                    float t = 0;
                    while (t < 0.2f)
                    {
                        t += Time.deltaTime;
                        topCard.position = Vector3.Lerp(startPos, endPos, t / 0.2f);
                        yield return null;
                    }

                    yield return new WaitForSeconds(0.4f);
                }
            }

            // --- 2. 敗者の手札の表示とアニメーション ---
            if (uiController.revealedHandContainer != null)
            {
                // 明かされた手札コンテナもオーバーレイの前に持ってくる
                uiController.revealedHandContainer.SetAsLastSibling();

                if (!string.IsNullOrEmpty(loserHandStr))
                {
                    if (winType == 2) // OUT (順番に敗者を処理)
                    {
                        string[] playerHands = loserHandStr.Split('|');
                        foreach (var hStr in playerHands)
                        {
                            if (string.IsNullOrEmpty(hStr)) continue;

                            // 予測されるフォーマット "ActorId:suit,rank;suit,rank"
                            string[] parts = hStr.Split(':');
                            if (parts.Length == 2)
                            {
                                int actorId;
                                int.TryParse(parts[0], out actorId);
                                string cardsData = parts[1];

                                // 誰の判定かを出す
                                ShowFloatingText(uiController.revealedHandContainer, $"Player {actorId}", Color.yellow);
                                yield return new WaitForSeconds(0.5f);

                                // カードのアニメーションとペナルティ計算
                                int penalty = 0;
                                yield return StartCoroutine(AnimateHandCards(cardsData, (sum) => penalty = sum * 10));

                                // このプレイヤーのペナルティを表示
                                ShowFloatingText(uiController.revealedHandContainer, $"-{penalty} Credits", Color.red);
                                yield return new WaitForSeconds(1.5f);

                                // 次の人のためにカードをクリア
                                foreach (Transform child in uiController.revealedHandContainer)
                                    Destroy(child.gameObject);
                                yield return new WaitForSeconds(0.5f);
                            }
                        }
                    }
                    else // Don, DonGaeshi
                    {
                        // Clear old cards
                        foreach (Transform child in uiController.revealedHandContainer)
                            Destroy(child.gameObject);

                        int _dummy = 0;
                        yield return StartCoroutine(AnimateHandCards(loserHandStr, (sum) => _dummy = sum));
                    }
                }
            }

            // --- 3. 全体スコアへの加算表示 ---
            if (winType == 2)
            {
                ShowFloatingStatusCenter($"P{winnerId} GAINED +{totalPenalty} Credits!");
            }
            else
            {
                ShowFloatingStatusCenter($"P{winnerId}: +{totalPenalty} / P{loserId}: -{totalPenalty}");
            }
            
            yield return new WaitForSeconds(2.0f);

            // インデックスとオーバーレイを元に戻す
            if (uiController.animationOverlay != null)
                uiController.animationOverlay.SetActive(false);

            if (originalHandContainerIndex >= 0 && uiController.revealedHandContainer != null)
                uiController.revealedHandContainer.SetSiblingIndex(originalHandContainerIndex);

            uiController.discardPileContainer.SetSiblingIndex(originalDiscardIndex);

            // --- 4. 最後にリザルトパネルを出す ---
            uiController.ShowRoundResult(resultMsg, isFinal);
        }

        private IEnumerator AnimateHandCards(string handStr, System.Action<int> onAnimationComplete)
        {
            int totalRank = 0;
            string[] cardStrs = handStr.Split(';');
            foreach (string cStr in cardStrs)
            {
                if (string.IsNullOrEmpty(cStr)) continue;
                string[] parts = cStr.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int suit) && int.TryParse(parts[1], out int rank))
                {
                    totalRank += rank;

                    // カード生成
                    GameObject go = Instantiate(uiController.cardPrefab, uiController.revealedHandContainer);
                    CardUI cUI = go.GetComponent<CardUI>();
                    cUI.SetupFusion(new CardInfo((Suit)suit, rank), true);
                    
                    Vector3 startPos = cUI.transform.position;
                    // 少し上に浮いて数字を出す
                    float t = 0;
                    while (t < 0.15f)
                    {
                        t += Time.deltaTime;
                        cUI.transform.position = startPos + new Vector3(0, 30f * (t / 0.15f), 0);
                        yield return null;
                    }
                    ShowFloatingText(cUI.transform, rank.ToString(), Color.red);
                    yield return new WaitForSeconds(0.4f);
                }
            }
            
            // 計算されたRank合計をコールバック経由で返す
            onAnimationComplete?.Invoke(totalRank);
        }

        private void ShowFloatingText(Transform target, string textMsg, Color color)
        {
            if (uiController.floatingTextPrefab == null) return;
            
            GameObject textObj = Instantiate(uiController.floatingTextPrefab, target.position + new Vector3(0, 50f, 0), Quaternion.identity, target.parent);
            Text txt = textObj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = textMsg;
                txt.color = color;
            }
            // TextFloatingController等が付いていれば上に消えるアニメーションをする想定
            // 無ければ適当にCoroutineで消す
            StartCoroutine(Co_FadeOutAndDestroy(textObj, 2.0f));
        }

        private void ShowFloatingStatusCenter(string textMsg)
        {
            if (uiController.floatingTextPrefab == null) return;
            
            // GameManager(自分)ではなく Canvas以下である revealedHandContainer の親に置く
            Transform parentT = uiController.revealedHandContainer != null ? uiController.revealedHandContainer.parent : uiController.transform;

            GameObject textObj = Instantiate(uiController.floatingTextPrefab, parentT); // Center of Canvas
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.SetAsLastSibling(); // 最前面にする
            
            Text txt = textObj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = textMsg;
                txt.color = Color.cyan;
                txt.fontSize = 60;
            }
            StartCoroutine(Co_FadeOutAndDestroy(textObj, 2.0f));
        }

        private IEnumerator Co_FadeOutAndDestroy(GameObject target, float duration)
        {
            CanvasGroup cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.AddComponent<CanvasGroup>();

            float t = 0;
            while (t < duration)
            {
                if (target == null || cg == null) yield break;
                t += Time.deltaTime;
                cg.alpha = 1f - (t / duration);
                yield return null;
            }
            if (target != null)
                Destroy(target);
        }

        // --- 追加: エディタからのテスト実行用メソッド ---
        [ContextMenu("Test Don Animation (WinType 0)")]
        public void TestDonAnimation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Test methods can only be run in Play Mode.");
                return;
            }
            // ActorId=1がドンで勝利、ActorId=2が敗北。対象カードは手札合計5のカード(例: クラブの5)
            // 手札データ: "Suit,Rank;Suit,Rank" (クラブの5なので 0,5)
            PlayWinAnimation(0, 1, 2, 5, "0,5", 50, "Player 1 WON by DON!\nPlayer 2 lost 50 Credits", false);
        }

        [ContextMenu("Test OUT Animation (WinType 2)")]
        public void TestOutAnimation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Test methods can only be run in Play Mode.");
                return;
            }
            
            // ActorId=1がOUTで勝利（手札0枚）
            // 敗者はActorId=2 (クラブの5, ダイヤの3) と ActorId=3 (ハートの10, スペードの2)
            // 新フォーマット: "ActorId:Suit,Rank;Suit,Rank|ActorId:Suit,Rank;Suit,Rank"
            string testLoserHandStr = "2:0,5;1,3|3:2,10;3,2";
            
            // Player 2ペナルティ = 80
            // Player 3ペナルティ = 120
            // 合計獲得額 = 200
            
            PlayWinAnimation(2, 1, -1, 0, testLoserHandStr, 200, "Player 1 OUT!\nGained 200 Credits\n(P2:-80 P3:-120)", false);
        }
    }
}
