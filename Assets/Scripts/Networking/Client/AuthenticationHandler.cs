// AuthenticationHandler
// ---------------------
// Static helper for signing the local player in to Unity Authentication Services
// anonymously. Keeps track of the current AuthState (NothAuthenticated,
// Authenticating, Authenticated, Error, TimedOut) so other systems can query it.
//
// Usage:
//   AuthState state = await AuthenticationHandler.DoAuth();
//
// Behavior:
// - Always clears any existing session token first (so we get a fresh sign-in).
// - If already authenticated, returns immediately.
// - If a sign-in is already in progress, waits for it to finish.
// - Otherwise, tries to sign in anonymously up to `tries` times with 1s delay.
//   On success, state becomes Authenticated. On failure, state becomes Error
//   or TimedOut.
//
// Note: enum value `NothAuthenticated` keeps the original spelling (typo).
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public static class AuthenticationHandler
{
    // Current authentication state. Updated by DoAuth / SignInAnonymouslyAsync.
    public static AuthState AuthState { get; private set; } = AuthState.NothAuthenticated;

    // Public entry point. Returns the final AuthState.
    // `tries` controls how many times we retry sign-in before giving up.
    public static async Task<AuthState> DoAuth(int tries = 5)
    {

        // Always clear any cached token so we start from a clean state.
        AuthenticationService.Instance.ClearSessionToken();

        // Already signed in - nothing to do.
        if (AuthState == AuthState.Authenticated)
        {
            return AuthState;
        }

        // Another sign-in is already running; wait for it.
        if (AuthState == AuthState.Authenticating)
        {
            Debug.LogWarning("Authentication is already in progress");
            await Authenticating();
            return AuthState;
        }

        // Start a fresh sign-in attempt.
        await SignInAnonymouslyAsync(tries);

        return AuthState;
    }


    // Polls every 500ms until the in-flight authentication finishes.
    private static async Task<AuthState> Authenticating()
    {
        while (AuthState == AuthState.Authenticating || AuthState == AuthState.NothAuthenticated)
        {
            await Task.Delay(500);
        }

        return AuthState;
    }

    // Performs the anonymous sign-in, retrying on failure up to `tries` times.
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
                    // Success - mark as authenticated and stop retrying.
                    AuthState = AuthState.Authenticated;
                    break;
                }
            }
            catch (AuthenticationException ex)
            {
                // Auth-specific error.
                AuthState = AuthState.Error;
                Debug.LogError(ex);

            }
            catch (RequestFailedException ex)
            {
                // Generic Unity Services request error.
                AuthState = AuthState.Error;
                Debug.LogError(ex);
            }
            retries++;
            await Task.Delay(1000);
        }

        // We exhausted the retry budget without success.
        if (AuthState == AuthState.Authenticating)
        {
            AuthState = AuthState.TimedOut;
            Debug.LogError($"Authentication timed out after {retries} tries");
        }
    }
}



// Possible values for the global AuthState. Enum name "NothAuthenticated"
// preserves the original spelling.
public enum AuthState
{
    NothAuthenticated,
    Authenticating,
    Authenticated,
    Error,
    TimedOut
}
