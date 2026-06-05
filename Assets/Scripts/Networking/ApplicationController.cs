using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class ApplicationController : MonoBehaviour
{

    [SerializeField] private ClientSingleton clientPrefab;
    [SerializeField] private HostSingleton hostPrefab;

    private async void Start()
    {
        DontDestroyOnLoad(this);

        // check if we are in dedicated server mode
        // as dedicated server won't have graphics device
        await LaunchInMode(SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null);
    }


    async Task LaunchInMode(bool isDedicatedServer)
    {


        if (isDedicatedServer)
        {
        }
        else
        {

            HostSingleton hostSingleton = Instantiate(hostPrefab);
            hostSingleton.CreateHost();

            ClientSingleton clientSingleton = Instantiate(clientPrefab);
            bool isAuthenticated = await clientSingleton.CreateClient();

            if (isAuthenticated)
            {
                clientSingleton.GoToMainMenu();
            }
        }

    }

}
