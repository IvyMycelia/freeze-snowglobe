using UnityEngine;

/// <summary>Optional tiny oscillation for one sunbeam. Attach to its light object.</summary>
public sealed class SnowglobeSunbeamSway : MonoBehaviour
{
    [Range(0f, 10f)] public float swayDegrees = 1.2f;
    [Min(0f)] public float swayCyclesPerSecond = 0.08f;
    public float phaseOffset;

    private Quaternion initialRotation;

    private void Start() => initialRotation = transform.localRotation;

    private void Update()
    {
        float angle = Mathf.Sin(
            (Time.time * swayCyclesPerSecond + phaseOffset) * Mathf.PI * 2f) * swayDegrees;
        transform.localRotation = initialRotation * Quaternion.Euler(0f, 0f, angle);
    }
}
