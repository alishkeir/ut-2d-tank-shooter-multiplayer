using UnityEngine;

public class Lifetime : MonoBehaviour
{
    [SerializeField] float lifetime = 2f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
