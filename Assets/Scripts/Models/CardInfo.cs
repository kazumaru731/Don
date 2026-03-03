using Fusion;
using System;

namespace DonGame2D.Models
{
    /// <summary>
    /// FusionのNetworked変数として直接使用可能なカード情報構造体
    /// </summary>
    public struct CardInfo : INetworkStruct
    {
        public int SuitInt;
        public int Rank;

        public CardInfo(Suit suit, int rank)
        {
            this.SuitInt = (int)suit;
            this.Rank = rank;
        }

        public Suit Suit => (Suit)SuitInt;

        public override string ToString()
        {
            return $"{Suit} {Rank}";
        }
    }
}
