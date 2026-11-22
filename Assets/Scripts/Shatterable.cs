using UnityEngine;
using UnityEngine.Events;

public class Shatterable : MonoBehaviour
{
    public enum ShatterCategory
    {
        CorruptedLife,
        Obstacle
    }

    [Header("Object Behavior")]
    [SerializeField] private ShatterCategory category = ShatterCategory.CorruptedLife;
    [SerializeField] private bool canBeShattered = true;
    [SerializeField] private bool damagesOnContact = true;

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

        shattered = true;

        if (GameState.Instance != null)
            GameState.Instance.RecordShatter(category);

        onShattered?.Invoke();

        if (shatterEffectPrefab != null)
            Instantiate(shatterEffectPrefab, transform.position, Quaternion.identity);

        // Disable collision immediately; Destroy completes at end of frame.
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D item in colliders)
            item.enabled = false;

        Destroy(gameObject);
    }
}
