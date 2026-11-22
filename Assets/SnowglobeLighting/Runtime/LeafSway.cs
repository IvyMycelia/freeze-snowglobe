using UnityEngine;

/// <summary>Animate a decorative child SpriteRenderer's transform, never a collider.</summary>
public sealed class SnowglobeLeafSway : MonoBehaviour
{
    [Range(0f, 12f)] public float angleDegrees = 2f;
    [Min(0f)] public float frequency = 0.7f;
    public float phaseOffset;

    private Quaternion initialRotation;

    private void Start() => initialRotation = transform.localRotation;

    private void Update()
    {
        float a = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f + phaseOffset)
            * angleDegrees;
        transform.localRotation = initialRotation * Quaternion.Euler(0f, 0f, a);
    }
}
