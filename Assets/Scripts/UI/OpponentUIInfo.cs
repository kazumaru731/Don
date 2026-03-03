using UnityEngine;
using UnityEngine.UI;
using DonGame2D.Models;
using DonGame2D.Logic;

namespace DonGame2D.UI
{
    public class OpponentUIInfo : MonoBehaviour
    {
        public Text nameText;
        public Text countText;
        public Transform cardIconContainer;
        public GameObject cardPrefab; // 互換性のため残す
        public Image backgroundImage;
        public Outline outline;
        public Color reachColor = new Color(1f, 0.3f, 0.3f, 0.8f);
        public Color normalColor = new Color(0f, 0f, 0f, 0.4f);

        private const float CardWidth = 100f;
        private const float CardHeight = 140f; // 1:1.4 比率 (プレイヤーや場と同じサイズ)

        // FindObjectOfType を毎回呼ばないよう、一度だけキャッシュする
        private Sprite cachedBackSprite = null;
        private bool isBackSpriteCached = false;

        private void Awake()
        {
            // 親の VerticalLayoutGroup を完全に無効化し、絶対位置で制御する
            var vlg = GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.enabled = false;

            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(300, 180); // 大きなカードに合わせてコンテナ拡大
                rt.pivot = new Vector2(0.5f, 0.5f);
            }

            if (backgroundImage != null) backgroundImage.enabled = false; // 背景の枠を削除

            SetupNameTextPosition();
            SetupCountTextPosition();
            SetupCardContainerPosition();
        }

        /// <summary>
        /// 画面上の配置位置と角度を更新し、UI要素を中央に向ける
        /// </summary>
        public void UpdateLayout(Vector2 anchoredPos, float rotationAngle)
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) return;

            rt.anchoredPosition = anchoredPos;
            rt.localRotation = Quaternion.Euler(0, 0, rotationAngle);

            // テキストが逆さまにならないように、Transform 自体は回転させず、
            // カードレイアウトの基準点として利用するなどの工夫が必要な場合があるが、
            // 今回は単純化のため全体を回転させ、テキストの RectTransform を逆回転させて補正する
            if (nameText != null) nameText.transform.rotation = Quaternion.identity;
            if (countText != null) countText.transform.rotation = Quaternion.identity;
            
            // 背景の透明度を少し上げるなど（オプション）
            if (backgroundImage != null)
            {
                var c = backgroundImage.color;
                c.a = 0.2f; // より控えめに
                backgroundImage.color = c;
            }
        }

        private void SetupNameTextPosition()
        {
            if (nameText == null) return;
            var rt = nameText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(0, 22);
            rt.anchoredPosition = new Vector2(8, -5);
        }

        private void SetupCountTextPosition()
        {
            if (countText == null) return;
            var rt = countText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(0, 18);
            rt.anchoredPosition = new Vector2(8, -29);
        }

        private void SetupCardContainerPosition()
        {
            if (cardIconContainer == null) return;
            var rt = cardIconContainer.GetComponent<RectTransform>();
            if (rt == null) return;

            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(-16f, 150f); // カードの高さ(140)が入るように拡大
            rt.anchoredPosition = new Vector2(8, -52);

            var lg = cardIconContainer.GetComponent<HorizontalLayoutGroup>();
            if (lg != null) lg.enabled = false;
        }

        private Sprite GetBackSprite()
        {
            // 一度だけキャッシュする（毎回 FindObjectOfType を呼ばない）
            if (!isBackSpriteCached)
            {
                isBackSpriteCached = true;
                var uiCtrl = FindObjectOfType<GameUIController>();
                if (uiCtrl != null && uiCtrl.cardDatabase != null)
                {
                    cachedBackSprite = uiCtrl.cardDatabase.GetCardBack();
                }
            }
            return cachedBackSprite;
        }

        public void Setup(int actorId, int cardCount, bool isReach)
        {
            if (nameText) nameText.text = $"Player {actorId}";
            if (countText) countText.text = $"x{cardCount}";

            if (backgroundImage != null)
            {
                backgroundImage.color = isReach ? reachColor : normalColor;
            }

            if (cardIconContainer == null) return;

            // --- 枚数を合わせる（追加・削除は最小限に）---
            var backSprite = GetBackSprite(); // ここで1回だけ取得

            // --- 枚数を合わせる（事前に差分を計算してからループする）---
            // ★ 重要: Destroy() はフレーム末尾まで遅延されるため、
            //    while(childCount > N) のように書くと無限ループになる！
            int toRemove = cardIconContainer.childCount - cardCount;
            for (int i = 0; i < toRemove; i++)
            {
                // 末尾から削除（indexは詰まるので最後の子を取得）
                int lastIdx = cardIconContainer.childCount - 1 - i;
                if (lastIdx >= 0)
                    Destroy(cardIconContainer.GetChild(lastIdx).gameObject);
            }

            int toAdd = cardCount - cardIconContainer.childCount;
            for (int i = 0; i < toAdd; i++)
            {
                CreateCardIcon(backSprite);
            }

            // --- 全カードを扇状に並べる ---
            var containerRT = cardIconContainer.GetComponent<RectTransform>();
            float containerWidth = (containerRT != null && containerRT.rect.width > 10f)
                ? containerRT.rect.width
                : 200f;

            int count = cardIconContainer.childCount;
            
            // カードが大きいので、広がり角と半径も微調整
            float fanAngleSpan = Mathf.Min(count * 12f, 60f); // 重なりが見えるように拡大
            float startAngle = fanAngleSpan / 2f;
            float angleStep = count > 1 ? fanAngleSpan / (count - 1) : 0f;
            
            // 扇の半径
            float radius = 150f; // カードの大きさに合わせて湾曲具合をなだらかに

            for (int i = 0; i < count; i++)
            {
                var rt = cardIconContainer.GetChild(i).GetComponent<RectTransform>();
                if (rt == null) continue;

                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0f); // カードの下端を回転軸にする
                rt.sizeDelta = new Vector2(CardWidth, CardHeight);
                rt.localScale = Vector3.one;

                float currentAngle = startAngle - (i * angleStep);
                rt.localRotation = Quaternion.Euler(0, 0, currentAngle);
                
                // 円周上に配置 (X軸の移動方向を回転角に合わせるため -Sin とする)
                float rad = currentAngle * Mathf.Deg2Rad;
                rt.anchoredPosition = new Vector2(-Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius - radius);
            }
        }

        public void SetTurnActive(bool isActive)
        {
            if (outline != null)
            {
                outline.enabled = isActive;
            }
        }

        private void CreateCardIcon(Sprite backSprite)
        {
            GameObject go = new GameObject("CardBack", typeof(RectTransform), typeof(Image));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(cardIconContainer, false);

            Image img = go.GetComponent<Image>();
            img.preserveAspect = true;

            if (backSprite != null)
            {
                img.sprite = backSprite;
                img.color = Color.white;
            }
            else
            {
                img.color = Color.gray;
            }
        }
    }
}
