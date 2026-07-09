namespace Script.Network
{
    public enum RoundPhase
    {
        WaitingForReady, // カウントダウン中
        Thinking,        // 考え中（ボタン表示・タイマー減少中）
        Judging,         // 出した手の判定結果
        Result,          // 勝者リザルト
    }
    
    /// <summary>
    /// じゃんけんの手。まだ選んでいない状態もNoneとして表現する
    /// </summary>
    public enum HandType
    {
        None,
        Rock,
        Scissors,
        Paper
    }
}