using Fusion;
using Script.Network.Manager;

namespace Script.Network.Player
{
    /// <summary>
    /// マッチング成功時にSpawn()される
    /// プレイヤー情報を持つネットワーククラス
    /// </summary>
    public class TitlePlayer : NetworkBehaviour
    {
        [Networked] 
        public NetworkString<_16> PlayerName { get; set; }
        
        // IsReadyの値が変わったらこのメソッドを呼ぶ（Fusion限定機能）
        [Networked, OnChangedRender(nameof(OnIsReadyChanged))] 
        public NetworkBool IsReady { get; set; }
    
        public override void Spawned()
        {
            // 生成されたら情報をステータスを受け取りに行く
            if (Object.HasStateAuthority)
            {
                PlayerName = JankenNetworkManager.Instance.PlayerName; // 永続クラスから名前だけ借りる
            }
        
            // 全クライアントの参加者リストに登録する
            TitleMatchingManager.Instance.RegisterPlayer(this);
        }
    
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            TitleMatchingManager.Instance.UnregisterPlayer(this);
        }
        
        /// <summary>
        /// IsReadyの値が変わったら OnIsReadyChanged が呼ばれる
        /// </summary>
        private void OnIsReadyChanged()
        {
            TitleMatchingManager.Instance.RefreshPlayerList();
            TitleMatchingManager.Instance.CheckAllReadyAndStartGame();
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