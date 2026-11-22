using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class SporeProjectile : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float lifetime = 3.5f;
    [SerializeField] private LayerMask solidTerrain;
    [SerializeField] private GameObject impactEffectPrefab;

    private Rigidbody2D body;
    private BlobSlingshot playerMotor;
    private bool spent;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start()
    {
        playerMotor = FindFirstObjectByType<BlobSlingshot>();
        if (playerMotor != null) playerMotor.Respawned += OnPlayerRespawned;
    }

    private void OnDestroy()
    {
        if (playerMotor != null) playerMotor.Respawned -= OnPlayerRespawned;
    }

    private void OnPlayerRespawned()
    {
        Destroy(gameObject);
    }

    public void Launch(Vector2 velocity)
    {
        body.linearVelocity = velocity;
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (spent) return;
        BlobSlingshot blob = other.GetComponentInParent<BlobSlingshot>();
        if (blob != null)
        {
            spent = true;
            blob.Respawn();
            Burst();
            return;
        }
        if ((solidTerrain.value & (1 << other.gameObject.layer)) != 0)
        {
            spent = true;
            Burst();
        }
    }

    private void Burst()
    {
        if (impactEffectPrefab != null)
            Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}
