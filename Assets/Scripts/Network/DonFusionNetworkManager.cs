using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DonGame2D.Network
{
    /// <summary>
    /// Photon Fusion 2 のセッション管理クラス（Shared Mode）
    /// ゲームオブジェクトにアタッチして使用してください。
    /// </summary>
    public class DonFusionNetworkManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static DonFusionNetworkManager Instance { get; private set; }
        private NetworkRunner _runner;
        public NetworkRunner Runner => _runner;

        [Header("Fusion Settings")]
        [Tooltip("作成または参加するルーム名（空の場合は自動生成）")]
        public string roomName = "DonRoom";

        [Tooltip("最大プレイヤー数")]
        public int maxPlayers = 8;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // TitleUIControllerから手動でStartGameを呼ぶため、自動開始は削除
        }

        /// <summary>
        /// 指定されたモードでゲームを開始します。
        /// </summary>
        public async System.Threading.Tasks.Task<bool> StartGame(GameMode mode)
        {
            return await StartGame(mode, roomName, 8); // デフォルトは8人
        }


        /// <summary>
        /// 指定されたモードとルーム名でゲームを開始します（フレンドマッチ用）。
        /// isHostがfalseの場合は、既存のルームへの参加のみを許可します（新規作成はしない）。
        /// </summary>
        public async System.Threading.Tasks.Task<bool> StartGame(GameMode mode, string sessionName, int playersCount, bool isHost = true)
        {
            // もし以前のRunnerが残っていればシャットダウンして破棄する
            if (_runner != null)
            {
                await _runner.Shutdown(destroyGameObject: true);
                _runner = null;
            }

            // NetworkRunner用の専用ゲームオブジェクトを作成（複数回実行時の競合を避けるため子オブジェクトにする）
            GameObject runnerObj = new GameObject("FusionRunner");
            runnerObj.transform.SetParent(this.transform);

            // NetworkRunner を作成してゲームオブジェクトにアタッチ
            _runner = runnerObj.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            
            // マネージャ自身のコールバックを受け取るように登録
            _runner.AddCallbacks(this);

            // シーン情報の設定
            var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);

            // セッション開始
            var startGameArgs = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = sessionName,
                PlayerCount = playersCount,

                Scene = scene,
                SceneManager = runnerObj.AddComponent<NetworkSceneManagerDefault>(),
                EnableClientSessionCreation = isHost, // ホストでなければ新規ルームを作成させない
            };
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log($"<color=green>Fusion セッション開始成功！ ルーム: {sessionName}</color>");
                
                // 人数設定は DonFusionManager2D.ForceStartGameByHost で直接行われるため、ここでの遅延設定は不要
                
                return true;
            }
            else
            {
                Debug.LogWarning($"Fusion セッション開始失敗 (ルーム未発見など): {result.ShutdownReason}");
                return false;
            }
        }


        // ==============================
        // INetworkRunnerCallbacks の実装
        // ==============================

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"プレイヤーが参加しました: {player}");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"プレイヤーが退出しました: {player}");
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("サーバーに接続しました。");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.LogWarning($"サーバーから切断されました: {reason}");
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"接続失敗: {reason}");
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"Fusion シャットダウン: {shutdownReason}");
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        /// <summary>
        /// 現在のネットワークセッションを終了します。
        /// </summary>
        public void ShutdownNetRunner()
        {
            if (_runner != null)
            {
                _runner.Shutdown(destroyGameObject: true);
                _runner = null;
                Debug.Log("Fusion Runner has been shut down.");
            }
        }
    }
}
