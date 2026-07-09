using Fusion;
using UnityEngine;

namespace Script.Network
{
    /// <summary>
    /// カウントダウンとラウンドタイマーを全員で同期するためのオブジェクト。
    /// 代表者（マスタークライアント）だけが生成・書き込みを行う。
    /// </summary>
    public class RoundController : NetworkBehaviour
    {
        public static RoundController Instance { get; private set; }

        [SerializeField] private float readyCountdownSeconds = 3f;
        [SerializeField] private float roundTimeSeconds = 10f;

        // ネット上で時間を管理するには、FusionのTickTimerで同期する必要がある
        [Networked] public TickTimer ReadyCountdown { get; set; }
        [Networked] public TickTimer RoundTimer { get; set; }

        [Networked, OnChangedRender(nameof(OnPhaseChanged))]
        public RoundPhase Phase { get; set; }

        public override void Spawned()
        {
            Instance = this;

            if (Object.HasStateAuthority)
            {
                Phase = RoundPhase.WaitingForReady;
                ReadyCountdown = TickTimer.CreateFromSeconds(Runner, readyCountdownSeconds);
            }
        }

        /// <summary>
        /// StateAuthorityを持つクライアントの中だけで、毎Tick呼ばれる
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            if (Phase == RoundPhase.WaitingForReady && ReadyCountdown.Expired(Runner))
            {
                Phase = RoundPhase.Thinking;
                RoundTimer = TickTimer.CreateFromSeconds(Runner, roundTimeSeconds);
            }
        }

        private void OnPhaseChanged()
        {
            InGameNetworkManager.Instance.NotifyPhaseChanged(Phase);
        }
    }
}