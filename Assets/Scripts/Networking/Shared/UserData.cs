using System;

[Serializable]
public class UserData
{
    public string userName;
    public string userAuthId;
    // UNITY 6 COMPAT: Added to satisfy MatchplayMatchmaker and MatchplayBackfiller,
    // which were imported before this field was defined in the course sequence.
    public GameInfo userGamePreferences = new();
}