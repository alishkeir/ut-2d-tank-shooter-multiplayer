using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ParticlesAligner : MonoBehaviour
{

    ParticleSystem.MainModule psMain;

    void Start()
    {
        psMain = GetComponent<ParticleSystem>().main;
    }

    void Update()
    {
        psMain.startRotation = -transform.rotation.eulerAngles.z * Mathf.Deg2Rad;
    }
}
