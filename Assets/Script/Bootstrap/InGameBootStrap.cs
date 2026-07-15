using Cysharp.Threading.Tasks;
using Script.Network.Manager;
using Script.UI;
using UnityEngine;

namespace Script.Bootstrap
{
    /// <summary>
    /// InGameシーンの処理はここから始まる
    /// </summary>
    public class InGameBootstrap : MonoBehaviour
    {
        [SerializeField] private InGameSceneView inGameSceneView;
        [SerializeField] private InGameNetworkManager inGameNetworkManager;
        
        /// <summary>
        /// InGameシーンの初期化フロー
        /// </summary>
        private async void Start()
        {
            // Manager初期化（参加者スポーン&登録）
            await inGameNetworkManager.InitializePlayer();

            // ラウンド管理開始（起動し、Phase更新によるカウントダウン）
            await inGameNetworkManager.InitializeRoundController();
            
            // （代表者が）RoundクラスをSpawnしきったのを（参加者みんな）確認できたら…
            await UniTask.WaitUntil(() => RoundController.Instance != null);
            
            // UI初期化は最後！！（Manager,Roundが準備できてること前提）
            // ここでUI初期化すると同時に MarkLocalPlayerUiReady() を呼び
            // 代表者側で全員分揃ったことが確認できたタイミングでRoundクラスの RunGameLoop が開始される
            inGameSceneView.Initialize();
        }
    }
}