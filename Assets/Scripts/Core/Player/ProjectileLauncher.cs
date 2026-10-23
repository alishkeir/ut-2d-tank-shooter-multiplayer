// ProjectileLauncher
// ------------------
// Handles firing projectiles for the player. Uses a hybrid client prediction /
// server-authoritative approach for responsive, cheat-resistant shooting.
//
// Flow (per shot, owner client only):
// 1. Owner sends PrimaryFireServerRpc(position, direction).
// 2. Owner immediately spawns a local "dummy" projectile (clientProjectilePrefab)
//    for instant visual feedback.
// 3. The server validates the player has enough coins, deducts the cost, spawns
//    the real (serverProjectilePrefab) projectile, and tells the other clients
//    to spawn a dummy projectile as well (via SpawnDummyProjectileClientRpc).
//
// Cost is in coins (see CoinCollector). Players without enough coins cannot fire.
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class ProjectileLauncher : NetworkBehaviour
{



    [Header("References")]
    [SerializeField] private InputReader inputReader;             // ScriptableObject providing PrimaryFireEvent (bool).
    [SerializeField] private Transform projectileSpawnPoint;      // Where projectiles appear (usually the muzzle).
    [SerializeField] private GameObject serverProjectilePrefab;  // The networked, server-authoritative projectile prefab.
    [SerializeField] private GameObject clientProjectilePrefab;  // The non-networked local-only "dummy" projectile prefab.
    [SerializeField] private GameObject muzzleFlash;             // Visual effect shown briefly when firing.
    [SerializeField] private Collider2D playerCollider;          // The shooter's collider - ignored by their own projectile.
    [SerializeField] private CoinCollector coinCollector;        // Used to check and spend the cost of firing.



    [Header("Settings")]
    [SerializeField] private float projectileSpeed = 10f;   // Initial speed of the projectile.
    [SerializeField] private float fireRate = 1f;           // Shots per second.
    [SerializeField] private float muzzleFlashDuration = 0.5f; // How long the muzzle flash stays visible.
    [SerializeField] private int costToFire = 1;            // Coins required per shot.



    bool shouldFire;     // True while the fire button is held (from the InputReader).
    float timer;         // Cooldown timer between shots.
    float muzzleFlashTimer; // Time remaining on the muzzle flash effect.
    bool isPointerOverUI;




    // Update handles cooldown, muzzle-flash timing, and (on the owner) firing.
    private void Update()
    {
        // Tick down the muzzle flash timer regardless of who we are.
        if (muzzleFlashTimer > 0f)
        {
            muzzleFlashTimer -= Time.deltaTime;

            if (muzzleFlashTimer <= 0f)
            {
                muzzleFlash.SetActive(false);
            }
        }

        // Only the owner can request firing.
        if (!IsOwner) { return; }

        isPointerOverUI = EventSystem.current.IsPointerOverGameObject();

        // Tick down the fire-rate cooldown.
        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }

        if (!shouldFire) { return; }
        if (timer > 0) { return; }

        // Don't fire if the player can't afford it.
        if (coinCollector.TotalCoins.Value < costToFire) { return; }

        // Ask the server to spawn the authoritative projectile, and spawn our own
        // dummy locally for instant feedback.
        PrimaryFireServerRpc(projectileSpawnPoint.position, projectileSpawnPoint.up);

        SpawnDummyProjectile(projectileSpawnPoint.position, projectileSpawnPoint.up);

        timer = 1 / fireRate;
    }




    // start method is too early for calling , can be messed up with delays
    // will tells unity to know that variables are synced with the server
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { return; }

        // Begin listening for fire input on the owner.
        inputReader.PrimaryFireEvent += HandlePrimaryFire;

    }


    // same as OnNetworkSpawn, but for despawning
    public override void OnNetworkDespawn()
    {
        if (!IsOwner) { return; }
        inputReader.PrimaryFireEvent -= HandlePrimaryFire;
    }


    // Cache the latest fire button state.
    private void HandlePrimaryFire(bool shouldFire)
    {
        if (shouldFire && isPointerOverUI) return;

        this.shouldFire = shouldFire;
    }


    // Server-side spawn. Validates the cost, deducts coins, spawns the real
    // projectile, and asks remote clients to spawn their dummy copies.
    [ServerRpc]
    private void PrimaryFireServerRpc(Vector3 position, Vector3 direction)
    {

        if (coinCollector.TotalCoins.Value < costToFire) { return; }

        coinCollector.SpendCoins(costToFire);


        GameObject projectileInstance = Instantiate(serverProjectilePrefab, position, Quaternion.identity);
        projectileInstance.transform.up = direction;

        // The shooter's own collider should not collide with their own projectile.
        Physics2D.IgnoreCollision(playerCollider, projectileInstance.GetComponent<Collider2D>());




        // Tag the projectile with the firing client so DamageDealer can avoid self-damage.
        if (projectileInstance.TryGetComponent<DamageDealer>(out DamageDealer damageDealer))
        {
            damageDealer.SetOwner(OwnerClientId);
        }

        // Give the projectile its initial velocity.
        if (projectileInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.linearVelocity = rb.transform.up * projectileSpeed;
        }

        // Tell all OTHER clients to spawn a dummy projectile at the same location.
        SpawnDummyProjectileClientRpc(position, direction);
    }


    // Spawns a local non-networked projectile for instant visual feedback.
    private void SpawnDummyProjectile(Vector3 position, Vector3 direction)
    {
        GameObject projectileInstance = Instantiate(clientProjectilePrefab, position, Quaternion.identity);
        projectileInstance.transform.up = direction;

        // Show the muzzle flash.
        muzzleFlash.SetActive(true);
        muzzleFlashTimer = muzzleFlashDuration;

        Physics2D.IgnoreCollision(playerCollider, projectileInstance.GetComponent<Collider2D>());

        if (projectileInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.linearVelocity = rb.transform.up * projectileSpeed;
        }
    }


    // Plays the dummy projectile + muzzle flash on every non-owner client.
    [ClientRpc]
    private void SpawnDummyProjectileClientRpc(Vector3 position, Vector3 direction)
    {
        if (IsOwner) { return; }

        SpawnDummyProjectile(position, direction);
    }

}
