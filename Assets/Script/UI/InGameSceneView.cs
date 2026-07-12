using R3;
using Script.Button;
using Script.Network.Manager;
using Script.Network.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI
{
    public class InGameSceneView : MonoBehaviour
    {
        [Header("準備中画面")]
        [SerializeField] private GameObject loadingPanel; // 初期化終わってUI更新する時 HiddenAllPanel() で消される
        
        [Header("ホストアイコン")]
        [SerializeField] private GameObject defaultIcon;
        [SerializeField] private GameObject rockIcon;
        
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
        [SerializeField] private GameObject hasWinnerPanel;
        [SerializeField] private GameObject noWinnerPanel;
        [SerializeField] private GameObject returnTimePanel;
        [SerializeField] private TextMeshProUGUI winnerNameText;
        [SerializeField] private TextMeshProUGUI returnTimeText;
        
        /// <summary>
        /// BootStrapクラスから呼ばれる
        /// </summary>
        public void Initialize()
        {
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
            
            // Phaseが変わったら呼んでほしい処理として登録
            InGameNetworkManager.Instance.OnPhaseChanged
                .Subscribe(OnPhaseChanged)
                .AddTo(this);
            
            // 購読してもBootStrapのフロー的に初期化処理できない
            // （RoundControllerの後に呼ばれる）ので初ターン時だけ自分で呼ぶ
            OnPhaseChanged(RoundPhase.Countdown);
        }
        
        private void Update()
        {
            // このクラスの存在が前提
            if (RoundController.Instance == null) return;
            
            switch (RoundController.Instance.CurrentPhase)
            {
                case RoundPhase.Countdown:
                    // 3,2,1,と順番にテキストが更新される
                    var remainingCountdown = RoundController.Instance.PhaseTimer.RemainingTime(JankenNetworkManager.Instance.Runner);
                    countText.text = remainingCountdown.HasValue ? Mathf.CeilToInt(remainingCountdown.Value).ToString() : "0"; // int変換 or 見つからなくて0
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
                
                case RoundPhase.Result:
                    // カウントダウンと同じ仕組み
                    var remainingResult = RoundController.Instance.PhaseTimer.RemainingTime(JankenNetworkManager.Instance.Runner);
                    int seconds = remainingResult.HasValue ? Mathf.CeilToInt((float)remainingResult.Value) : 0;
                    returnTimeText.text = $"終了まで… {seconds}";
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
                    SetHostIcon(false);
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
                    HiddenAllPanel();
                    SetHostIcon(true); // ポン
                    break;
                
                case RoundPhase.Result:
                    // 勝者アリ・ナシのどちらかに分岐表示
                    RoundOutcome outcome = InGameNetworkManager.Instance.Outcome;
                    if (outcome == RoundOutcome.AllEliminated)
                    {
                        ShowNoWinnerPanel();
                    }
                    else if (outcome == RoundOutcome.LastSurvivor || outcome == RoundOutcome.PaperWin)
                    {
                        ShowHasWinnerPanel();
                    }
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
            loadingPanel.SetActive(false);
            
            countDownPanel.SetActive(false);
            judgeCallPanel.SetActive(false);
            
            hasWinnerPanel.SetActive(false);
            noWinnerPanel.SetActive(false);
            returnTimePanel.SetActive(false);
            
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
        
        private void ShowHasWinnerPanel()
        {
            // 優勝者名を適応しておく
            winnerNameText.text = InGameNetworkManager.Instance.WinnerName;
            
            HiddenAllPanel();
            hasWinnerPanel.SetActive(true);
            returnTimePanel.SetActive(true);
        }
        
        private void ShowNoWinnerPanel()
        {
            HiddenAllPanel();
            noWinnerPanel.SetActive(true);
            returnTimePanel.SetActive(true);
        }
        
        private void ResetThinkingPanel()
        {
            Debug.Log($"Turn={RoundController.Instance.TurnNum}");
            Debug.Log($"Snapshot={InGameNetworkManager.Instance.RoundSnapshot.Count}");
            Debug.Log($"Expected={JankenNetworkManager.Instance.ExpectedPlayerCount}");
            
            // ThinkingPanel が非表示の間にリセット・更新しておく
            turnText.text = $"ターン {RoundController.Instance.TurnNum}";
            memberNumText.text = $"のこり {InGameNetworkManager.Instance.RoundSnapshot.Count} / {JankenNetworkManager.Instance.ExpectedPlayerCount}";
            timerGauge.fillAmount = 0f;
                    
            // 生き残りのみView初期化。アイコンを元に戻す
            foreach (var player in InGameNetworkManager.Instance.RoundSnapshot)
            { 
                if (InGameNetworkManager.Instance.MemberStates.TryGetValue(player, out var view))
                    view.SetIcon(IconType.Default);
            }
        }
        
        /// <summary>
        /// ホスト用のアイコン切り替え
        /// 「デフォルト」「グー」の2パターンしかない
        /// </summary>
        private void SetHostIcon(bool isRock)
        {
            if (isRock)
            {
                rockIcon.SetActive(true);
                defaultIcon.SetActive(false);
            }
            else
            {
                rockIcon.SetActive(false);
                defaultIcon.SetActive(true);
            }
        }
    }
}