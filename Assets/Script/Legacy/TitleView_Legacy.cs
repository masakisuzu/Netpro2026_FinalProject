// using R3;
// using Script.UI.Button;
// using UnityEngine;
//
// namespace Script.Legacy
// {
//     public class TitleView_Legacy : MonoBehaviour
//     {
//         [Header("ボタンクラス（タイトル）")]
//         // [SerializeField] private ButtonBase hostJoinButton;
//         // [SerializeField] private ButtonBase clientJoinButton;
//         [SerializeField] private ButtonBase startButton;
//         
//         /*
//         [Header("ボタンクラス（ホスト）")]
//         [SerializeField] private ButtonBase backTitleFromHostButton;
//         [SerializeField] private ButtonBase createRoomButton;
//         [SerializeField] private ButtonBase leaveRoomByHostButton;
//         [SerializeField] private ButtonBase goToInGameButton;
//         // ID入力
//         */
//         
//         [Header("ボタンクラス")]
//         [SerializeField] private ButtonBase backTitleFromClientButton; // タイトルに戻るボタン
//         [SerializeField] private ButtonBase enterRoomButton; // マッチング開始ボタン
//         // [SerializeField] private ButtonBase leaveRoomByClientButton; // マッチングできたけどやっぱ抜けるボタン
//         // ID入力
//         // 名前入力
//         
//         [Header("UI表示クラス")]
//         [SerializeField] private GameObject titlePanel;
//         // [SerializeField] private GameObject hostPanel;
//         // [SerializeField] private GameObject hostMatchingPanel;
//         // [SerializeField] private GameObject clientPanel;
//         // [SerializeField] private GameObject clientMatchingPanel;
//         
//         [SerializeField] private GameObject guidePanel; // ルール説明する背景
//         [SerializeField] private GameObject checkInPanel; // ID,名前を入力する枠
//         [SerializeField] private GameObject matchedPanel; // 参加者一覧を表示する枠
//         
//         /// <summary>
//         /// ゲーム起動後はここから始まる
//         /// </summary>
//         void Start()
//         {
//             ShowTitlePanel();
//             
//             /*
//             // 「タイトル」に戻る
//             Observable.Merge(
//                     backTitleFromHostButton.OnClickAsObservable,
//                     backTitleFromClientButton.OnClickAsObservable)
//                 .Subscribe(_ => ShowTitlePanel())
//                 .AddTo(this);
//             
//             // 「ホスト用の画面」を表示
//             hostJoinButton.OnClickAsObservable
//                 .Subscribe(_ => ShowHostPanel())
//                 .AddTo(this);
//             
//             // 「参加者用の画面」を表示
//             clientJoinButton.OnClickAsObservable
//                 .Subscribe(_ => ShowClientPanel())
//                 .AddTo(this);
//     
//             createRoomButton.OnClickAsObservable
//                 .Subscribe(_ => CreateRoom())
//                 .AddTo(this);
//                 */
//             
//             startButton.OnClickAsObservable
//                 .Subscribe(_ => ShowHostPanel())
//                 .AddTo(this);
//         }
//         
//         /// <summary>
//         /// 各UIパネルを表示する前に一旦全て非表示にしたいので用意
//         /// </summary>
//         private void HiddenAllPanel()
//         {
//             titlePanel.SetActive(false);
//             hostPanel.SetActive(false);
//             clientPanel.SetActive(false);
//         }
//         
//         private void ShowTitlePanel()
//         {
//             HiddenAllPanel();
//             titlePanel.SetActive(true);
//         }
//
//         private void ShowHostPanel()
//         {
//             HiddenAllPanel();
//             hostPanel.SetActive(true);
//             
//         }
//         
//         private void ShowClientPanel()
//         {
//             HiddenAllPanel();
//             clientPanel.SetActive(true);
//         }
//         
//         private void EnterRoom()
//         {
//             Debug.Log("IDを元に検索！");
//             // 時間経過で返す、見つからなかったら…
//         }
//         
//         
//
//
//         private void CreateRoom()
//         {
//             Debug.Log("部屋を作る");
//         }
//         
//         
//         
//         private void LeaveRoom()
//         {
//             Debug.Log("部屋を抜ける");
//         }
//         
//         // TODO ホストがID設定する必要ないか…すぐにマッチングボードでよさそう
//     }
// }