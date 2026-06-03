using System;
using UnityEngine;


public class RespawningCoin : Coin
{

    public event Action<RespawningCoin> OnCollected;

    private Vector3 previousPosition;


    private void Update()
    {

        if (previousPosition != transform.position)
        {
            Show(true);
        }

        previousPosition = transform.position;
    }

    public override int Collect()
    {
        if (!IsServer)
        {
            Show(false);
            return 0;
        }

        if (alraedyCollected) { return 0; }

        alraedyCollected = true;

        OnCollected?.Invoke(this);

        return coinValue;
    }

    internal void Reset()
    {
        alraedyCollected = false;
    }
}
