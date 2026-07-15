using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using R3;
using Script.Network.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Script.Network.Manager
{
    /// <summary>
    /// シーンを跨ぐネットワーク管理クラス
    /// Runner本体、自分の名前・部屋ID、マッチング処理等
    /// </summary>
    public class JankenNetworkManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static JankenNetworkManager Instance { get; private set; }

        [SerializeField] private NetworkRunner networkRunnerPrefab; // Fusion自体のエンジン、ゲームロジックとは違うネットワーク接続そのもの
        [SerializeField] private NetworkPrefabRef titlePlayerPrefab; // 電話で話している内容そのもの。ネットワーク経由で名前等、伝えたい情報を送り合える
        
        private const string SessionPropertyIsInGame = "IsInGame"; // 開始前か開始中かの判定管理
        public NetworkRunner Runner { get; private set; } // networkRunnerPrefabを生成（ネットワーク処理を開始）したらここで管理
        public string PlayerName { get; private set; } = ""; // 詳細(PlayerInfo)を外部から参照するためにも必要
        public string RoomId { get; private set; } = "";

        public const int MaxCCULimit = 100; // Fusion FREEプラン上限（1つのプロジェクトだけ）

        // セッション一覧を「一度だけ」受け取るための待機用
        private UniTaskCompletionSource<List<SessionInfo>> _sessionListTcs;

        // マッチング処理の結果をViewクラスに伝えるための発火装置（Viewクラスが購読して待機している）
        private readonly Subject<MatchingResult> _onMatchingResult = new();
        public Observable<MatchingResult> OnMatchingResult => _onMatchingResult;

        /// <summary>
        /// BootStrapクラスから呼ばれる
        /// </summary>
        public void Initialize()
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

        /// <summary>
        /// 入室を試みる。
        /// ネット上に部屋は常に1つだけという制約を守るため、
        /// StartGameを呼ぶ前に既存の部屋の有無・IDをチェックする。
        /// </summary>
        public async UniTask EnterRoomAsync(string desiredRoomId, string playerName)
        {
            Runner = Instantiate(networkRunnerPrefab);
            Runner.ProvideInput = true; // 送信を許可する
            Runner.AddCallbacks(this); // 通信結果をthis(JankenNetworkManagerクラス)に伝える
            
            // OnSessionListUpdated から部屋一覧を受け取るための待機オブジェクト
            _sessionListTcs = new UniTaskCompletionSource<List<SessionInfo>>();
            
            // ロビーへ接続する（部屋一覧を受け取る準備）
            await Runner.JoinSessionLobby(SessionLobby.Shared);
            
            // OnSessionListUpdated が呼ばれる(JoinSessionLobbyが成功する)と、この変数も更新される
            // 部屋一覧を取得するかタイムアウトするまで待機
            List<SessionInfo> sessionList;
            try
            {
                // 原因は見つかればエラーとして直ぐ返答するのであまりここのタイムアウト処理は呼ばれない…？
                sessionList = await _sessionListTcs.Task.Timeout(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                _onMatchingResult.OnNext(new MatchingResult(MatchingResultType.Error, "セッション一覧の取得がタイムアウトしました"));
                Runner.Shutdown();
                Destroy(Runner.gameObject);
                return;
            }
            
            // 部屋を検査し、入れるかチェック。人がいなかったらスキップ
            if (sessionList != null && sessionList.Count > 0)
            {
                // 想定上、部屋は常に1つだけ存在する
                var existing = sessionList[0];
                
                // 名前が違う部屋は拒否
                if (existing.Name != desiredRoomId)
                {
                    _onMatchingResult.OnNext(new MatchingResult(MatchingResultType.Error, $"既に別の部屋（ID: {existing.Name}）が開催中らしい"));
                    Runner.Shutdown();
                    Destroy(Runner.gameObject);
                    return;
                }
                
                // CCU集計により、満員だったら拒否
                if (existing.PlayerCount >= MaxCCULimit)
                {
                    _onMatchingResult.OnNext(new MatchingResult(MatchingResultType.Error, "どうやら満員みたいです…"));
                    Runner.Shutdown();
                    Destroy(Runner.gameObject);
                    return;
                }
                
                // IDは一致しても、既にゲームが始まっているなら入れない
                bool isExisting = existing.Properties.TryGetValue(SessionPropertyIsInGame, out var prop) && prop == true;
                if (isExisting)
                {
                    _onMatchingResult.OnNext(new MatchingResult(MatchingResultType.Error, "その部屋は既に開始中らしい"));
                    Runner.Shutdown();
                    Destroy(Runner.gameObject);
                    return;
                }
            }
            
            // これで名前とIDの準備が整った！
            PlayerName = playerName;
            RoomId = desiredRoomId;

            // 部屋参加の型を作って
            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = RoomId,
                
                // 皆が同時にシーン遷移できるように
                SceneManager = Runner.gameObject.GetComponent<NetworkSceneManagerDefault>(),
                
                // 部屋を新規作成する場合、最初から「まだゲームは始まっていない」状態で公開しておく
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    { SessionPropertyIsInGame, false }
                }
            };

            // 参加を試みる！
            var result = await Runner.StartGame(args);
            if (result.Ok)
            {
                // Fusion（ネットワーク越し）での生成は instantiate ではなくSpawn
                // そのNetworkObjectの入力権限を誰が持つかを指定する。Spawn本人に持たせたいので自分を指定
                Runner.Spawn(titlePlayerPrefab, inputAuthority: Runner.LocalPlayer);
                _onMatchingResult.OnNext(new MatchingResult(MatchingResultType.Success, $"入室しました（現在{Runner.SessionInfo.PlayerCount}人）"));
            }
            else
            {
                Runner.Shutdown();
                Destroy(Runner.gameObject);
            }
        }
        
        /// <summary>
        /// InGameへ遷移する直前に呼ぶ。セッションを「もう募集していない」状態に切り替える
        /// 参加を試みる新規クライアントは、これ以降このセッションを弾けさせられる
        /// </summary>
        public void MarkSessionAsInGame()
        {
            if (Runner == null) return;

            Runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>
            {
                { SessionPropertyIsInGame, true }
            });
        }
        
        /// <summary>
        /// タイトルに戻りつつ、ネット切断をする
        /// インゲームを終えた時に呼ぶ
        /// </summary>
        public async UniTask CloseRoomAsync()
        {
            if (Runner == null) return;
            await Runner.Shutdown();
            
            Destroy(Runner.gameObject);
            Runner = null;

            SceneManager.LoadScene("Title");
        }
        
        // ------------------------ INetworkRunnerCallbacks ------------------------
        
        /// <summary>
        /// JoinSessionLobby が成功すると呼ばれる
        /// </summary>
        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            // sessionListの値を結果として渡し、awaitを解決(終了)させる
            // 満員かどうか、その分岐処理などは呼び出し元で判断させる
            _sessionListTcs?.TrySetResult(sessionList);
        }
        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            _onMatchingResult.OnNext(new MatchingResult(MatchingResultType.Error, reason.ToString()));
        }
        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    }
}