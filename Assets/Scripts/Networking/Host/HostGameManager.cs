using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HostGameManager
{
    private const int _MAX_CONNECTIONS = 20;
    private const string _GAME_SCENE_NAME = "Game";

    private Allocation allocation;
    private string joinCode;

    public async Task StartHostAsync()
    {
        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(_MAX_CONNECTIONS);

        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return;
        }

        try
        {
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"Join Code: {joinCode}");

        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "udp");
        transport.SetRelayServerData(relayServerData);


        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene(_GAME_SCENE_NAME, LoadSceneMode.Single);



    }
}