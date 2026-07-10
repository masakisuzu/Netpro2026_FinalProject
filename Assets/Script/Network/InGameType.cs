namespace Script.Network
{
    public enum RoundPhase
    {
        Countdown,       // カウントダウン中（Thinkingに入る準備）
        Thinking,        // 考え中（ボタン表示・タイマー減少中）
        Judging,         // 出した手の判定結果
        Result,          // 勝者リザルト
    }
    
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
}