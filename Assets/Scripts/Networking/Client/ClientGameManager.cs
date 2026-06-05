using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;

public class ClientGameManager
{

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
}
