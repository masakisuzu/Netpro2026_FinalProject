using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using R3;
using Script.Network.Player;
using Script.Network.Utility;
using Script.UI;
using UnityEngine;

namespace Script.Network.Manager
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
        
        // インゲーム内のフェーズ管理（初期はカウントダウン）
        private readonly Subject<RoundPhase> _onPhaseChanged = new(); 
        public Observable<RoundPhase> OnPhaseChanged => _onPhaseChanged;
        
        // 「このラウンドの対象者」として確定させたスナップショット
        // Thinking中はこのリストに対して一切追従しないことで、結果発表時に切断者のUI処理ができる
        public IReadOnlyList<InGamePlayer> AliveSnapshot => aliveSnapshot;
        private List<InGamePlayer> aliveSnapshot = new();

        // どの InGamePlayer がどの UI に対応しているか覚えておく
        public IReadOnlyDictionary<InGamePlayer, InGameMemberStateView> MemberStates => _memberStates;
        private readonly Dictionary<InGamePlayer, InGameMemberStateView> _memberStates = new();

        // 直近のラウンド結果。EvaluateJudgement() で更新される
        // RoundControllerがこれを見てループを続けるか決める
        public RoundOutcome Outcome { get; private set; } = RoundOutcome.Continue;
        public string WinnerName { get; private set; } = "";
        
        // 共倒れ対象として確定したプレイヤー、つまり独り勝ちパーの場合は空
        private List<InGamePlayer> _pendingEliminations = new();
        
        /// <summary>
        /// BootStrapクラスから呼ばれる初期化処理
        /// </summary>
        public async UniTask InitializePlayer()
        {
            Instance = this;
            var runner = JankenNetworkManager.Instance.Runner;

            // 各自、自分専用のInGamePlayerを生成する
            // 同期Spawnだとシーン遷移直後にPrefabロードが間に合わないことがあるため、非同期版を使う
            await runner.SpawnAsync(inGamePlayerPrefab, inputAuthority: runner.LocalPlayer);
            
            // 「人数分が揃うまで」を条件に待つ
            await UniTask.WaitUntil(() => _players.CurrentValue.Count >= runner.SessionInfo.PlayerCount);
            
            // メンバー情報確定（スポーンした参加者をリスト化）
            foreach (var player in _players.CurrentValue)
            {
                if (_memberStates.ContainsKey(player)) continue; // 念のための安全策

                var item = Instantiate(memberStatePrefab, memberListContainer);
                item.SetData(player.PlayerName.ToString(), player.Object.HasStateAuthority); // 名前の反映
                item.SetIcon(IconType.Default); // アイコンの反映

                _memberStates.Add(player, item);
            }

            // Roundから毎ターン呼ばれるけど、初期化時の初ターンは自分で呼ぶ
            // （OnChangeRenderやフローの仕組み上自分で呼んだ方がわかりやすい）
            PrepareForNewRound();
        }

        /// <summary>
        /// 代表者だけ、ラウンド管理オブジェクトを1つ生成する 
        /// </summary>
        public async UniTask InitializeRoundController()
        {
            var runner = JankenNetworkManager.Instance.Runner;
            
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
        /// Viewの初期化完了時に呼ばれる。自分自身のUI準備完了を通知する
        /// </summary>
        public void MarkLocalPlayerUiReady()
        {
            foreach (var player in _players.CurrentValue)
            {
                if (player.Object.HasStateAuthority)
                    player.MarkUiReady();
            }
        }
        
        /// <summary>
        /// 現在接続中の全プレイヤーのUI初期化が完了しているかを返す。
        /// RoundController側で、ゲームループを開始してよいかの判定に使う。
        /// </summary>
        public bool AllPlayersUiReady()
        {
            // 誰もいない状態では判定しない（初期化中の一瞬を誤検知しないため）
            if (_players.CurrentValue.Count == 0) return false;

            // まだUI初期化ができていない！
            foreach (var player in _players.CurrentValue)
            {
                if (!player.IsUiReady) return false;
            }

            // Roundクラスへ、ラウンド管理ループ初めてもいいですよ
            return true;
        }

        /// <summary>
        /// Phaseの更新をする
        /// 主に時間管理をするRoundクラスから呼ばれる
        /// </summary>
        public void NotifyPhaseChanged(RoundPhase phase)
        {
            switch (phase)
            {
                case RoundPhase.Countdown:
                    RevealRetirees();
                    PrepareForNewRound();
                    break;
                
                case RoundPhase.JudgeSync:
                    MarkRetirees(); // 時間切れなので未選択者はリタイアを強制的に選ばせる
                    break;

                case RoundPhase.JudgeCall:
                    RevealRetirees(); // 結果発表の前に敗北者を表示する（UIの仕事もあるが一行で済むので…）
                    break;
                
                case RoundPhase.Judged:
                    RevealHandResults(); // 生き残った人の実際の手（グー・パー）を反映（UIの仕事もあるが一行で済むので…）
                    EvaluateJudgement(); // ここで勝敗を計算し、Outcomeに結果を残す
                    break;
                
                case RoundPhase.Eliminate:
                    EvaluateEliminations(); // ここで初めてPaper共倒れの書き換えを実行
                    break;

                case RoundPhase.Result:
                    RevealRetirees(); // 生き残り勝ちした時のためにパー同士の脱落も分かるようにしておく
                    CloseRoomCountdown().Forget();
                    break;
            }

            _onPhaseChanged.OnNext(phase); // これを発火させて今度はUIが更新
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
            foreach (var player in aliveSnapshot)
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
            var activePlayers = new List<InGamePlayer>();
            foreach (var player in _players.CurrentValue)
            {
                // 既にリタイア表示になっている人はスキップして除外
                // （Viewは消さないのでそのままリタイア表示のまま画面には残り続ける）
                if (player.IsRetired) 
                    continue;

                // 生き残りのみ追加
                activePlayers.Add(player);
            }
            
            // 生き残りリスト更新
            aliveSnapshot = activePlayers;
            
            // 生存者だけ、自分自身のHandIconをDefaultへ戻す
            // UI反映用ではなくネットワークの内部値なのでここで
            // （他人の分はHasStateAuthority、つまり本人確認で弾かれる）
            foreach (var player in aliveSnapshot)
            {
                player.ResetHand();
            }
        }
        
        /// <summary>
        /// 自分自身の InGamePlayer に対して、未選択分の確定を行わせる
        /// チョキボタンの入力を内部で行うタイプ
        /// ThinkingPhase が終わった時、Default = 未選択だとリタイア扱いにするために必要
        /// </summary>
        private void MarkRetirees()
        {
            foreach (var player in _players.CurrentValue)
            {
                if (player.Object.HasStateAuthority && player.HandIcon == IconType.Default) // 未だ未入力
                    player.SetHand(IconType.Retire);
            }
        }

        /// <summary>
        /// 「リタイア」になった人達のアイコンをRetireに切り替える
        /// Judging開始の直前（結果発表の直前）に呼び、先に敗北者だけ表示させたい
        /// 
        /// 「時間切れ」「切断」の場合、その本人が既に別のメソッドで内部値Retireを選ばせてる
        /// でないとここでチェックする時はまだ default なので変更しても同期が間に合わないから
        /// （チョキ選んだ人はその時点で内部値Retireにしてるから大丈夫）
        /// </summary>
        private void RevealRetirees()
        {
            foreach (var player in aliveSnapshot)
            {
                // Dictionaryで、playerをキーにして対応するviewを取り出す
                if (!_memberStates.TryGetValue(player, out var view)) continue;
                
                // aliveSnapshotの時は生きておりリストに情報があるけど
                // 現時点で切断（＝Despawn）していた場合、HandIconへのアクセス自体が例外になるため
                // Networked値には触れず、無条件でRetire表示にするだけにする
                if (!_players.CurrentValue.Contains(player))
                {
                    view.SetIcon(IconType.Retire);
                    continue;
                }

                if (player.HandIcon == IconType.Retire)
                    view.SetIcon(IconType.Retire); // リタイアUI表示！！
            }
        }

        /// <summary>
        /// 生存者について、実際に選んだ手（Rock/Paperのみ）を公開
        /// Judging開始時に呼ぶ
        /// </summary>
        private void RevealHandResults()
        {
            foreach (var player in aliveSnapshot)
            {
                if (!_memberStates.TryGetValue(player, out var view)) continue; // キーでプレイヤーを特定
                if (!_players.CurrentValue.Contains(player)) continue; // 生き残りではないならスキップ

                if (player.HandIcon != IconType.Retire)
                    view.SetIcon(player.HandIcon); // Rock / Paper 公開！！
            }
        }
        
        /// <summary>
        /// Judged開始時（手が公開された直後）に呼ぶ。
        /// パーの人数を数えて独り勝ち/共倒れを判定し、その後の生存者数から
        /// ラウンドの決着（続行/勝者確定/全滅）をOutcomeに確定させる。
        /// </summary>
        private void EvaluateJudgement()
        {
            // 現在の生存者を集める（リタイアを除く）
            var paperPlayers = new List<InGamePlayer>();
            var rockPlayers = new List<InGamePlayer>();
            foreach (var player in aliveSnapshot)
            {
                if (!_players.CurrentValue.Contains(player)) // リストにいるか事前チェック
                    continue;

                if (player.HandIcon == IconType.Paper) // パーをカウント
                    paperPlayers.Add(player);
                else if (player.HandIcon == IconType.Rock) // グーをカウント
                    rockPlayers.Add(player);
            }
            
            // 共倒れ対象を確定させる（独り勝ちの場合は空にする）
            _pendingEliminations = paperPlayers.Count > 1 ? paperPlayers : new List<InGamePlayer>();

            // 決着判定
            if (paperPlayers.Count == 1) // パー独り勝ち（パー1,グー1でも先にこっちでif分岐するので大丈夫）
            {
                Outcome = RoundOutcome.PaperWin;
                WinnerName = paperPlayers[0].PlayerName.ToString();
            }
            else if (rockPlayers.Count == 1) // グー生き残り勝ち（グー1 = 残りはみんな敗北予定のチョキ達）
            {
                Outcome = RoundOutcome.LastSurvivor;
                WinnerName = rockPlayers[0].PlayerName.ToString();
            }
            else if (rockPlayers.Count == 0) // 全滅（グー0 = みんなチョキ）
            {
                Outcome = RoundOutcome.AllEliminated;
            }
            else // 継続（rockPlayerCount >= 2 ともいえる）
            {
                Outcome = RoundOutcome.Continue;
            }
        }
        
        /// <summary>
        /// Eliminate開始時に呼ぶ。
        /// パーが2人以上いた場合の共倒れを、ここで初めて実行する。
        /// IsRetire化するため、みんなが結果表示をし終えた段階によぶこのメソッドが必要
        /// </summary>
        private void EvaluateEliminations()
        {
            foreach (var p in _pendingEliminations)
                p.SetHand(IconType.Retire);
        }
        
        /// <summary>
        /// Eliminateフェーズの待機条件。
        /// パー共倒れの対象者全員がRetire化したか確認する（全員 EvaluateEliminations() 呼ばれたか）
        /// 共倒れ対象がいない（Paperが1人以下）場合は常にtrue
        /// </summary>
        public bool AllPaperEliminationsFinalized()
        {
            foreach (var player in _pendingEliminations)
            {
                if (player.HandIcon != IconType.Retire)
                    return false;
            }

            return true;
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
                    return player.IsRetired;
            }

            return false; // 自分のInGamePlayerが見つからない場合は、念のためfalse扱い
        }
        
        /// <summary>
        /// aliveSnapshot 内の全員（切断者を除く）の HandIconがDefault 以外に確定しているかを返す。
        /// RoundController の待機条件として使う。みんながdefault以外になってから結果発表したいからね
        /// </summary>
        public bool AllHandsFinalized()
        {
            foreach (var player in aliveSnapshot)
            {
                if (!_players.CurrentValue.Contains(player)) continue; // 切断者は判定から除外
                if (player.HandIcon == IconType.Default) return false; // defaultが一人でも見つかばまだ通さない
            }

            return true;
        }
        
        /// <summary>
        /// 任意で終わらず、Result表示中の残り時間によってタイトルに戻らせる
        /// でもゲームはもう終了したので、誤差を気にせず各自処理させる
        /// </summary>
        private async UniTaskVoid CloseRoomCountdown()
        {
            // RoundControllerが既に破棄されている可能性も考慮しつつ待つ
            await UniTask.WaitUntil(() => RoundController.Instance == null || RoundController.Instance.PhaseTimer.Expired(JankenNetworkManager.Instance.Runner));

            // 一定時間経過後、強制終了
            await JankenNetworkManager.Instance.CloseRoomAsync();
        }
    }
}