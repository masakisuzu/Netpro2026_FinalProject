using Fusion;

namespace Script.Network
{
    public class InGamePlayer : NetworkBehaviour
    {
        [Networked]
        public NetworkString<_16> PlayerName { get; set; }
        
        [Networked, OnChangedRender(nameof(OnStateChanged))]
        public HandType Hand { get; set; }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                PlayerName = JankenNetworkManager.Instance.PlayerName;
            }

            InGameNetworkManager.Instance.RegisterPlayer(this);
        }
        
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            InGameNetworkManager.Instance.UnregisterPlayer(this);
        }
        
        private void OnStateChanged()
        {
            // InGameNetworkManager.Instance.RefreshPlayerList();
        }
        
        /// <summary>
        /// 自分自身の手を決定する（ボタンクラスから呼ばれる想定）
        /// </summary>
        public void SetHand(HandType hand)
        {
            if (Object.HasStateAuthority)
                Hand = hand;
        }
    }
}