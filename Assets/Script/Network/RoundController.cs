using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace Script.Network
{
    /// <summary>
    /// 「カウントダウン」と「残り時間タイマー」を全員で同期するためのオブジェクト
    /// 代表者（マスタークライアント）だけが生成・書き込みを行う
    /// </summary>
    public class RoundController : NetworkBehaviour
    {
        public static RoundController Instance { get; private set; }
        
        // 現在のフェーズはここで一元管理
        [Networked, OnChangedRender(nameof(OnPhaseRenderChanged))] 
        public RoundPhase CurrentPhase { get; set; }
        
        // ネット上で時間を管理するには、FusionのTickTimerで同期する必要がある
        [Networked] public TickTimer PhaseTimer { get; set; }
        [Networked] public int TurnNum { get; set; }

        // カウントダウンのカウント数(s)
        private const float countdownSeconds = 5f;
        public float CountdownSeconds => countdownSeconds;
        
        // シンキングタイムの制限時間(s)
        private const float thinkingSeconds = 20f;
        public float ThinkingSeconds => thinkingSeconds;
        
        // 「じゃーん・けーん・ポン」演出専用の秒数
        private const float judgingSeconds = 3f;
        public float JudgingSeconds => judgingSeconds;

        // 結果を見せている秒数（次のラウンドへ移るまでの間）
        private const float judgedSeconds = 5f;
        public float JudgedSeconds => judgedSeconds;

        public override void Spawned()
        {
            Instance = this;

            if (Object.HasStateAuthority)
                RunGameLoop().Forget();
        }
        
        /// <summary>
        /// 代表者だけが実行するラウンド進行本体。
        /// TickTimerで実時間を管理しつつ、await で「次に進むタイミング」を待つ。
        /// </summary>
        private async UniTaskVoid RunGameLoop()
        {
            while (true) // 決着判定は今後Judging内に実装予定。ひとまずループの骨組みのみ
            {
                TurnNum++;

                // カウントダウン！3,2,1,,,
                CurrentPhase = RoundPhase.Countdown; // Phaseチェンジの命令
                PhaseTimer = TickTimer.CreateFromSeconds(Runner, countdownSeconds); // countdownSeconds秒後に期限切れになるタイマーを作成
                await UniTask.WaitUntil(() => PhaseTimer.Expired(Runner)); // 期限切れになるまでawait
                
                // シンキングタイム！ぐー、ちー、ぱーの選択
                CurrentPhase = RoundPhase.Think;
                PhaseTimer = TickTimer.CreateFromSeconds(Runner, thinkingSeconds);
                await UniTask.WaitUntil(() => PhaseTimer.Expired(Runner));
                
                // 結果発表準備！じゃーん、けーん、、、
                CurrentPhase = RoundPhase.JudgeCall;
                PhaseTimer = TickTimer.CreateFromSeconds(Runner, judgingSeconds);
                await UniTask.WaitUntil(() => PhaseTimer.Expired(Runner));

                // 結果発表！ポン！
                CurrentPhase = RoundPhase.Judged;
                PhaseTimer = TickTimer.CreateFromSeconds(Runner, judgedSeconds);
                await UniTask.WaitUntil(() => PhaseTimer.Expired(Runner));
            }
        }
        
        /// <summary>
        /// Phaseの変化と同時に、全クライアントへ（ローカルロジックの）InGameNetworkManagerを処理させる
        /// プロパティ OnChangedRender(nameof()) で定義したメソッドでないと、全員に届かないらしい…
        /// </summary>
        private void OnPhaseRenderChanged()
        {
            InGameNetworkManager.Instance.NotifyPhaseChanged(CurrentPhase);
        }
        
        /// <summary>
        /// Thinking中、全員が選び終えた時に代表者だけが呼ぶ
        /// 残り時間を待たずに即座にタイマーを終了させる
        /// </summary>
        public void ForceExpireTimer()
        {
            if (!Object.HasStateAuthority) return;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, 0f);
        }
        
        /// <summary>
        /// 現在のフェーズの経過割合(0〜1)を返す。ゲージ表示用。
        /// </summary>
        public float GetPhaseProgress(float totalSeconds)
        {
            var remaining = PhaseTimer.RemainingTime(Runner);
            if (!remaining.HasValue) return 1f;
            return 1f - Mathf.Clamp01((float)(remaining.Value / totalSeconds));
        }
    }
}