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
        public NetworkString<_8> PlayerName { get; set; }
        
        [Networked, OnChangedRender(nameof(OnHandIconChanged))]
        public IconType HandIcon { get; set; } // 選択したぐーちーぱーのどれかを持つ（他のIconTypeも持てるけど役割が違う）

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
        
        /// <summary>
        /// 自分自身の手を決定する（Viewのボタンクラス → Managerクラス から呼ばれる）
        /// 内部的な更新、UIはまだ更新しない（結果発表の時にする）
        /// </summary>
        public void SetHand(IconType type)
        {
            if (Object.HasStateAuthority)
                HandIcon = type;
        }
        
        /// <summary>
        /// 新しいラウンド開始時、自分自身の手をDefaultに戻す
        /// Networked値なので StateAuthority を持つ本人にしかできない
        /// 敗北してたら戻せない
        /// </summary>
        public void ResetHand()
        {
            if (Object.HasStateAuthority && HandIcon != IconType.Retire)
                HandIcon = IconType.Default;
        }
        
        /// <summary>
        /// HandIconが変化したら Manager に報告する
        /// 「全員選び終えたか」 Manager は知りたがってるから
        /// </summary>
        private void OnHandIconChanged()
        {
            InGameNetworkManager.Instance.NotifyHandChanged();
        }
    }
}