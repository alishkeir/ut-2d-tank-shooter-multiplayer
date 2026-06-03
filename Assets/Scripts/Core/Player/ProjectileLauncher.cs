using System;
using Unity.Netcode;
using UnityEngine;

public class ProjectileLauncher : NetworkBehaviour
{



    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private GameObject serverProjectilePrefab;
    [SerializeField] private GameObject clientProjectilePrefab;
    [SerializeField] private GameObject muzzleFlash;
    [SerializeField] private Collider2D playerCollider;



    [Header("Settings")]
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private float muzzleFlashDuration = 0.5f;

    private float previousFireTime;
    private float muzzleFlashTimer;





    private bool shouldFire;

    private void Update()
    {

        if (muzzleFlashTimer > 0f)
        {
            muzzleFlashTimer -= Time.deltaTime;

            muzzleFlash.SetActive(muzzleFlashTimer > 0f);

            // if(muzzleFlashTimer <= 0f)
            // {
            //     muzzleFlash.SetActive(false);
            // }
        }

        if (!IsOwner) { return; }
        if (!shouldFire) { return; }


        if (Time.time < (1 / fireRate) + previousFireTime) { return; } // check if we can fire again

        SpawnDummyProjectile(projectileSpawnPoint.position, projectileSpawnPoint.up);
        PrimaryFireServerRpc(projectileSpawnPoint.position, projectileSpawnPoint.up);

        previousFireTime = Time.time;


    }


    // start method is too early for calling , can be messed up with delays
    // will tells unity to know that variables are synced with the server
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { return; }

        inputReader.PrimaryFireEvent += HandlePrimaryFire;

    }


    // same as OnNetworkSpawn, but for despawning
    public override void OnNetworkDespawn()
    {
        if (!IsOwner) { return; }
        inputReader.PrimaryFireEvent -= HandlePrimaryFire;
    }


    private void HandlePrimaryFire(bool shouldFire)
    {
        this.shouldFire = shouldFire;
    }


    [ServerRpc]
    private void PrimaryFireServerRpc(Vector3 position, Vector3 direction)
    {
        GameObject projectileInstance = Instantiate(serverProjectilePrefab, position, Quaternion.identity);
        projectileInstance.transform.up = direction;

        Physics2D.IgnoreCollision(playerCollider, projectileInstance.GetComponent<Collider2D>());


        if (projectileInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.linearVelocity = rb.transform.up * projectileSpeed;
        }

        SpawnDummyProjectileClientRpc(position, direction);
    }


    private void SpawnDummyProjectile(Vector3 position, Vector3 direction)
    {
        GameObject projectileInstance = Instantiate(clientProjectilePrefab, position, Quaternion.identity);
        projectileInstance.transform.up = direction;

        muzzleFlash.SetActive(true);
        muzzleFlashTimer = muzzleFlashDuration;

        Physics2D.IgnoreCollision(playerCollider, projectileInstance.GetComponent<Collider2D>());

        if (projectileInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.linearVelocity = rb.transform.up * projectileSpeed;
        }
    }


    [ClientRpc]
    private void SpawnDummyProjectileClientRpc(Vector3 position, Vector3 direction)
    {
        if (IsOwner) { return; }

        SpawnDummyProjectile(position, direction);
    }

}
