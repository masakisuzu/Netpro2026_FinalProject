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
        // 初ターン判定に、RoundクラスのTurnNumを参照してもいいが、更新タイミングのずれとかあり分かりやすく今回はBoolを使う
        private bool _isFirstRound = true;

        // インゲーム内のフェーズ管理（初期はカウントダウン）
        private readonly Subject<RoundPhase> _onPhaseChanged = new();
        public Observable<RoundPhase> OnPhaseChanged => _onPhaseChanged;
        
        // 「このラウンドの対象者」として確定させたスナップショット
        // Thinking中はこのリストに対して一切追従しないことで、結果発表時に切断者のUI処理ができる
        public IReadOnlyList<InGamePlayer> RoundSnapshot => _roundSnapshot;
        private List<InGamePlayer> _roundSnapshot = new();

        // どの InGamePlayer がどの UI に対応しているか覚えておく
        public IReadOnlyDictionary<InGamePlayer, InGameMemberStateView> MemberStates => _memberStates;
        private readonly Dictionary<InGamePlayer, InGameMemberStateView> _memberStates = new();

        private void Awake()
        {
            Instance = this;
        }
        
        private async void Start()
        {
            var runner = JankenNetworkManager.Instance.Runner;

            // 各自、自分専用のInGamePlayerを生成する
            // 同期Spawnだとシーン遷移直後にPrefabロードが間に合わないことがあるため、非同期版を使う
            await runner.SpawnAsync(inGamePlayerPrefab, inputAuthority: runner.LocalPlayer);

            // 代表者だけがラウンド管理オブジェクトを1つ生成する
            // これがあることでカウントダウンや、シンキングタイムの制御をしてくれる
            if (runner.IsSharedModeMasterClient)
                await runner.SpawnAsync(roundControllerPrefab);
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
        /// Phaseの更新をする
        /// 主に時間管理をするRoundクラスから呼ばれる
        /// </summary>
        public void NotifyPhaseChanged(RoundPhase phase)
        {
            // CurrentPhase = phase; // Phaseチェンジ！
            switch (phase)
            {
                case RoundPhase.Countdown:
                    PrepareForNewRound();
                    break;

                case RoundPhase.JudgeCall:
                    MarkRetirees(); // 結果発表の前に敗北者を表示する（UIの仕事もあるが一行で済むので…）
                    break;
                
                case RoundPhase.Judged:
                    RevealHandResults(); // 生き残った人の実際の手（グー・パー）を反映（UIの仕事もあるが一行で済むので…）
                    break;

                case RoundPhase.Result:
                    
                    break;
            }

            // これを発火させて今度はUIが更新
            // ModelクラスがViewクラスを直接参照（依存）してなくていい設計！
            _onPhaseChanged.OnNext(phase);
        }
        
        /// <summary>
        /// InGamePlayerのHandIconが変化するたびに呼ばれる。
        /// Thinking中に、切断者を除いた全員が選び終えたかを確認し、
        /// 揃っていれば代表者だけがタイマーを強制終了させる。
        /// </summary>
        public void NotifyHandChanged()
        {
            if (RoundController.Instance.CurrentPhase != RoundPhase.Think) return;

            var currentPlayers = _players.CurrentValue;
            foreach (var player in _roundSnapshot)
            {
                // 既に切断してしまった人は「選べない」ので判定から除外する
                if (!currentPlayers.Contains(player)) continue;

                // まだ誰か1人でも未選択(Default)なら、全員揃っていない
                if (player.HandIcon == IconType.Default) return;
            }

            // 全員選び終えているそう！！代表者だけがタイマーを終了させる
            if (JankenNetworkManager.Instance.Runner.IsSharedModeMasterClient)
                RoundController.Instance?.ForceExpireTimer();
        }
        
        /// <summary>
        /// Countdown開始時（毎ラウンド）呼ぶ
        /// 「接続中 かつ 未リタイア」の人だけをスナップショットする
        /// </summary>
        private void PrepareForNewRound()
        {
            // 初回ターンのみ処理したい内容
            if (_isFirstRound)
            {
                InitializeMembers();
                _isFirstRound = false;
            }
            
            var activePlayers = new List<InGamePlayer>();
            foreach (var player in _players.CurrentValue)
            {
                // 既にリタイア表示になっている人はスキップして除外
                // （Viewは消さないのでそのままリタイア表示のまま画面には残り続ける）
                if (_memberStates.TryGetValue(player, out var view) && view.IsRetired)
                    continue;

                // 生き残りのみ追加
                activePlayers.Add(player);
            }
            
            // 生き残りリスト更新
            _roundSnapshot = activePlayers;
            
            // 生存者だけ、自分自身のHandIconをDefaultへ戻す
            // UI反映用ではなくネットワークの内部値なのでここで
            // （他人の分はHasStateAuthority、つまり本人確認で弾かれる）
            foreach (var player in _roundSnapshot)
            {
                player.ResetHand();
            }
        }
        
        /// <summary>
        /// ゲーム開始時（初回のCountdownの時）に一度だけ呼ぶ。
        /// 参加者総数を固定し、全員分のView（表示行）をここでまとめて生成する。
        /// </summary>
        private void InitializeMembers()
        {
            // 参加者総数を把握する（母数、定数として扱っていく）
            InitialMemberCount = _players.CurrentValue.Count;
            
            foreach (var player in _players.CurrentValue)
            {
                if (_memberStates.ContainsKey(player)) continue; // 念のための安全策

                var item = Instantiate(memberStatePrefab, memberListContainer);
                item.SetData(player.PlayerName.ToString()); // 名前の反映
                item.SetIcon(IconType.Default); // アイコンの反映

                _memberStates.Add(player, item);
            }
        }

        /// <summary>
        /// 「切断」「リタイア」「時間切れ」になった人のアイコンをRetireに切り替える
        /// Judging開始の直前（結果発表の直前）に呼び、先に敗北者だけ表示させたい
        /// </summary>
        private void MarkRetirees()
        {
            var currentPlayers = _players.CurrentValue;
            foreach (var player in _roundSnapshot)
            {
                // Dictionaryで、playerをキーにして対応するviewを取り出す
                if (!_memberStates.TryGetValue(player, out var view)) continue;

                // 敗北者の条件
                bool disconnected = !currentPlayers.Contains(player); // 切断されてる
                bool chooseRetireHand = !disconnected && player.HandIcon == IconType.Retire; // チョキ選んでる
                bool timedOut = !disconnected && player.HandIcon == IconType.Default; // 時間切れ

                if (disconnected || chooseRetireHand || timedOut)
                {
                    view.SetIcon(IconType.Retire); // リタイアUI表示！！
                    player.SetHand(IconType.Retire); // 内部値にもリタイア扱いに
                }
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
                if (!_memberStates.TryGetValue(player, out var view)) continue; // キーでプレイヤーを特定
                if (!currentPlayers.Contains(player)) continue; // 生き残りではないならスキップ

                if (player.HandIcon != IconType.Retire)
                    view.SetIcon(player.HandIcon); // Rock / Paper 公開！！
            }
        }
        
        /// <summary>
        /// Thinking中、ボタンから呼ばれる。自分自身のInGamePlayerに手を書き込む
        /// </summary>
        public void SetLocalPlayerHand(IconType type)
        {
            foreach (var player in _players.CurrentValue)
            {
                if (player.Object.HasStateAuthority)
                    player.SetHand(type);
            }
        }
        
        /// <summary>
        /// 自分自身（StateAuthorityを持つInGamePlayer）が、既にリタイア済みかどうかを返す
        /// </summary>
        public bool IsLocalPlayerRetired()
        {
            foreach (var player in _players.CurrentValue)
            {
                if (player.Object.HasStateAuthority)
                    return _memberStates.TryGetValue(player, out var view) && view.IsRetired;
            }

            return false; // 自分のInGamePlayerが見つからない場合は、念のためfalse扱い
        }
    }
}