using Fusion;

namespace Script.Network
{
    /// <summary>
    /// マッチング成功時にSpawn()される
    /// プレイヤー情報を持つネットワーククラス
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        [Networked] 
        public NetworkString<_16> PlayerName { get; set; }
        
        [Networked, OnChangedRender(nameof(OnIsReadyChanged))] // IsReadyの値が変わったら OnIsReadyChanged が呼ばれる
        public NetworkBool IsReady { get; set; }
    
        public override void Spawned()
        {
            // 生成されたら情報をステータスを受け取りに行く
            if (Object.HasStateAuthority)
            {
                PlayerName = JankenNetworkManager.Instance.PlayerName;
            }
        
            // 全クライアントの参加者リストに登録する
            JankenNetworkManager.Instance.RegisterPlayer(this);
        }
    
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            JankenNetworkManager.Instance.UnregisterPlayer(this);
        }
        
        private void OnIsReadyChanged()
        {
            JankenNetworkManager.Instance.RefreshPlayerList();
        }
        
        /// <summary>
        /// 自分自身のReady状態をtrueにする
        /// StateAuthorityを持つ本人しか呼べない！
        /// </summary>
        public void SetReady()
        {
            if (Object.HasStateAuthority) 
                IsReady = true;
        }
    }
}