using System;
using DonGame2D.Models;

namespace DonGame2D.Models
{
    [Serializable]
    public class Card
    {
        public Suit suit;
        public int rank; // 1-13

        public Card(Suit suit, int rank)
        {
            this.suit = suit;
            this.rank = rank;
        }

        public override string ToString()
        {
            return $"{suit} {rank}";
        }

        /// <summary>
        /// カードの得点を取得します。
        /// </summary>
        public int GetScore()
        {
            return rank;
        }
    }
}
