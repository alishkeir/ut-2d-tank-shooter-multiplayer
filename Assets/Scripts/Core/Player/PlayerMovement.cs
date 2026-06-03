
using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{

    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private Rigidbody2D rb;

    [Header("Settings")]
    [SerializeField] private float movementSpeed = 4f;
    [SerializeField] private float turningRate = 30f;

    private Vector2 previousMovementInput;


    private void Update()
    {
        if (!IsOwner) { return; }

        float zRotation = previousMovementInput.x * -turningRate * Time.deltaTime;

        bodyTransform.Rotate(0f, 0f, zRotation);

    }

    private void FixedUpdate()
    {
        if (!IsOwner) { return; }

        rb.linearVelocity = movementSpeed * previousMovementInput.y * bodyTransform.up;
    }



    // start method is too early for calling , can be messed up with delays
    // will tells unity to know that variables are synced with the server
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { return; }

        inputReader.MoveEvent += HandleMove;
    }

    // same as OnNetworkSpawn, but for despawning
    public override void OnNetworkDespawn()
    {
    }


    private void HandleMove(Vector2 movementInput)
    {
        previousMovementInput = movementInput;
    }
}
