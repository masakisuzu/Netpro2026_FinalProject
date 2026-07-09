using System.Collections.Generic;
using Fusion;
using R3;
using UnityEngine;

namespace Script.Network
{
    public class InGameNetworkManager : MonoBehaviour
    {
        public static InGameNetworkManager Instance { get; private set; }

        [SerializeField] private NetworkPrefabRef inGamePlayerPrefab;
        [SerializeField] private NetworkPrefabRef roundControllerPrefab;

        private readonly ReactiveProperty<List<InGamePlayer>> _players = new(new List<InGamePlayer>());
        public ReadOnlyReactiveProperty<List<InGamePlayer>> Players => _players;

        private readonly Subject<RoundPhase> _onPhaseChanged = new();
        public Observable<RoundPhase> OnPhaseChanged => _onPhaseChanged;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            var runner = JankenNetworkManager.Instance.Runner;

            // 各自、自分専用のInGamePlayerを生成する
            runner.Spawn(inGamePlayerPrefab, inputAuthority: runner.LocalPlayer);

            // 代表者だけがラウンド管理オブジェクトを1つ生成する
            if (runner.IsSharedModeMasterClient)
            {
                runner.Spawn(roundControllerPrefab);
            }
        }

        public void RegisterPlayer(InGamePlayer player)
        {
            var list = new List<InGamePlayer>(_players.CurrentValue);
            list.Add(player);
            _players.Value = list;
        }

        public void UnregisterPlayer(InGamePlayer player)
        {
            var list = new List<InGamePlayer>(_players.CurrentValue);
            list.Remove(player);
            _players.Value = list;
        }

        public void NotifyPhaseChanged(RoundPhase phase)
        {
            _onPhaseChanged.OnNext(phase);
        }
    }
}