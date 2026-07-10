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

        [Header("カウントダウンのカウント数")]
        [SerializeField] private float countdownSeconds = 3f;
        public float CountdownSeconds => countdownSeconds;
        
        [Header("Thinkingの制限時間")]
        [SerializeField] private float thinkingSeconds = 10f;
        public float ThinkingSeconds => thinkingSeconds;

        // ネット上で時間を管理するには、FusionのTickTimerで同期する必要がある
        [Networked] public TickTimer PhaseTimer { get; set; }
        [Networked] public int RoundNumber { get; set; }

        [Networked, OnChangedRender(nameof(OnPhaseChanged))]
        public RoundPhase Phase { get; set; }

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
                RoundNumber++;

                Phase = RoundPhase.Countdown;
                PhaseTimer = TickTimer.CreateFromSeconds(Runner, countdownSeconds); // countdownSeconds秒後に期限切れになるタイマーを作成
                await UniTask.WaitUntil(() => PhaseTimer.Expired(Runner)); // 期限切れになるまでawait

                Phase = RoundPhase.Thinking;
                PhaseTimer = TickTimer.CreateFromSeconds(Runner, thinkingSeconds);
                await UniTask.WaitUntil(() => PhaseTimer.Expired(Runner));

                // Phase = RoundPhase.Judging;
                // await UniTask.Delay(2000); // 判定演出用の仮の待機（実際の勝敗判定は別途実装）
                //
                // Phase = RoundPhase.Result;
                // await UniTask.Delay(2000); // 結果表示用の仮の待機
            }
        }
        
        /// <summary>
        /// Phaseが変わったらここからManagerに伝える
        /// </summary>
        private void OnPhaseChanged()
        {
            InGameNetworkManager.Instance.NotifyPhaseChanged(Phase);
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