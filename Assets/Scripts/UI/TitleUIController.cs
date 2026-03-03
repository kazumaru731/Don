using UnityEngine;
using UnityEngine.UI;
using DonGame2D.Network;
using System.Linq;

namespace DonGame2D.UI
{
    public class TitleUIController : MonoBehaviour
    {
        [Header("メイン画面")]
        public GameObject titleCanvasObj;
        public GameObject gameCanvasObj;
        public Button randomMatchButton;
        public Button friendMatchButton;   // フレンドマッチボタン
        public Button readyButton;         // ゲスト用Readyボタン
        public Button hostStartButton;     // ホスト用開始ボタン（新規追加）
        public Button randomMatchBackButton; // ランダムマッチ用戻るボタン
        public Text playersCountText;

        [Header("フレンドマッチパネル")]
        public GameObject friendMatchPanel;     // ホスト/ゲスト選択パネル
        public Button hostButton;               // ホスト選択ボタン
        public Button guestButton;              // ゲスト選択ボタン
        public GameObject hostPanel;            // ルームID表示パネル
        public GameObject guestPanel;           // ルームID入力パネル
        public Text roomIdDisplayText;          // ホスト用：生成されたルームIDを表示
        public Button copyIdButton;             // ホスト用：IDコピーボタン
        public InputField roomIdInputField;     // ゲスト用：ルームIDの入力欄
        public Button joinButton;               // ゲスト用：参加ボタン
        public Button backButton;               // 戻るボタン

        [Header("CPUマッチパネル")]
        public GameObject cpuMatchPanel;
        public Button cpuMatchButton;
        public Button cpu2PlayerButton;
        public Button cpu3PlayerButton;
        public Button cpu4PlayerButton;
        public Button cpuBackButton;

        [Header("フレンドマッチ CPU 追加")]
        public Text cpuCountLabel;       // "現在 X CPU" 表示ラベル
        public Button cpuAddButton;      // + ボタン
        public Button cpuRemoveButton;   // - ボタン

        private int friendCpuCount = 0;  // フレンドマッチで追加する CPU 数

        [Header("State")]
        public bool isMatching = false;
        public bool isReady = false;

        public static int selectedTargetPlayers = 4;

        private string generatedRoomId = "";

        // ランダムマッチかフレンドマッチかを区別するフラグ
        private bool isRandomMatch = false;

        private void Start()
        {
            if (titleCanvasObj != null) titleCanvasObj.SetActive(true);
            if (gameCanvasObj != null) gameCanvasObj.SetActive(false);

            // フレンドマッチパネルは最初は非表示
            if (friendMatchPanel != null) friendMatchPanel.SetActive(false);
            if (hostPanel != null) hostPanel.SetActive(false);
            if (guestPanel != null) guestPanel.SetActive(false);
            if (cpuMatchPanel != null) cpuMatchPanel.SetActive(false);

            // CPU追加数を必ず初期化
            friendCpuCount = 0;
            if (cpuAddButton != null) cpuAddButton.interactable = false;
            if (cpuRemoveButton != null) cpuRemoveButton.interactable = false;

            // CPUマッチボタンが未設定の場合、シーンから探す（不具合修正用）
            if (cpuMatchButton == null)
            {
                var allButtons = FindObjectsOfType<Button>(true);
                foreach (var btn in allButtons)
                {
                    var text = btn.GetComponentInChildren<Text>();
                    if (text != null && (text.text == "CPUマッチ" || text.text == "CPU Match"))
                    {
                        // 枠あり（オレンジ色）でないものを優先的に探すが、ここでは最初に見つかったものをセット
                        cpuMatchButton = btn;
                        Debug.Log($"[TitleUI] cpuMatchButton was null, found and assigned: {btn.name}");
                        break;
                    }
                }
            }

            if (randomMatchButton != null)
                randomMatchButton.onClick.AddListener(OnRandomMatchClicked);

            if (friendMatchButton != null)
                friendMatchButton.onClick.AddListener(OnFriendMatchClicked);

            if (hostButton != null)
                hostButton.onClick.AddListener(OnHostSelected);

            if (guestButton != null)
                guestButton.onClick.AddListener(OnGuestSelected);

            if (joinButton != null)
                joinButton.onClick.AddListener(OnJoinClicked);

            if (copyIdButton != null)
                copyIdButton.onClick.AddListener(OnCopyIdClicked);

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);

            if (cpuMatchButton != null)
                cpuMatchButton.onClick.AddListener(OnCpuMatchClicked);

            if (cpu2PlayerButton != null)
                cpu2PlayerButton.onClick.AddListener(() => { Debug.Log("[TitleUI] 2人ボタンが押されました"); OnCpuPlayerCountSelected(2); });

            if (cpu3PlayerButton != null)
                cpu3PlayerButton.onClick.AddListener(() => { Debug.Log("[TitleUI] 3人ボタンが押されました"); OnCpuPlayerCountSelected(3); });

            if (cpu4PlayerButton != null)
                cpu4PlayerButton.onClick.AddListener(() => { Debug.Log("[TitleUI] 4人ボタンが押されました"); OnCpuPlayerCountSelected(4); });


            if (cpuBackButton != null)
                cpuBackButton.onClick.AddListener(OnCpuBackClicked);

            // フレンドマッチ CPU 追加ボタン
            if (cpuAddButton != null)
                cpuAddButton.onClick.AddListener(OnCpuAddClicked);
            if (cpuRemoveButton != null)
                cpuRemoveButton.onClick.AddListener(OnCpuRemoveClicked);

            // Cancelボタンを作る代わりに、戻るボタンの役割を拡張する
            // 既存の backButton が押された時、または Title 表示中のどこか戻るような動作でキャンセルを呼ぶ
            // UnityのUI上で、ホスト/ゲストマッチング後に戻る手段として Cancel ボタンを割り当てても良いです。
            // 今回は戻るボタン(backButton)の仕様を調整します。

            if (readyButton != null)
            {
                readyButton.onClick.AddListener(OnReadyClicked);
                readyButton.gameObject.SetActive(false);
            }

            if (hostStartButton != null)
            {
                hostStartButton.onClick.AddListener(OnHostStartClicked);
                hostStartButton.gameObject.SetActive(false);
            }

            if (randomMatchBackButton != null)
            {
                randomMatchBackButton.onClick.AddListener(OnRandomMatchBackClicked);
                randomMatchBackButton.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (isMatching && DonFusionNetworkManager.Instance != null && DonFusionNetworkManager.Instance.Runner != null)
            {
                var runner = DonFusionNetworkManager.Instance.Runner;
                int count = runner.ActivePlayers.Count();
                
                // ホストかどうかの判定 (自身がサーバーまたはSharedMasterClientか)
                bool isHostLocal = runner.IsServer || runner.IsSharedModeMasterClient;

                                var fm = DonGame2D.Logic.DonFusionManager2D.Instance;
                
                // --- ゲスト画面用のカウントダウン表示 ---
                // Networked プロパティへのアクセスは Object.IsValid が true の時のみ許可される
                if (!isHostLocal && fm != null && fm.Object != null && fm.Object.IsValid && fm.GameStartCountdown.IsRunning && playersCountText != null)
                {
                    float remaining = fm.GameStartCountdown.RemainingTime(fm.Runner) ?? 0f;
                    playersCountText.text = $"ホストがゲームを開始しました！ {Mathf.CeilToInt(remaining)}秒後に開始...";
                }
                // --- その他の待機画面 ---
                else if (playersCountText != null && isMatching && !isReady)
                {
                    bool isCpuOnlyMatch = selectedTargetPlayers > 0 && !isRandomMatch && friendCpuCount == 0;

                    if (isRandomMatch)
                    {
                        playersCountText.text = $"現在 {count} 人\n3人以上で開始できます";
                        if (readyButton != null)
                        {
                            readyButton.interactable = (count >= 3);
                        }
                    }
                    else
                    {
                        if (isHostLocal)
                        {
                            int totalCount = isCpuOnlyMatch ? selectedTargetPlayers : (count + friendCpuCount);
                            
                            if (isCpuOnlyMatch)
                                playersCountText.text = $"CPUと対戦中... ({totalCount}人プレイ)\n準備ができたら開始してください";
                            else if (friendCpuCount > 0)
                                playersCountText.text = $"現在 {count} 人 + CPU {friendCpuCount} = 合計 {totalCount} 人\n3人以上で開始できます";
                            else
                                playersCountText.text = $"現在 {count} 人\n3人以上になったら開始してください";

                            if (hostStartButton != null)
                            {
                                // ネットワークオブジェクトが有効でない間は開始できない
                                bool isNetworkReady = fm != null && fm.Object != null && fm.Object.IsValid;
                                
                                if (isNetworkReady)
                                {
                                    hostStartButton.interactable = isCpuOnlyMatch ? true : (totalCount >= 3);
                                }
                                else
                                {
                                    hostStartButton.interactable = false;
                                    playersCountText.text = "ネットワーク初期化中...";
                                }
                            }
                        }
                        else
                        {
                            playersCountText.text = $"ホストの開始を待っています...\n現在 {count} 人 (開始には3人以上必要)";
                        }
                    }

                    var rect = playersCountText.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 320f);
                    }
                }
            }
        }
        private async void OnRandomMatchClicked()
        {
            if (isMatching) return;

            isMatching = true;
            isRandomMatch = true; // ランダムマッチ開始
            if (randomMatchButton != null) randomMatchButton.gameObject.SetActive(false);
            if (friendMatchButton != null) friendMatchButton.gameObject.SetActive(false);
            if (randomMatchBackButton != null) randomMatchBackButton.gameObject.SetActive(true);
            if (cpuMatchButton != null) cpuMatchButton.gameObject.SetActive(false); // CPUボタンも非表示
            if (playersCountText != null) playersCountText.text = "接続中...";

            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(false);
                readyButton.interactable = false;
            }

                        if (DonFusionNetworkManager.Instance != null)
                await DonFusionNetworkManager.Instance.StartGame(Fusion.GameMode.Shared, null, 4);


            if (!isMatching) return; // 接続中にキャンセルされた場合

            // 接続完了 → Readyボタンを表示
            ShowReadyButton();
        }

        // ========== フレンドマッチ ==========

        private void OnFriendMatchClicked()
        {
            if (friendMatchPanel != null) friendMatchPanel.SetActive(true);
            if (randomMatchButton != null) randomMatchButton.gameObject.SetActive(false);
            if (friendMatchButton != null) friendMatchButton.gameObject.SetActive(false);
            if (cpuMatchButton != null) cpuMatchButton.gameObject.SetActive(false); // CPUボタンも非表示
            if (hostPanel != null) hostPanel.SetActive(false);
            if (guestPanel != null) guestPanel.SetActive(false);

            // CPU追加数を必ずリセット（前回の残りを引き継がないようにする）
            friendCpuCount = 0;
            if (cpuAddButton != null) cpuAddButton.interactable = false;   // 接続前は操作不可
            if (cpuRemoveButton != null) cpuRemoveButton.interactable = false;
            if (cpuCountLabel != null) cpuCountLabel.text = "CPU: なし";

            // 状態のリセット（再選択時にボタンが押せるようにする）
            if (hostButton != null) hostButton.interactable = true;
            if (guestButton != null) guestButton.interactable = true;
        }

        private async void OnHostSelected()
        {
            if (isMatching) return;

            // 6桁のランダムIDを生成
            generatedRoomId = GenerateRoomId();
            if (roomIdDisplayText != null) roomIdDisplayText.text = $"ルームID: {generatedRoomId}";
            if (hostPanel != null) hostPanel.SetActive(true);
            if (guestPanel != null) guestPanel.SetActive(false);
            // 選択後はホスト・ゲストボタンを非表示にする
            if (hostButton != null) hostButton.gameObject.SetActive(false);
            if (guestButton != null) guestButton.gameObject.SetActive(false);

            isMatching = true;
            isRandomMatch = false; // フレンドマッチのホスト
            friendCpuCount = 0; // CPU追加数リセット
            UpdateCpuCountLabel();
            if (playersCountText != null) playersCountText.text = "ルーム作成中...";

            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(false);
                readyButton.interactable = false;
            }

            if (DonFusionNetworkManager.Instance != null)
                                await DonFusionNetworkManager.Instance.StartGame(Fusion.GameMode.Shared, generatedRoomId, 4);


            if (!isMatching) return; // 接続中にキャンセルされた場合

            ShowReadyButton();
        }

        // ========== CPUマッチ ==========

        private void OnCpuMatchClicked()
        {
            if (cpuMatchPanel != null) cpuMatchPanel.SetActive(true);
            if (randomMatchButton != null) randomMatchButton.gameObject.SetActive(false);
            if (friendMatchButton != null) friendMatchButton.gameObject.SetActive(false);
            if (cpuMatchButton != null) cpuMatchButton.gameObject.SetActive(false);
            
            // 選ぶ前はselectedTargetPlayersをリセットしておく（UpdateでのCPUマッチ判定のため）
            selectedTargetPlayers = 0;
        }

                        private async void OnCpuPlayerCountSelected(int count)
        {
            Debug.Log($"[TitleUI] OnCpuPlayerCountSelected called. Count: {count}, isMatching: {isMatching}");
            if (isMatching) return;

            selectedTargetPlayers = count;
            generatedRoomId = GenerateRoomId();
            isMatching = true;
            isRandomMatch = false;
            
            if (playersCountText != null) playersCountText.text = $"CPUと対戦 ({count}人プレイ) を準備中...";
            if (cpuMatchPanel != null) cpuMatchPanel.SetActive(false);

            if (DonFusionNetworkManager.Instance != null)
            {
                Debug.Log("[TitleUI] DonFusionNetworkManager.Instance.StartGame を呼び出します...");
                // 指定された人数でセッションを作成
                                await DonFusionNetworkManager.Instance.StartGame(Fusion.GameMode.Shared, generatedRoomId, count);
                Debug.Log("[TitleUI] DonFusionNetworkManager.Instance.StartGame が完了しました。");
            }

            if (!isMatching) return;

            ShowReadyButton();

            // CPUマッチは自分がホストなので、開始処理を呼ぶ。
            // ネットワークオブジェクトの生成(Spawned)を待つために少し待機するか、
            // ForceStartGameByHost 内部の予約機能に任せる。
            if (DonGame2D.Logic.DonFusionManager2D.Instance != null)
            {
                Debug.Log("[TitleUI] CPUマッチの開始を要求します。");
                DonGame2D.Logic.DonFusionManager2D.Instance.ForceStartGameByHost();
            }

        }


        private void OnCpuBackClicked()
        {
            if (cpuMatchPanel != null) cpuMatchPanel.SetActive(false);
            if (randomMatchButton != null) randomMatchButton.gameObject.SetActive(true);
            if (friendMatchButton != null) friendMatchButton.gameObject.SetActive(true);
            if (cpuMatchButton != null) cpuMatchButton.gameObject.SetActive(true);
        }

        private void OnGuestSelected()
        {
            if (hostPanel != null) hostPanel.SetActive(false);
            if (guestPanel != null) guestPanel.SetActive(true);
            // 選択後はホスト・ゲストボタンを非表示にする
            if (hostButton != null) hostButton.gameObject.SetActive(false);
            if (guestButton != null) guestButton.gameObject.SetActive(false);
        }

        private async void OnJoinClicked()
        {
            if (isMatching) return;
            if (roomIdInputField == null || string.IsNullOrEmpty(roomIdInputField.text)) return;

            string inputId = roomIdInputField.text.Trim().ToUpper();
            isMatching = true;
            isRandomMatch = false; // フレンドマッチのゲスト
            if (joinButton != null) joinButton.interactable = false;
            if (playersCountText != null) playersCountText.text = $"ID [{inputId}] に接続中...";

            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(false);
                readyButton.interactable = false;
            }

            if (DonFusionNetworkManager.Instance != null)
            {
                bool success =                     await DonFusionNetworkManager.Instance.StartGame(Fusion.GameMode.Shared, inputId, 4, isHost: false);

                
                if (!success)
                {
                    // 参加失敗時（IDが存在しないなど）
                    isMatching = false;
                    if (joinButton != null) joinButton.interactable = true;
                    if (roomIdInputField != null) roomIdInputField.interactable = true;
                    if (playersCountText != null) playersCountText.text = "ルームが見つかりませんでした。";
                    return;
                }
            }

            if (!isMatching) return; // 接続中にキャンセルされた場合

            // フレンドマッチパネル全体を閉じるのではなく、
            // 戻るボタンなどを残すため、ここでは何も非表示にせずReadyボタンだけ出す
            ShowReadyButton();
        }

        private void OnCopyIdClicked()
        {
            if (!string.IsNullOrEmpty(generatedRoomId))
            {
                GUIUtility.systemCopyBuffer = generatedRoomId;
                Debug.Log($"ルームIDをクリップボードにコピーしました: {generatedRoomId}");
                
                // コピー完了を分かりやすくするため、ボタンのテキストを一時的に変更する等の処理を入れても良いです
                if (copyIdButton != null)
                {
                    var textCmp = copyIdButton.GetComponentInChildren<Text>();
                    if (textCmp != null) textCmp.text = "コピー完了！";
                }
            }
        }

        private void OnRandomMatchBackClicked()
        {
            // キャンセル処理
            CancelMatchmaking();

            // ランダムマッチ特有のUIリセット
            if (randomMatchBackButton != null) randomMatchBackButton.gameObject.SetActive(false);
            if (randomMatchButton != null) randomMatchButton.gameObject.SetActive(true);
            if (friendMatchButton != null) friendMatchButton.gameObject.SetActive(true);
            if (cpuMatchButton != null) cpuMatchButton.gameObject.SetActive(true); // CPUボタンも復帰
        }

        private void OnBackClicked()
        {
            // マッチング中（接続中 or Ready待機中）であれば、まずは通信をキャンセルする
            if (isMatching || isReady)
            {
                CancelMatchmaking();
            }

            // UIを初期状態に戻す（フレンドマッチパネルを閉じる）
            if (friendMatchPanel != null) friendMatchPanel.SetActive(false);
            if (randomMatchButton != null) randomMatchButton.gameObject.SetActive(true);
            if (friendMatchButton != null) friendMatchButton.gameObject.SetActive(true);
            if (cpuMatchButton != null) cpuMatchButton.gameObject.SetActive(true); // CPUボタンも復帰
            // ホスト・ゲストボタンも復帰
            if (hostButton != null) hostButton.gameObject.SetActive(true);
            if (guestButton != null) guestButton.gameObject.SetActive(true);
            
            // READYボタンを隠す
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(false);
            }
        }

        private void CancelMatchmaking()
        {
            // 既にキャンセル処理中で何もないなら無視
            if (!isMatching && !isReady) return;

            Debug.Log("マッチングをキャンセルしました。");
            isMatching = false;
            isReady = false;
            
            // 状態（UI）のリセット
            if (playersCountText != null) playersCountText.text = "誰もいません";
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(false);
                readyButton.GetComponentInChildren<Text>().text = "READY";
            }
            if (hostStartButton != null)
            {
                hostStartButton.gameObject.SetActive(false);
            }
            if (randomMatchBackButton != null) randomMatchBackButton.gameObject.SetActive(false);
            if (randomMatchButton != null) 
            {
                randomMatchButton.gameObject.SetActive(true);
                randomMatchButton.interactable = true;
            }
            if (friendMatchButton != null) 
            {
                friendMatchButton.gameObject.SetActive(true);
                friendMatchButton.interactable = true;
            }
            if (hostButton != null) hostButton.interactable = true;
            if (guestButton != null) guestButton.interactable = true;

            if (cpuMatchPanel != null) cpuMatchPanel.SetActive(false);
            if (cpuMatchButton != null) cpuMatchButton.gameObject.SetActive(true);

            // 各パネルの表示状態もリセット（初期化）
            if (hostPanel != null) hostPanel.SetActive(false);
            if (guestPanel != null) guestPanel.SetActive(false);
            if (roomIdInputField != null)
            {
                roomIdInputField.interactable = true;
                roomIdInputField.text = "";
            }
            if (copyIdButton != null)
            {
                var textCmp = copyIdButton.GetComponentInChildren<Text>();
                if (textCmp != null) textCmp.text = "コピー";
            }

            // 通信の切断
            if (DonFusionNetworkManager.Instance != null && DonFusionNetworkManager.Instance.Runner != null)
            {
                // Fusionセッションから退出/停止する（ゲームオブジェクト自体は使い回すためdestroyGameObjectはfalse）
                DonFusionNetworkManager.Instance.Runner.Shutdown(destroyGameObject: false);
            }
            
            // DonFusionManager2D(実際にゲームを管理するクラス)のReady状態もリセットする
            if (DonGame2D.Logic.DonFusionManager2D.Instance != null)
            {
                DonGame2D.Logic.DonFusionManager2D.Instance.SetPlayerReady(false);
            }
        }

        // ========== Ready ==========

        private void ShowReadyButton()
        {
            // 以前は `friendMatchPanel.SetActive(false);` をおこなっていたため、
            // 戻るボタンが消えてしまっていた。
            // 戻るボタンを使用可能にし続けるため、フレンドマッチパネルは開いたままとする
            bool isHostLocal = DonFusionNetworkManager.Instance != null && DonFusionNetworkManager.Instance.Runner != null && (DonFusionNetworkManager.Instance.Runner.IsServer || DonFusionNetworkManager.Instance.Runner.IsSharedModeMasterClient);

            if (isRandomMatch)
            {
                // ランダムマッチ時は全員にREADYボタンを表示する（ホスト開始ボタンは使わない）
                if (readyButton != null)
                {
                    readyButton.gameObject.SetActive(true);
                    readyButton.interactable = true;
                }
                if (hostStartButton != null) hostStartButton.gameObject.SetActive(false);
            }
            else
            {
                // フレンドマッチ時
                if (isHostLocal)
                {
                    // ホストはゲーム開始ボタン（Updateで interactable を制御するので初期 false）
                    if (hostStartButton != null)
                    {
                        hostStartButton.gameObject.SetActive(true);
                        hostStartButton.interactable = false; // Updateで人数チェック後に有効化
                    }
                    // CPUボタンを接続後に有効化
                    UpdateCpuCountLabel();
                    if (readyButton != null) readyButton.gameObject.SetActive(false);
                }
                else
                {
                    // ゲストは何もボタンを押さず待つだけ
                    if (readyButton != null) readyButton.gameObject.SetActive(false);
                    if (hostStartButton != null) hostStartButton.gameObject.SetActive(false);
                }
            }

            if (playersCountText != null && DonFusionNetworkManager.Instance?.Runner != null)
            {
                var runner = DonFusionNetworkManager.Instance.Runner;
                int count = runner.ActivePlayers.Count();
                // ActivePlayers が空（または自分のみ）の場合、SessionInfo から正確な人数を取る試み
                if (count <= 1 && runner.SessionInfo != null && runner.SessionInfo.PlayerCount > count)
                {
                    count = runner.SessionInfo.PlayerCount;
                }

                if (isRandomMatch)
                {
                    playersCountText.text = $"他のプレイヤーを待っています...\n現在 {count} 人 (3人以上で開始可能)";
                }
                else
                {
                    if (isHostLocal)
                        playersCountText.text = $"準備ができたら開始してください\n待機中 {count} 人";
                    else
                        playersCountText.text = $"ホストの開始を待っています...\n待機中 {count} 人";
                }
            }
            if (backButton != null) backButton.gameObject.SetActive(true);
        }

        private void OnHostStartClicked()
        {
            Debug.Log($"<color=white>[TitleUI] OnHostStartClicked. isMatching: {isMatching}, friendCpuCount: {friendCpuCount}</color>");
            if (!isMatching) return;

            int currentRealPlayers = DonFusionNetworkManager.Instance?.Runner?.ActivePlayers.Count() ?? 1;
            int totalForStart = currentRealPlayers + friendCpuCount;
            
            // CPUマッチ（selectedTargetPlayers > 0 且つ FriendMatch でない場合）の判定
            bool isCpuMatch = selectedTargetPlayers > 0 && !isRandomMatch && friendCpuCount == 0;
            
            Debug.Log($"<color=white>[TitleUI] Start Check: total={totalForStart}, isCpuMatch={isCpuMatch}</color>");

            if (!isCpuMatch && totalForStart < 3)
            {
                Debug.LogWarning($"<color=orange>[TitleUI] 人数不足のため開始できません: 合計 {totalForStart} 人 (必要: 3人以上)</color>");
                return;
            }

            if (hostStartButton != null)
            {
                hostStartButton.interactable = false;
                hostStartButton.GetComponentInChildren<Text>().text = "Starting...";
            }

            var fm = DonGame2D.Logic.DonFusionManager2D.Instance;
            if (fm != null && fm.Object != null && fm.Object.IsValid)
            {
                int finalTarget = isCpuMatch ? selectedTargetPlayers : totalForStart;
                Debug.Log($"<color=white>[TitleUI] RPC_FriendMatchForceStart({finalTarget}) を送信します。</color>");
                fm.RPC_FriendMatchForceStart(finalTarget);
            }
            else
            {
                Debug.LogError("<color=red>[TitleUI] DonFusionManager2D.Instance is null or not valid yet! Cannot start game.</color>");
                if (hostStartButton != null) hostStartButton.interactable = true;
            }
        }

        private void OnReadyClicked()
        {
            if (!isMatching || isReady) return;

            isReady = true;
            if (readyButton != null)
            {
                readyButton.interactable = false;
                readyButton.GetComponentInChildren<Text>().text = "Waiting...";
            }

            if (DonGame2D.Logic.DonFusionManager2D.Instance != null)
            {
                DonGame2D.Logic.DonFusionManager2D.Instance.SetPlayerReady(true);
            }
        }

        public void SwitchToGameUI()
        {
            if (titleCanvasObj != null) titleCanvasObj.SetActive(false);
            if (gameCanvasObj != null) gameCanvasObj.SetActive(true);
        }

        // ========== フレンドマッチ CPU 追加 ==========

        private void OnCpuAddClicked()
        {
            if (DonFusionNetworkManager.Instance?.Runner == null) return;
            int realPlayers = DonFusionNetworkManager.Instance.Runner.ActivePlayers.Count();
            int maxCpu = Mathf.Max(0, 4 - realPlayers); // 上限 4人 - 実プレイヤー数
            if (friendCpuCount < maxCpu)
            {
                friendCpuCount++;
                UpdateCpuCountLabel();
            }
        }

        private void OnCpuRemoveClicked()
        {
            if (friendCpuCount > 0)
            {
                friendCpuCount--;
                UpdateCpuCountLabel();
            }
        }

        private void UpdateCpuCountLabel()
        {
            if (cpuCountLabel != null)
            {
                if (friendCpuCount == 0)
                    cpuCountLabel.text = "CPU: なし";
                else
                    cpuCountLabel.text = $"CPU: +{friendCpuCount}";
            }
            // +/-ボタンの有効化制御
            if (DonFusionNetworkManager.Instance?.Runner != null)
            {
                int realPlayers = DonFusionNetworkManager.Instance.Runner.ActivePlayers.Count();
                int maxCpu = Mathf.Max(0, 4 - realPlayers);
                if (cpuAddButton != null) cpuAddButton.interactable = (friendCpuCount < maxCpu);
                if (cpuRemoveButton != null) cpuRemoveButton.interactable = (friendCpuCount > 0);
            }
        }

        // ========== ユーティリティ ==========

        private string GenerateRoomId()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 「O」「0」「I」「1」など紛らわしい文字を除外
            var random = new System.Random();
            var result = new System.Text.StringBuilder(6);
            for (int i = 0; i < 6; i++)
                result.Append(chars[random.Next(chars.Length)]);
            return result.ToString();
        }
    }
}
