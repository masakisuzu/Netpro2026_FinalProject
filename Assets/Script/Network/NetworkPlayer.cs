using Fusion;

namespace Script.Network
{
    /// <summary>
    /// マッチング成功時にSpawn()される
    /// プレイヤー情報を持つネットワーククラス
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        [Networked, OnChangedRender(nameof(OnPlayerNameChanged))]
        public NetworkString<_16> PlayerName { get; set; }

        [Networked]
        public NetworkBool IsReady { get; set; } // 準備OKかどうか
    
        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                PlayerName = JankenNetworkManager.Instance.PlayerName;
            }
        
            // 生成されたら全クライアントで参加者リストに登録する
            JankenNetworkManager.Instance.RegisterPlayer(this);
        }
    
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            JankenNetworkManager.Instance.UnregisterPlayer(this);
        }
    
        /// <summary>
        /// PlayerNameがネットワーク経由で同期された時に呼ばれる（自分以外の値が届いた時など）
        /// リスト自体の参照は変わらないので、表示側に更新を伝えるためにリスト再発火させる
        /// </summary>
        private void OnPlayerNameChanged()
        {
            JankenNetworkManager.Instance.RefreshPlayerList();
        }
    }
}