using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using Script.Network;
using UnityEngine.UI;

namespace Script.UI
{
    public class InGameSceneView : MonoBehaviour
    {
        [Header("Thinkingフェーズ")]
        [SerializeField] private GameObject thinkingPanel;
        [SerializeField] private Image timerGauge;
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI memberNumText;
        
        [Header("CountDownフェーズ")]
        [SerializeField] private GameObject countDownPanel;
        [SerializeField] private TextMeshProUGUI countText;
        
        private void Start()
        {
            // Phaseが変わったら呼んでほしい処理として登録
            InGameNetworkManager.Instance.OnPhaseChanged
                .Subscribe(OnPhaseChanged)
                .AddTo(this);
        }
        
        private void Update()
        {
            // このクラスの存在が前提
            if (RoundController.Instance == null) return;
            
            switch (InGameNetworkManager.Instance.CurrentPhase)
            {
                case RoundPhase.Countdown:
                    // TODO
                    var remaining = RoundController.Instance.PhaseTimer.RemainingTime(JankenNetworkManager.Instance.Runner);
                    countText.text = remaining.HasValue ? Mathf.CeilToInt(remaining.Value).ToString() : "0"; // int変換 or 見つからなくて0
                    break;
                
                case RoundPhase.Thinking:
                    // Roundクラスの内部値を逐一UIゲージに反映していく（0→1、1で時間切れ）
                    timerGauge.fillAmount = RoundController.Instance.GetPhaseProgress(RoundController.Instance.ThinkingSeconds);
                    break;
            }
        }

        /// <summary>
        /// Phaseに応じた分岐処理、購読しているのでInGameNetworkManagerから都度呼ばれる
        /// </summary>
        private void OnPhaseChanged(RoundPhase phase)
        {
            switch (phase)
            {
                // ThinkingPanelがまだ非表示の間にテキストをセットしておく
                case RoundPhase.Countdown:
                    turnText.text = $"{RoundController.Instance.RoundNumber}ターン";
                    memberNumText.text = $"残り {InGameNetworkManager.Instance.RoundSnapshot.Count} / {InGameNetworkManager.Instance.InitialMemberCount} 人";
                    timerGauge.fillAmount = 0f;
                    // TODO 生存者のHandIconを元に戻す
                    ShowCountDownPanel();
                    break;

                case RoundPhase.Thinking:
                    ShowThinkingPanel();
                    break;
            }
        }

        private void HiddenAllPanel()
        {
            thinkingPanel.SetActive(false);
            countDownPanel.SetActive(false);
        }
        
        private void ShowThinkingPanel()
        {
            HiddenAllPanel();
            thinkingPanel.SetActive(true);
        }
        
        private void ShowCountDownPanel()
        {
            HiddenAllPanel();
            countDownPanel.SetActive(true);
        }
    }
}