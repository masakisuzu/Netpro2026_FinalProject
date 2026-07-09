using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using R3;
using Script.Button;
using Script.Network;
using TMPro;

namespace Script.UI
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
        
        [Header("Text表示クラス")]
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private TextMeshProUGUI matchingIDText;
        [SerializeField] private TextMeshProUGUI matchingNameText;
        
        [Header("メンバー一覧")]
        [SerializeField] private Transform memberListContainer; // Vertical Layout Groupを付けた親。memberListItemPrefab一覧を持つ
        [SerializeField] private MemberListItemView memberListItemPrefab;
        [SerializeField] private TextMeshProUGUI memberNumText;
        [SerializeField] private TextMeshProUGUI roomIdText;
        
        // どのNetworkPlayerがどのUI行に対応しているかを覚えておく
        private readonly Dictionary<NetworkPlayer, MemberListItemView> _memberItems = new();
        
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
            
            readyButton.OnClickAsObservable
                .Subscribe(_ => JankenNetworkManager.Instance.SetLocalPlayerReady())
                .AddTo(this);
            
            // ネットワーク側からの結果通知を購読
            JankenNetworkManager.Instance.OnMatchingResult
                .Subscribe(OnMatchingResult)
                .AddTo(this);
            
            JankenNetworkManager.Instance.Players
                .Subscribe(UpdateMatchedList)
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
        
        private void ShowMatchingPanel(string id, string name)
        {
            HiddenAllPanel();
            guidePanel.SetActive(true); // マッチング中もガイド表示
            matchingPanel.SetActive(true);
            matchingIDText.text = $"ID: {id}"; // 入力したテキスト情報更新
            matchingNameText.text = $"NAME: {name}";
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
            ShowMatchingPanel(roomId, playerName);
            
            // ネットワークオブジェクトを呼んでネット接続開始！表示に関する結果はR3で発火させて受け取る
            JankenNetworkManager.Instance.EnterRoomAsync(roomId, playerName).Forget();
        }
        
        /// <summary>
        /// JankenNetworkManager の Register / UnRegister / Refresh によるR3発火で処理を呼ぶ
        /// 差分だけを更新して大人数に対応
        /// </summary>
        private void UpdateMatchedList(List<NetworkPlayer> players)
        {
            // 人数表示
            memberNumText.text = $"{players.Count} / {JankenNetworkManager.MaxCCULimit}";
            
            // Id表示（毎回呼ぶ必要ないけどまあいいか）
            roomIdText.text = $"ID: {JankenNetworkManager.Instance.RoomId}";
            
            // 退出した人のUI行だけを消す（今回のplayersに含まれていないキーを探す）
            var removedPlayers = new List<NetworkPlayer>();
            foreach (var existingPlayer in _memberItems.Keys)
            {
                if (!players.Contains(existingPlayer))
                {
                    removedPlayers.Add(existingPlayer);
                }
            }
            foreach (var removedPlayer in removedPlayers)
            {
                Destroy(_memberItems[removedPlayer].gameObject);
                _memberItems.Remove(removedPlayer);
            }

            // 全員分ループし、新規は作成、既存は表示内容だけ更新
            foreach (var player in players)
            {
                if (_memberItems.TryGetValue(player, out var item))
                {
                    // 既に行がある→中身だけ更新（Instantiateしない）
                    item.SetData(player.PlayerName.ToString(), player.IsReady);
                }
                else
                {
                    // 新規参加者→行を1つだけ新しく作る
                    var newItem = Instantiate(memberListItemPrefab, memberListContainer);
                    newItem.SetData(player.PlayerName.ToString(), player.IsReady);
                    _memberItems.Add(player, newItem);
                }
            }
        }
        
        /// <summary>
        /// マッチングの結果通知（EnterRoomAsync完了後に呼ばれる想定）
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