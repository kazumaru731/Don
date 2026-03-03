using System;
using System.Collections.Generic;
using UnityEngine;
using DonGame2D.Models;

namespace DonGame2D.Logic
{
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "DonGame/CardDatabase")]
    public class CardDatabase : ScriptableObject
    {
        [Header("Card Back")]
        public Sprite cardBackSprite;

        [Header("Card Fronts")]
        public List<CardSpriteEntry> cardEntries = new List<CardSpriteEntry>();

        [Serializable]
        public class CardSpriteEntry
        {
            public Suit suit;
            public int rank;
            public Sprite sprite;
        }

        /// <summary>
        /// スートとランクから対応するスプライトを取得します。
        /// </summary>
        public Sprite GetCardSprite(Suit suit, int rank)
        {
            var entry = cardEntries.Find(e => e.suit == suit && e.rank == rank);
            if (entry != null && entry.sprite != null)
            {
                return entry.sprite;
            }
            Debug.LogWarning($"Card sprite not found for {suit} {rank}");
            return null;
        }

        public Sprite GetCardBack()
        {
            return cardBackSprite;
        }
    }
}
