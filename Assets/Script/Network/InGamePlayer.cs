using Fusion;

namespace Script.Network
{
    /// <summary>
    /// ネットワーク越しに同期される個別の実データ
    /// InGameSceneView も個別のデータを持つけど役割はUI表示
    /// </summary>
    public class InGamePlayer : NetworkBehaviour
    {
        [Networked]
        public NetworkString<_16> PlayerName { get; set; }
        
        [Networked, OnChangedRender(nameof(OnStateChanged))]
        public IconType HandIcon { get; set; }

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
            // 全員が選択したかチェックしに行く
            // InGameNetworkManager.Instance.RefreshPlayerList();
        }
        
        /// <summary>
        /// 自分自身の手を決定する（ボタンクラスから呼ばれる想定）
        /// 内部的な更新、UIはまだ更新しない（結果発表の時にする）
        /// </summary>
        public void SetHand(IconType type)
        {
            if (Object.HasStateAuthority)
                HandIcon = type;
        }
    }
}