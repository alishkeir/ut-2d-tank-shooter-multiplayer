// ChatManager
// -----------
// Server-authoritative chat relay for a multiplayer session.
//
// Flow:
// - Any client calls SendChatMessageRpc(playerName, message).
// - The Server validates the message (ignores empty/whitespace) and broadcasts
//   ReceiveChatMessageRpc to every client (including the host).
// - All clients (and host) fire OnMessageReceived with a formatted, colorized string.
//
// Subscribe to OnMessageReceived from a UI script (see ChatUI) to display messages.
// Access the manager globally via ChatManager.Instance. There should only be one
// instance in the scene, and it must be a NetworkObject (spawned by the host).
using Unity.Netcode;
using UnityEngine;
using System;

public class ChatManager : NetworkBehaviour
{
    // Global access point for the chat manager.
    // Set in Awake() and used by the UI to subscribe to OnMessageReceived.
    public static ChatManager Instance;

    // Fired on every client and the host whenever a chat message is broadcast.
    // The payload is the formatted string (with player name + rich text color).
    public event Action<string> OnMessageReceived;

    private void Awake()
    {
        // Assign the static instance so other systems can reach this manager.
        Instance = this;
    }

    // Called by a client to request sending a chat message.
    // The RPC is routed to the server, which validates and rebroadcasts it.
    [Rpc(SendTo.Server)]
    public void SendChatMessageRpc(string playerName, string message)
    {
        // Ignore blank messages - prevents spam and empty chat entries.
        if (string.IsNullOrWhiteSpace(message))
            return;

        // Re-broadcast to every client and the host.
        ReceiveChatMessageRpc(playerName, message);
    }

    // Sent from the server to all clients (and the host) when a message is received.
    // The formatted string includes a colored "[PlayerName]" prefix for easy reading.
    [Rpc(SendTo.ClientsAndHost)]
    private void ReceiveChatMessageRpc(string playerName, string message)
    {
        // Build a rich-text formatted line: "[PlayerName] message".
        string formatted = $"<color=#00FFAA>[{playerName}]</color> {message}"; ;
        // Notify any UI listeners.
        OnMessageReceived?.Invoke(formatted);
    }
}
