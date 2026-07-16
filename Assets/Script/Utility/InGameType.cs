namespace Script.Utility
{
    /// <summary>
    /// じゃんけんの手やまだ選んでいない状態のDefaultなど、アイコンのパターン
    /// （チョキはRetire）
    /// </summary>
    public enum IconType
    {
        Default,
        Retire,
        Rock,
        Paper
    }
    
    public enum RoundPhase
    {
        Countdown,       // カウントダウン中（Thinkingに入る準備）
        Think,           // 考え中（ボタン表示・タイマー減少中）
        JudgeSync,       // 選択が終了し、ラグを抑える待ち時間
        JudgeCall,       // 結果発表前のじゃんけんコール
        Judged,          // 出した手の判定結果
        Eliminate,       // パー共倒れ等の「表示後の脱落処理」を確定する取り除きフェーズ
        Result,          // 勝者リザルト
    }
    
    public enum RoundOutcome
    {
        Continue,      // まだ決着していない。次ラウンドへ
        PaperWin,      // パーの独り勝ち
        LastSurvivor,  // 最後の1人が残った（グー耐えなど）
        AllEliminated  // 全員が脱落した（誰も勝者なし）
    }
}