using Unity.Netcode;
using UnityEngine;

public class CoinCollector : NetworkBehaviour
{
    public NetworkVariable<int> TotalCoins = new NetworkVariable<int>();



    private void OnTriggerEnter2D(Collider2D other)
    {

        if (!other.TryGetComponent<Coin>(out Coin coin)) { return; }

        int coinValue = coin.Collect();

        if (!IsServer) { return; }

        TotalCoins.Value += coinValue;

    }

    public void SpendCoins(int coins)
    {
        TotalCoins.Value -= coins;
    }
}
