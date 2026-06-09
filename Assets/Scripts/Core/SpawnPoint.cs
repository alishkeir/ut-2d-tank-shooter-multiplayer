// SpawnPoint
// ----------
// Marks a position in the world where a player (or other NetworkObject) can spawn.
// All active SpawnPoint components register themselves into a static list, and any
// system can call SpawnPoint.GetRandomSpawnPos() to obtain a random spawn location
// from the registered points.
//
// Usage:
// - Drop this component on any empty GameObject placed where players may spawn.
// - Multiple SpawnPoints can exist in a scene; one is picked at random.
// - Spawn points are auto-registered on enable and unregistered on disable.
//
// Used by:
// - RespawnHandler (player respawn after death).
// - NetworkServer connection approval (initial spawn position).
using System.Collections.Generic;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    // Static list of every active SpawnPoint in any loaded scene.
    private static List<SpawnPoint> spawnPoints = new();


    // Register this spawn point as soon as it becomes active.
    private void OnEnable()
    {
        spawnPoints.Add(this);
    }

    // Unregister when disabled or destroyed so we never spawn at a stale location.
    private void OnDisable()
    {
        spawnPoints.Remove(this);
    }

    // Returns the world position of a randomly chosen active SpawnPoint.
    // If no SpawnPoints exist, returns Vector3.zero as a safe fallback.
    public static Vector3 GetRandomSpawnPos()
    {

        if (spawnPoints.Count == 0) { return Vector3.zero; }
        return spawnPoints[Random.Range(0, spawnPoints.Count)].transform.position;
    }

    // Draws a small blue gizmo sphere in the editor so designers can see spawn points at a glance.
    // private void OnDrawGizmosSelected()
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.position, 1.5f);
    }
}
