// UserData
// --------
// Small payload sent from a client to the server during connection approval.
// Contains just enough info for the server to identify the player and pick a
// display name for them.
//
// Serialization:
// - Marked [Serializable] so JsonUtility can (de)serialize it.
// - The client side serializes a UserData instance to JSON, UTF-8 encodes it,
//   and attaches the bytes to NetworkConfig.ConnectionData.
// - The server reads those bytes back in NetworkServer.ApprovalCheck and
//   deserializes them into a UserData instance.
using System;


[Serializable]
public class UserData
{
    // The player's chosen display name (e.g. "TankMaster42").
    public string userName;

    // The player's Unity Authentication ID (a stable, unique identifier
    // that persists across sessions for the same anonymous account).
    public string userAuthId;
}
