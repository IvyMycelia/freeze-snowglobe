using UnityEngine;

/// <summary>
/// Horizontal parallax for side-scrolling, Y stays fixed.
/// Factor 0 = locked to camera (distant sky); 1 = stationary in world.
/// </summary>
public sealed class SnowglobeParallaxLayer : MonoBehaviour
{
    public Transform cameraTransform;
    [Range(0f, 1f)] public float worldMotionFactor = 0.3f;

    private Vector3 startPosition;
    private float startCameraX;

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        startPosition = transform.position;
        if (cameraTransform != null)
            startCameraX = cameraTransform.position.x;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;
        float cameraDeltaX = cameraTransform.position.x - startCameraX;
        transform.position = new Vector3(
            startPosition.x + cameraDeltaX * (1f - worldMotionFactor),
            startPosition.y,
            startPosition.z);
    }
}
