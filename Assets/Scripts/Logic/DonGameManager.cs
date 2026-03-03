using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DonGame2D.Models;

namespace DonGame2D.Logic
{
    public class DonGameManager : MonoBehaviour
    {
        public static DonGameManager Instance { get; private set; }

        [Header("Game Settings")]
        [Range(3, 8)]
        public int playerCount = 4;
        public int initialHandCount = 5;

        [Header("Runtime State")]
        public List<DonPlayer> players = new List<DonPlayer>();
        public List<Card> deck = new List<Card>();
        public List<Card> discardPile = new List<Card>();
        public int currentPlayerIndex = 0;
        public int drawPenaltyCount = 0; // 「2」による累積ドロー数
        public Suit currentActiveSuit;   // 「8」で変更された現在のスート
        public bool isRoundOver = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // Fusion2によるオンライン化を進めているため、旧仕様の自動開始を停止
            // InitializeGame();
        }

        #region Game Initialization

        public void InitializeGame()
        {
            players.Clear();
            for (int i = 0; i < playerCount; i++)
            {
                players.Add(new DonPlayer($"P{i}", $"Player {i + 1}", i > 0));
            }

            StartNewRound();
        }

        public void StartNewRound()
        {
            isRoundOver = false;
            drawPenaltyCount = 0;
            currentPlayerIndex = 0;

            CreateAndShuffleDeck();

            foreach (var player in players)
            {
                player.ClearHand();
                for (int i = 0; i < initialHandCount; i++)
                {
                    player.AddCard(DrawFromDeck());
                }
            }

            discardPile.Clear();
            Card firstCard = DrawFromDeck();
            discardPile.Add(firstCard);
            currentActiveSuit = firstCard.suit;

            Debug.Log($"Round Started. First Card: {firstCard}. Active Suit: {currentActiveSuit}");
        }

        private void CreateAndShuffleDeck()
        {
            deck.Clear();
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
            {
                for (int rank = 1; rank <= 13; rank++)
                {
                    deck.Add(new Card(suit, rank));
                }
            }

            for (int i = deck.Count - 1; i > 0; i--)
            {
                int r = Random.Range(0, i + 1);
                Card temp = deck[i];
                deck[i] = deck[r];
                deck[r] = temp;
            }
        }

        private Card DrawFromDeck()
        {
            if (deck.Count == 0)
            {
                Card topCard = discardPile.Last();
                discardPile.RemoveAt(discardPile.Count - 1);
                deck.AddRange(discardPile);
                discardPile.Clear();
                discardPile.Add(topCard);

                for (int i = deck.Count - 1; i > 0; i--)
                {
                    int r = Random.Range(0, i + 1);
                    Card temp = deck[i];
                    deck[i] = deck[r];
                    deck[r] = temp;
                }
                Debug.Log("Deck reshuffled from discard pile.");
            }

            Card drawn = deck[0];
            deck.RemoveAt(0);
            return drawn;
        }

        #endregion

        #region Player Actions

        public bool TryPlayCard(DonPlayer player, Card card)
        {
            if (players[currentPlayerIndex] != player) return false;

            Card topCard = GetTopDiscard();

            bool canPlayNormal = card.rank == topCard.rank || card.suit == currentActiveSuit;
            bool isEight = card.rank == 8;

            if (drawPenaltyCount > 0)
            {
                if (card.rank == 2)
                {
                    ExecutePlay(player, card);
                    return true;
                }
                return false;
            }

            if (canPlayNormal || isEight)
            {
                ExecutePlay(player, card);
                return true;
            }

            return false;
        }

        private void ExecutePlay(DonPlayer player, Card card)
        {
            player.RemoveCard(card);
            discardPile.Add(card);
            currentActiveSuit = card.suit;

            Debug.Log($"{player.name} played {card}");

            if (player.IsHandEmpty())
            {
                WinByEmptyHand(player);
                return;
            }

            if (card.rank == 2)
            {
                drawPenaltyCount += 2;
            }
            else if (card.rank == 8)
            {
                if (player.isAI)
                {
                    currentActiveSuit = GetAiBestSuit(player);
                    Debug.Log($"AI changed suit to {currentActiveSuit}");
                }
            }

            NextTurn();
        }

        public void PlayerDraw(DonPlayer player)
        {
            if (players[currentPlayerIndex] != player) return;

            int countToDraw = drawPenaltyCount > 0 ? drawPenaltyCount : 1;
            drawPenaltyCount = 0;

            for (int i = 0; i < countToDraw; i++)
            {
                player.AddCard(DrawFromDeck());
            }

            Debug.Log($"{player.name} drew {countToDraw} card(s).");
            NextTurn();
        }

        private void NextTurn()
        {
            if (isRoundOver) return;

            currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
            Debug.Log($"Next Turn: {players[currentPlayerIndex].name}");

            if (players[currentPlayerIndex].isAI)
            {
                Invoke(nameof(ExecuteAiTurn), 1.0f);
            }
        }

        #endregion

        #region Victory Conditions (Don!)

        public void DeclareDon(DonPlayer callingPlayer)
        {
            if (isRoundOver) return;

            Card topCard = GetTopDiscard();
            int total = callingPlayer.GetHandTotal();

            if (total == topCard.rank && total <= 13)
            {
                Debug.Log($"{callingPlayer.name} DECLARED DON! (Total: {total}, Target: {topCard.rank})");

                int targetIndex = (currentPlayerIndex + players.Count - 1) % players.Count;
                DonPlayer targetPlayer = players[targetIndex];

                if (targetPlayer.GetHandTotal() == total)
                {
                    Debug.Log($"{targetPlayer.name} DECLARED DON-GAESHI!!!");
                    WinByDonGaeshi(targetPlayer, callingPlayer);
                }
                else
                {
                    WinByDon(callingPlayer, targetPlayer, total);
                }
            }
            else
            {
                Debug.Log($"{callingPlayer.name} failed Don check. Total: {total}, Target: {topCard.rank}");
            }
        }

        private void WinByEmptyHand(DonPlayer winner)
        {
            isRoundOver = true;
            Debug.Log($"{winner.name} won by emptying hand!");

            int totalWinPoints = 0;
            foreach (var p in players)
            {
                if (p == winner) continue;
                int penalty = p.GetHandTotal() * 10;
                p.credits -= penalty;
                totalWinPoints += penalty;
            }
            winner.credits += totalWinPoints;

            EndRound();
        }

        private void WinByDon(DonPlayer winner, DonPlayer loser, int donValue)
        {
            isRoundOver = true;
            int penalty = (donValue * 2 + loser.GetHandTotal()) * 10;
            loser.credits -= penalty;
            winner.credits += penalty;

            Debug.Log($"{winner.name} won by Don! {loser.name} lost {penalty} credits.");
            EndRound();
        }

        private void WinByDonGaeshi(DonPlayer winner, DonPlayer loser)
        {
            isRoundOver = true;
            int donValue = GetTopDiscard().rank;
            int award = donValue * 100;
            loser.credits -= award;
            winner.credits += award;

            Debug.Log($"{winner.name} won by Don-Gaeshi! {loser.name} lost {award} credits.");
            EndRound();
        }

        private void EndRound()
        {
            Debug.Log("Round Ended.");
            foreach (var p in players)
            {
                Debug.Log($"{p.name}: {p.credits} Credits");
            }
        }

        #endregion

        #region Helper Methods

        public Card GetTopDiscard()
        {
            return discardPile.Count > 0 ? discardPile.Last() : null;
        }

        private Suit GetAiBestSuit(DonPlayer ai)
        {
            return ai.hand.GroupBy(c => c.suit)
                          .OrderByDescending(g => g.Count())
                          .First().Key;
        }

        private void ExecuteAiTurn()
        {
            if (isRoundOver) return;

            DonPlayer ai = players[currentPlayerIndex];

            if (ai.GetHandTotal() == GetTopDiscard().rank && ai.GetHandTotal() <= 13)
            {
                DeclareDon(ai);
                return;
            }

            Card playable = ai.hand.FirstOrDefault(c => {
                Card top = GetTopDiscard();
                if (drawPenaltyCount > 0) return c.rank == 2;
                return c.rank == top.rank || c.suit == currentActiveSuit || c.rank == 8;
            });

            if (playable != null)
            {
                TryPlayCard(ai, playable);
            }
            else
            {
                PlayerDraw(ai);
            }
        }

        #endregion
    }
}
