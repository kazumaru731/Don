using System;

namespace DonGame2D.Models
{
    public enum Suit
    {
        Spades,
        Hearts,
        Diamonds,
        Clubs
    }

    [Serializable]
    public class CardData
    {
        public Suit suit;
        public int rank; // 1-13

        public CardData(Suit suit, int rank)
        {
            this.suit = suit;
            this.rank = rank;
        }

        // 通信用にint配列などへ変換するユーティリティ
        public static object[] Serialize(CardData card)
        {
            return new object[] { (int)card.suit, card.rank };
        }

        public static CardData Deserialize(object[] data)
        {
            return new CardData((Suit)(int)data[0], (int)data[1]);
        }
    }
}
