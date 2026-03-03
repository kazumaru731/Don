using System;
using System.Collections.Generic;
using System.Linq;

namespace DonGame2D.Models
{
    /// <summary>
    /// ローカルゲーム用プレイヤーモデル
    /// ※ Photon.Realtime.Player との名前衝突を避けるため DonPlayer と命名
    /// </summary>
    [Serializable]
    public class DonPlayer
    {
        public string id;
        public string name;
        public List<Card> hand = new List<Card>();
        public int credits = 0;
        public bool isAI = false;

        public DonPlayer(string id, string name, bool isAI = false)
        {
            this.id = id;
            this.name = name;
            this.isAI = isAI;
        }

        /// <summary>
        /// 手札の合計値を計算します。
        /// </summary>
        public int GetHandTotal()
        {
            return hand.Sum(c => c.GetScore());
        }

        /// <summary>
        /// 手札が空かどうかを確認します。
        /// </summary>
        public bool IsHandEmpty()
        {
            return hand.Count == 0;
        }

        /// <summary>
        /// 手札をクリアします（ラウンド終了時など）。
        /// </summary>
        public void ClearHand()
        {
            hand.Clear();
        }

        /// <summary>
        /// 手札にカードを追加します。
        /// </summary>
        public void AddCard(Card card)
        {
            hand.Add(card);
        }

        /// <summary>
        /// 手札からカードを削除します。
        /// </summary>
        public bool RemoveCard(Card card)
        {
            return hand.Remove(card);
        }
    }
}
