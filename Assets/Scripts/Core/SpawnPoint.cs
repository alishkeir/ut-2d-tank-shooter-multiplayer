using System.Collections.Generic;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    private static List<SpawnPoint> spawnPoints = new();


    private void OnEnable()
    {
        spawnPoints.Add(this);
    }

    private void OnDisable()
    {
        spawnPoints.Remove(this);
    }

    public static Vector3 GetRandomSpawnPos()
    {

        if (spawnPoints.Count == 0) { return Vector3.zero; }
        return spawnPoints[Random.Range(0, spawnPoints.Count)].transform.position;
    }

    // private void OnDrawGizmosSelected()
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.position, 1.5f);
    }
}
