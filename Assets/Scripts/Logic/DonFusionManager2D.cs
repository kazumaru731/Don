using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DonGame2D.Models;

namespace DonGame2D.Logic
{
    public struct ActorInfo : INetworkStruct
    {
        public int ActorId;
        public PlayerRef PlayerRef;
        public NetworkBool IsCPU;
        public NetworkBool IsActive;
    }

    public class DonFusionManager2D : NetworkBehaviour
    {
        public static DonFusionManager2D Instance { get; private set; }

        [Header("Game Settings")]
        public int initialHandCount = 5;

        [Networked]
        public NetworkBool IsGameStarted { get; set; }
        private bool _uiStarted = false;

        [Networked]
        public int DrawPenaltyCount { get; set; }

        [Networked]
        public int ActiveSuitInt { get; set; }

        [Networked]
        public int DiscardCount { get; set; }

        [Networked]
        public int DrawCount { get; set; }

        [Networked]
        public NetworkBool IsRoundOver { get; set; }

        [Networked]
        public int WinnerActorId { get; set; }

        [Networked]
        public NetworkBool IsWaitingForSuitSelection { get; set; }

        [Networked, Capacity(8)]
        public NetworkArray<ActorInfo> Actors { get; }

        [Networked, Capacity(54)]
        public NetworkArray<CardInfo> DiscardPile { get; }

        [Networked, Capacity(54)]
        public NetworkArray<CardInfo> DrawPile { get; }

        [Networked, Capacity(8)]
        public NetworkArray<int> PlayerHandCounts { get; }

        [Networked]
        public int CurrentTurnPlayerActorId { get; set; }

        [Networked]
        public int TargetTotalPlayers { get; set; } = 4;

        [Networked, Capacity(8)]
        public NetworkDictionary<PlayerRef, NetworkBool> PlayerReadyStates { get; }

        [Networked]
        public int CurrentRound { get; set; }

        [Networked, Capacity(8)]
        public NetworkDictionary<int, int> PlayerCredits { get; }

        [Networked]
        public NetworkBool IsGameOver { get; set; }

        [Networked]
        public TickTimer RoundEndTimer { get; set; }

        [Networked]
        public TickTimer GameStartCountdown { get; set; }

        [Networked]
        public int ReadyPlayerCount { get; set; }

        [Networked]
        public TickTimer DonGraceTimer { get; set; }

        [Networked]
        public NetworkBool IsDonWindowOpen { get; set; }

        [Networked]
        public int LastPlayedPlayerActorId { get; set; }

        [Networked, Capacity(8)]
        public NetworkArray<NetworkBool> IsDonRestricted { get; }

        [Networked]
        public NetworkBool IsWaitingForDonGaeshi { get; set; }

        [Networked]
        public NetworkBool IsInitialDeal { get; set; }

        [Networked]
        public NetworkBool IsDeterminingStarter { get; set; } // [NEW] 1ラウンド目の親決め中かどうか

        [Networked]
        public int DonDeclarerActorId { get; set; }

        [Networked]
        public int DonTargetActorId { get; set; }

        [Networked]
        public int MaxRounds { get; set; } = 5;

        [Networked]
        public TickTimer CpuThinkTimer { get; set; }

        [Networked]
        public TickTimer DrawAnimationTimer { get; set; }

        public bool IsDrawing => DrawAnimationTimer.TargetTick > 0 && !DrawAnimationTimer.Expired(Runner);

        [Networked]
        public int PendingWinnerActorId { get; set; }

        [Networked, Capacity(8)]
        public NetworkArray<int> DonCallerActorIds { get; }

        [Networked]
        public int DonCallersCount { get; set; }

        [Networked]
        public NetworkBool IsStartingWild { get; set; }

        [Networked]
        public int FinishedAnimationCount { get; set; }

        public List<CardInfo> myLocalHand = new List<CardInfo>();
        public event System.Action OnHandUpdated;

        private Dictionary<int, List<CardInfo>> serverHandData = new Dictionary<int, List<CardInfo>>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public override void Spawned()
        {
            if (Instance == null) Instance = this;
            PendingWinnerActorId = -1;
            if (IsGameStarted) OnGameStartedChanged();
        }

        public override void Render()
        {
            if (IsGameStarted && !_uiStarted) OnGameStartedChanged();
        }

        private void OnGameStartedChanged()
        {
            if (IsGameStarted && !_uiStarted)
            {
                Debug.Log("[Don] Game started detected in Render. Switching UI...");
                var titleUI = FindObjectOfType<DonGame2D.UI.TitleUIController>();
                if (titleUI != null) { _uiStarted = true; titleUI.SwitchToGameUI(); }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            if (!IsGameStarted) UpdateLobbyCountdown();
            else if (!IsRoundOver) UpdateGameLogic();
            else if (!IsGameOver)
            {
                int humanPlayerCount = 0;
                for (int i = 0; i < Actors.Length; i++) {
                    var a = Actors.Get(i);
                    if (a.IsActive && !a.IsCPU) humanPlayerCount++;
                }

                bool allFinished = (humanPlayerCount == 0 || FinishedAnimationCount >= humanPlayerCount);
                
                if (RoundEndTimer.IsRunning && (RoundEndTimer.Expired(Runner) || allFinished))
                {
                    RoundEndTimer = TickTimer.None;
                    FinishedAnimationCount = 0;
                    RequestNextRoundInternal();
                }
            }
        }

        private void UpdateLobbyCountdown()
        {
            var players = Runner.ActivePlayers.ToList();
            if (players.Count == 0) return;
            bool allReady = true;
            foreach (var p in players) { if (!PlayerReadyStates.TryGet(p, out var r) || !r) { allReady = false; break; } }

            if (allReady)
            {
                if (!GameStartCountdown.IsRunning) { ReadyPlayerCount = players.Count; GameStartCountdown = TickTimer.CreateFromSeconds(Runner, 5f); }
                if (players.Count > ReadyPlayerCount) { GameStartCountdown = TickTimer.None; ReadyPlayerCount = 0; }
                if (GameStartCountdown.Expired(Runner)) { GameStartCountdown = TickTimer.None; RPC_StartGame(); }
            }
            else { GameStartCountdown = TickTimer.None; ReadyPlayerCount = 0; }
        }

        private void UpdateGameLogic()
        {
            if (IsDeterminingStarter) return;

            if (!IsWaitingForDonGaeshi && DiscardCount > 0) TryCpuDonAction();

            if (IsDonWindowOpen && DonGraceTimer.IsRunning && DonGraceTimer.Expired(Runner))
            {
                if (DiscardCount > 0)
                {
                    CardInfo top = DiscardPile.Get(DiscardCount - 1);
                    for (int i = 0; i < Actors.Length; i++)
                    {
                        var a = Actors.Get(i);
                        if (a.IsActive && a.ActorId != LastPlayedPlayerActorId && !IsDonRestricted[i])
                        {
                            if (ServerGetHandTotal(a.ActorId) == top.Rank)
                            {
                                bool didCall = false;
                                for (int j = 0; j < DonCallersCount; j++)
                                {
                                    if (DonCallerActorIds[j] == a.ActorId) { didCall = true; break; }
                                }
                                if (!didCall)
                                {
                                    IsDonRestricted.Set(i, true);
                                    Debug.Log($"[Don] Player {a.ActorId} restricted from Donning (skipped opportunity).");
                                }
                            }
                        }
                    }
                }

                IsDonWindowOpen = false;
                IsWaitingForDonGaeshi = false;
                DonGraceTimer = TickTimer.None;

                if (DonCallersCount > 0) ConfirmMultiDonWin();
                else if (PendingWinnerActorId > 0)
                {
                    int win = PendingWinnerActorId; PendingWinnerActorId = -1;
                    if (DrawPenaltyCount > 0) StartCoroutine(Co_ProcessOutWinWithPenalty(win));
                    else ConfirmOutWin(win);
                }
                else
                {
                    RotateTurn();
                }
            }

            bool drawing = DrawAnimationTimer.TargetTick > 0 && !DrawAnimationTimer.Expired(Runner);
            if (!drawing && !IsDonWindowOpen && !IsWaitingForDonGaeshi && DonCallersCount == 0 && PendingWinnerActorId == -1)
            {
                var actor = GetActor(CurrentTurnPlayerActorId);
                bool shouldThink = actor.IsActive && actor.IsCPU && !CpuThinkTimer.IsRunning;
                
                if (!IsWaitingForSuitSelection)
                {
                    if (shouldThink) CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, Random.Range(0.4f, 0.8f));
                }
                else
                {
                    if (shouldThink) CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, Random.Range(0.4f, 0.8f));
                }
            }

            if (DrawAnimationTimer.TargetTick > 0 && DrawAnimationTimer.Expired(Runner))
            {
                DrawAnimationTimer = TickTimer.None;
                bool wasInitial = IsInitialDeal;
                IsInitialDeal = false;
                
                if (!wasInitial) 
                {
                    RotateTurn();
                }
                else
                {
                    CurrentTurnPlayerActorId = (WinnerActorId > 0) ? WinnerActorId : Actors.Get(0).ActorId;
                    Debug.Log($"[Don] Initial deal finished. Starting first turn: {CurrentTurnPlayerActorId}");
                }
            }

            if (CpuThinkTimer.IsRunning && CpuThinkTimer.Expired(Runner))
            {
                CpuThinkTimer = TickTimer.None;
                if (IsWaitingForSuitSelection) ProcessCpuSuitSelection();
                else ProcessCpuAction();
            }
        }

        private void ProcessCpuSuitSelection()
        {
            if (IsDonWindowOpen || PendingWinnerActorId != -1) return;
            var actor = GetActor(CurrentTurnPlayerActorId);
            if (actor.IsActive && actor.IsCPU && serverHandData.TryGetValue(actor.ActorId, out var hand))
            {
                ActiveSuitInt = hand.Count > 0 ? hand.GroupBy(c => c.SuitInt).OrderByDescending(g => g.Count()).First().Key : 0;
                IsWaitingForSuitSelection = false;
                
                if (IsStartingWild)
                {
                    IsStartingWild = false;
                    CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, Random.Range(0.5f, 1.0f));
                    Debug.Log($"[Don] CPU {actor.ActorId} selected starting suit: {ActiveSuitInt}. Continuing turn.");
                }
                else
                {
                    CheckDonOpportunity(actor.ActorId, DiscardPile.Get(DiscardCount - 1), false);
                }
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_StartGame()
        {
            Debug.Log($"[Don] RPC_StartGame received. HasStateAuthority: {Object.HasStateAuthority}");
            if (!Object.HasStateAuthority) return;
            StartGameInternal();
        }

        public void StartGameInternal(bool isRestart = false)
        {
            if (IsGameStarted && !isRestart) return;
            if (!isRestart) CurrentRound = 1;
            Debug.Log($"[Don] StartGameInternal: Beginning setup (isRestart={isRestart}) for {TargetTotalPlayers} players...");
            IsGameStarted = true; IsInitialDeal = true; LastPlayedPlayerActorId = -1;
            PendingWinnerActorId = -1;
            
            ServerSetupActors(TargetTotalPlayers);
            
            RPC_NotifyGameStarted();
            TriggerLocalUIGameTransition();

            if (CurrentRound == 1 && !isRestart)
            {
                StartCoroutine(ServerStarterDeterminationSequence());
                return;
            }

            FinishGameSetupAndDeal(isRestart);
        }

        private IEnumerator ServerStarterDeterminationSequence()
        {
            IsDeterminingStarter = true;
            WinnerActorId = -1;

            var activeActors = new List<ActorInfo>();
            for (int i = 0; i < Actors.Length; i++)
                if (Actors[i].IsActive) activeActors.Add(Actors[i]);

            List<int> candidates = activeActors.Select(a => a.ActorId).ToList();
            candidates.Sort(); // ID順でソートして順序を固定する

            while (candidates.Count > 1)
            {
                var deck = CreateFullDeck();
                Shuffle(deck);
                
                Dictionary<int, CardInfo> drawnCards = new Dictionary<int, CardInfo>();
                foreach (var actorId in candidates)
                {
                    drawnCards[actorId] = deck[0];
                    deck.RemoveAt(0);
                }

                int[] actorIds = candidates.ToArray();
                CardInfo[] cards = new CardInfo[actorIds.Length];
                for (int i = 0; i < actorIds.Length; i++) cards[i] = drawnCards[actorIds[i]];
                
                RPC_AnimateStarterDraw(actorIds, cards);
                
                // 配布スピード (0.4s + 0.08s * 人数) 分待機
                yield return new WaitForSeconds(0.5f * actorIds.Length + 1.0f);

                // 公開して結果判定
                int maxVal = -1;
                foreach (var card in cards)
                {
                    int val = GetStarterCardValue(card);
                    if (val > maxVal) maxVal = val;
                }

                List<int> winners = new List<int>();
                List<int> losers = new List<int>();
                foreach (var kvp in drawnCards)
                {
                    if (GetStarterCardValue(kvp.Value) == maxVal) winners.Add(kvp.Key);
                    else losers.Add(kvp.Key);
                }

                // 敗者を山札に戻す演出
                RPC_AnimateStarterResult(winners.ToArray(), losers.ToArray());
                
                // 演出待ち (敗者回収 1s + 強調 1s + 勝者回収 1s + 余裕 1s)
                yield return new WaitForSeconds(4.0f);

                candidates = winners;
                // まだ決まらなければループ
            }

            if (candidates.Count == 1)
            {
                WinnerActorId = candidates[0];
                Debug.Log($"[Don] Starter determined: Player {WinnerActorId}");
            }

            IsDeterminingStarter = false;
            
            FinishGameSetupAndDeal(false);
        }

        private int GetStarterCardValue(CardInfo card)
        {
            if (card.SuitInt == 4) return 100;
            if (card.Rank == 1) return 14;
            return card.Rank;
        }

        private void FinishGameSetupAndDeal(bool isRestart)
        {
            IsInitialDeal = true;
            LastPlayedPlayerActorId = -1;

            // [FIX] 実際の手札配布が始まるこのタイミングで正確な待ち時間を設定する
            float dealDuration = Mathf.Max(2.8f, (TargetTotalPlayers * initialHandCount * 0.08f) + 1.0f);
            DrawAnimationTimer = TickTimer.CreateFromSeconds(Runner, dealDuration);
            
            var deck = CreateFullDeck();
            Shuffle(deck);
            serverHandData.Clear();
            for (int i = 0; i < Actors.Length; i++)
            {
                var actor = Actors.Get(i); if (!actor.IsActive) continue;
                var hand = deck.GetRange(0, initialHandCount); deck.RemoveRange(0, initialHandCount);
                serverHandData[actor.ActorId] = new List<CardInfo>(hand);
                if (!actor.IsCPU) foreach (var c in hand) RPC_ReceiveCard(actor.PlayerRef, c);
                PlayerHandCounts.Set(i, initialHandCount);
            }

            DrawCount = 0; DiscardCount = 0;
            foreach (var card in deck) { DrawPile.Set(DrawCount, card); DrawCount++; }
            
            if (isRestart && WinnerActorId > 0)
            {
                CurrentTurnPlayerActorId = -1;
            }
            else
            {
                CurrentTurnPlayerActorId = -1;
            }
            if (DrawCount > 0)
            {
                DrawCount--;
                var top = DrawPile.Get(DrawCount);
                AddCardToDiscard(top);
                LastPlayedPlayerActorId = -1;
                if (top.Rank == 2) DrawPenaltyCount = 2;
                if (top.Rank == 8)
                {
                    IsStartingWild = true;
                    IsWaitingForSuitSelection = true;
                }
            }
            Debug.Log("[Don] StartGameInternal: Setup Complete.");
        }

        private void TriggerLocalUIGameTransition()
        {
            if (_uiStarted) return;
            var titleUI = FindObjectOfType<DonGame2D.UI.TitleUIController>();
            if (titleUI != null) { _uiStarted = true; titleUI.SwitchToGameUI(); }
        }

        public void ServerSetupActors(int count)
        {
            int idx = 0; int id = 1;
            var players = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
            foreach (var p in players) { if (idx >= Actors.Length) break; Actors.Set(idx++, new ActorInfo { ActorId = id++, PlayerRef = p, IsCPU = false, IsActive = true }); }
            while (idx < count && idx < Actors.Length) { Actors.Set(idx++, new ActorInfo { ActorId = id++, PlayerRef = PlayerRef.None, IsCPU = true, IsActive = true }); }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SubmitCard(PlayerRef player, CardInfo card)
        {
            int id = GetActorId(player);
            if (id != CurrentTurnPlayerActorId || IsWaitingForSuitSelection || IsDonWindowOpen || IsInitialDeal || IsDrawing) return;
            if (serverHandData.TryGetValue(id, out var hand))
            {
                int idx = hand.FindIndex(c => c.SuitInt == card.SuitInt && c.Rank == card.Rank);
                if (idx == -1) return;
                var cp = hand[idx]; hand.RemoveAt(idx);
                UpdateHandCount(id, -1); AddCardToDiscard(cp);
                RPC_NotifyOpponentPlayCard(id, cp);
                RPC_RemoveCard(player, cp);
                if (cp.Rank == 2) DrawPenaltyCount += 2;
                if (hand.Count == 0) { PendingWinnerActorId = id; IsDonWindowOpen = true; DonGraceTimer = TickTimer.CreateFromSeconds(Runner, 1.5f); return; }
                if (cp.Rank == 8) IsWaitingForSuitSelection = true;
                else CheckDonOpportunity(id, cp, false);
            }
        }

        public void RequestDraw() { if (GetActorId(Runner.LocalPlayer) == CurrentTurnPlayerActorId && !IsWaitingForSuitSelection && !IsDonWindowOpen) RPC_RequestDraw(Runner.LocalPlayer); }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestDraw(PlayerRef p)
        {
            if (IsInitialDeal || IsDrawing) return;
            int id = GetActorId(p); int count = DrawPenaltyCount > 0 ? DrawPenaltyCount : 1; DrawPenaltyCount = 0;
            int actual = 0;
            for (int i = 0; i < count; i++) if (DrawCount > 0) { DrawCount--; var card = DrawPile.Get(DrawCount); ServerAddCardToHand(id, card); RPC_ReceiveCard(p, card); actual++; }
            if (actual > 0) { 
                UpdateHandCount(id, actual); 
                RPC_NotifyOpponentDrawCard(id, actual); 
                DrawAnimationTimer = TickTimer.CreateFromSeconds(Runner, 0.5f + (actual * 0.15f));
            }
            else RotateTurn();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_DeclareDon(PlayerRef p)
        {
            int id = GetActorId(p); if (IsRoundOver) return;
            for (int i = 0; i < DonCallersCount; i++) if (DonCallerActorIds.Get(i) == id) return;
            int total = ServerGetHandTotal(id); CardInfo top = DiscardCount > 0 ? DiscardPile.Get(DiscardCount - 1) : default;
            if (DiscardCount > 0 && total == top.Rank && total <= 13)
            {
                if (DonCallersCount < 8) { DonCallerActorIds.Set(DonCallersCount++, id); RPC_NotifyDon(id, ServerGetHandString(id)); }
                if (DonCallersCount == 1) { DonTargetActorId = LastPlayedPlayerActorId; DonDeclarerActorId = id; PendingWinnerActorId = -1; IsDonWindowOpen = true; DonGraceTimer = TickTimer.CreateFromSeconds(Runner, 3.0f); if (DonTargetActorId != -1 && ServerGetHandTotal(DonTargetActorId) == top.Rank) IsWaitingForDonGaeshi = true; }
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_DeclareDonGaeshi(PlayerRef p)
        {
            int id = GetActorId(p);
            if (IsWaitingForDonGaeshi && id == DonTargetActorId && ServerGetHandTotal(id) == DiscardPile.Get(DiscardCount - 1).Rank)
            {
                IsWaitingForDonGaeshi = false;
                RPC_NotifyDon(id, ServerGetHandString(id));
                ConfirmDonGaeshiWin(id, DonDeclarerActorId, DiscardPile.Get(DiscardCount - 1).Rank);
            }
        }

        private void ConfirmDonGaeshiWin(int win, int lose, int val) { StartCoroutine(Co_ConfirmDonGaeshiWin(win, lose, val)); }
        private IEnumerator Co_ConfirmDonGaeshiWin(int win, int lose, int val)
        {
            if (IsRoundOver) yield break; IsRoundOver = true; int pen = val * 100; AddCredits(lose, -pen);
            WinnerActorId = win; RPC_PlayRoundEndAnim(1, win, lose, val, $"{lose}:{ServerGetHandString(lose)}", pen, $"Player {win} DON-GAESHI! (+{pen} Credits)", CurrentRound >= MaxRounds, $"Player {win}", ServerGetHandString(win));
            yield return new WaitForSeconds(2.5f); AddCredits(win, pen); 
            FinishedAnimationCount = 0;
            RoundEndTimer = TickTimer.CreateFromSeconds(Runner, 30.0f);
        }

        private void ConfirmMultiDonWin() { StartCoroutine(Co_ConfirmMultiDonWin()); }
        private IEnumerator Co_ConfirmMultiDonWin()
        {
            if (IsRoundOver) yield break; 
            IsRoundOver = true; 
            FinishedAnimationCount = 0;
            int callersCount = DonCallersCount; 
            int target = DonTargetActorId; 
            List<string> losers = new List<string>();

            CardInfo topCard = DiscardCount > 0 ? DiscardPile.Get(DiscardCount - 1) : default;
            int rank = topCard.Rank;

            int totalPenalty = rank * (callersCount + 1) * 10;
            
            if (target != -1) 
            { 
                AddCredits(target, -totalPenalty); 
                losers.Add($"{target}:{ServerGetHandString(target)}"); 
            }
            else 
            { 
                for (int i = 0; i < Actors.Length; i++) 
                { 
                    var a = Actors.Get(i); if (!a.IsActive) continue; 
                    bool isWinner = false; 
                    for (int j = 0; j < callersCount; j++) if (DonCallerActorIds.Get(j) == a.ActorId) isWinner = true; 
                    if (!isWinner) { AddCredits(a.ActorId, -totalPenalty); losers.Add($"{a.ActorId}:{ServerGetHandString(a.ActorId)}"); } 
                } 
            }

            int totalUnits = totalPenalty / 10;
            int unitsPerCaller = totalUnits / callersCount;
            int remainderUnits = totalUnits % callersCount;

            int luckyIndex = UnityEngine.Random.Range(0, callersCount);
            int luckyActorId = DonCallerActorIds.Get(luckyIndex);
            
            WinnerActorId = luckyActorId;

            string winners = string.Join(", ", Enumerable.Range(0, callersCount).Select(i => $"Player {DonCallerActorIds.Get(i)}"));
            RPC_PlayRoundEndAnim(0, WinnerActorId, target, rank, string.Join("|", losers), totalPenalty, $"{winners} DON! (+{totalPenalty} Credits)", CurrentRound >= MaxRounds, winners, ServerGetHandString(WinnerActorId));

            yield return new WaitForSeconds(2.5f); 
            
            for (int i = 0; i < callersCount; i++) 
            {
                int actorId = DonCallerActorIds.Get(i);
                int units = unitsPerCaller + (i == luckyIndex ? remainderUnits : 0);
                AddCredits(actorId, units * 10);
            }
            
            RoundEndTimer = TickTimer.CreateFromSeconds(Runner, 30.0f);
        }

        private void ConfirmOutWin(int win)
        {
            if (IsRoundOver) return; IsRoundOver = true; WinnerActorId = win; int total = 0; List<string> hands = new List<string>();
            for (int i = 0; i < Actors.Length; i++) { var a = Actors.Get(i); if (a.IsActive && a.ActorId != win) { int p = ServerGetHandTotal(a.ActorId) * 10; AddCredits(a.ActorId, -p); total += p; hands.Add($"{a.ActorId}:{ServerGetHandString(a.ActorId)}"); } }
            RPC_PlayRoundEndAnim(2, win, -1, 0, string.Join("|", hands), total, $"Player {win} OUT WIN! (+{total} Credits)", CurrentRound >= MaxRounds, $"Player {win}", "");
            AddCredits(win, total); 
            FinishedAnimationCount = 0;
            RoundEndTimer = TickTimer.CreateFromSeconds(Runner, 30.0f);
        }

        private IEnumerator Co_ProcessOutWinWithPenalty(int win)
        {
            if (IsRoundOver) yield break; int pen = DrawPenaltyCount; int target = GetNextActorId(win);
            if (pen > 0) { int drawn = 0; for (int i = 0; i < pen; i++) if (DrawCount > 0) { DrawCount--; var c = DrawPile.Get(DrawCount); ServerAddCardToHand(target, c); drawn++; } if (drawn > 0) { UpdateHandCount(target, drawn); RPC_NotifyOpponentDrawCard(target, drawn); yield return new WaitForSeconds(1.5f + drawn * 0.2f); } }
            ConfirmOutWin(win);
        }

        private void AddCredits(int id, int amt) { if (PlayerCredits.ContainsKey(id)) { int curr = PlayerCredits[id]; PlayerCredits.Set(id, curr + amt); } else PlayerCredits.Add(id, amt); }

        private void RotateTurn() 
        { 
            int prevId = CurrentTurnPlayerActorId;
            int prevIdx = GetActorIndex(prevId);
            if (prevIdx != -1)
            {
                IsDonRestricted.Set(prevIdx, false);
            }

            int next = GetNextActorId(prevId); 
            CurrentTurnPlayerActorId = next; 
            var a = GetActor(next); 
            if (a.IsActive && a.IsCPU) CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, Random.Range(0.5f, 1.0f)); 
        }
        public int GetNextActorId(int curr) { int idx = GetActorIndex(curr); for (int i = 1; i <= Actors.Length; i++) { int n = (idx + i) % Actors.Length; if (Actors.Get(n).IsActive) return Actors.Get(n).ActorId; } return curr; }
        private void CheckDonOpportunity(int playedId, CardInfo card, bool isEight) 
        { 
            LastPlayedPlayerActorId = playedId; 
            bool target = false; 
            if (card.Rank <= 13) 
            { 
                for (int i = 0; i < Actors.Length; i++) 
                { 
                    var a = Actors.Get(i); 
                    if (a.IsActive && a.ActorId != playedId && !IsDonRestricted[i] && ServerGetHandTotal(a.ActorId) == card.Rank) { target = true; break; } 
                } 
            } 
            
            if (target) 
            { 
                IsDonWindowOpen = true; 
                DonTargetActorId = playedId; 
                DonGraceTimer = TickTimer.CreateFromSeconds(Runner, 3.0f); 
                DonCallersCount = 0; 
                for (int i = 0; i < Actors.Length; i++) DonCallerActorIds.Set(i, -1); 
            } 
            else 
            { 
                IsDonWindowOpen = false; 
                DonGraceTimer = TickTimer.None; 
                if (!isEight) RotateTurn(); 
            } 
        }
        private void ProcessCpuAction()
        {
            if (IsDonWindowOpen || IsWaitingForSuitSelection || PendingWinnerActorId != -1) return;
            var a = GetActor(CurrentTurnPlayerActorId);
            if (!a.IsActive || !a.IsCPU || !serverHandData.TryGetValue(a.ActorId, out var hand)) return;

            CardInfo top = DiscardCount > 0 ? DiscardPile.Get(DiscardCount - 1) : default;
            var playable = hand.Where(c => CanPlayCard(c)).ToList();

            if (playable.Count > 0)
            {
                var card = playable.OrderByDescending(c => c.Rank).First();
                hand.Remove(card);
                AddCardToDiscard(card);
                RPC_NotifyOpponentPlayCard(a.ActorId, card);
                if (card.Rank == 2) DrawPenaltyCount += 2;
                UpdateHandCount(a.ActorId, -1);
                if (hand.Count == 0)
                {
                    PendingWinnerActorId = a.ActorId;
                    IsDonWindowOpen = true;
                    DonGraceTimer = TickTimer.CreateFromSeconds(Runner, 1.5f);
                }
                else if (card.Rank == 8)
                {
                    ActiveSuitInt = hand.Count > 0 ? hand.GroupBy(c => c.SuitInt).OrderByDescending(g => g.Count()).First().Key : 0;
                    IsWaitingForSuitSelection = false;
                    CheckDonOpportunity(a.ActorId, card, false);
                }
                else CheckDonOpportunity(a.ActorId, card, false);
            }
            else
            {
                IsDonWindowOpen = false;
                int count = DrawPenaltyCount > 0 ? DrawPenaltyCount : 1;
                DrawPenaltyCount = 0;
                int drawn = 0;
                for (int i = 0; i < count; i++)
                {
                    if (DrawCount > 0)
                    {
                        DrawCount--;
                        var c = DrawPile.Get(DrawCount);
                        ServerAddCardToHand(a.ActorId, c);
                        drawn++;
                    }
                }
                if (drawn > 0)
                {
                    UpdateHandCount(a.ActorId, drawn);
                    RPC_NotifyOpponentDrawCard(a.ActorId, drawn);
                    DrawAnimationTimer = TickTimer.CreateFromSeconds(Runner, 0.5f + (drawn * 0.15f));
                }
                else RotateTurn();
            }
        }
        private void TryCpuDonAction()
        {
            if (!Object.HasStateAuthority || !IsDonWindowOpen || IsWaitingForDonGaeshi) return;
            CardInfo top = DiscardCount > 0 ? DiscardPile.Get(DiscardCount - 1) : default;
            if (DiscardCount == 0 || top.Rank > 13) return;

            for (int i = 0; i < Actors.Length; i++)
            {
                var a = Actors.Get(i);
                if (a.IsActive && a.IsCPU && ServerGetHandTotal(a.ActorId) == top.Rank)
                {
                    bool already = false;
                    for (int j = 0; j < DonCallersCount; j++) if (DonCallerActorIds[j] == a.ActorId) already = true;
                    if (already) continue;

                    if (DonCallersCount < 8)
                    {
                        DonCallerActorIds.Set(DonCallersCount++, a.ActorId);
                        RPC_NotifyDon(a.ActorId, ServerGetHandString(a.ActorId));

                        if (DonCallersCount == 1)
                        {
                            DonTargetActorId = LastPlayedPlayerActorId;
                            DonDeclarerActorId = a.ActorId;
                            PendingWinnerActorId = -1;
                            IsDonWindowOpen = true;
                            DonGraceTimer = TickTimer.CreateFromSeconds(Runner, 3.0f);

                            if (DonTargetActorId != -1 && ServerGetHandTotal(DonTargetActorId) == top.Rank)
                            {
                                IsWaitingForDonGaeshi = true;
                                if (GetActor(DonTargetActorId).IsCPU)
                                {
                                    RPC_NotifyDon(DonTargetActorId, ServerGetHandString(DonTargetActorId));
                                    IsWaitingForDonGaeshi = false;
                                    ConfirmDonGaeshiWin(DonTargetActorId, a.ActorId, top.Rank);
                                }
                            }
                        }
                        return;
                    }
                }
            }
        }
        private void ConfirmDonWin(int win, int target, int val) { StartCoroutine(Co_ConfirmDonWin(win, target, val)); }
        private IEnumerator Co_ConfirmDonWin(int win, int target, int val) { if (IsRoundOver) yield break; IsRoundOver = true; int pen = val * 100; AddCredits(target, -pen); WinnerActorId = win; RPC_PlayRoundEndAnim(0, win, target, val, $"{target}:{ServerGetHandString(target)}", pen, $"Player {win} DON! (+{pen} Credits)", CurrentRound >= MaxRounds, $"Player {win}", ServerGetHandString(win)); yield return new WaitForSeconds(2.5f); AddCredits(win, pen); FinishedAnimationCount = 0; RoundEndTimer = TickTimer.CreateFromSeconds(Runner, 30.0f); }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ReportAnimationFinished() { if (Object.HasStateAuthority) FinishedAnimationCount++; }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)] public void RPC_NotifyDon(int id, string hand) { var ui = FindObjectOfType<DonGame2D.UI.GameUIController>(); if (ui != null) ui.ShowDonAnimation(id, hand); }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)] public void RPC_PlayRoundEndAnim(int type, int win, int lose, int val, string loseHand, int gain, string msg, bool isFinal, string winName, string winHand) { var ui = FindObjectOfType<DonGame2D.UI.GameUIController>(); if (ui != null) ui.PlayRoundEndAnimation(type, win, lose, val, loseHand, gain, msg, isFinal, winName, winHand); }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)] public void RPC_NotifyOpponentPlayCard(int id, CardInfo c) { var ui = FindObjectOfType<DonGame2D.UI.GameUIController>(); if (ui != null) ui.PlayOpponentCardAnimation(id, c); }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)] public void RPC_NotifyOpponentDrawCard(int id, int c) { var ui = FindObjectOfType<DonGame2D.UI.GameUIController>(); if (ui != null) ui.PlayOpponentDrawAnimation(id, c); }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyPenaltyReceived(int actorId, int amount)
        {
            var ui = FindObjectOfType<DonGame2D.UI.GameUIController>();
            if (ui != null)
            {
                ui.ShowDrawPenaltyEffect(actorId, amount);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_AnimateStarterDraw(int[] actors, CardInfo[] cards)
        {
            var ui = FindObjectOfType<DonGame2D.UI.GameUIController>();
            if (ui != null)
                ui.OnStarterDrawStarted(actors, cards);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_AnimateStarterResult(int[] winners, int[] losers)
        {
            var ui = FindObjectOfType<DonGame2D.UI.GameUIController>();
            if (ui != null)
                ui.OnStarterResult(winners, losers);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyGameStarted()
        { 
            myLocalHand.Clear(); 
            TriggerLocalUIGameTransition();
            
            var ui = FindObjectOfType<DonGame2D.UI.GameUIController>();
            if (ui != null) ui.ResetUIForNewRound();

            OnHandUpdated?.Invoke();
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)] public void RPC_ReceiveCard([RpcTarget] PlayerRef p, CardInfo c) { if (Runner.LocalPlayer == p) { myLocalHand.Add(c); OnHandUpdated?.Invoke(); } }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)] public void RPC_RemoveCard([RpcTarget] PlayerRef p, CardInfo c) { if (Runner.LocalPlayer == p) { int idx = myLocalHand.FindIndex(card => card.SuitInt == c.SuitInt && card.Rank == c.Rank); if (idx != -1) { myLocalHand.RemoveAt(idx); OnHandUpdated?.Invoke(); } } }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)] public void RPC_RequestNextRound() { if (Object.HasStateAuthority) { CurrentRound++; StartNewRound(); } }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SubmitSuitChoice(int suit)
        {
            if (IsWaitingForSuitSelection)
            {
                ActiveSuitInt = suit;
                IsWaitingForSuitSelection = false;
                
                if (IsStartingWild)
                {
                    // 開始時の 8 の場合はターンを回さず、最初のプレイヤーがそのままプレイを続ける
                    IsStartingWild = false;
                }
                else
                {
                    // 通常時（手札から8を出した時）はターン終了判定へ
                    CheckDonOpportunity(CurrentTurnPlayerActorId, DiscardPile.Get(DiscardCount - 1), false);
                }
            }
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)] public void RPC_UpdateRoundSettings(int rounds) { MaxRounds = rounds; }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)] public void RPC_FriendMatchForceStart(int count, int rounds) { ServerSetupActors(count); MaxRounds = rounds; RPC_StartGame(); }

        public void SetPlayerReady(bool ready) { if (Runner.LocalPlayer != PlayerRef.None) RPC_SetPlayerReady(Runner.LocalPlayer, ready); }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)] private void RPC_SetPlayerReady(PlayerRef p, bool r) { PlayerReadyStates.Set(p, r); }
        public void ForceStartGameByHost(int count = -1) { if (count > 0) TargetTotalPlayers = count; Debug.Log($"[Don] ForceStartGameByHost called (count={count}). Valid: {Object != null && Object.IsValid}, Auth: {Object?.HasStateAuthority}"); if (Object != null && Object.IsValid) { if (Object.HasStateAuthority) StartGameInternal(); else RPC_StartGame(); } }
        public bool CanPlayCard(CardInfo card)
        {
            if (!IsGameStarted || IsRoundOver || IsInitialDeal || IsDrawing) return false;
            // 2ドロー（スタック）発生中は、2以外出せない
            if (DrawPenaltyCount > 0) return card.Rank == 2;

            var top = DiscardCount > 0 ? DiscardPile.Get(DiscardCount - 1) : default;
            return DiscardCount == 0 || card.Rank == top.Rank || card.SuitInt == ActiveSuitInt || card.Rank == 8;
        }
        public void TryPlayCard(CardInfo card) { RPC_SubmitCard(Runner.LocalPlayer, card); }
        public void SetLocalHand(List<CardInfo> hand) { myLocalHand = hand; OnHandUpdated?.Invoke(); }

        private List<CardInfo> CreateFullDeck()
        {
            var list = new List<CardInfo>();
            for (int s = 0; s < 4; s++)
            {
                for (int r = 1; r <= 13; r++)
                {
                    list.Add(new CardInfo { SuitInt = s, Rank = r });
                }
            }
            // ジョーカー（Rank 14, 15 / Suit 4）は削除
            return list;
        }
        private void Shuffle(List<CardInfo> list) { for (int i = 0; i < list.Count; i++) { int r = Random.Range(i, list.Count); var tmp = list[i]; list[i] = list[r]; list[r] = tmp; } }
        private void AddCardToDiscard(CardInfo c) { DiscardPile.Set(DiscardCount, c); DiscardCount++; ActiveSuitInt = c.SuitInt; LastPlayedPlayerActorId = CurrentTurnPlayerActorId; }
        private void ServerAddCardToHand(int id, CardInfo c) { if (serverHandData.TryGetValue(id, out var h)) h.Add(c); }
        private int ServerGetHandTotal(int id) => serverHandData.TryGetValue(id, out var h) ? h.Sum(ca => ca.Rank) : 0;
        private string ServerGetHandString(int id) => serverHandData.TryGetValue(id, out var h) ? string.Join(";", h.Select(ca => $"{ca.SuitInt},{ca.Rank}")) : "";
        private void UpdateHandCount(int id, int delta) { int idx = GetActorIndex(id); if (idx != -1) PlayerHandCounts.Set(idx, PlayerHandCounts[idx] + delta); }
        public ActorInfo GetActor(int id) { for (int i = 0; i < Actors.Length; i++) if (Actors[i].ActorId == id) return Actors[i]; return default; }
        public int GetActorId(PlayerRef p) { for (int i = 0; i < Actors.Length; i++) if (Actors[i].PlayerRef == p) return Actors[i].ActorId; return -1; }
        public int GetActorIndex(int id) { for (int i = 0; i < Actors.Length; i++) if (Actors[i].ActorId == id) return i; return -1; }
        private void RequestNextRoundInternal() { if (CurrentRound < MaxRounds) { CurrentRound++; StartNewRound(); } else IsGameOver = true; }
        private void StartNewRound() { DiscardCount = 0; DrawCount = 0; DrawPenaltyCount = 0; IsRoundOver = false; IsDonWindowOpen = false; IsWaitingForDonGaeshi = false; IsWaitingForSuitSelection = false; DonCallersCount = 0; for (int i = 0; i < 8; i++) DonCallerActorIds.Set(i, -1); CurrentTurnPlayerActorId = -1; LastPlayedPlayerActorId = -1; PendingWinnerActorId = -1; StartGameInternal(true); }
    }
}
