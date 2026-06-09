// PlayerAiming
// ------------
// Rotates the player's turret so that it points at the cursor's world position.
// Only runs on the owner client (the other clients receive the rotation via
// NetworkTransform replication).
//
// Requires:
// - An InputReader ScriptableObject providing the current AimPosition (mouse pos).
// - A turretTransform (the part of the tank that should rotate to face the cursor).
// - An orthographic main camera.
using Unity.Netcode;
using UnityEngine;

public class PlayerAiming : NetworkBehaviour
{

    [Header("References")]
    [SerializeField] private InputReader inputReader;     // ScriptableObject that exposes the current cursor position.
    [SerializeField] private Transform turretTransform;   // The transform (turret) that should rotate to face the cursor.

    // LateUpdate so the camera has finished moving this frame before we aim.
    private void LateUpdate()
    {
        // Only the owning client drives the turret rotation.
        if (!IsOwner) { return; }

        // Convert the cursor screen position to a world position using the main camera.
        Vector2 aimScreenPosition = inputReader.AimPosition;
        Vector2 aimWorldPosition = Camera.main.ScreenToWorldPoint(aimScreenPosition);

        // Point the turret's "up" vector at the world-space aim position.
        turretTransform.up = new Vector2(aimWorldPosition.x - turretTransform.position.x, aimWorldPosition.y - turretTransform.position.y);

    }



}
