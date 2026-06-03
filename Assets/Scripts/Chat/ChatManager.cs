using Unity.Netcode;
using UnityEngine;
using System;

public class ChatManager : NetworkBehaviour
{
    public static ChatManager Instance;

    public event Action<string> OnMessageReceived;

    private void Awake()
    {
        Instance = this;
    }

    [Rpc(SendTo.Server)]
    public void SendChatMessageRpc(string playerName, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        ReceiveChatMessageRpc(playerName, message);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ReceiveChatMessageRpc(string playerName, string message)
    {
        string formatted = $"<color=#00FFAA>[{playerName}]</color> {message}"; ;
        OnMessageReceived?.Invoke(formatted);
    }
}