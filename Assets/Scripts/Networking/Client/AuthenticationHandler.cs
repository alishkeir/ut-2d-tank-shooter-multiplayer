
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

public static class AuthenticationHandler
{
    public static AuthState AuthState { get; private set; } = AuthState.NothAuthenticated;

    public static async Task<AuthState> DoAuth(int tries = 5)
    {

        if (AuthState == AuthState.Authenticated)
        {
            return AuthState;
        }

        AuthState = AuthState.Authenticating;

        int i = 0;
        while (AuthState == AuthState.Authenticating && i < tries)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            if (AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
            {
                AuthState = AuthState.Authenticated;
                break;

            }

            i++;
            await Task.Delay(1000);

        }


        return AuthState;

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