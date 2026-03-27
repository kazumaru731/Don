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
        private bool _uiStarted = false; // ローカル用UI遷移フラグ

        private void OnGameStartedChanged()
        {
            if (_isNetworkGameStarted)
            {
                var titleUI = UnityEngine.Object.FindObjectOfType<DonGame2D.UI.TitleUIController>();
                if (titleUI != null)
                {
                    _uiStarted = true;
                    titleUI.SwitchToGameUI();
                }
            }
        }

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

        [Networked]
        public int PendingWinnerActorId { get; set; }

        [Networked, Capacity(4)]
        public NetworkArray<int> DonCallerActorIds { get; }

        [Networked]
        public int DonCallersCount { get; set; }

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
                    PendingWinnerActorId = -1;
                    LastPlayedPlayerActorId = -1;
                    DonDeclarerActorId = -1;
                    DonTargetActorId = -1;
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

            // 参加時にすでにゲームが開始されていたらUIを切り替える
            if (_isNetworkGameStarted)
            {
                OnGameStartedChanged();
            }
        }

        public override void Render()
        {
            if (_isNetworkGameStarted && !_uiStarted)
            {
                OnGameStartedChanged();
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

                    // --- Don受付時間（あがり時も含む）の監視 ---
                    if (IsDonWindowOpen && DonGraceTimer.IsRunning && DonGraceTimer.Expired(Runner))
                    {
                        IsDonWindowOpen = false;
                        DonGraceTimer = TickTimer.None;

                        // もしDon宣言者がいれば、Don確定処理へ
                        if (DonCallersCount > 0)
                        {
                            Debug.Log($"[Server] DonGraceTimer Expired. Starting ConfirmMultiDonWin for {DonCallersCount} caller(s).");
                            ConfirmMultiDonWin();
                        }
                        // もし「あがり待機」中のプレイヤーがいれば勝利確定へ
                        else if (PendingWinnerActorId > 0)
                        {
                            int winnerId = PendingWinnerActorId;
                            PendingWinnerActorId = -1;
                            Debug.Log($"[Server] DonGraceTimer Expired. Starting Win process for PendingWinner: {winnerId}");

                            if (DrawPenaltyCount > 0)
                            {
                                StartCoroutine(Co_ProcessOutWinWithPenalty(winnerId));
                            }
                            else
                            {
                                ConfirmOutWin(winnerId);
                            }
                        }
                        
                        // 8 が出された場合はスート選択が終わるまで待機
                        bool shouldWaitEight = IsWaitingForSuitSelection;
                        
                        // 次のプレイヤーがCPUなら思考タイマーが既に切れている可能性があるので、
                        // 思考タイマーがRunning中でない（＝既にExpiredしている）か、既にExpiredしていれば即座に行動
                        var nextActor = GetActor(CurrentTurnPlayerActorId);
                        if (nextActor.IsActive && nextActor.IsCPU && !IsRoundOver && !shouldWaitEight && !IsWaitingForDonGaeshi && DonCallersCount == 0 && PendingWinnerActorId <= 0)
                        {
                            if (!CpuThinkTimer.IsRunning)
                            {
                                Debug.Log($"[Server] Don window closed. CPU {nextActor.ActorId} thinks complete. Executing action.");
                                ProcessCpuAction();
                            }
                        }
                        else if (!shouldWaitEight && !IsRoundOver && !IsWaitingForDonGaeshi && DonCallersCount == 0 && PendingWinnerActorId <= 0)
                        {
                            // Don窓が閉まって、かつ誰もDonしておらず、上がり待機もいない場合のみターン回転
                            if (LastPlayedPlayerActorId == CurrentTurnPlayerActorId)
                            {
                                Debug.Log("[Server] Don window closed. Turn was stuck after played card, rotating now.");
                                RotateTurn();
                            }
                        }
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
                                int total = ServerGetHandTotal(cpuActor.ActorId);
                                CardInfo top = DiscardCount > 0 ? DiscardPile.Get(DiscardCount - 1) : default;
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

            // ラウンド情報を更新する前にフラグをリセット
            IsRoundOver = false;

            if (CurrentRound < 5)
            {
                CurrentRound++;
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
        CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, UnityEngine.Random.Range(0.5f, 1.0f));

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
                CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, UnityEngine.Random.Range(0.5f, 1.0f));
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

        public bool CanPlayCard(CardInfo card)
    {
        int localActorId = GetActorId(Runner.LocalPlayer);
        if (localActorId != CurrentTurnPlayerActorId) return false;
        if (IsWaitingForSuitSelection) return false;
        if (IsWaitingForDonGaeshi) return false;
        if (DonGraceTimer.IsRunning) return false;

        CardInfo top = DiscardPile.Get(DiscardCount - 1);
        if (DrawPenaltyCount > 0)
        {
            return card.Rank == 2;
        }
        else
        {
            if (card.Rank == top.Rank || card.SuitInt == ActiveSuitInt) return true;
            if (ActiveSuitInt == -1) return true;
        }
        return false;
    }

    public bool TryPlayCard(CardInfo card)
        {
            int localActorId = GetActorId(Runner.LocalPlayer);
            if (localActorId != CurrentTurnPlayerActorId) return false;
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
                
                // ローカルでの提出中フラグを立てて、UpdateFusionDiscardPileUI のガードを一時的に外す
                var gameUI = UnityEngine.Object.FindObjectOfType<UI.GameUIController>();
                if (gameUI != null) gameUI.SetLocalPlayerDiscarding(true);

                OnHandUpdated?.Invoke();

                if (card.Rank == 8)
                {
                    // 8の場合�Eスート選択UIを表示
                                        if (gameUI != null) gameUI.ShowSuitSelectionUI();
                }

                return true;
            }
            return false;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SubmitCard(PlayerRef player, CardInfo card)
        {
            IsDonWindowOpen = false; // 古いDon受付終了
            PendingWinnerActorId = -1; // 勝利判定をリセット

            int actorId = GetActorId(player);
            LastPlayedPlayerActorId = actorId; // 即座に更新してDon判定のガードを有効にする

            // サーバE側E権限老Eで最終確認して状態更新
                        // アニメーション通知を先に送る
            RPC_NotifyOpponentPlayCard(actorId, card);

            AddCardToDiscard(card);
            ServerRemoveCardFromHand(actorId, card);


            if (card.Rank == 2)
            {
                DrawPenaltyCount += 2;
            }

            // 手札枚数更新
            UpdateHandCount(actorId, -1);

            // 勝利判定
            if (serverHandData.ContainsKey(actorId) && serverHandData[actorId].Count == 0)
            {
                Debug.Log($"[Server] Player {actorId} played last card. Entering Don grace window.");
                PendingWinnerActorId = actorId;
                
                // Donができる人が一人でもいるかチェック
                bool expressionTargetExists = false;
                if (card.Rank <= 13)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var actor = Actors.Get(i);
                        if (!actor.IsActive || actor.ActorId == actorId) continue;
                        if (ServerGetHandTotal(actor.ActorId) == card.Rank) { expressionTargetExists = true; break; }
                    }
                }

                // Don受付を開く
                if (expressionTargetExists)
                {
                    IsDonWindowOpen = true;
                    DonGraceTimer = TickTimer.CreateFromSeconds(Runner, 3.0f);
                    DonCallersCount = 0;
                    for (int i = 0; i < 4; i++) DonCallerActorIds.Set(i, -1);
                }
                else
                {
                    IsDonWindowOpen = false;
                    DonGraceTimer = TickTimer.None;
                    
                    // 即座に勝利確定処理（ペナルティチェック込み）を実行
                    int winnerId = PendingWinnerActorId;
                    PendingWinnerActorId = -1;
                    if (DrawPenaltyCount > 0)
                    {
                        StartCoroutine(Co_ProcessOutWinWithPenalty(winnerId));
                    }
                    else
                    {
                        ConfirmOutWin(winnerId);
                    }
                }
                return; // 上がり時はここで終了（通常プレイの RotateTurn や CheckDonOpportunity をスキップ）
            }

        // --- 手札が0枚でない場合の通常処理 ---
        if (card.Rank == 8)
        {
            IsWaitingForSuitSelection = true;
        }

        CheckDonOpportunity(actorId, card, card.Rank == 8);
    }

        private void CheckDonOpportunity(int playedActorId, CardInfo playedCard, bool isEight)
        {
            LastPlayedPlayerActorId = playedActorId;
            IsDonWindowOpen = true;
            // Donができる人が一人でもいるかチェック
            bool expressionTargetExists = false;
            if (playedCard.Rank <= 13) {
                for (int i = 0; i < 4; i++) {
                    var actor = Actors.Get(i);
                    if (!actor.IsActive || actor.ActorId == playedActorId) continue;
                    if (ServerGetHandTotal(actor.ActorId) == playedCard.Rank) {
                        expressionTargetExists = true;
                        break;
                    }
                }
            }

            // Don可能なプレイヤーがいる場合のみ3秒待つ。
            if (expressionTargetExists)
            {
                IsDonWindowOpen = true;
                DonGraceTimer = TickTimer.CreateFromSeconds(Runner, 3.0f);
                DonCallersCount = 0;
                for (int i = 0; i < 4; i++) DonCallerActorIds.Set(i, -1);
            }
            else
            {
                IsDonWindowOpen = false;
                DonGraceTimer = TickTimer.None;
            }

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
            else
            {
                Debug.Log("[Server] Suit choice submitted, but Don window is still open. Turn will rotate after window closes.");
            }
        }

        public void RequestDraw()
        {
            int localActorId = GetActorId(Runner.LocalPlayer);
            if (localActorId != CurrentTurnPlayerActorId) return;
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
            return -1; // Fallback to -1 as 0 is not a valid ActorId
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

        private int GetNextActorId(int currentActorId)
        {
            var activeActors = Enumerable.Range(0, 4)
                .Select(i => Actors.Get(i))
                .Where(a => a.IsActive)
                .OrderBy(a => a.ActorId)
                .ToList();

            if (activeActors.Count <= 1) return currentActorId;

            int currentIdx = activeActors.FindIndex(a => a.ActorId == currentActorId);
            if (currentIdx < 0) return activeActors[0].ActorId;

            int nextIdx = (currentIdx + 1) % activeActors.Count;
            return activeActors[nextIdx].ActorId;
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
                // レスポンス改善：Don受付中であっても先行して思考を開始する。
                // 実際の行動(ProcessCpuAction)側でDon受付終了を待つ。
                float thinkTime = UnityEngine.Random.Range(0.4f, 0.8f);
                CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, thinkTime);
                Debug.Log($"[Server] Next turn is CPU {nextActor.ActorId}. Starting anticipatory think timer ({thinkTime:F1}s).");
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
            DeclareDonInternal(GetActorId(callingPlayer));
        }

        private void DeclareDonInternal(int actorId)
        {
            if (IsRoundOver) return;
            
            bool isMyTurn = (CurrentTurnPlayerActorId == actorId);

            // 最初の一人目が出るまでは、自分のターンでないなら窓が開いている必要がある。
            // 最初の一人目が出た後は、窓が開いている間だけ受け付ける。
            if (DonCallersCount == 0)
            {
                if (!isMyTurn && DiscardCount == 0) return; // 最初のターンかつ自分のターンでないなら不可
            }
            else
            {
                if (!IsDonWindowOpen) return;
            }

            // すでに宣言済みかチェック
            for (int i = 0; i < DonCallersCount; i++) {
                if (DonCallerActorIds.Get(i) == actorId) return;
            }

            int total = ServerGetHandTotal(actorId);
            CardInfo top = DiscardCount > 0 ? DiscardPile.Get(DiscardCount - 1) : default;

            // 捨て札と手札合計が一致した場合のみ Don 可能 (Match Don)
            bool canMatchDon = (DiscardCount > 0 && total == top.Rank && total <= 13);

            if (canMatchDon)
            {
                if (DonCallersCount < 4) {
                    DonCallerActorIds.Set(DonCallersCount, actorId);
                    DonCallersCount++;
                    
                    // 全クライアントに視覚演出を表示するよう通知
                    RPC_NotifyDon(actorId, ServerGetHandString(actorId));
                }

                if (DonCallersCount == 1) {
                    // Match Don の場合は常に LastPlayedPlayerActorId がターゲット
                    DonTargetActorId = LastPlayedPlayerActorId;
                    
                    PendingWinnerActorId = -1;

                    // Don 窓口を開き、他プレイヤーの猶予時間を設ける
                    IsDonWindowOpen = true;
                    DonGraceTimer = TickTimer.CreateFromSeconds(Runner, 3.0f);

                    if (DonTargetActorId != -1)
                    {
                        var targetActor = GetActor(DonTargetActorId);
                        if (targetActor.IsActive)
                        {
                            int targetTotal = ServerGetHandTotal(targetActor.ActorId);
                            if (targetTotal == top.Rank)
                            {
                                IsWaitingForDonGaeshi = true;
                            }
                        }
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
                
                // 全員に演出を通知
                RPC_NotifyDon(actorId, ServerGetHandString(actorId));

                ConfirmDonGaeshiWin(actorId, DonDeclarerActorId, top.Rank);
            }
        }

        private void AddCredits(int actorId, int amount)
        {
            if (PlayerCredits.ContainsKey(actorId)) PlayerCredits.Set(actorId, PlayerCredits[actorId] + amount);
            else PlayerCredits.Add(actorId, amount);
        }

        private void ConfirmDonWin(int winnerId, int loserId, int donValue)
        {
            // 複数人Don対応ロジックにリダイレクト
            if (DonCallersCount == 0)
            {
                DonCallerActorIds.Set(0, winnerId);
                DonCallersCount = 1;
                DonTargetActorId = loserId;
            }
            StartCoroutine(Co_ConfirmMultiDonWin());
        }

        private System.Collections.IEnumerator Co_ConfirmMultiDonWin()
        {
            if (DonCallersCount == 0 || IsRoundOver) yield break;

            // 受付終了
            IsDonWindowOpen = false;
            DonGraceTimer = TickTimer.None;

            Debug.Log($"[Victory] Co_ConfirmMultiDonWin triggered. Initial Callers: {DonCallersCount}, Target: {DonTargetActorId}");

            // 自動Don判定: まだ宣言していないが、あがれる状態のプレイヤーを追加（Match Don のみ）
            CardInfo top = DiscardCount > 0 ? DiscardPile.Get(DiscardCount - 1) : default;
            if (DonTargetActorId != -1)
            {
                for (int i = 0; i < 4; i++)
                {
                    var actor = Actors.Get(i);
                    if (!actor.IsActive || actor.ActorId == DonTargetActorId) continue;
                    
                    bool alreadyDeclared = false;
                    for (int j = 0; j < DonCallersCount; j++) {
                        if (DonCallerActorIds.Get(j) == actor.ActorId) { alreadyDeclared = true; break; }
                    }
                    
                    if (!alreadyDeclared) {
                        int total = ServerGetHandTotal(actor.ActorId);
                        if (total == top.Rank) {
                            if (DonCallersCount < 4) {
                                DonCallerActorIds.Set(DonCallersCount, actor.ActorId);
                                DonCallersCount++;
                                Debug.Log($"[Server] Auto-Don for Player {actor.ActorId}");
                            }
                        }
                    }
                }
            }

            // 計算に必要な値を確定
            int rank = top.Rank;
            int callers = DonCallersCount;
            int targetId = DonTargetActorId;

            // Don演出を表示するための待機時間を短縮
            yield return new WaitForSeconds(0.4f);

            if (IsRoundOver) yield break;
            IsRoundOver = true;

            int totalPenalty = 0;
            List<string> loserHandStrings = new List<string>();

            if (targetId != -1)
            {
                // 単一ターゲット（Match Don）のペナルティ計算
                int targetHandTotal = ServerGetHandTotal(targetId);
                totalPenalty = (rank * 20) * callers + targetHandTotal * 10;
                AddCredits(targetId, -totalPenalty);
                loserHandStrings.Add($"{targetId}:" + ServerGetHandString(targetId));
            }
            else
            {
                // 全員ターゲット（Total Don）のペナルティ計算
                // 各自の手札合計 * 10 を徴収
                for (int i = 0; i < 4; i++)
                {
                    var actor = Actors.Get(i);
                    bool isWinner = false;
                    for (int j = 0; j < callers; j++) if (DonCallerActorIds.Get(j) == actor.ActorId) isWinner = true;
                    
                    if (actor.IsActive && !isWinner)
                    {
                        int penalty = ServerGetHandTotal(actor.ActorId) * 10;
                        AddCredits(actor.ActorId, -penalty);
                        totalPenalty += penalty;
                        loserHandStrings.Add($"{actor.ActorId}:" + ServerGetHandString(actor.ActorId));
                    }
                }
            }

            int totalUnits = totalPenalty / 10;
            int baseUnits = totalUnits / (callers > 0 ? callers : 1);
            int remainderUnits = totalUnits % (callers > 0 ? callers : 1);

            List<int> winnerList = new List<int>();
            int starterId = -1;
            int luckyIndex = UnityEngine.Random.Range(0, callers);

            for (int i = 0; i < callers; i++)
            {
                int winnerId = DonCallerActorIds.Get(i);
                winnerList.Add(winnerId);
                
                int gain = baseUnits * 10;
                if (i == luckyIndex)
                {
                    gain += remainderUnits * 10;
                    starterId = winnerId;
                }
                AddCredits(winnerId, gain);
            }

            WinnerActorId = starterId; 
            IsWaitingForDonGaeshi = false;
            IsWaitingForSuitSelection = false;
            PendingWinnerActorId = -1;

            string loserHandStr = string.Join("|", loserHandStrings);
            string winnerHandStr = ServerGetHandString(starterId);
            string winnersStr = string.Join(", ", winnerList.Select(id => $"Player {id}"));
            string resultMsg = (targetId != -1) ? $"{winnersStr} DON! Total: {totalPenalty} credits." : $"{winnersStr} TOTAL DON! (+{totalPenalty} Credits)";

            RPC_PlayRoundEndAnim(0, starterId, targetId, rank, loserHandStr, totalPenalty, resultMsg, winnersStr, winnerHandStr);
            
            RoundEndTimer = TickTimer.None;
        }

        private void ConfirmMultiDonWin()
        {
            StartCoroutine(Co_ConfirmMultiDonWin());
        }


        private void ConfirmDonGaeshiWin(int winnerId, int loserId, int donValue)
        {
            if (IsRoundOver) return;
            IsRoundOver = true;

            Debug.Log($"[Victory] ConfirmDonGaeshiWin triggered. Winner: {winnerId}, Loser: {loserId}, Value: {donValue}");

            int totalPenalty = donValue * 100;
            AddCredits(loserId, -totalPenalty);
            AddCredits(winnerId, totalPenalty);

            WinnerActorId = winnerId;
            IsWaitingForDonGaeshi = false;
            IsWaitingForSuitSelection = false;

            string loserHandStr = $"{loserId}:" + ServerGetHandString(loserId);
            string winnerHandStr = ServerGetHandString(winnerId);
            string resultMsg = $"Player {winnerId} DON-GAESHI! (+{totalPenalty} Credits)";
            RPC_PlayRoundEndAnim(1, winnerId, loserId, donValue, loserHandStr, totalPenalty, resultMsg, $"Player {winnerId}", winnerHandStr);
            
            RoundEndTimer = TickTimer.None;
        }

        private System.Collections.IEnumerator Co_ProcessOutWinWithPenalty(int winnerId)
        {
            if (IsRoundOver) yield break;
            IsRoundOver = true;

            Debug.Log($"[Victory] Co_ProcessOutWinWithPenalty triggered. Winner: {winnerId}, DrawPenalty: {DrawPenaltyCount}");

            WinnerActorId = winnerId;
            IsWaitingForDonGaeshi = false;
            IsWaitingForSuitSelection = false;

            // 追加: ペナルティドローを実際に実行
            int penaltyCount = DrawPenaltyCount;
            // 修正：勝者自身ではなく、次のプレイヤーを対象にする
            int targetActorId = GetNextActorId(winnerId);
            var targetActor = GetActor(targetActorId);

            if (penaltyCount > 0 && targetActor.IsActive)
            {
                Debug.Log($"[Server] Forcing final penalty draw of {penaltyCount} cards for Player {targetActorId}");
                
                int actualDrawn = 0;
                for (int i = 0; i < penaltyCount; i++)
                {
                    if (DrawCount > 0)
                    {
                        DrawCount--;
                        var card = DrawPile.Get(DrawCount);
                        ServerAddCardToHand(targetActorId, card);
                        if (!targetActor.IsCPU)
                        {
                            RPC_ReceiveCard(targetActor.PlayerRef, card);
                        }
                        actualDrawn++;
                    }
                }
                
                if (actualDrawn > 0)
                {
                    UpdateHandCount(targetActorId, actualDrawn);
                    // 全クライアントにドロー演出を通知
                    RPC_NotifyOpponentDrawCard(targetActorId, actualDrawn);
                    
                    // アニメーションの完了を待つ (枚数に応じて待機時間を調整)
                    // 修正：ドロー演出が確実に終わるように少し長めに待機
                    yield return new WaitForSeconds(1.5f + (actualDrawn * 0.2f));
                }
            }

            // 通常のOutと同様に他プレイヤーの手札を収集
            List<string> otherHands = new List<string>();
            int totalBonus = 0;

            for (int i = 0; i < 4; i++)
            {
                var actor = Actors.Get(i);
                if (actor.IsActive && actor.ActorId != winnerId)
                {
                    if (serverHandData.TryGetValue(actor.ActorId, out var hand))
                    {
                        string handStr = string.Join(";", hand.Select(c => $"{c.SuitInt},{c.Rank}"));
                        otherHands.Add($"{actor.ActorId}:{handStr}");
                        
                        int penalty = hand.Sum(c => c.Rank) * 10;
                        AddCredits(actor.ActorId, -penalty);
                        totalBonus += penalty;
                    }
                }
            }
            
            AddCredits(winnerId, totalBonus);

            string combinedHands = string.Join("|", otherHands);
            string resultMsg = $"Player {winnerId} OUT WIN (Penalty)! (+{totalBonus} Credits)";
            RPC_PlayRoundEndAnim(2, winnerId, -1, 0, combinedHands, totalBonus, resultMsg, $"Player {winnerId}", "");
            
            RoundEndTimer = TickTimer.None;
            yield break;
        }




        private void ConfirmOutWin(int winnerId)
        {
            if (IsRoundOver) return;
            IsRoundOver = true;

            Debug.Log($"[Victory] ConfirmOutWin triggered. Winner: {winnerId}");

            WinnerActorId = winnerId;
            IsWaitingForDonGaeshi = false;
            IsWaitingForSuitSelection = false;

            // 他のプレイヤーの手札を収集（PlayerId:Card,Card|...）
            List<string> otherHands = new List<string>();
            int totalBonus = 0;

            for (int i = 0; i < 4; i++)
            {
                var actor = Actors.Get(i);
                if (actor.IsActive && actor.ActorId != winnerId)
                {
                    if (serverHandData.TryGetValue(actor.ActorId, out var hand))
                    {
                        string handStr = string.Join(";", hand.Select(c => $"{c.SuitInt},{c.Rank}"));
                        otherHands.Add($"{actor.ActorId}:{handStr}");
                        
                        int penalty = hand.Sum(c => c.Rank) * 10;
                        AddCredits(actor.ActorId, -penalty);
                        totalBonus += penalty;
                    }
                }
            }
            AddCredits(winnerId, totalBonus);

            string combinedHands = string.Join("|", otherHands);
            string resultMsg = $"Player {winnerId} OUT WIN! (+{totalBonus} Credits)";
            RPC_PlayRoundEndAnim(2, winnerId, -1, 0, combinedHands, totalBonus, resultMsg, $"Player {winnerId}", "");
            
            RoundEndTimer = TickTimer.None;
        }




        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayRoundEndAnim(int winType, int winnerId, int loserId, int donValue, string loserHandStr, int totalPenalty, string resultMsg, string winnerNames = "", string winnerHandStr = "")
        {
            var ui = UnityEngine.Object.FindObjectOfType<UI.GameUIController>();
            if (ui != null)
            {
                string fullMsg = resultMsg + "\n\n" + GetScoreBoardText();
                ui.PlayRoundEndAnimation(winType, winnerId, loserId, donValue, loserHandStr, totalPenalty, fullMsg, CurrentRound >= 5, winnerNames, winnerHandStr);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ClearHandUI(int actorId)
        {
            var ui = UnityEngine.Object.FindObjectOfType<UI.GameUIController>();
            if (ui != null)
            {
                ui.ClearHandUI(actorId);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyDon(int actorId, string handData)
        {
            var ui = UnityEngine.Object.FindObjectOfType<UI.GameUIController>();
            if (ui != null)
            {
                ui.ShowDonAnimation(actorId, handData);
            }
        }

        private string ServerGetHandString(int actorId)
        {
            if (serverHandData.ContainsKey(actorId))
            {
                return string.Join(";", serverHandData[actorId].Select(c => $"{(int)c.Suit},{c.Rank}"));
            }
            return "";
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

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ReportAnimationFinished()
        {
            if (IsRoundOver && Object.HasStateAuthority)
            {
                // アニメーション終了から2秒後に次のラウンドへ（少しの余韻）
                RoundEndTimer = TickTimer.CreateFromSeconds(Runner, 2.0f);
            }
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
            PendingWinnerActorId = -1;
            LastPlayedPlayerActorId = -1;
            DonDeclarerActorId = -1;
            DonTargetActorId = -1;
            DonCallersCount = 0;
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
                CpuThinkTimer = TickTimer.CreateFromSeconds(Runner, UnityEngine.Random.Range(0.5f, 1.0f));
            }
        }

        private void ProcessCpuAction()
        {
            if (!Object.HasStateAuthority) return;

            // バグ修正: ドン受付中、ドン確定演出中、上がり確定待機中はCPUの行動を完全に遮断する
            if (IsRoundOver || IsWaitingForSuitSelection || IsWaitingForDonGaeshi || DonGraceTimer.IsRunning || DonCallersCount > 0 || PendingWinnerActorId != -1) return;

            var cpuActor = GetActor(CurrentTurnPlayerActorId);
            if (!cpuActor.IsActive || !cpuActor.IsCPU) return;

            if (serverHandData.TryGetValue(cpuActor.ActorId, out var hand))
            {
                CardInfo top = DiscardCount > 0 ? DiscardPile.Get(DiscardCount - 1) : default;
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
                    // 最もRankが大きいもEをEぁE
                    CardInfo playCard = playableCards.OrderByDescending(c => c.Rank).First();
                    hand.Remove(playCard);

                    // アニメーション通知を先に送る
                    RPC_NotifyOpponentPlayCard(cpuActor.ActorId, playCard);

                    AddCardToDiscard(playCard);
                    ServerRemoveCardFromHand(cpuActor.ActorId, playCard);
                    if (playCard.Rank == 2) DrawPenaltyCount += 2;
                    UpdateHandCount(cpuActor.ActorId, -1);

                    if (hand.Count == 0)
                    {
                        Debug.Log($"[Server] CPU {cpuActor.ActorId} played last card. Entering Don grace window.");
                        PendingWinnerActorId = cpuActor.ActorId;

                        IsDonWindowOpen = true;
                        DonGraceTimer = TickTimer.CreateFromSeconds(Runner, 1.2f);

                        if (playCard.Rank != 8) RotateTurn();
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
                    IsDonWindowOpen = false; // CPUが行動（ドロー）したためDon受付を終了
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
                if (actor.IsActive && actor.IsCPU)
                {
                    int total = ServerGetHandTotal(actor.ActorId);
                    if (total == top.Rank && total <= 13)
                    {
                        Debug.Log($"[CPU] Actor {actor.ActorId} (CPU) Declaring DON! Hand Total: {total}, Discard Rank: {top.Rank}");
                        IsDonWindowOpen = false;
                        DonGraceTimer = TickTimer.None;

                        // CPUのDonが成功したので、待機中の勝利（あれば）はキャンセル
                        PendingWinnerActorId = -1;

                        DonDeclarerActorId = actor.ActorId;
                        DonTargetActorId = LastPlayedPlayerActorId;
                        IsWaitingForDonGaeshi = true;

                        var targetActor = GetActor(DonTargetActorId);
                        if (targetActor.IsActive)
                        {
                            int targetTotal = ServerGetHandTotal(targetActor.ActorId);
                            if (targetTotal != top.Rank)
                            {
                                RPC_NotifyDon(actor.ActorId, ServerGetHandString(actor.ActorId));
                                ConfirmDonWin(actor.ActorId, targetActor.ActorId, top.Rank);
                            }
                            else if (targetActor.IsCPU)
                            {
                                // ターゲットがCPUの場合：ドンに対して自動でドン返し
                                RPC_NotifyDon(actor.ActorId, ServerGetHandString(actor.ActorId)); // まず最初のドン
                                RPC_NotifyDon(targetActor.ActorId, ServerGetHandString(targetActor.ActorId)); // 次にドン返し
                                IsWaitingForDonGaeshi = false;
                                ConfirmDonGaeshiWin(targetActor.ActorId, actor.ActorId, top.Rank);
                            }
                            // ターゲチEが実Eレイヤーの場合E手動でドン返しするか、猶予時間Eれを征E
                            // 猶予時間Eれ時の勝敗確定E後述のDonGraceTimer.Expiredにて忁EE
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

