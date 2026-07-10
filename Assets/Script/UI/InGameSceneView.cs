using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using Script.Network;

namespace Script.UI
{
    public class InGameSceneView : MonoBehaviour
    {
        [Header("メンバー一覧")]
        [SerializeField] private Transform memberListContainer;
        [SerializeField] private InGameMemberStateView memberStatePrefab;
        
        [Header("遷移時のカウントダウン表示")]
        [SerializeField] private GameObject countdownPanel;
        [SerializeField] private TextMeshProUGUI countdownText;
        
        [Header("ボタン表示クラス")]

        [Header("UI表示クラス")]
        [SerializeField] private GameObject rockButton;
        [SerializeField] private GameObject scissorsButton;
        [SerializeField] private GameObject paperButton;
        
        [Header("テキスト表示クラス")]
        [SerializeField] private TextMeshProUGUI roundTimerText;

        private readonly Dictionary<InGamePlayer, InGameMemberStateView> _memberStates = new();
        private RoundPhase _currentPhase = RoundPhase.WaitingForReady;

        private void Start()
        {
            InGameNetworkManager.Instance.Players
                .Subscribe(UpdateMemberList)
                .AddTo(this);

            InGameNetworkManager.Instance.OnPhaseChanged
                .Subscribe(OnPhaseChanged)
                .AddTo(this);

            // ぐーちょきぱーボタンの入力受付はまた別途実装（今回はUI表示切り替えまで）
            
            
            countdownPanel.SetActive(true);
        }
        
        /// <summary>
        /// UI配置を含めたメンバーたちの初期化
        /// </summary>
        private void UpdateMemberList(List<InGamePlayer> players)
        {
            var removed = new List<InGamePlayer>();
            foreach (var existing in _memberStates.Keys)
            {
                if (!players.Contains(existing)) removed.Add(existing);
            }
            foreach (var r in removed)
            {
                Destroy(_memberStates[r].gameObject);
                _memberStates.Remove(r);
            }

            foreach (var player in players)
            {
                if (_memberStates.TryGetValue(player, out var item))
                {
                    item.SetData(player.PlayerName.ToString());
                }
                else
                {
                    var newItem = Instantiate(memberStatePrefab, memberListContainer);
                    newItem.SetData(player.PlayerName.ToString());
                    _memberStates.Add(player, newItem);
                }
            }
        }

        private void Update()
        {
            // RoundControllerはまだSpawnされていない可能性があるのでチェック
            if (RoundController.Instance == null) return;

            var runner = JankenNetworkManager.Instance.Runner;

            if (_currentPhase == RoundPhase.WaitingForReady)
            {
                var remaining = RoundController.Instance.ReadyCountdown.RemainingTime(runner);
                countdownText.text = remaining.HasValue ? Mathf.CeilToInt(remaining.Value).ToString() : "0";
            }
            else if (_currentPhase == RoundPhase.Thinking)
            {
                var remaining = RoundController.Instance.RoundTimer.RemainingTime(runner);
                roundTimerText.text = remaining.HasValue ? Mathf.CeilToInt(remaining.Value).ToString() : "0";
            }
        }

        /// <summary>
        /// Phaseを変えて分岐処理
        /// </summary>
        private void OnPhaseChanged(RoundPhase phase)
        {
            _currentPhase = phase;

            if (phase == RoundPhase.Thinking)
            {
                countdownPanel.SetActive(false);
                rockButton.SetActive(true);
                scissorsButton.SetActive(true);
                paperButton.SetActive(true);
            }
            else if (phase == RoundPhase.Judging)
            {
                
            }
            else if (phase == RoundPhase.Result)
            {
                
            }
        }
    }
}