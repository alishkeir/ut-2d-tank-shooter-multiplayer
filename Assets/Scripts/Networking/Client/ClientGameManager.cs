using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class ClientGameManager
{

    private const string _GAME_SCENE_NAME = "Game";

    private JoinAllocation allocation;


    public async Task<bool> InitAsync()
    {
        // Authenticate player
        await UnityServices.InitializeAsync();
        AuthState authState = await AuthenticationHandler.DoAuth();

        if (authState == AuthState.Authenticated)
        {
            return true;

        }
        return false;
    }

    public async Task StartClientAsync(string joinCode)
    {
        try
        {
            allocation = await RelayService.Instance.JoinAllocationAsync(joinCode.ToString());

        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls"); //TODO: check for "udp"

        transport.SetRelayServerData(relayServerData);

        NetworkManager.Singleton.StartClient();
    }
}
