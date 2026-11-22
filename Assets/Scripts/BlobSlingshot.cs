using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.UI;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

// Unity 6 / New Input System. Put this on the PLAYER ROOT, not on the sprite.
[RequireComponent(typeof(Rigidbody2D))]
public class BlobSlingshot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private LineRenderer trajectory;
    [SerializeField] private LayerMask trajectoryCollisionLayers;
    [SerializeField] private Transform checkpoint;

    [Header("Ground and Air Steering")]
    [SerializeField] private float walkSpeed = 5.6f;
    [SerializeField] private float groundAcceleration = 68f;
    [SerializeField] private float groundBraking = 54f;
    [SerializeField] private float airSteerAcceleration = 8f;
    [SerializeField] private float groundCheckRadius = 0.12f;
    [SerializeField] private int extraAirShots = 1;

    [Header("Powerful Slingshot")]
    [SerializeField] private float maxLaunchSpeed = 22.5f;
    [SerializeField] private float maxTotalSpeed = 29f;
    [SerializeField, Range(0.5f, 1.6f)] private float dragPowerExponent = 0.88f;
    [SerializeField, Range(0.05f, 0.5f)] private float maxDragScreenHeightFraction = 0.18f;
    [SerializeField, Range(0.002f, 0.1f)] private float minDragScreenHeightFraction = 0.012f;
    [SerializeField, Range(0f, 0.6f)] private float momentumCarry = 0.18f;
    [SerializeField, Range(0.005f, 0.2f)] private float aimSmoothingSeconds = 0.035f;

    [Header("Charge: only begins at maximum pull")]
    [SerializeField] private float chargeStartDelay = 0.20f;
    [SerializeField] private float chargeDuration = 0.55f;
    [SerializeField] private float chargedWindow = 0.28f;
    [SerializeField] private float perfectWindow = 0.12f;
    [SerializeField, Range(0f, 0.3f)] private float chargedSpeedBonus = 0.10f;

    [Header("Trajectory Preview")]
    [SerializeField, Range(16, 160)] private int trajectoryPoints = 90;
    [SerializeField, Range(0.4f, 4f)] private float trajectorySeconds = 2.5f;
    [SerializeField] private float trajectoryCastRadius = 0.30f;
    [SerializeField] private Color normalColor = new Color(0.48f, 0.85f, 1f);
    [SerializeField] private Color chargedColor = new Color(0.44f, 1f, 0.72f);
    [SerializeField] private Color perfectColor = new Color(1f, 0.84f, 0.32f);

    [Header("Physics Feel")]
    [SerializeField] private float playerGravityScale = 2.10f;
    [SerializeField] private float postLaunchGroundGrace = 0.13f;
    [SerializeField] private float maxShatterFlightTime = 3f;
    [SerializeField] private float bounceCooldown = 0.07f;

    [Header("Optional Charge UI")]
    [SerializeField] private Slider chargeBar;
    [SerializeField] private Image chargeIndicator;

    [Header("Optional Effects (Inspector events)")]
    public UnityEvent onLaunch;
    public UnityEvent onPerfectLaunch;
    public UnityEvent onLand;
    public UnityEvent onBounce;

    public event System.Action<bool> Launched;
    public event System.Action<float> Landed;
    public event System.Action<float> Bounced;
    public event System.Action Respawned;

    private Rigidbody2D body;
    private Vector2 spawnPoint;
    private Vector2 pressPosition;
    private Vector2 rawPull;
    private Vector2 smoothPull;
    private Vector2 launchVelocityPending;
    private Vector2 wallKickVelocityPending;
    private Vector2 incomingVelocity;
    private int airShotsRemaining;
    private float maxDragReachedAt = -1f;
    private float launchTime = -100f;
    private float bounceTime = -100f;
    private float shatterTime;
    private bool grounded;
    private bool aiming;
    private bool launchPending;
    private bool wallKickPending;
    private bool refreshAirOnWallKick;
    private bool shatterArmed;
    private bool controlsEnabled = true;

    public bool IsGrounded => grounded;
    public bool ControlsEnabled => controlsEnabled;
    public bool IsAiming => aiming;
    public bool CanShatter => shatterArmed;
    public bool IsCharged => IsChargedNow();
    public bool IsPerfectReady => IsPerfectNow();
    public float AimStrength => aiming ? GetSmoothedStrength() : 0f;
    public float ChargeProgress => GetChargeProgress();
    public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
    public Vector2 IncomingVelocity => incomingVelocity;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spawnPoint = body.position;
        airShotsRemaining = extraAirShots;
        body.gravityScale = playerGravityScale;
        body.linearDamping = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.freezeRotation = true;

        if (groundCheck == null)
        {
            UnityEngine.Debug.LogError("BlobSlingshot: assign GroundCheck.", this);
            enabled = false;
            return;
        }

        if (trajectory != null)
        {
            trajectory.useWorldSpace = true;
            trajectory.enabled = false;
        }
        UpdateChargeUI();
    }

    private void Update()
    {
        if (shatterArmed && Time.time - shatterTime > maxShatterFlightTime)
            shatterArmed = false;

        if (!controlsEnabled)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (!aiming && mouse.leftButton.wasPressedThisFrame && CanAim())
            BeginAim(mouse.position.ReadValue());

        if (!aiming)
            return;

        rawPull = pressPosition - mouse.position.ReadValue();
        rawPull = Vector2.ClampMagnitude(rawPull, MaxDragPixels());

        // Visually smooth aim direction/power, not the actual Rigidbody motion.
        float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.005f, aimSmoothingSeconds));
        smoothPull = Vector2.Lerp(smoothPull, rawPull, t);

        UpdateChargeClock();

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            ReleaseAim();
            return;
        }

        DrawTrajectory();
        UpdateChargeUI();
    }

    private void FixedUpdate()
    {
        // IMPORTANT: apply a queued shot before checking grounded state.
        // Otherwise the ground contact on the launch frame disarms a
        // perfect shot before the blob has even left the floor.
        if (launchPending)
        {
            body.linearVelocity = launchVelocityPending;
            body.WakeUp();
            launchPending = false;
            grounded = false;
            launchTime = Time.time;
            incomingVelocity = body.linearVelocity;
            return;
        }

        // Wall jumps use the same motor as slingshot launches. Other scripts
        // request a kick rather than writing the Rigidbody velocity directly.
        if (wallKickPending)
        {
            float impactSpeed = incomingVelocity.magnitude;
            body.linearVelocity = wallKickVelocityPending;
            body.WakeUp();
            wallKickPending = false;
            grounded = false;
            launchTime = Time.time;
            bounceTime = Time.time;
            shatterArmed = false;
            if (refreshAirOnWallKick)
                airShotsRemaining = extraAirShots;
            incomingVelocity = body.linearVelocity;
            Bounced?.Invoke(impactSpeed);
            onBounce?.Invoke();
            return;
        }

        bool wasGrounded = grounded;
        grounded = Time.time - launchTime >= postLaunchGroundGrace
            && body.linearVelocity.y <= 1f
            && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayers) != null;

        if (grounded)
        {
            airShotsRemaining = extraAirShots;
            shatterArmed = false;
            if (!wasGrounded)
            {
                float landingSpeed = Mathf.Max(0f, -incomingVelocity.y);
                Landed?.Invoke(landingSpeed);
                onLand?.Invoke();
            }
        }

        if (controlsEnabled)
        {
            float input = ReadHorizontal();
            Vector2 v = body.linearVelocity;

            if (grounded)
            {
                // Do not slam velocity to zero when aiming; ease to a stop.
                float target = aiming ? 0f : input * walkSpeed;
                float rate = aiming || Mathf.Abs(input) < 0.01f
                    ? groundBraking : groundAcceleration;
                v.x = Mathf.MoveTowards(v.x, target, rate * Time.fixedDeltaTime);
                body.linearVelocity = v;
            }
            else if (!aiming && Mathf.Abs(input) > 0.01f)
            {
                // Gentle steering: air control must not cancel a powerful shot.
                float candidate = v.x + input * airSteerAcceleration * Time.fixedDeltaTime;
                if (Mathf.Abs(candidate) <= maxTotalSpeed || Mathf.Abs(candidate) < Mathf.Abs(v.x))
                    v.x = candidate;
                body.linearVelocity = v;
            }
        }

        // Cached before the physics simulation resolves the next collision.
        incomingVelocity = body.linearVelocity;
    }

    private float ReadHorizontal()
    {
        Keyboard keys = Keyboard.current;
        if (keys == null) return 0f;
        float x = 0f;
        if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) x -= 1f;
        if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) x += 1f;
        return x;
    }

    private bool CanAim() => !launchPending && (grounded || airShotsRemaining > 0);

    private void BeginAim(Vector2 position)
    {
        aiming = true;
        pressPosition = position;
        rawPull = Vector2.zero;
        smoothPull = Vector2.zero;
        maxDragReachedAt = -1f;
        UpdateChargeUI();
    }

    private float MaxDragPixels() => Mathf.Max(1f, Screen.height * maxDragScreenHeightFraction);
    private float GetRawStrength() => Mathf.Clamp01(rawPull.magnitude / MaxDragPixels());
    private float GetSmoothedStrength() => Mathf.Clamp01(smoothPull.magnitude / MaxDragPixels());
    private float SecondsAtMaxDrag() => maxDragReachedAt < 0f ? -1f : Time.unscaledTime - maxDragReachedAt;

    private void UpdateChargeClock()
    {
        float strength = GetRawStrength();
        if (maxDragReachedAt < 0f && strength >= 0.985f)
            maxDragReachedAt = Time.unscaledTime;
        else if (maxDragReachedAt >= 0f && strength < 0.92f)
            maxDragReachedAt = -1f;
    }

    private float GetChargeProgress()
    {
        float elapsed = SecondsAtMaxDrag();
        if (!aiming || elapsed < 0f) return 0f;
        if (elapsed >= chargeStartDelay + chargeDuration + chargedWindow) return 0f;
        return Mathf.Clamp01((elapsed - chargeStartDelay) / Mathf.Max(0.01f, chargeDuration));
    }

    private bool IsChargedNow()
    {
        if (!aiming) return false;
        float elapsed = SecondsAtMaxDrag();
        float begin = chargeStartDelay + chargeDuration;
        return elapsed >= begin && elapsed < begin + chargedWindow;
    }

    private bool IsPerfectNow()
    {
        if (!IsChargedNow()) return false;
        float center = chargeStartDelay + chargeDuration + chargedWindow * 0.5f;
        return Mathf.Abs(SecondsAtMaxDrag() - center) <= perfectWindow * 0.5f;
    }

    // EXACT SAME function is used for the predicted arc and for the actual shot.
    private Vector2 CalculateShotVelocity()
    {
        if (smoothPull.sqrMagnitude < 0.000001f) return Vector2.zero;

        Vector2 direction = smoothPull.normalized;
        float strength = Mathf.Pow(GetSmoothedStrength(), dragPowerExponent);
        float speed = maxLaunchSpeed * strength;
        if (IsChargedNow()) speed *= 1f + chargedSpeedBonus;

        Vector2 inherited = body.linearVelocity * momentumCarry;
        // Do not allow reverse momentum to eat away an intentional launch.
        float reverseComponent = Vector2.Dot(inherited, direction);
        if (reverseComponent < 0f)
            inherited -= direction * reverseComponent;

        return Vector2.ClampMagnitude(direction * speed + inherited, maxTotalSpeed);
    }

    private void ReleaseAim()
    {
        if (rawPull.magnitude >= Screen.height * minDragScreenHeightFraction && CanAim())
        {
            bool perfect = IsPerfectNow();
            bool launchedFromGround = grounded;
            launchVelocityPending = CalculateShotVelocity();
            if (launchVelocityPending.sqrMagnitude > 0.001f)
            {
                launchPending = true;
                shatterArmed = perfect;
                shatterTime = Time.time;
                if (!launchedFromGround) airShotsRemaining--;
                Launched?.Invoke(perfect);
                onLaunch?.Invoke();
                if (perfect) onPerfectLaunch?.Invoke();
            }
        }
        CancelAim();
    }

    private void DrawTrajectory()
    {
        if (trajectory == null) return;
        if (rawPull.magnitude < Screen.height * minDragScreenHeightFraction)
        {
            trajectory.enabled = false;
            return;
        }

        trajectory.enabled = true;
        Color tint = IsPerfectNow() ? perfectColor : IsChargedNow() ? chargedColor : normalColor;
        trajectory.startColor = tint;
        trajectory.endColor = tint;

        Vector2 origin = body.worldCenterOfMass;
        Vector2 initialVelocity = CalculateShotVelocity();
        Vector2 gravity = Physics2D.gravity * body.gravityScale;
        float z = transform.position.z;
        float step = trajectorySeconds / Mathf.Max(1, trajectoryPoints - 1);
        Vector2 previous = origin;
        trajectory.positionCount = trajectoryPoints;
        trajectory.SetPosition(0, new Vector3(origin.x, origin.y, z));

        for (int i = 1; i < trajectoryPoints; i++)
        {
            float time = i * step;
            Vector2 next = origin + initialVelocity * time + 0.5f * gravity * time * time;
            Vector2 displacement = next - previous;

            if (displacement.sqrMagnitude > 0.000001f)
            {
                RaycastHit2D hit = Physics2D.CircleCast(
                    previous, trajectoryCastRadius, displacement.normalized,
                    displacement.magnitude, trajectoryCollisionLayers);
                if (hit.collider != null)
                {
                    Vector2 stop = hit.centroid;
                    trajectory.SetPosition(i, new Vector3(stop.x, stop.y, z));
                    trajectory.positionCount = i + 1;
                    return;
                }
            }
            trajectory.SetPosition(i, new Vector3(next.x, next.y, z));
            previous = next;
        }
    }

    private void UpdateChargeUI()
    {
        if (chargeBar != null)
        {
            chargeBar.gameObject.SetActive(aiming);
            if (aiming) chargeBar.value = GetChargeProgress();
        }
        if (chargeIndicator != null)
        {
            chargeIndicator.gameObject.SetActive(aiming);
            if (aiming)
                chargeIndicator.color = IsPerfectNow() ? perfectColor : IsChargedNow() ? chargedColor : normalColor;
        }
    }

    private void CancelAim()
    {
        aiming = false;
        maxDragReachedAt = -1f;
        rawPull = Vector2.zero;
        smoothPull = Vector2.zero;
        if (trajectory != null) trajectory.enabled = false;
        UpdateChargeUI();
    }

    // Called by BlobImpact whenever it hits a configured bounce surface.
    public bool TryBounce(Vector2 surfaceNormal, BounceSurface surface)
    {
        if (surface == null || Time.time - bounceTime < bounceCooldown) return false;
        Vector2 v = incomingVelocity;
        float approaching = Vector2.Dot(v, surfaceNormal);
        if (approaching >= -surface.MinimumImpactSpeed) return false;

        Vector2 reflected = Vector2.Reflect(v, surfaceNormal);
        Vector2 result = reflected * surface.SpeedRetention;
        if (result.magnitude < surface.MinimumReboundSpeed)
            result = reflected.normalized * surface.MinimumReboundSpeed;

        result = Vector2.ClampMagnitude(result, maxTotalSpeed);
        body.linearVelocity = result;
        incomingVelocity = result;
        bounceTime = Time.time;
        launchTime = Time.time;
        grounded = false;
        if (surface.RestoreAirShot) airShotsRemaining = extraAirShots;
        Bounced?.Invoke(v.magnitude);
        onBounce?.Invoke();
        return true;
    }

    // Called by BlobWallParkour while the player is sliding down a marked wall.
    // This is intentionally the only place that applies wall-slide velocity.
    public void ApplyWallSlide(float maximumFallSpeed, float outwardNormalX, float clingSpeed)
    {
        if (!controlsEnabled || grounded || launchPending || wallKickPending)
            return;

        Vector2 v = body.linearVelocity;
        v.y = Mathf.Max(v.y, -Mathf.Max(0f, maximumFallSpeed));
        // A tiny inward velocity keeps the blob against the wall as it slides.
        if (Mathf.Abs(outwardNormalX) > 0.5f)
            v.x = -Mathf.Sign(outwardNormalX) * Mathf.Max(0f, clingSpeed);
        body.linearVelocity = v;
        incomingVelocity = v;
    }

    public void RefreshAirShots()
    {
        airShotsRemaining = extraAirShots;
    }

    // Queues a predictable upward-and-away wall jump for the next physics tick.
    public bool RequestWallKick(float outwardNormalX, float horizontalSpeed,
        float upwardSpeed, bool restoreAirShot)
    {
        if (!controlsEnabled || grounded || launchPending || wallKickPending ||
            Mathf.Abs(outwardNormalX) < 0.5f)
            return false;

        CancelAim();
        wallKickVelocityPending = Vector2.ClampMagnitude(
            new Vector2(Mathf.Sign(outwardNormalX) * Mathf.Abs(horizontalSpeed),
                Mathf.Abs(upwardSpeed)), maxTotalSpeed);
        wallKickPending = true;
        refreshAirOnWallKick = restoreAirShot;
        return true;
    }

    // Perfect hits continue through the shattered obstacle instead of stopping dead.
    public void ContinueThroughShatteredObject()
    {
        body.linearVelocity = incomingVelocity;
    }

    public void ConsumeShatter() => shatterArmed = false;

    public void SetControlsEnabled(bool value)
    {
        controlsEnabled = value;
        if (!value) CancelAim();
    }

    public void SetCheckpoint(Transform value) => checkpoint = value;

    public void Respawn()
    {
        RespawnAt(checkpoint != null ? (Vector2)checkpoint.position : spawnPoint);
    }

    public void RespawnAt(Vector2 position)
    {
        CancelAim();
        launchPending = false;
        wallKickPending = false;
        shatterArmed = false;
        body.position = position;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        incomingVelocity = Vector2.zero;
        launchTime = -100f;
        grounded = false;
        airShotsRemaining = extraAirShots;
        Respawned?.Invoke();
    }

    private void OnDisable()
    {
        CancelAim();
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
