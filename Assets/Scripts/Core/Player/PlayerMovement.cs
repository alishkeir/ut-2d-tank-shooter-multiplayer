
// PlayerMovement
// --------------
// Tank-style movement for the player.
//
// Controls (driven by InputReader.MoveEvent, a Vector2):
// - X component: turn the body left/right (Y-rotation).
// - Y component: drive the rigidbody forward/backward along the body's "up".
//
// The script is owner-authoritative for the local client:
// - The owner reads input and applies transform/velocity changes locally.
// - Those changes are replicated to other clients by the ClientNetworkTransform
//   (which is owner-authoritative).
//
// Setup:
// - Attach this to a GameObject with a Rigidbody2D.
// - Assign the body transform (the tank chassis) and an InputReader asset.
using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{

    [Header("References")]
    [SerializeField] private InputReader inputReader;     // ScriptableObject providing MoveEvent (Vector2).
    [SerializeField] private Transform bodyTransform;     // The visual transform that should rotate with steering.
    [SerializeField] private Rigidbody2D rb;             // The physics body that actually moves the player.


    [Header("Settings")]
    [SerializeField] private float movementSpeed = 4f;   // Forward/backward speed in world units / second.
    [SerializeField] private float turningRate = 30f;    // Steering speed in degrees / second.

    [Header("Particles")]
    [SerializeField] private ParticleSystem dustCloud;
    [SerializeField] private ParticleSystem leftTracks;
    [SerializeField] private ParticleSystem rightTracks;


    [Header("Particle Settings")]
    [SerializeField] private float particleEmmisionValue = 10f;


    private ParticleSystem.EmissionModule dustCloudEmissionModule;
    private ParticleSystem.EmissionModule leftTracksEmissionModule;
    private ParticleSystem.EmissionModule rightTracksEmissionModule;

    const float particleStopThreshold = 0.005f;


    private Vector2 previousMovementInput; // Last movement input received from the InputReader.

    private Vector3 previousPosition;
    private Vector3 previousRotation;

    void Awake()
    {
        dustCloudEmissionModule = dustCloud.emission;
        leftTracksEmissionModule = leftTracks.emission;
        rightTracksEmissionModule = rightTracks.emission;
    }


    // Update is used for steering (rotation) so input feels responsive.
    private void Update()
    {
        if (!IsOwner) { return; }

        // Steer: rotate the body around Z by an amount proportional to horizontal input.
        float zRotation = previousMovementInput.x * -turningRate * Time.deltaTime;

        bodyTransform.Rotate(0f, 0f, zRotation);

    }

    // FixedUpdate is used for velocity (physics) so movement stays framerate independent.
    private void FixedUpdate()
    {

        if (((transform.position - previousPosition).sqrMagnitude > particleStopThreshold) || ((transform.rotation.eulerAngles.z - previousRotation.z) > particleStopThreshold))
        {
            dustCloudEmissionModule.rateOverTime = particleEmmisionValue;
            leftTracksEmissionModule.rateOverTime = particleEmmisionValue;
            rightTracksEmissionModule.rateOverTime = particleEmmisionValue;
        }
        else
        {
            dustCloudEmissionModule.rateOverTime = 0f;
            leftTracksEmissionModule.rateOverTime = 0f;
            rightTracksEmissionModule.rateOverTime = 0f;
        }

        previousPosition = transform.position;
        previousRotation = transform.rotation.eulerAngles;

        if (!IsOwner) { return; }

        // Move along the body's "up" direction by an amount proportional to vertical input.
        rb.linearVelocity = movementSpeed * previousMovementInput.y * bodyTransform.up;
    }



    // start method is too early for calling , can be messed up with delays
    // will tells unity to know that variables are synced with the server
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { return; }

        // Start listening for movement input on the owner client.
        inputReader.MoveEvent += HandleMove;
    }

    // same as OnNetworkSpawn, but for despawning
    public override void OnNetworkDespawn()
    {
    }


    // Cache the latest movement input. It is consumed in Update/FixedUpdate.
    private void HandleMove(Vector2 movementInput)
    {
        previousMovementInput = movementInput;
    }
}
