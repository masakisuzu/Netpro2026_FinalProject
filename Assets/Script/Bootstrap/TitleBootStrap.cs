using Script.Network.Manager;
using Script.UI;
using UnityEngine;

namespace Script.Bootstrap
{
    /// <summary>
    /// Titleシーンの処理はここから始まる
    /// </summary>
    public class TitleBootstrap : MonoBehaviour
    {
        [SerializeField] private JankenNetworkManager jankenNetworkManager;
        [SerializeField] private TitleMatchingManager titleMatchingManager;
        [SerializeField] private TitleSceneView titleSceneView;
    
        private void Start()
        {
            // 順序は関係ない、Instanceの処理
            jankenNetworkManager.Initialize();
            titleMatchingManager.Initialize();
        
            // 上記のInstance処理が出来たとこで呼ぶ
            titleSceneView.Initialize();
        }
    }
}