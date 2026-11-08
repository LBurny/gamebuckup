namespace GameBackup.Core.Models
{
    /// <summary>
    /// How trustworthy the current save-location knowledge is for a game.
    /// </summary>
    public enum SaveStatus
    {
        Unknown = 0,
        Candidate = 1,
        Confirmed = 2,
        Stale = 3,
        Error = 4,
    }

    public enum CandidateKind
    {
        Directory = 0,
        File = 1,
    }

    /// <summary>
    /// Which evidence produced a candidate. Higher-trust sources are listed first.
    /// </summary>
    public enum CandidateSource
    {
        UserRule = 0,
        Manifest = 1,
        Steam = 2,
        Ubisoft = 3,
        Gog = 4,
        Epic = 5,
        CommonRoot = 6,
        InstallDir = 7,
        SessionTrace = 8,
        Llm = 9,
        Manual = 10,
    }
}
