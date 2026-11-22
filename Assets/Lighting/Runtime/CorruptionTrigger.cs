using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class SnowglobeCorruptionTrigger : MonoBehaviour
{
    public SnowglobeLightingController lightingController;
    [Range(0f, 1f)] public float targetCorruption = 1f;
    public bool triggerOnce = true;

    private bool wasTriggered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerOnce && wasTriggered) return;
        if (!other.CompareTag("Player")) return;
        if (lightingController == null) return;

        wasTriggered = true;
        lightingController.TransitionTo(targetCorruption);
    }
}
