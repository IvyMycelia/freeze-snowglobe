using UnityEngine;

// Simple trigger-based hazard; also useful for death pits.
public class BlobHazard : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        BlobSlingshot blob = other.GetComponentInParent<BlobSlingshot>();
        if (blob == null) return;

        // If this trigger belongs to a shatterable enemy, let a perfect
        // shot destroy it instead of immediately respawning the player.
        Shatterable target = GetComponentInParent<Shatterable>();
        if (target != null)
        {
            if (target.IsShattered) return;

            BlobImpact impact = blob.GetComponent<BlobImpact>();
            if (impact != null && impact.TryShatterTarget(target))
                return;
        }

        blob.Respawn();
    }
}
