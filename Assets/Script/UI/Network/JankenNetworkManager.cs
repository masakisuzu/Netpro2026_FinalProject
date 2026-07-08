using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using R3;
using UnityEngine;

namespace Script.Network
{
    public enum MatchingResultType { Success, Error }

    public readonly struct MatchingResult
    {
        public readonly MatchingResultType Type;
        public readonly string Message;

        public MatchingResult(MatchingResultType type, string message)
        {
            Type = type;
            Message = message;
        }
    }

    public class JankenNetworkManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static JankenNetworkManager Instance { get; private set; }

        [SerializeField] private NetworkRunner networkRunnerPrefab; // Fusionの心臓、通信の中心
        public NetworkRunner Runner { get; private set; } // その心臓を生成（ネットワーク処理を開始）したらここで管理

        public string PlayerName { get; private set; } = "";
        public string RoomId { get; private set; } = "";

        private const int MaxCCULimit = 20; // Fusion FREEプラン上限
        private int _totalEstimatedConnections; // 現在の参加人数

        // セッション一覧を「一度だけ」受け取るための待機用
        private UniTaskCompletionSource<List<SessionInfo>> _sessionListTcs;

        // マッチング処理の結果をViewクラスに伝えるための発火装置（Viewクラスが購読して待機している）
        private readonly Subject<MatchingResult> _onMatchingResult = new();
        public Observable<MatchingResult> OnMatchingResult => _onMatchingResult;
        
        
        private CancellationTokenSource _cts;
        
        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

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

        /// <summary>
        /// 入室を試みる。
        /// ネット上に部屋は常に1つだけという制約を守るため、
        /// StartGameを呼ぶ前に既存の部屋の有無・IDをチェックする。
        /// </summary>
        public async UniTask EnterRoomAsync(string desiredRoomId, string playerName, CancellationToken ct = default)
        {
            Runner = Instantiate(networkRunnerPrefab);
            Runner.ProvideInput = true; // 送信を許可する
            Runner.AddCallbacks(this); // 通信結果をthis(JankenNetworkManagerクラス)に伝える
            
            _cts = new CancellationTokenSource();
            
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
            
            // 部屋を検査し、IDを定める
            string finalRoomId;
            if (sessionList == null || sessionList.Count == 0)
            {
                // 部屋が存在しない→自分が一人目として部屋を作る下準備
                finalRoomId = desiredRoomId;
            }
            else
            {
                // 想定上、部屋は常に1つだけ存在する
                var existing = sessionList[0];
                
                // 存在するかつ同じIDだったら参加の下準備
                if (existing.Name == desiredRoomId)
                {
                    finalRoomId = desiredRoomId;
                }
                else
                {
                    // 不一致→StartGameを呼ばずに即座に失敗とする
                    _onMatchingResult.OnNext(new MatchingResult(MatchingResultType.Error, $"既に別の部屋（{existing.Name}）が開催中です"));
                    Runner.Shutdown();
                    Destroy(Runner.gameObject);
                    return;
                }
            }

            // これで名前とIDの準備が整った！
            PlayerName = playerName;
            RoomId = finalRoomId;

            // 部屋参加の型を作って
            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = RoomId,
                SceneManager = Runner.gameObject.GetComponent<NetworkSceneManagerDefault>()
            };

            // 参加を試みる！
            var result = await Runner.StartGame(args);
            if (result.Ok)
            {
                _onMatchingResult.OnNext(new MatchingResult(MatchingResultType.Success, $"入室しました（現在{Runner.SessionInfo.PlayerCount}人）"));
            }
            else
            {
                _onMatchingResult.OnNext(new MatchingResult(MatchingResultType.Error, result.ShutdownReason.ToString()));
                Runner.Shutdown();
                Destroy(Runner.gameObject);
            }
        }

        public void LeaveRoom()
        {
            if (Runner != null)
            {
                Runner.Shutdown();
            }
        }

        // ------------------------ INetworkRunnerCallbacks ------------------------
        
        /// <summary>
        /// JoinSessionLobby が成功すると呼ばれる
        /// </summary>
        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            // CCU集計（ゲーム開始後も毎回更新）
            _totalEstimatedConnections = 0;
            
            foreach (var session in sessionList) 
                _totalEstimatedConnections += session.PlayerCount;

            if (_totalEstimatedConnections >= MaxCCULimit)
            {
                _onMatchingResult.OnNext(new MatchingResult(MatchingResultType.Error, "現在サーバーが混み合っています"));
                runner.Shutdown();
            }

            // sessionListの値を結果として渡し、awaitを解決(終了)させる
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