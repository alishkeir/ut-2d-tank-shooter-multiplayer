// BountyCoin
// ----------
// A concrete Coin subclass that destroys itself after being collected.
//
// Behavior:
// - On the client side (non-server), calling Collect() just hides the sprite.
// - On the server, Collect() marks the coin as collected, destroys the coin,
//   and returns its value.


public class BountyCoin : Coin
{

    public override int Collect()
    {
        // Clients just hide the visual; the server handles the actual game logic.
        if (!IsServer)
        {
            Show(false);
            return 0;
        }

        // Don't allow double-collection.
        if (alraedyCollected) { return 0; }

        alraedyCollected = true;

        //  Destroy the coin after being collected.
        Destroy(gameObject);

        return coinValue;
    }


}
