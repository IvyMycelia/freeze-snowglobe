using UnityEngine;
using Vector2 = UnityEngine.Vector2;

// Add this to the Player root beside BlobSlingshot.
[RequireComponent(typeof(BlobSlingshot))]
public class BlobImpact : MonoBehaviour
{
    [SerializeField] private float significantCollisionSpeed = 1.5f;
    private BlobSlingshot slingshot;

    private void Awake()
    {
        slingshot = GetComponent<BlobSlingshot>();
    }

    private void OnCollisionEnter2D(Collision2D hit)
    {
        if (TryShatter(hit.collider)) return;
        if (IsHazard(hit.collider))
        {
            slingshot.Respawn();
            return;
        }

        // Hitting anything substantial consumes the one-hit shatter power.
        if (hit.relativeVelocity.magnitude >= significantCollisionSpeed)
            slingshot.ConsumeShatter();

        BounceSurface surface = hit.collider.GetComponentInParent<BounceSurface>();
        if (surface != null && hit.contactCount > 0)
        {
            Vector2 normal = hit.GetContact(0).normal;
            slingshot.TryBounce(normal, surface);
        }
    }

    private void OnTriggerEnter2D(Collider2D hit)
    {
        if (TryShatter(hit)) return;
        if (IsHazard(hit)) slingshot.Respawn();
    }

    private bool TryShatter(Collider2D collider)
    {
        return TryShatterTarget(collider.GetComponentInParent<Shatterable>());
    }

    // Also called by BlobHazard if its trigger receives the contact first.
    // This makes perfect-shot shattering independent of callback order.
    public bool TryShatterTarget(Shatterable target)
    {
        if (target == null) return false;
        if (target.IsShattered) return true; // Already handled this contact.
        if (!slingshot.CanShatter || !target.CanBeShattered) return false;

        slingshot.ConsumeShatter();
        target.Shatter(); // Disables colliders before deferred Destroy.
        slingshot.ContinueThroughShatteredObject();
        return true;
    }

    private static bool IsHazard(Collider2D collider)
    {
        Shatterable target = collider.GetComponentInParent<Shatterable>();
        if (target != null && target.DamagesOnContact) return true;
        return collider.CompareTag("Hazard");
    }
}
