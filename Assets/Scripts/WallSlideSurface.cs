using UnityEngine;

// Place on a solid wall with Collider2D (Is Trigger OFF).
// Do NOT add BounceSurface to the same wall: this one is manual.
public class WallSlideSurface : MonoBehaviour
{
    [Header("Clinging")]
    [SerializeField, Min(0.1f)] private float maximumSlideSpeed = 2.2f;
    [SerializeField, Min(0f)] private float inwardClingSpeed = 0.8f;

    [Header("Wall Jump")]
    [SerializeField, Min(0.1f)] private float kickHorizontalSpeed = 13f;
    [SerializeField, Min(0.1f)] private float kickVerticalSpeed = 15f;
    [SerializeField] private bool restoreAirShot = true;

    public float MaximumSlideSpeed => maximumSlideSpeed;
    public float InwardClingSpeed => inwardClingSpeed;
    public float KickHorizontalSpeed => kickHorizontalSpeed;
    public float KickVerticalSpeed => kickVerticalSpeed;
    public bool RestoreAirShot => restoreAirShot;
}
