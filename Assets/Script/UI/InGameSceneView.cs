using System.Collections.Generic;
using R3;
using Script.Button;
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
        [Space(20)]
        [SerializeField] private ButtonBase rockButton;
        [SerializeField] private ButtonBase scissorsButton;
        [SerializeField] private ButtonBase paperButton;
        
        [Header("CountDownフェーズ")]
        [SerializeField] private GameObject countDownPanel;
        [SerializeField] private TextMeshProUGUI countText;
        [Space(20)]
        [SerializeField] private GameObject topBarPanel;
        [SerializeField] private GameObject waitingBoard;
        
        [Header("JudgeCallフェーズ")]
        [SerializeField] private GameObject judgeCallPanel;
        [SerializeField] private TextMeshProUGUI judgeCallText;
        
        [Header("Resultフェーズ")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultText;
        
        private void Start()
        {
            // Phaseが変わったら呼んでほしい処理として登録
            InGameNetworkManager.Instance.OnPhaseChanged
                .Subscribe(OnPhaseChanged)
                .AddTo(this);
            
            // グー、チー、パー
            rockButton.OnClickAsObservable
                .Subscribe(_ => OnHandSelected(IconType.Rock))
                .AddTo(this);

            scissorsButton.OnClickAsObservable
                .Subscribe(_ => OnHandSelected(IconType.Retire))
                .AddTo(this);

            paperButton.OnClickAsObservable
                .Subscribe(_ => OnHandSelected(IconType.Paper))
                .AddTo(this);
        }
        
        private void Update()
        {
            // このクラスの存在が前提
            if (RoundController.Instance == null) return;
            
            switch (RoundController.Instance.CurrentPhase)
            {
                case RoundPhase.Countdown:
                    // 3,2,1,と順番にテキストが更新される
                    var remaining = RoundController.Instance.PhaseTimer.RemainingTime(JankenNetworkManager.Instance.Runner);
                    countText.text = remaining.HasValue ? Mathf.CeilToInt(remaining.Value).ToString() : "0"; // int変換 or 見つからなくて0
                    break;
                
                case RoundPhase.Think:
                    // Roundクラスの内部値を逐一UIゲージに反映していく（0→1、1で時間切れ）
                    timerGauge.fillAmount = RoundController.Instance.GetPhaseProgress(RoundController.Instance.ThinkingSeconds);
                    break;
                
                case RoundPhase.JudgeCall:
                    // 「じゃーん」「けーん」を経過割合で切り替える簡易演出
                    var progress = RoundController.Instance.GetPhaseProgress(RoundController.Instance.JudgingSeconds);
                    judgeCallText.text = progress < 0.5f ? "じゃーん" : "けーん";
                    break;
            }
        }

        /// <summary>
        /// Phaseに応じた分岐処理
        /// 購読しているので InGameNetworkManager による発火で都度呼ばれる
        /// </summary>
        private void OnPhaseChanged(RoundPhase phase)
        {
            switch (phase)
            {
                case RoundPhase.Countdown:
                    ShowCountDownPanel();
                    ResetThinkingPanel();
                    break;

                case RoundPhase.Think:
                    if (InGameNetworkManager.Instance.IsLocalPlayerRetired())
                    {
                        // 敗北者は選択済みと同じ表示にする（選択パネルは無く、詳細パネルと待ち合図を表示）
                        HiddenAllPanel();
                        topBarPanel.SetActive(true);
                        waitingBoard.SetActive(true);
                    }
                    else
                    {
                        ShowThinkingPanel();
                    }
                    break;
                
                case RoundPhase.JudgeCall:
                    ShowJudgeCallPanel(); // じゃーん、けーん
                    break;
                
                case RoundPhase.Judged:
                    HiddenAllPanel(); // ポン
                    break;
                
                case RoundPhase.Result:
                    ShowResultPanel();
                    break;
            }
        }
        
        /// <summary>
        /// 自分の手をセットして自分の画面だけ ThinkingPanel を閉じる
        /// </summary>
        private void OnHandSelected(IconType type)
        {
            InGameNetworkManager.Instance.SetLocalPlayerHand(type);
            thinkingPanel.SetActive(false); // 選択パネルは非表示にして、詳細パネル(topBar)はそのまま
            waitingBoard.SetActive(true); // そして待ち時間のUIを表示させる
        }

        private void HiddenAllPanel()
        {
            countDownPanel.SetActive(false);
            judgeCallPanel.SetActive(false);
            resultPanel.SetActive(false);
            
            thinkingPanel.SetActive(false);
            topBarPanel.SetActive(false);
            waitingBoard.SetActive(false);
        }
        
        private void ShowCountDownPanel()
        {
            HiddenAllPanel();
            countDownPanel.SetActive(true);
        }
        
        private void ShowThinkingPanel()
        {
            HiddenAllPanel();
            thinkingPanel.SetActive(true);
            topBarPanel.SetActive(true);
        }
        
        private void ShowJudgeCallPanel()
        {
            HiddenAllPanel();
            judgeCallPanel.SetActive(true);
        }
        
        private void ShowResultPanel()
        {
            HiddenAllPanel();
            resultPanel.SetActive(true);
        }
        
        private void ResetThinkingPanel()
        {
            // ThinkingPanel が非表示の間にリセット・更新しておく
            turnText.text = $"ターン {RoundController.Instance.TurnNum}";
            memberNumText.text = $"のこり {InGameNetworkManager.Instance.RoundSnapshot.Count} / {InGameNetworkManager.Instance.InitialMemberCount}";
            timerGauge.fillAmount = 0f;
                    
            // 生き残りのみView初期化。アイコンを元に戻す
            foreach (var player in InGameNetworkManager.Instance.RoundSnapshot)
            { 
                if (InGameNetworkManager.Instance.MemberStates.TryGetValue(player, out var item))
                    item.SetIcon(IconType.Default);
            }
        }
    }
}