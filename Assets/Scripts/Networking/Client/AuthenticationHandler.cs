
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public static class AuthenticationHandler
{
    public static AuthState AuthState { get; private set; } = AuthState.NothAuthenticated;

    public static async Task<AuthState> DoAuth(int tries = 5)
    {

        AuthenticationService.Instance.ClearSessionToken();

        if (AuthState == AuthState.Authenticated)
        {
            return AuthState;
        }

        if (AuthState == AuthState.Authenticating)
        {
            Debug.LogWarning("Authentication is already in progress");
            await Authenticating();
            return AuthState;
        }

        await SignInAnonymouslyAsync(tries);

        return AuthState;
    }


    private static async Task<AuthState> Authenticating()
    {
        while (AuthState == AuthState.Authenticating || AuthState == AuthState.NothAuthenticated)
        {
            await Task.Delay(500);
        }

        return AuthState;
    }

    private static async Task SignInAnonymouslyAsync(int tries)
    {
        AuthState = AuthState.Authenticating;

        int retries = 0;

        while (AuthState == AuthState.Authenticating && retries < tries)
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

                if (AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
                {
                    AuthState = AuthState.Authenticated;
                    break;
                }
            }
            catch (AuthenticationException ex)
            {
                AuthState = AuthState.Error;
                Debug.LogError(ex);

            }
            catch (RequestFailedException ex)
            {
                AuthState = AuthState.Error;
                Debug.LogError(ex);
            }
            retries++;
            await Task.Delay(1000);
        }

        if (AuthState == AuthState.Authenticating)
        {
            AuthState = AuthState.TimedOut;
            Debug.LogError($"Authentication timed out after {retries} tries");
        }
    }
}



public enum AuthState
{
    NothAuthenticated,
    Authenticating,
    Authenticated,
    Error,
    TimedOut
}