// LeaderboardEntityState
// ----------------------
// One row in the Leaderboard's NetworkList. A small value type that holds the
// minimum data needed to render a row (which client, what name, how many coins).
//
// Implements INetworkSerializable so it can travel inside a NetworkList<T>.
// Implements IEquatable<T> so the NetworkList can detect changes and raise
// Value-changed events on the clients.
using System;
using Unity.Collections;
using Unity.Netcode;

public struct LeaderboardEntityState : INetworkSerializable, IEquatable<LeaderboardEntityState>
{
    // The owning client's NetworkManager id.
    public ulong ClientId;

    // The display name. FixedString32Bytes is a blittable, network-friendly string type.
    public FixedString32Bytes PlayerName;

    // The current coin count for this player.
    public int Coins;

    // Tells Netcode how to (de)serialize each field.
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref Coins);
    }

    // Equality check used by the NetworkList to detect value changes.
    public bool Equals(LeaderboardEntityState other)
    {
        return ClientId == other.ClientId &&
            PlayerName.Equals(other.PlayerName) &&
            Coins == other.Coins;
    }
}
