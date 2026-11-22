using UnityEngine;

// Add this to walls, mushroom platforms, or frozen creatures that should bounce the blob.
// No custom script is needed for each kind of surface.
public class BounceSurface : MonoBehaviour
{
    [Header("Bounce physics")]
    [SerializeField, Range(0.1f, 1.3f)] private float speedRetention = 0.96f;
    [SerializeField] private float minimumImpactSpeed = 2.5f;
    [SerializeField] private float minimumReboundSpeed = 7f;
    [SerializeField] private bool restoreAirShot = false;

    public float SpeedRetention => speedRetention;
    public float MinimumImpactSpeed => minimumImpactSpeed;
    public float MinimumReboundSpeed => minimumReboundSpeed;
    public bool RestoreAirShot => restoreAirShot;
}
