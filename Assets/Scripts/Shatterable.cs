using UnityEngine;
using UnityEngine.Events;

// Compatible with BlobImpact / BlobHazard and GameState.
// Choose DestroyObject for obstacles or LeaveRemains for creatures.
public class Shatterable : MonoBehaviour
{
    public enum ShatterCategory
    {
        CorruptedLife,
        Obstacle
    }

    public enum AfterShatter
    {
        DestroyObject,
        LeaveRemains
    }

    [Header("Object Behavior")]
    [SerializeField] private ShatterCategory category = ShatterCategory.CorruptedLife;
    [SerializeField] private bool canBeShattered = true;
    [SerializeField] private bool damagesOnContact = true;
    [SerializeField] private AfterShatter afterShatter = AfterShatter.DestroyObject;

    [Header("If Leaving Remains")]
    [Tooltip("Drag FaunaEnemy here. You can also include other AI or attack scripts.")]
    [SerializeField] private MonoBehaviour[] behavioursToDisable;

    [Tooltip("Stop the enemy's Rigidbody2D at the spot where it was hit. Useful for grounded fauna.")]
    [SerializeField] private bool stopPhysicsWhenShattered = true;

    [Header("Optional Effects")]
    [SerializeField] private GameObject shatterEffectPrefab;
    [SerializeField] private UnityEvent onShattered;

    private bool shattered;

    public bool IsShattered => shattered;
    public bool CanBeShattered => canBeShattered && !shattered;
    public bool DamagesOnContact => damagesOnContact && !shattered;

    public void Shatter()
    {
        if (!CanBeShattered)
            return;

        // Set this immediately to make repeat collision callbacks harmless.
        shattered = true;

        if (GameState.Instance != null)
            GameState.Instance.RecordShatter(category);

        // An independently spawned effect is optional; the body can remain.
        if (shatterEffectPrefab != null)
            Instantiate(shatterEffectPrefab, transform.position, Quaternion.identity);

        // Remove the ability to damage, block, or re-trigger collisions.
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D collider in colliders)
            collider.enabled = false;

        if (afterShatter == AfterShatter.LeaveRemains)
        {
            // Disable movement / attack AI, but do NOT disable the Animator
            // or FaunaAnimationBridge: the bridge plays the death animation.
            if (behavioursToDisable != null)
            {
                foreach (MonoBehaviour behaviour in behavioursToDisable)
                {
                    if (behaviour != null && behaviour != this)
                        behaviour.enabled = false;
                }
            }

            if (stopPhysicsWhenShattered)
            {
                Rigidbody2D body = GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                    body.simulated = false;
                }
            }

            // Stasis sometimes sets animator.speed = 0. Ensure the
            // death animation can still play even if hit while frozen.
            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
                animator.speed = 1f;
        }

        onShattered?.Invoke();

        if (afterShatter == AfterShatter.DestroyObject)
            Destroy(gameObject);
    }
}
