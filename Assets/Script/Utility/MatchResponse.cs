namespace Script.Utility
{
    /// <summary>
    /// マッチング結果だけどどちらにするか定めるもの
    /// </summary>
    public enum MatchingResultType
    {
        Success, 
        Error
    }

    /// <summary>
    /// マッチング結果で返す情報の型
    /// </summary>
    public readonly struct MatchingResult
    {
        public readonly MatchingResultType Type;
        public readonly string Message;

        public MatchingResult(MatchingResultType type, string message)
        {
            Type = type;
            Message = message;
        }
    }
}
