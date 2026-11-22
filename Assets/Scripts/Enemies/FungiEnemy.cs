using UnityEngine;
using UnityEngine.Events;

// Replacement for the original projectile-based FungiEnemy.
// One emission creates one SporeCloud, which produces many falling dust particles.
public sealed class FungiEnemy : MonoBehaviour
{
    [Header("Cloud prefab")]
    [SerializeField] private SporeCloud cloudPrefab;
    [SerializeField] private Transform emissionPoint;
    [SerializeField] private EnemyStatus status;

    [Header("Emission timing")]
    [SerializeField, Min(0.5f)] private float intervalSeconds = 3.5f;
    [SerializeField, Range(0f, 1f)] private float intervalRandomness = 0.15f;
    [SerializeField, Min(0f)] private float warningSeconds = 0.7f;
    [SerializeField, Min(0f)] private float firstEmissionDelay = 1.2f;

    [Header("Optional feedback")]
    [SerializeField] private UnityEvent onTelegraph;
    [SerializeField] private UnityEvent onCloudReleased;

    private float timeUntilEmission;
    private bool isTelegraphing;
    private BlobSlingshot playerMotor;

    private void Awake()
    {
        if (status == null)
            status = GetComponent<EnemyStatus>();

        if (emissionPoint == null)
            emissionPoint = transform;

        // Don't instantly attack as soon as the scene loads.
        timeUntilEmission = Mathf.Max(firstEmissionDelay, warningSeconds + 0.05f);
    }

    private void Start()
    {
        playerMotor = FindFirstObjectByType<BlobSlingshot>();
        if (playerMotor != null)
            playerMotor.Respawned += ResetEmissionCycle;
    }

    private void OnDestroy()
    {
        if (playerMotor != null)
            playerMotor.Respawned -= ResetEmissionCycle;
    }

    private void ResetEmissionCycle()
    {
        isTelegraphing = false;
        timeUntilEmission = NextInterval();
    }

    private void Update()
    {
        // The fungus stops releasing new clouds while frozen.
        // Previously released clouds continue drifting independently.
        if (status != null && status.IsFrozen)
            return;

        timeUntilEmission -= Time.deltaTime;

        if (!isTelegraphing && timeUntilEmission <= warningSeconds)
        {
            isTelegraphing = true;
            onTelegraph?.Invoke();
        }

        if (timeUntilEmission > 0f)
            return;

        ReleaseCloud();
        isTelegraphing = false;
        timeUntilEmission = NextInterval();
    }

    private float NextInterval()
    {
        float variation = Random.Range(-intervalRandomness, intervalRandomness);
        float interval = intervalSeconds * (1f + variation);
        return Mathf.Max(warningSeconds + 0.1f, interval);
    }

    private void ReleaseCloud()
    {
        if (cloudPrefab != null)
        {
            SporeCloud cloud = Instantiate(
                cloudPrefab,
                emissionPoint.position,
                Quaternion.identity
            );

            cloud.Release();
        }

        onCloudReleased?.Invoke();
    }
}
