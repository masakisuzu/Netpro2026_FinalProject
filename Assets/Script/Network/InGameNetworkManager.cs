using System.Collections.Generic;
using Fusion;
using R3;
using Script.UI;
using UnityEngine;

namespace Script.Network
{
    public class InGameNetworkManager : MonoBehaviour
    {
        public static InGameNetworkManager Instance { get; private set; } // どこからでも参照できるように

        // InGameの舞台を作る者たち
        [SerializeField] private NetworkPrefabRef inGamePlayerPrefab;
        [SerializeField] private NetworkPrefabRef roundControllerPrefab;
        
        [Header("メンバー表示")]
        [SerializeField] private Transform memberListContainer;
        [SerializeField] private InGameMemberStateView memberStatePrefab;

        // 参加者一覧の管理
        private readonly ReactiveProperty<List<InGamePlayer>> _players = new(new List<InGamePlayer>());
        public ReadOnlyReactiveProperty<List<InGamePlayer>> Players => _players;
        
        // ゲーム開始時点の部屋の参加者総数（一度だけ確定する定数。以後変わらない）
        public int InitialMemberCount { get; private set; }
        private bool _initialCountCaptured = false;

        // インゲーム内のフェーズ管理（初期はカウントダウン）
        private readonly Subject<RoundPhase> _onPhaseChanged = new();
        public Observable<RoundPhase> OnPhaseChanged => _onPhaseChanged;
        
        // 現在のフェーズはここで一元管理
        public RoundPhase CurrentPhase { get; private set; } = RoundPhase.Countdown;
        
        // 「このラウンドの対象者」として確定させたスナップショット
        // Thinking中はこのリストに対して一切追従しないことで、結果発表時に切断者のUI処理ができる
        public IReadOnlyList<InGamePlayer> RoundSnapshot => _roundSnapshot;
        private List<InGamePlayer> _roundSnapshot = new();

        // どの InGamePlayer がどの UI に対応しているか覚えておく
        private readonly Dictionary<InGamePlayer, InGameMemberStateView> _memberStates = new();

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
                runner.Spawn(roundControllerPrefab);
        }

        public void RegisterPlayer(InGamePlayer player)
        {
            var list = new List<InGamePlayer>(_players.CurrentValue); // TitleManagerのやつと同じ仕組みの追加方法
            list.Add(player);
            _players.Value = list;
        }

        public void UnregisterPlayer(InGamePlayer player)
        {
            var list = new List<InGamePlayer>(_players.CurrentValue);
            list.Remove(player);
            _players.Value = list;
        }

        /// <summary>
        /// Roundクラスから呼ばれる、Phaseの更新を受け取る
        /// </summary>
        public void NotifyPhaseChanged(RoundPhase phase)
        {
            CurrentPhase = phase;

            switch (phase)
            {
                case RoundPhase.Countdown:
                    PrepareForNewRound();
                    break;

                case RoundPhase.Judging:
                    // 結果発表の直前に、離脱者・未選択者だけ先に確定させる
                    UpdateRetireesDisplay();
                    
                    // じゃーんけーん・・・
                    
                    // 生き残った人の実際の手（グー・パー）をここで初めて反映する
                    RevealHandResults();
                    break;

                case RoundPhase.Result:
                    
                    break;
            }

            // これを発火させて今度はUIが更新
            // ModelクラスがViewクラスを直接参照（依存）してなくていい設計！
            _onPhaseChanged.OnNext(phase);
        }
        
        /// <summary>
        /// Countdown開始時（毎ラウンド）呼ぶ
        /// 「接続中 かつ 未リタイア」の人だけをスナップショットする
        /// </summary>
        private void PrepareForNewRound()
        {
            // 参加者総数を把握するため初回のみ計算する（あまり良くない設計…？）
            if (!_initialCountCaptured)
            {
                InitialMemberCount = _players.CurrentValue.Count;
                _initialCountCaptured = true;
            }
            
            var activePlayers = new List<InGamePlayer>();
            foreach (var player in _players.CurrentValue)
            {
                // 既にリタイア表示になっている人は除外
                // （Viewは消さないのでそのままリタイア表示のまま画面には残り続ける）
                if (_memberStates.TryGetValue(player, out var view) && view.IsRetired)
                    continue;

                activePlayers.Add(player);
            }
            
            _roundSnapshot = activePlayers;
            SyncMemberViews(_roundSnapshot);
        }
        
        /// <summary>
        /// 生き残りのView初期化。
        /// リタイア済みの人はここを通らないので、Defaultに巻き戻されず表示が維持される
        /// </summary>
        private void SyncMemberViews(List<InGamePlayer> players)
        {
            foreach (var player in players)
            {
                if (!_memberStates.TryGetValue(player, out var item))
                {
                    item = Instantiate(memberStatePrefab, memberListContainer);
                    _memberStates.Add(player, item);
                }

                item.SetData(player.PlayerName.ToString()); // 1ターン目以降、無意味なるけど別にいいか
                item.SetIcon(IconType.Default); // アイコンを元に戻す！
            }
        }

        /// <summary>
        /// 切断者・リタイア・時間切れになった人のアイコンをRetireに切り替える
        /// Judging開始の直前（結果発表の直前）に呼び、先に敗北者だけ表示させたい
        /// </summary>
        private void UpdateRetireesDisplay()
        {
            var currentPlayers = _players.CurrentValue;

            foreach (var player in _roundSnapshot)
            {
                if (!_memberStates.TryGetValue(player, out var view)) continue;

                bool disconnected = !currentPlayers.Contains(player); // 切断されてる
                bool chooseRetireHand = !disconnected && player.HandIcon == IconType.Retire; // チョキ選んでる
                bool timedOut = !disconnected && player.HandIcon == IconType.Default; // 時間切れ

                if (disconnected || chooseRetireHand || timedOut)
                    view.SetIcon(IconType.Retire); // リタイア者表示！！
            }
        }

        /// <summary>
        /// 生存者について、実際に選んだ手（Rock/Paperのみ）を公開
        /// Judging開始時に呼ぶ
        /// </summary>
        private void RevealHandResults()
        {
            var currentPlayers = _players.CurrentValue;

            foreach (var player in _roundSnapshot)
            {
                if (!_memberStates.TryGetValue(player, out var view)) continue;
                if (!currentPlayers.Contains(player)) continue;

                if (player.HandIcon != IconType.Retire)
                    view.SetIcon(player.HandIcon); // Rock / Paper 公開！！
            }
        }
    }
}