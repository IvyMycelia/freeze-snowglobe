using UnityEngine;

// Attach this to a PREFAB with Rigidbody2D (Kinematic) + CircleCollider2D (Trigger).
// A child Particle System renders numerous spores that spray upward, drift, fall,
// and fade. The CircleCollider2D approximates the dangerous center of the cloud.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class SporeCloud : MonoBehaviour
{
    [Header("Particle system")]
    [SerializeField] private ParticleSystem sporeParticles;
    [SerializeField, Range(8, 250)] private int particleCount = 85;
    [SerializeField, Min(0.05f)] private float spraySeconds = 0.85f;
    [SerializeField, Min(0f)] private float sprayWidth = 0.16f;
    [SerializeField, Min(0.1f)] private float minUpwardSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float maxUpwardSpeed = 4.2f;
    [SerializeField, Min(0f)] private float horizontalSpread = 1.35f;
    [SerializeField, Range(0f, 2f)] private float particleGravity = 0.22f;
    [SerializeField, Min(0.1f)] private float minParticleLifetime = 2.2f;
    [SerializeField, Min(0.1f)] private float maxParticleLifetime = 3.2f;
    [SerializeField, Min(0.005f)] private float minParticleSize = 0.045f;
    [SerializeField, Min(0.005f)] private float maxParticleSize = 0.105f;
    [SerializeField] private Color dustColorA = new Color(0.60f, 0.86f, 0.78f, 0.85f);
    [SerializeField] private Color dustColorB = new Color(0.77f, 0.94f, 0.86f, 0.7f);

    [Header("Cloud drift (world units)")]
    [SerializeField] private float initialCloudRise = 2f;
    [SerializeField] private float cloudDownwardAcceleration = 1.4f;
    [SerializeField] private float windDriftX = 0.35f;
    [SerializeField, Min(0f)] private float randomWindVariation = 0.25f;

    [Header("Hazard volume")]
    [SerializeField, Min(0.05f)] private float maximumDangerRadius = 1.15f;
    [SerializeField, Min(0.1f)] private float expandSeconds = 1f;
    [SerializeField, Min(0f)] private float dangerousAfterSeconds = 0.25f;
    [SerializeField, Min(0.1f)] private float dangerousUntilSeconds = 3.0f;
    [SerializeField, Min(0.1f)] private float dangerFadeSeconds = 0.8f;

    private Rigidbody2D body;
    private CircleCollider2D dangerCollider;
    private BlobSlingshot playerMotor;

    private Vector2 origin;
    private float actualWindX;
    private float age;
    private int particlesEmitted;
    private bool released;
    private bool hitPlayer;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        dangerCollider = GetComponent<CircleCollider2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        dangerCollider.isTrigger = true;
        dangerCollider.enabled = false;

        if (sporeParticles == null)
            sporeParticles = GetComponentInChildren<ParticleSystem>();
    }

    // FungiEnemy calls this immediately after instantiating the prefab.
    public void Release()
    {
        if (released)
            return;

        released = true;
        age = 0f;
        particlesEmitted = 0;
        hitPlayer = false;
        origin = transform.position;
        actualWindX = windDriftX + Random.Range(-randomWindVariation, randomWindVariation);

        ConfigureParticles();

        playerMotor = FindFirstObjectByType<BlobSlingshot>();
        if (playerMotor != null)
            playerMotor.Respawned += OnPlayerRespawned;

        // Keep the object alive until the LAST emitted particles have faded.
        float totalDuration = spraySeconds + Mathf.Max(minParticleLifetime, maxParticleLifetime) + 0.25f;
        Destroy(gameObject, totalDuration);
    }

    private void OnDestroy()
    {
        if (playerMotor != null)
            playerMotor.Respawned -= OnPlayerRespawned;
    }

    private void OnPlayerRespawned()
    {
        dangerCollider.enabled = false;
        Destroy(gameObject);
    }

    private void ConfigureParticles()
    {
        if (sporeParticles == null)
        {
            Debug.LogWarning("SporeCloud needs a child ParticleSystem assigned.", this);
            return;
        }

        var main = sporeParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.gravityModifier = particleGravity;
        main.maxParticles = Mathf.Max(128, particleCount + 16);

        // We emit particles manually in a staggered spray; prevent automatic emission.
        var emission = sporeParticles.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        // Every speck fades in quickly and then gradually disappears.
        Gradient fade = new Gradient();
        fade.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0.85f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        var colorOverLifetime = sporeParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        sporeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        sporeParticles.Play(true);
    }

    private void Update()
    {
        if (!released)
            return;

        age += Time.deltaTime;

        if (sporeParticles == null)
            return;

        // Spread a CLOUD over time instead of instantly launching three orbs.
        float progress = Mathf.Clamp01(age / Mathf.Max(0.05f, spraySeconds));
        int targetCount = Mathf.FloorToInt(progress * particleCount);

        while (particlesEmitted < targetCount)
        {
            EmitOneSpore();
            particlesEmitted++;
        }
    }

    private void EmitOneSpore()
    {
        ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();

        // Simulating in World space means particles remain behind as the cloud drifts.
        p.position = new Vector3(
            origin.x + Random.Range(-sprayWidth, sprayWidth),
            origin.y + Random.Range(-0.07f, 0.07f),
            transform.position.z
        );

        p.velocity = new Vector3(
            actualWindX + Random.Range(-horizontalSpread, horizontalSpread),
            Random.Range(minUpwardSpeed, Mathf.Max(minUpwardSpeed, maxUpwardSpeed)),
            0f
        );

        p.startLifetime = Random.Range(
            minParticleLifetime,
            Mathf.Max(minParticleLifetime, maxParticleLifetime)
        );

        p.startSize = Random.Range(
            minParticleSize,
            Mathf.Max(minParticleSize, maxParticleSize)
        );

        p.startColor = Color.Lerp(dustColorA, dustColorB, Random.value);
        sporeParticles.Emit(p, 1);
    }

    private void FixedUpdate()
    {
        if (!released)
            return;

        // The center initially rises, then gravity slowly pulls the cloud down.
        float t = age;
        Vector2 cloudCenter = origin + new Vector2(
            actualWindX * t,
            initialCloudRise * t - 0.5f * cloudDownwardAcceleration * t * t
        );

        body.MovePosition(cloudCenter);

        // Keep the approximate danger region with the dense middle of the dust.
        float growth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / expandSeconds));
        float fade = Mathf.Clamp01((dangerousUntilSeconds - t) / dangerFadeSeconds);
        dangerCollider.radius = maximumDangerRadius *
                                Mathf.Lerp(0.2f, 1f, growth) *
                                Mathf.Lerp(0.15f, 1f, fade);

        dangerCollider.enabled = !hitPlayer &&
                                 t >= dangerousAfterSeconds &&
                                 t < dangerousUntilSeconds;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHitPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHitPlayer(other);
    }

    private void TryHitPlayer(Collider2D other)
    {
        if (hitPlayer || !released || !dangerCollider.enabled)
            return;

        BlobSlingshot blob = other.GetComponentInParent<BlobSlingshot>();
        if (blob == null)
            return;

        // Disable first to avoid a duplicate respawn from subsequent contacts.
        hitPlayer = true;
        dangerCollider.enabled = false;
        blob.Respawn();
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.55f, 0.9f, 0.8f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, maximumDangerRadius);
    }
}
