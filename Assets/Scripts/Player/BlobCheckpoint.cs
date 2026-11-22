using UnityEngine;

// Put this on a trigger collider. Assign a child SpawnPoint for precise positioning.
public class BlobCheckpoint : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        BlobSlingshot blob = other.GetComponentInParent<BlobSlingshot>();
        if (blob != null) blob.SetCheckpoint(spawnPoint != null ? spawnPoint : transform);
    }
}
