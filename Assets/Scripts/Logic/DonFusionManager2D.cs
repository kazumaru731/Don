using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DonGame2D.Models;

namespace DonGame2D.Logic
{
    /// <summary>
    /// ゲームに参加する1人�E�実�Eレイヤーまた�ECPU�E��E惁E��を保持する構造佁E
    /// </summary>
    public struct ActorInfo : INetworkStruct
    {
        public int ActorId;             // 1~4などの連番もしく�E固定ID
        public PlayerRef PlayerRef;     // 実�Eレイヤーなら有効値、CPUなめENone
        public NetworkBool IsCPU;       // CPUフラグ
        public NetworkBool IsActive;    // こ�E枠が使用中かどぁE��
    }

    /// <summary>
    /// Photon Fusion 2 (Shared Mode) を使用した 2D ゲームマネージャー

    /// </summary>
    public class DonFusionManager2D : NetworkBehaviour
    {
        public static DonFusionManager2D Instance { get; private set; }

        [Header("Game Settings")]
        public int initialHandCount = 5;

                // --- 同期変数 (Networked) ---

        [Networked]
        private NetworkBool _isNetworkGameStarted { get; set; }

        public bool IsGameStarted
        {
            get
            {
                if (Object == null || !Object.IsValid) return false;
                return _isNetworkGameStarted;
            }
            set
            {
                if (Object != null && Object.IsValid && Object.HasStateAuthority)
                {
                    _isNetworkGameStarted = value;
                }
            }
        }

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

        [Networked, Capacity(4)]
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

        // 全員Ready後E開始征EタイマEEこの間に新規参加があれEリセチEEE
        [Networked]
        public TickTimer GameStartCountdown { get; set; }

        // 全員Ready時Eプレイヤー数E新規参加老EEに使用EE
        [Networked]
        public int ReadyPlayerCount { get; set; }

        [Networked]
        public TickTimer DonGraceTimer { get; set; }

        [Networked]
        public NetworkBool IsDonWindowOpen { get; set; }

        [Networked]
        public int LastPlayedPlayerActorId { get; set; }

        [Networked]
        public NetworkBool IsWaitingForDonGaeshi { get; set; }

        [Networked]
        public int DonDeclarerActorId { get; set; }

        [Networked]
        public int DonTargetActorId { get; set; }

        [Networked]
        public TickTimer CpuThinkTimer { get; set; }

        // --- ローカル状態 ---
        public List<CardInfo> myLocalHand = new List<CardInfo>();
        public event System.Action OnHandUpdated;

        // --- マスター専用の手札管理 ---
        private Dictionary<int, List<CardInfo>> serverHandData = new Dictionary<int, List<CardInfo>>();
        private bool _pendingForceStart = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public override void Spawned()
        {
            if (Instance == null) Instance = this;
            
            if (Object.HasStateAuthority)
            {
                // 初期状態のリセット（既に開始されている場合はスキップ）
                if (!IsGameStarted)
                {
                    TargetTotalPlayers = DonGame2D.UI.TitleUIController.selectedTargetPlayers;
                    DrawPenaltyCount = 0;
                    ActiveSuitInt = -1;
                    DiscardCount = 0;
                    DrawCount = 0;
                    IsRoundOver = false;
                    IsGameOver = false;
                    CurrentRound = 1;
                    WinnerActorId = -1;
                    PlayerReadyStates.Clear();
                    PlayerCredits.Clear();
                    IsDonWindowOpen = false;
                    IsWaitingForDonGaeshi = false;
                    serverHandData.Clear();
                }
            }

            if (_pendingForceStart)
            {
                _pendingForceStart = false;
                Debug.Log("<color=cyan>[DonFusionManager2D] Executing PENDING ForceStart...</color>");
                ForceStartGameByHost();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority)
            {
                if (!IsGameStarted && Runner.ActivePlayers.Any())
                {
                    int activeCount = Runner.ActivePlayers.Count();

                    // 全員がReadyかどうか判定
                    bool allReady = true;
                    int readyCount = 0;
                    foreach (var player in Runner.ActivePlayers)
                    {
                        if (PlayerReadyStates.TryGet(player, out var isReady) && isReady)
                            readyCount++;
                        else
                            allReady = false;
                    }

                    if (allReady) // 実プレイヤー全員がReadyなら開始プロセスへ
                    {
                        if (!GameStartCountdown.IsRunning)
                        {
                            ReadyPlayerCount = activeCount;
                            GameStartCountdown = TickTimer.CreateFromSeconds(Runner, 5f);
                            Debug.Log($"[Ready] 全員Ready　5秒後にゲーム開始（現在{activeCount}人）");
                        }

                        if (GameStartCountdown.IsRunning && activeCount > ReadyPlayerCount)
                        {
                            Debug.Log($"[Ready] 新規参加によりカウントダウンをリセット ({activeCount}人)");
                            GameStartCountdown = TickTimer.None;
                            ReadyPlayerCount = 0;
                        }

                        if (GameStartCountdown.Expired(Runner))
                        {
                            GameStartCountdown = TickTimer.None;
                            IsGameStarted = true;
                            RPC_StartGame();
                        }
                    }
                    else
                    {
                        if (GameStartCountdown.IsRunning)
                        {
                            GameStartCountdown = TickTimer.None;
                            ReadyPlayerCount = 0;
                        }
                    }
                }
                else if (IsGameStarted && !IsRoundOver)
                {
                    if (!IsWaitingForDonGaeshi && DiscardCount > 0)
                    {
                        TryCpuDonAction();
                    }

                    if (CpuThinkTimer.IsRunning && CpuThinkTimer.Expired(Runner))
                    {
                        CpuThinkTimer = TickTimer.None;

                        if (IsWaitingForSuitSelection)
                        {
                            var cpuActor = GetActor(CurrentTurnPlayerActorId);
                            if (cpuActor.IsActive && cpuActor.IsCPU && serverHandData.TryGetValue(cpuActor.ActorId, out var hand))
                            {
                                int nextSuit = 0;
                                if (hand.Count > 0)
                                {
                                    var suitGroups = hand.GroupBy(c => c.SuitInt).OrderByDescending(g => g.Count());
                                    nextSuit = suitGroups.First().Key;
                                }
                                ActiveSuitInt = nextSuit;
                                IsWaitingForSuitSelection = false;
                                
                                CardInfo top = DiscardPile.Get(DiscardCount - 1);
                                CheckDonOpportunity(cpuActor.ActorId, top, false);
                            }
                        }
                        else
                        {
                            ProcessCpuAction();
                        }
                    }
                }
                else if (IsGameStarted && IsRoundOver && !IsGameOver)
                {
                    if (RoundEndTimer.IsRunning && RoundEndTimer.Expired(Runner))
                    {
                        Debug.Log("RoundEndTimer Expired! Moving to next round automatically.");
                        RoundEndTimer = TickTimer.None;
                        RequestNextRoundInternal();
                    }
                }
            }
        }

        private void RequestNextRoundInternal()
        {
            if (Object.HasStateAuthority)
            {
                Debug.Log($"RequestNextRoundInternal called. CurrentRound: {CurrentRound}, IsRoundOver: {IsRoundOver}");
            }

            if (!IsRoundOver) return;

            if (CurrentRound < 5)
            {
                CurrentRound++;
                IsRoundOver = false;
                ResetRoundState();
                
                if (Object.HasStateAuthority)
                {
                    Debug.Log($"[Server] Next round setup...");
                }

                StartNewRound();
                Debug.Log($"Round Advanced to {CurrentRound}");
            }
            else
            {
                IsGameOver = true;
                Debug.Log("Game Over reached (5 rounds completed)");
            }
        }

        #region Game Actions (Master/Authority)
        
        /// <summary>
        /// プレイヤーのReady状態をサーバ�Eに送信します、E
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SetReady(PlayerRef player, NetworkBool isReady)
        {
            PlayerReadyStates.Set(player, isReady);
        }

        public void SetPlayerReady(bool isReady)
        {
            if (Runner != null)
            {
                RPC_SetReady(Runner.LocalPlayer, isReady);
            }
        }

        /// <summary>
        /// ホストが強制皁E��ゲームを開始するため�E処琁E
        /// 全員のReady状態をTrueにしてカウントダウンを開始させまぁE
        /// </summary>
        public void ForceStartGameByHost()
        {
            Debug.Log($"<color=cyan>[DonFusionManager2D] ForceStartGameByHost called. Runner: {(Runner != null ? "OK" : "null")}, IsGameStarted: {IsGameStarted}</color>");
            
            if (Object == null || !Object.IsValid)
            {
                Debug.Log("<color=yellow>[DonFusionManager2D] Object is not valid yet. PENDING ForceStart scheduled.</color>");
                _pendingForceStart = true;
                return;
            }

            if (!Runner.IsSharedModeMasterClient && !Runner.IsServer) return;

            Debug.Log("<color=cyan>[DonFusionManager2D] Starting Game as Host...</color>");
            GameStartCountdown = TickTimer.None;
            IsGameStarted = true;
            
            foreach (var player in Runner.ActivePlayers)
            {
                RPC_SetReady(player, true);
            }
            
            RPC_StartGame();
        }



/// <summary>
/// フレンドマッチ用: TargetTotalPlayers を設定してゲーム強制開始
/// RpcTargets.All + IsSharedModeMasterClient フィルターでホストのみ処理
/// </summary>
        [Rpc(RpcSources.All, RpcTargets.All)]
public void RPC_FriendMatchForceStart(int targetPlayers)
{
    // マスタークライアント（ホスト）のみゲーム設定を処理
    if (!Runner.IsSharedModeMasterClient) return;

    Debug.Log($"[FriendMatch] RPC_FriendMatchForceStart 実行: target={targetPlayers}, IsGameStarted={IsGameStarted}");
    if (IsGameStarted) return;

    // 1) ターゲット人数を設定
    TargetTotalPlayers = targetPlayers;
    GameStartCountdown = TickTimer.None;
    IsGameStarted = true;

    if (Runner?.SessionInfo != null)
    {
        Runner.SessionInfo.IsOpen = false;
        Runner.SessionInfo.IsVisible = false;
    }

    // 2) Actor をリセットしてセットアップ
    for (int i = 0; i < 4; i++)
        Actors.Set(i, default);
    ServerSetupActors(targetPlayers);

    // 3) 全プレイヤーに通知（UI切り替え）
    RPC_NotifyGameStarted();

    // 4) デッキと配札
    var deck = CreateFullDeck();
    Shuffle(deck);
    serverHandData.Clear();

    for (int i = 0; i < 4; i++)
    {
        var actor = Actors.Get(i);
        if (!actor.IsActive) continue;

        var hand = deck.GetRange(0, initialHandCount);
        deck.RemoveRange(0, initialHandCount);
        serverHandData[actor.ActorId] = new List<CardInfo>(hand);

        if (!actor.IsCPU)
            foreach (var card in hand)
                RPC_ReceiveCard(actor.PlayerRef, card);

        PlayerHandCounts.Set(i, initialHandCount);
    }

    // 5) 山札と捨て札の初期化
    DrawCount = 0;
    DiscardCount = 0;
    foreach (var card in deck)
    {
        DrawPile.Set(DrawCount, card);
        DrawCount++;
    }

    if (DrawCount > 0)
    {
        DrawCount--;
        var firstCard = DrawPile.Get(DrawCount);
        AddCardToDiscard(firstCard);
        if (firstCard.Rank == 2)
            DrawPenaltyCount = 2;
    }

    // 6) 最初のターン
    CurrentRound = 1;
    CurrentTurnPlayerActorId = Actors.Get(0).ActorId;
    var startingActor = GetActor(CurrentTurnPlayerActorId);
    if (startingActor.IsActive && startingActor.IsCPU)
        CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, UnityEngine.Random.Range(1.5f, 2.5f));

    Debug.Log($"[FriendMatch] ゲーム開始完了: {targetPlayers}人構成");
}


        /// <summary>
        /// ホストがゲームの参加枠�E�実�Eレイヤー�E�CPU�E�をセチE��アチE�EしまぁE
        /// </summary>
        public void ServerSetupActors(int targetTotalPlayers)
        {
            if (!Runner.IsServer && !Runner.IsSharedModeMasterClient) return;

            // 既にセチE��アチE�E済みの場合�EスキチE�E
            if (Actors.Get(0).IsActive) return;

            int actorId = 1;
            int actorIndex = 0;

            // まず実�Eレイヤーを登録
            var players = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
            foreach (var p in players)
            {
                if (actorIndex >= 4) break;
                ActorInfo info = new ActorInfo
                {
                    ActorId = actorId++,
                    PlayerRef = p,
                    IsCPU = false,
                    IsActive = true
                };
                Actors.Set(actorIndex++, info);
            }

            // 足りなぁE�EをCPUで埋めめE
            while (actorIndex < targetTotalPlayers && actorIndex < 4)
            {
                ActorInfo cpuInfo = new ActorInfo
                {
                    ActorId = actorId++,
                    PlayerRef = PlayerRef.None,
                    IsCPU = true,
                    IsActive = true
                };
                Actors.Set(actorIndex++, cpuInfo);
            }
        }

        /// <summary>
        /// ゲームを開始します！Easter Clientのみ実行可能�E�E
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_StartGame()
        {
            if (Runner != null && Runner.SessionInfo != null)
            {
                Runner.SessionInfo.IsOpen = false;
                Runner.SessionInfo.IsVisible = false;
            }

            // アクターが未セチE��アチE�Eなら現在の設定人数でセチE��アチE�E
            if (!Actors.Get(0).IsActive)
            {
                ServerSetupActors(TargetTotalPlayers);
            }

            // TitleUIController に対して画面刁E��替えを持E���E�EI依存�Eためローカルでイベント呼び出し！E
            RPC_NotifyGameStarted();

            // チE��キ作�E�E�ローカルで生�Eして配币E��示を�Eす！E
            var deck = CreateFullDeck();
            Shuffle(deck);

            serverHandData.Clear();
            
            // アクター全員�E�EPU含む�E�に手札を�EめE
            for (int i = 0; i < 4; i++)
            {
                var actor = Actors.Get(i);
                if (!actor.IsActive) continue;

                var hand = deck.GetRange(0, initialHandCount);
                deck.RemoveRange(0, initialHandCount);

                serverHandData[actor.ActorId] = new List<CardInfo>(hand);

                // 吁E��クライアントにはRPCで送信
                // CPUの場合�Eマスター自身が管琁E��る�Eで、RPCで送る忁E���EなぁE��状態�E持つ
                if (!actor.IsCPU)
                {
                    foreach (var card in hand)
                    {
                        RPC_ReceiveCard(actor.PlayerRef, card);
                    }
                }
                
                // 枚数惁E��の同期�E�EI用�E�E
                PlayerHandCounts.Set(i, initialHandCount);
            }

            // ====== 山札と捨て札の設宁E======

            // 配られなかったカードを山札にセチE��
            foreach (var card in deck)
            {
                DrawPile.Set(DrawCount, card);
                DrawCount++;
            }

            // 最初�E1枚を山札から引いて捨て場�E�EiscardPile�E�に置ぁE
            if (DrawCount > 0)
            {
                DrawCount--;
                var firstCard = DrawPile.Get(DrawCount);
                AddCardToDiscard(firstCard);
                
                if (firstCard.Rank == 2)
                {
                    DrawPenaltyCount = 2;
                }
            }
            
            // 最初�Eターン
            CurrentTurnPlayerActorId = Actors.Get(0).ActorId;
            var startingActor = GetActor(CurrentTurnPlayerActorId);
            if (startingActor.IsActive && startingActor.IsCPU)
            {
                CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, UnityEngine.Random.Range(1.5f, 2.5f));
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ReceiveCard(PlayerRef targetPlayer, CardInfo card)
        {
            if (Runner.LocalPlayer == targetPlayer)
            {
                myLocalHand.Add(card);
                OnHandUpdated?.Invoke();
            }
        }

        public void SetLocalHand(List<CardInfo> newHand)
        {
            myLocalHand = newHand;
            OnHandUpdated?.Invoke();
        }

        private void OnFusionHandUpdated()
        {
            // 他�Eプレイヤーの枚数更新などを監視する場吁E
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyGameStarted()
        {
            // インスタンスが見つからなぁE��合�EFindを試みめE
            var titleUI = UnityEngine.Object.FindObjectOfType<UI.TitleUIController>();
            if (titleUI != null)
            {
                titleUI.SwitchToGameUI();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyOpponentPlayCard(int actorId, CardInfo card)
        {
            var ui = FindObjectOfType<DonGame2D.UI.GameUIController>();
            if (ui != null) ui.PlayOpponentCardAnimation(actorId, card);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyOpponentDrawCard(int actorId, int count)
        {
            var ui = FindObjectOfType<DonGame2D.UI.GameUIController>();
            if (ui != null) ui.PlayOpponentDrawAnimation(actorId, count);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyDiscardChanged(CardInfo topCard)
        {
            // DiscardPile の NetworkArray 同期より先にカードデータが届くことを保証するための RPC
            // GameUIController 側でこ�Eイベントを受け取り、捨て札UIを確実に更新する
            var ui = FindObjectOfType<DonGame2D.UI.GameUIController>();
            if (ui != null) ui.OnDiscardPileChanged(topCard);
        }
private void AddCardToDiscard(CardInfo card, bool isInitialPlay = true)
        {
            if (DiscardCount >= 52)
            {
                for (int i = 0; i < 51; i++)
                {
                    DiscardPile.Set(i, DiscardPile.Get(i + 1));
                }
                DiscardCount = 51;
            }

            DiscardPile.Set(DiscardCount, card);
            DiscardCount++;
            
            if (isInitialPlay && card.Rank != 8)
            {
                ActiveSuitInt = card.SuitInt;
            }

            // 全クライアントにカードデータめERPC で直接送信、E
            // NetworkArray の同期遁E��にかかわらず捨て札UIを確実に更新するため 
            RPC_NotifyDiscardChanged(card);

            for (int i = DrawCount; i > 0; i--)
            {
                if (i < 52)
                {
                    DrawPile.Set(i, DrawPile.Get(i - 1));
                }
            }
            DrawPile.Set(0, card);
            if (DrawCount < 52) DrawCount++;
        }

        #endregion

        #region Player Actions

        public bool TryPlayCard(CardInfo card)
        {
            if (Runner.LocalPlayer.PlayerId != CurrentTurnPlayerActorId) return false;
            if (IsWaitingForSuitSelection) return false; // スート選択征E��中はプレイ不可
            if (IsWaitingForDonGaeshi) return false; // Don返し征E��中はプレイ不可
            if (DonGraceTimer.IsRunning) return false; // 2秒タイマ�E稼働中はプレイ不可

            // バリチE�Eション�E�ローカルでもチェチE��して無駁E��通信を防ぐ！E
            CardInfo top = DiscardPile.Get(DiscardCount - 1);
            bool canPlay = false;

            if (DrawPenaltyCount > 0)
            {
                if (card.Rank == 2) canPlay = true;
            }
            else
            {
                // 同じマ�Eク また�E 同じ数孁E
                if (card.Rank == top.Rank || card.SuitInt == ActiveSuitInt) canPlay = true;
                
                // 最初が8でスート未持E��E-1)の場合�E何でも�Eせる
                if (ActiveSuitInt == -1) canPlay = true;
            }

            if (canPlay)
            {
                myLocalHand.Remove(card);
                RPC_SubmitCard(Runner.LocalPlayer, card);
                OnHandUpdated?.Invoke();

                if (card.Rank == 8)
                {
                    // 8の場合�Eスート選択UIを表示
                    var gameUI = UnityEngine.Object.FindObjectOfType<UI.GameUIController>();
                    if (gameUI != null) gameUI.ShowSuitSelectionUI();
                }

                return true;
            }
            return false;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SubmitCard(PlayerRef player, CardInfo card)
        {
            IsDonWindowOpen = false; // 古いDon受付�E終亁E

            int actorId = GetActorId(player);

            // サーバ�E側�E�権限老E��で最終確認して状態更新
            AddCardToDiscard(card);
            ServerRemoveCardFromHand(actorId, card);
            
            // アニメーション用の通知
            RPC_NotifyOpponentPlayCard(actorId, card);

            if (card.Rank == 2)
            {
                DrawPenaltyCount += 2;
            }

            // 手札枚数更新
            UpdateHandCount(actorId, -1);

            // 勝利判宁E
            if (serverHandData.ContainsKey(actorId) && serverHandData[actorId].Count == 0)
            {
                ConfirmOutWin(actorId);
                return;
            }

            if (card.Rank == 8)
            {
                // 、E」が出された場合�Eターンの進行を征E��E
                IsWaitingForSuitSelection = true;
            }

            CheckDonOpportunity(actorId, card, card.Rank == 8);
        }

        private void CheckDonOpportunity(int playedActorId, CardInfo playedCard, bool isEight)
        {
            // リアルタイム判定モード：タイマ�EなぁE
            // LastPlayedPlayerActorId を記録し、条件チェチE��はクライアント�Eでリアルタイムに行う
            LastPlayedPlayerActorId = playedActorId;
            IsDonWindowOpen = false;   // タイマ�Eウィンドウは使わなぁE
            DonGraceTimer = TickTimer.None;

            if (!isEight) RotateTurn();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SubmitSuitChoice(int suitInt)
        {
            // 強制皁E��マ�Eクを更新 (丁E��一フラグがFalseでも受け付けめE
            ActiveSuitInt = suitInt;
            IsWaitingForSuitSelection = false;

            // スートが決定されたので次のターンへ
            if (!DonGraceTimer.IsRunning)
            {
                RotateTurn();
            }
        }

        public void RequestDraw()
        {
            if (Runner.LocalPlayer.PlayerId != CurrentTurnPlayerActorId) return;
            if (IsWaitingForSuitSelection) return;
            if (IsWaitingForDonGaeshi) return;
            if (DonGraceTimer.IsRunning) return;

            RPC_RequestDraw(Runner.LocalPlayer);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestDraw(PlayerRef player)
        {
            IsDonWindowOpen = false; // なにか行動が起きたら窓口を閉じる
            DonGraceTimer = TickTimer.None;

            int actorId = GetActorId(player);

            int count = DrawPenaltyCount > 0 ? DrawPenaltyCount : 1;
            DrawPenaltyCount = 0;

            // 山札からカードを引く
            int cardsDrawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (DrawCount > 0)
                {
                    DrawCount--;
                    var card = DrawPile.Get(DrawCount);
                    ServerAddCardToHand(actorId, card);
                    RPC_ReceiveCard(player, card);
                    cardsDrawn++;
                }
                else
                {
                    // 山札が尽きた場合�E処琁E��EiscardPileからシャチE��ルして戻すなど�E��E封E��実裁E
                    Debug.Log("[Log Recovered - encoding fix]");
                    break;
                }
            }

            if (cardsDrawn > 0)
            {
                UpdateHandCount(actorId, cardsDrawn);
                
                // アニメーション用の通知
                RPC_NotifyOpponentDrawCard(actorId, cardsDrawn);
            }
            RotateTurn();
        }

        #endregion

        #region Helpers

        public int GetActorId(PlayerRef playerRef)
        {
            for (int i = 0; i < 4; i++)
            {
                var a = Actors.Get(i);
                if (a.IsActive && !a.IsCPU && a.PlayerRef == playerRef) return a.ActorId;
            }
            return playerRef.PlayerId; // Fallback
        }

        public ActorInfo GetActor(int actorId)
        {
            for (int i = 0; i < 4; i++)
            {
                var a = Actors.Get(i);
                if (a.IsActive && a.ActorId == actorId) return a;
            }
            return default;
        }

        private void RotateTurn()
        {
            var activeActors = Enumerable.Range(0, 4)
                .Select(i => Actors.Get(i))
                .Where(a => a.IsActive)
                .OrderBy(a => a.ActorId)
                .ToList();

            if (activeActors.Count == 0) return;

            int currentIdx = activeActors.FindIndex(a => a.ActorId == CurrentTurnPlayerActorId);
            if (currentIdx < 0) currentIdx = 0;

            int nextIdx = (currentIdx + 1) % activeActors.Count;
            CurrentTurnPlayerActorId = activeActors[nextIdx].ActorId;
            
            var nextActor = activeActors[nextIdx];
            if (nextActor.IsCPU)
            {
                // CPUのターンの場合、E、E秒�E遁E��を�Eれて思老E��シミュレーチE
                CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, UnityEngine.Random.Range(1.0f, 2.0f));
            }
        }

        private void UpdateHandCount(int actorId, int delta)
        {
            var activeActors = Enumerable.Range(0, 4)
                .Select(i => Actors.Get(i))
                .Where(a => a.IsActive)
                .OrderBy(a => a.ActorId)
                .ToList();

            int idx = activeActors.FindIndex(a => a.ActorId == actorId);
            if (idx >= 0)
            {
                int lastCount = PlayerHandCounts.Get(idx);
                int newCount = lastCount + delta;
                PlayerHandCounts.Set(idx, newCount);

                // リーチ判定！E枚になった時�E�E
                if (newCount == 1 && lastCount != 1)
                {
                    var actor = activeActors[idx];
                    if (!actor.IsCPU)
                    {
                        RPC_NotifyReach(actor.PlayerRef.PlayerId);
                    }
                }
            }
        }

        private List<CardInfo> CreateFullDeck()
        {
            List<CardInfo> deck = new List<CardInfo>();
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
            {
                for (int rank = 1; rank <= 13; rank++) deck.Add(new CardInfo(suit, rank));
            }
            return deck;
        }

        private void Shuffle(List<CardInfo> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int r = Random.Range(0, i + 1);
                var tmp = list[i]; list[i] = list[r]; list[r] = tmp;
            }
        }

        // --- サーバ�E手札管琁E�E琁E---
        private void ServerAddCardToHand(int actorId, CardInfo card)
        {
            if (!serverHandData.ContainsKey(actorId)) serverHandData[actorId] = new List<CardInfo>();
            serverHandData[actorId].Add(card);
        }

        private void ServerRemoveCardFromHand(int actorId, CardInfo card)
        {
            if (serverHandData.TryGetValue(actorId, out var hand))
            {
                var target = hand.FirstOrDefault(c => c.SuitInt == card.SuitInt && c.Rank == card.Rank);
                if (target.Rank != 0) hand.Remove(target);
            }
        }

        private int ServerGetHandTotal(int actorId)
        {
            if (serverHandData.TryGetValue(actorId, out var hand))
            {
                return hand.Sum(c => c.Rank);
            }
            return 0;
        }

        // --- Don処琁E��連 ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_DeclareDon(PlayerRef callingPlayer)
        {
            if (IsWaitingForDonGaeshi) return; // 既に誰かがDonしてぁE��
            if (IsRoundOver) return;
            if (DiscardCount == 0) return;

            int actorId = GetActorId(callingPlayer);
            int total = ServerGetHandTotal(actorId);
            CardInfo top = DiscardPile.Get(DiscardCount - 1);

            // リアルタイム判定：�E刁E��最後にカードを出してぁE��ぁE��手札合訁E== 捨て札ランク
            if (total == top.Rank && total <= 13 && actorId != LastPlayedPlayerActorId)
            {
                DonDeclarerActorId = actorId;
                DonTargetActorId = LastPlayedPlayerActorId;
                IsWaitingForDonGaeshi = true;

                var targetActor = GetActor(DonTargetActorId);
                if (targetActor.IsActive)
                {
                    int targetTotal = ServerGetHandTotal(targetActor.ActorId);
                    if (targetTotal != top.Rank)
                    {
                        // Don返し不可のため、即座にDonを確宁E
                        ConfirmDonWin(actorId, targetActor.ActorId, top.Rank);
                    }
                }
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_DeclareDonGaeshi(PlayerRef targetPlayer)
        {
            if (!IsWaitingForDonGaeshi) return;

            int actorId = GetActorId(targetPlayer);
            if (actorId != DonTargetActorId) return;

            CardInfo top = DiscardPile.Get(DiscardCount - 1);
            int total = ServerGetHandTotal(actorId);

            if (total == top.Rank)
            {
                // Don返し成功
                IsWaitingForDonGaeshi = false;
                ConfirmDonGaeshiWin(actorId, DonDeclarerActorId, top.Rank);
            }
        }

        private void AddCredits(int actorId, int amount)
        {
            if (PlayerCredits.ContainsKey(actorId)) PlayerCredits.Set(actorId, PlayerCredits[actorId] + amount);
            else PlayerCredits.Add(actorId, amount);
        }

        private void ConfirmDonWin(int winnerActorId, int loserActorId, int donValue)
        {
            IsRoundOver = true;
            IsWaitingForDonGaeshi = false;
            WinnerActorId = winnerActorId;

            int loserTotal = ServerGetHandTotal(loserActorId);
            int penalty = (donValue * 2 + loserTotal) * 10; // Donされた数字ÁE + 残りの手札の合訁Eに10を掛けたも�Eが�EナルチE��

            AddCredits(winnerActorId, penalty);
            AddCredits(loserActorId, -penalty);
            
            RoundEndTimer = TickTimer.CreateFromSeconds(Runner, 14.0f); // アニメーション用に時間を延長
            string handStr = string.Join(";", serverHandData[loserActorId].Select(c => $"{c.SuitInt},{c.Rank}"));
            string msg = $"Actor {winnerActorId} WON by DON!\nActor {loserActorId} lost {penalty} Credits";
            
            // TODO: UI表示用に、�Eの PlayerRef.PlayerId を渡す忁E��があるかもしれなぁE��、いったん ActorId で代替
            RPC_PlayRoundEndAnim(0, winnerActorId, loserActorId, donValue, handStr, penalty, msg);
        }

        private void ConfirmDonGaeshiWin(int winnerActorId, int loserActorId, int donValue)
        {
            IsRoundOver = true;
            IsWaitingForDonGaeshi = false;
            WinnerActorId = winnerActorId;

            int award = donValue * 100;

            AddCredits(winnerActorId, award);
            AddCredits(loserActorId, -award);

            RoundEndTimer = TickTimer.CreateFromSeconds(Runner, 14.0f);
            string handStr = string.Join(";", serverHandData[loserActorId].Select(c => $"{c.SuitInt},{c.Rank}"));
            string msg = $"Actor {winnerActorId} WON by DON-GAESHI!\nActor {loserActorId} lost {award} Credits";
            RPC_PlayRoundEndAnim(1, winnerActorId, loserActorId, donValue, handStr, award, msg);
        }

        private void ConfirmOutWin(int winnerActorId)
        {
            IsRoundOver = true;
            WinnerActorId = winnerActorId;

            int totalGain = 0;
            string details = "";

            foreach (var kvp in serverHandData)
            {
                if (kvp.Key == winnerActorId) continue;

                int loserHandTotal = ServerGetHandTotal(kvp.Key);
                int penalty = loserHandTotal * 10;
                
                AddCredits(kvp.Key, -penalty);
                totalGain += penalty;
                details += $"P{kvp.Key}:-{penalty} ";
            }

            AddCredits(winnerActorId, totalGain);
            RoundEndTimer = TickTimer.CreateFromSeconds(Runner, 16.0f); // 褁E��人のアウト演�Eのためにさらに延長
            
            var allLosers = serverHandData
                .Where(k => k.Key != winnerActorId)
                .Select(k => $"{k.Key}:" + string.Join(";", k.Value.Select(c => $"{c.SuitInt},{c.Rank}")))
                .ToArray();
            
            string combinedHandStr = string.Join("|", allLosers);
            string msg = $"Actor {winnerActorId} OUT!\nGained {totalGain} Credits\n({details})";
            RPC_PlayRoundEndAnim(2, winnerActorId, -1, 0, combinedHandStr, totalGain, msg);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayRoundEndAnim(int winType, int winnerId, int loserId, int donValue, string loserHandStr, int totalPenalty, string resultMsg)
        {
            var ui = UnityEngine.Object.FindObjectOfType<UI.GameUIController>();
            if (ui != null)
            {
                string fullMsg = resultMsg + "\n\n" + GetScoreBoardText();
                ui.PlayRoundEndAnimation(winType, winnerId, loserId, donValue, loserHandStr, totalPenalty, fullMsg, CurrentRound >= 5);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyReach(int actorId)
        {
            var ui = UnityEngine.Object.FindObjectOfType<UI.GameUIController>();
            if (ui != null)
            {
                ui.ShowReach(actorId);
            }
        }



        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestNextRound()
        {
            RoundEndTimer = TickTimer.None;
            RequestNextRoundInternal();
        }

        private void ResetRoundState()
        {
            DrawCount = 0;
            DiscardCount = 0;
            ActiveSuitInt = -1;
            DrawPenaltyCount = 0;
            IsDonWindowOpen = false;
            IsWaitingForDonGaeshi = false;
            IsWaitingForSuitSelection = false;
            serverHandData.Clear();
            
            for (int i = 0; i < PlayerHandCounts.Length; i++) PlayerHandCounts.Set(i, 0);
            
            RPC_ClearLocalHands();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ClearLocalHands()
        {
            myLocalHand.Clear();
            OnHandUpdated?.Invoke();
        }

        private void StartNewRound()
        {
            var deck = CreateFullDeck();
            Shuffle(deck);

            serverHandData.Clear();

            for (int i = 0; i < 4; i++)
            {
                var actor = Actors.Get(i);
                if (!actor.IsActive) continue;

                var hand = deck.GetRange(0, initialHandCount);
                deck.RemoveRange(0, initialHandCount);

                serverHandData[actor.ActorId] = new List<CardInfo>(hand);

                if (!actor.IsCPU)
                {
                    foreach (var card in hand) RPC_ReceiveCard(actor.PlayerRef, card);
                }
                
                PlayerHandCounts.Set(i, initialHandCount);
            }

            foreach (var card in deck)
            {
                DrawPile.Set(DrawCount, card);
                DrawCount++;
            }

            if (DrawCount > 0)
            {
                DrawCount--;
                var firstCard = DrawPile.Get(DrawCount);
                AddCardToDiscard(firstCard);
                if (firstCard.Rank == 2) DrawPenaltyCount = 2;
            }
            
            if (WinnerActorId != -1)
            {
                CurrentTurnPlayerActorId = WinnerActorId;
                WinnerActorId = -1; 
            }
            else
            {
                CurrentTurnPlayerActorId = Actors.Get(0).ActorId;
            }

            var startingActor = GetActor(CurrentTurnPlayerActorId);
            if (startingActor.IsActive && startingActor.IsCPU)
            {
                CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, UnityEngine.Random.Range(1.5f, 2.5f));
            }
        }

        private void ProcessCpuAction()
        {
            if (!Object.HasStateAuthority) return;

            if (IsWaitingForSuitSelection || IsWaitingForDonGaeshi || DonGraceTimer.IsRunning || IsDonWindowOpen) return;

            var cpuActor = GetActor(CurrentTurnPlayerActorId);
            if (!cpuActor.IsActive || !cpuActor.IsCPU) return;

            if (serverHandData.TryGetValue(cpuActor.ActorId, out var hand))
            {
                CardInfo top = DiscardPile.Get(DiscardCount - 1);
                List<CardInfo> playableCards = new List<CardInfo>();
                
                if (DrawPenaltyCount > 0)
                {
                    playableCards = hand.Where(c => c.Rank == 2).ToList();
                }
                else
                {
                    playableCards = hand.Where(c => c.Rank == top.Rank || c.SuitInt == ActiveSuitInt || ActiveSuitInt == -1).ToList();
                }

                if (playableCards.Count > 0)
                {
                    // 最もRankが大きいも�Eを�EぁE
                    CardInfo playCard = playableCards.OrderByDescending(c => c.Rank).First();
                    hand.Remove(playCard);

                    AddCardToDiscard(playCard);
                    ServerRemoveCardFromHand(cpuActor.ActorId, playCard);
                    if (playCard.Rank == 2) DrawPenaltyCount += 2;
                    UpdateHandCount(cpuActor.ActorId, -1);
                    
                    // アニメーション通知
                    RPC_NotifyOpponentPlayCard(cpuActor.ActorId, playCard);

                    if (hand.Count == 0)
                    {
                        ConfirmOutWin(cpuActor.ActorId);
                        return;
                    }

                    if (playCard.Rank == 8)
                    {
                        // 8を�Eした場合�Eここで一度止める�E�EIでアニメーションを見せる時間を稼ぐ！E
                        IsWaitingForSuitSelection = true;
                        CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, 1.0f); // 1秒後にマ�Eクを決める
                    }
                    else
                    {
                        CheckDonOpportunity(cpuActor.ActorId, playCard, false);
                    }
                }
                else
                {
                    // ドロー
                    int count = DrawPenaltyCount > 0 ? DrawPenaltyCount : 1;
                    DrawPenaltyCount = 0;
                    int cardsDrawn = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (DrawCount > 0)
                        {
                            DrawCount--;
                            var card = DrawPile.Get(DrawCount);
                            ServerAddCardToHand(cpuActor.ActorId, card);
                            cardsDrawn++;
                        }
                    }
                    if (cardsDrawn > 0)
                    {
                        UpdateHandCount(cpuActor.ActorId, cardsDrawn);
                        
                        // アニメーション通知
                        RPC_NotifyOpponentDrawCard(cpuActor.ActorId, cardsDrawn);
                    }
                    RotateTurn();
                }
            }
        }

        private void TryCpuDonAction()
        {
            if (!Object.HasStateAuthority) return;
            if (!IsDonWindowOpen) return;
            if (IsWaitingForDonGaeshi) return;

            CardInfo top = DiscardPile.Get(DiscardCount - 1);

            for (int i = 0; i < 4; i++)
            {
                var actor = Actors.Get(i);
                if (actor.IsActive && actor.IsCPU && actor.ActorId != LastPlayedPlayerActorId)
                {
                    int total = ServerGetHandTotal(actor.ActorId);
                    if (total == top.Rank && total <= 13)
                    {
                        IsDonWindowOpen = false;
                        DonGraceTimer = TickTimer.None;

                        DonDeclarerActorId = actor.ActorId;
                        DonTargetActorId = LastPlayedPlayerActorId;
                        IsWaitingForDonGaeshi = true;

                        var targetActor = GetActor(DonTargetActorId);
                        if (targetActor.IsActive)
                        {
                            int targetTotal = ServerGetHandTotal(targetActor.ActorId);
                            if (targetTotal != top.Rank)
                            {
                                ConfirmDonWin(actor.ActorId, targetActor.ActorId, top.Rank);
                            }
                            else if (targetActor.IsCPU)
                            {
                                // ターゲチE��めEPUの場合�E自動でドン返し
                                IsWaitingForDonGaeshi = false;
                                ConfirmDonGaeshiWin(targetActor.ActorId, actor.ActorId, top.Rank);
                            }
                            // ターゲチE��が実�Eレイヤーの場合�E手動でドン返しするか、猶予時間�Eれを征E��
                            // 猶予時間�Eれ時の勝敗確定�E後述のDonGraceTimer.Expiredにて忁E��E
                        }
                        return; // 1人がDonしたら終亁E
                    }
                }
            }
        }

        public string GetScoreBoardText()
        {
            string text = $"--- ROUND {CurrentRound} SCORE ---\n";
            foreach (var player in Runner.ActivePlayers)
            {
                int credits = PlayerCredits.ContainsKey(player.PlayerId) ? PlayerCredits[player.PlayerId] : 0;
                text += $"Player {player.PlayerId}: {credits} Credits\n";
            }
            return text;
        }

        #endregion
    }
}

