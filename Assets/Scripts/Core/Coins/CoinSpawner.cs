using Unity.Netcode;
using UnityEngine;

public class CoinSpawner : NetworkBehaviour
{
    [SerializeField] private RespawningCoin coinPrefab;
    [SerializeField] private int maxCoins = 50;
    [SerializeField] private int coinValue = 10;

    [SerializeField] private Vector2 xSpawnRange;
    [SerializeField] private Vector2 ySpawnRange;

    [SerializeField] private LayerMask layerMask;

    [SerializeField] private NetworkObject coinParentPrefab;



    private float coinRadius;

    private Transform coinParent;



    override public void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        var parentObj = Instantiate(coinParentPrefab);
        parentObj.Spawn();

        coinParent = parentObj.transform;

        coinRadius = coinPrefab.GetComponent<CircleCollider2D>().radius;

        for (int i = 0; i < maxCoins; i++)
        {
            SpawnCoin();
        }
    }


    private void SpawnCoin()
    {
        RespawningCoin coinInstance = Instantiate(coinPrefab, GetSpawnPoint(), Quaternion.identity, coinParent);

        coinInstance.SetValue(coinValue);
        coinInstance.GetComponent<NetworkObject>().Spawn();
        coinInstance.OnCollected += HandleCoinCollected;
        coinInstance.transform.SetParent(coinParent);

        Debug.Log(coinParent.name);
    }

    private void HandleCoinCollected(RespawningCoin coin)
    {
        coin.transform.position = GetSpawnPoint();
        coin.Reset();
    }


    private Vector2 GetSpawnPoint()
    {
        float x = 0f;
        float y = 0f;

        while (true)
        {
            x = Random.Range(xSpawnRange.x, xSpawnRange.y);
            y = Random.Range(ySpawnRange.x, ySpawnRange.y);

            Vector2 spawnPoint = new Vector2(x, y);

            Collider2D hitCollider = Physics2D.OverlapCircle(spawnPoint, coinRadius, layerMask);

            if (hitCollider == null)
            {
                return spawnPoint; // no colliders found, return spawn point
            }
        }
    }
}
