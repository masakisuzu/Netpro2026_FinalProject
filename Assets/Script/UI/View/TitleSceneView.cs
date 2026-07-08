using Cysharp.Threading.Tasks;
using UnityEngine;
using R3;
using Script.Network;
using Script.UI.Button;
using TMPro;

namespace Script.UI.View
{
    public class TitleSceneView : MonoBehaviour
    {
        [Header("ボタンクラス")]
        [SerializeField] private ButtonBase startButton; // タイトルにある参加ボタン
        [SerializeField] private ButtonBase backTitleButton; // タイトルに戻るボタン
        [SerializeField] private ButtonBase enterRoomButton; // マッチング開始ボタン
        [SerializeField] private ButtonBase readyButton; // マッチング後の開始準備ボタン
        
        [Header("入力クラス")]
        [SerializeField] private TMP_InputField roomIdInput;
        [SerializeField] private TMP_InputField nameInput;
        
        [Header("UI表示クラス")]
        [SerializeField] private GameObject titlePanel; // 起動後初めに見える背景
        [SerializeField] private GameObject guidePanel; // ルール説明する背景
        [SerializeField] private GameObject checkInPanel; // ID,名前を入力する枠
        [SerializeField] private GameObject matchingPanel; // マッチング中…
        [SerializeField] private GameObject matchedPanel; // 参加者一覧を表示する枠
        [SerializeField] private TextMeshProUGUI errorText;
        
        
        /// <summary>
        /// ゲーム起動後はここから始まる
        /// </summary>
        void Start()
        {
            ShowTitlePanel();
            
            backTitleButton.OnClickAsObservable
                .Subscribe(_ => ShowTitlePanel())
                .AddTo(this);
            
            startButton.OnClickAsObservable
                .Subscribe(_ => ShowJoinPanel())
                .AddTo(this);
            
            enterRoomButton.OnClickAsObservable
                .Subscribe(_ => TryEnterRoom().Forget())
                .AddTo(this);
            
            /*
            readyButton.OnClickAsObservable
                .Subscribe(_ => TryEnterRoom())
                .AddTo(this);
                */
            
            // ネットワーク側からの結果通知を購読
            JankenNetworkManager.Instance.OnMatchingResult
                .Subscribe(OnMatchingResult)
                .AddTo(this);
        }
        
        /// <summary>
        /// 各UIパネルを表示する前に一旦全て非表示にしたいので用意
        /// </summary>
        private void HiddenAllPanel()
        {
            titlePanel.SetActive(false);
            guidePanel.SetActive(false);
            checkInPanel.SetActive(false);
            matchedPanel.SetActive(false);
            matchingPanel.SetActive(false);
        }
        
        /// <summary>
        /// タイトルUI表示
        /// </summary>
        private void ShowTitlePanel()
        {
            HiddenAllPanel();
            titlePanel.SetActive(true);
        }

        /// <summary>
        /// 参加準備の「ガイド」「入力欄」表示
        /// </summary>
        private void ShowJoinPanel()
        {
            HiddenAllPanel();
            guidePanel.SetActive(true);
            checkInPanel.SetActive(true);
        }
        
        private void ShowMatchingPanel()
        {
            HiddenAllPanel();
            guidePanel.SetActive(true); // マッチング中もガイド表示
            matchingPanel.SetActive(true);
        }
        
        private void ShowMatchedPanel()
        {
            HiddenAllPanel();
            guidePanel.SetActive(true); // マッチング成功後もガイド表示
            matchedPanel.SetActive(true);
        }
        
        /// <summary>
        /// マッチング開始処理
        /// </summary>
        private async UniTaskVoid TryEnterRoom()
        {
            // 空欄対策
            string roomId = roomIdInput.text.Trim();
            string playerName = nameInput.text.Trim();
            
            if (string.IsNullOrEmpty(roomId))
            {
                Debug.LogWarning("IDが未入力です");
                return;
            }
            
            if (string.IsNullOrEmpty(playerName))
            {
                Debug.LogWarning("NAMEが未入力です");
                return;
            }
            
            // マッチング中と表示
            ShowMatchingPanel();
            
            // ネットワークオブジェクトを呼んでネット接続開始！表示に関する結果はR3で発火させて受け取る
            JankenNetworkManager.Instance.EnterRoomAsync(roomId, playerName).Forget();
        }
        
        /// <summary>
        /// ネットワークからの結果通知（EnterRoomAsync完了後に呼ばれる想定）
        /// </summary>
        private void OnMatchingResult(MatchingResult result)
        {
            if (result.Type == MatchingResultType.Success)
            {
                ShowMatchedPanel();
            }
            else
            {
                // エラーテキストを表示
                errorText.text = result.Message;
                Debug.LogWarning($"マッチング失敗: {result.Message}");
            }
        }
    }
}