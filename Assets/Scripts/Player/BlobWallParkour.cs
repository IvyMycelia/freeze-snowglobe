using UnityEngine;
using UnityEngine.InputSystem;
using Vector2 = UnityEngine.Vector2;

// Space reading for manual wall grip and quick-release jumps.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BlobSlingshot))]
[DefaultExecutionOrder(100)]
public class BlobWallParkour : MonoBehaviour
{
    [Header("Space Timing")]
    [SerializeField, Range(0.08f, 0.35f)] private float quickTapSeconds = 0.18f;
    [SerializeField, Range(0f, 0.25f)] private float tapBeforeImpactBuffer = 0.13f;
    [SerializeField, Range(0.05f, 0.5f)] private float kickCooldown = 0.18f;
    [SerializeField] private float slideStartsBelowVerticalSpeed = 0.35f;

    [Header("Particle Prefabs (assign assets from Project window)")]
    [Tooltip("Looping ParticleSystem prefab. Instantiated in the scene when the blob slides; it is NOT parented to Player.")]
    [SerializeField] private ParticleSystem wallSlidePrefab;
    [Tooltip("Non-looping ParticleSystem prefab. Instantiated at the wall contact for each kick.")]
    [SerializeField] private ParticleSystem wallKickPrefab;
    [SerializeField, Min(1)] private int kickParticleCount = 12;

    [Header("Optional Animator")]
    [SerializeField] private Animator blobAnimator;

    public bool IsWallSliding { get; private set; }
    // +1 = wall on player's LEFT, -1 = wall on player's RIGHT.
    public float WallOutwardNormalX => wallNormalX;

    private BlobSlingshot motor;
    private Rigidbody2D body;
    private WallSlideSurface contactedWall;
    // A restoring wall can grant air shots only once per continuous contact, otherwise overpowered :p
    private WallSlideSurface wallThatRestoredThisContact;
    private Vector2 contactPoint;
    private float wallNormalX;
    private float lastContactTime = -100f;
    private float pressedAt = -100f;
    private float bufferedTapExpiresAt = -100f;
    private float lastKickAt = -100f;
    private bool pressActive;
    private bool hasWallSlideParam;
    private bool hasWallKickTrigger;

    // Scene instance rather than the prefab asset. Reused whilst this Player is enabled.
    private ParticleSystem activeWallSlide;

    private void Awake()
    {
        motor = GetComponent<BlobSlingshot>();
        body = GetComponent<Rigidbody2D>();
        if (blobAnimator == null)
            blobAnimator = GetComponentInChildren<Animator>();

        if (blobAnimator != null)
        {
            foreach (AnimatorControllerParameter parameter in blobAnimator.parameters)
            {
                if (parameter.name == "WallSliding" &&
                    parameter.type == AnimatorControllerParameterType.Bool)
                    hasWallSlideParam = true;
                if (parameter.name == "WallKick" &&
                    parameter.type == AnimatorControllerParameterType.Trigger)
                    hasWallKickTrigger = true;
            }
        }

    }

    private void OnEnable()
    {
        if (motor == null) motor = GetComponent<BlobSlingshot>();
        motor.Respawned += ResetWallState;
    }

    private void OnDisable()
    {
        if (motor != null) motor.Respawned -= ResetWallState;
        ResetWallState();
        DestroySlideInstance();
    }

    private void Update()
    {
        Keyboard keys = Keyboard.current;
        if (keys == null || !motor.ControlsEnabled)
        {
            SetSliding(false);
            pressActive = false;
            bufferedTapExpiresAt = -100f;
            return;
        }

        if (keys.spaceKey.wasPressedThisFrame)
        {
            pressActive = true;
            pressedAt = Time.unscaledTime;
        }

        if (keys.spaceKey.wasReleasedThisFrame)
        {
            bool quickTap = pressActive &&
                Time.unscaledTime - pressedAt <= quickTapSeconds;
            pressActive = false;
            if (quickTap && !motor.IsGrounded)
            {
                if (!TryKick())
                    bufferedTapExpiresAt = Time.time + tapBeforeImpactBuffer;
            }
            else
            {
                bufferedTapExpiresAt = -100f;
            }
            SetSliding(false);
        }
    }

    private void FixedUpdate()
    {
        Keyboard keys = Keyboard.current;
        bool canUseWall = keys != null && motor.ControlsEnabled &&
            !motor.IsGrounded && ContactIsFresh() &&
            Time.time - lastKickAt >= kickCooldown;

        if (!canUseWall)
        {
            SetSliding(false);
            return;
        }

        // Forgives a quick tap just BEFORE reaching the wall (Coyote time!!).
        if (Time.time <= bufferedTapExpiresAt && !keys.spaceKey.isPressed)
        {
            if (TryKick()) return;
        }

        bool holdingSpace = keys.spaceKey.isPressed;
        bool fallingOrNearApex = body.linearVelocity.y <= slideStartsBelowVerticalSpeed;

        if (holdingSpace && fallingOrNearApex)
        {
            motor.ApplyWallSlide(contactedWall.MaximumSlideSpeed,
                wallNormalX, contactedWall.InwardClingSpeed);
            SetSliding(true);
            if (activeWallSlide != null)
                activeWallSlide.transform.position = SlideEffectPosition();
        }
        else
        {
            SetSliding(false);
        }
    }

    private bool TryKick()
    {
        if (motor.IsGrounded || !ContactIsFresh() ||
            Time.time - lastKickAt < kickCooldown)
            return false;

        WallSlideSurface wall = contactedWall;
        float normalX = wallNormalX;
        // Recharging happens on initial contact, not again on every kick.
        if (!motor.RequestWallKick(normalX,
                wall.KickHorizontalSpeed, wall.KickVerticalSpeed,
                false))
            return false;

        lastKickAt = Time.time;
        bufferedTapExpiresAt = -100f;
        lastContactTime = -100f; // Prevent a second kick from the same contact.
        contactedWall = null;
        SetSliding(false);

        SpawnKickBurst();
        if (blobAnimator != null && hasWallKickTrigger)
            blobAnimator.SetTrigger("WallKick");
        return true;
    }

    private bool ContactIsFresh()
    {
        return contactedWall != null &&
            Time.time - lastContactTime <= Time.fixedDeltaTime * 2.5f;
    }

    private void OnCollisionEnter2D(Collision2D collision) => RecordWall(collision);
    private void OnCollisionStay2D(Collision2D collision) => RecordWall(collision);

    private void RecordWall(Collision2D collision)
    {
        WallSlideSurface wall = collision.collider.GetComponentInParent<WallSlideSurface>();
        if (wall == null)
            wall = collision.gameObject.GetComponentInParent<WallSlideSurface>();
        if (wall == null) return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D point = collision.GetContact(i);
            // Only side contacts; never treat the floor or ceiling as a wall.
            if (Mathf.Abs(point.normal.x) < 0.7f ||
                Mathf.Abs(point.normal.y) > 0.55f)
                continue;

            contactedWall = wall;
            // Being near/holding on a wall does NOT restore charges.
            // Only this wall's explicit setting permits a one-time refill
            // for this continuous contact.
            if (!motor.IsGrounded && wall.RestoreAirShot &&
                wallThatRestoredThisContact != wall)
            {
                motor.RefreshAirShots();
                wallThatRestoredThisContact = wall;
            }
            contactPoint = point.point;
            float relativeX = body.worldCenterOfMass.x - point.point.x;
            wallNormalX = Mathf.Abs(relativeX) > 0.05f
                ? Mathf.Sign(relativeX)
                : Mathf.Sign(point.normal.x);
            lastContactTime = Time.time;
            break;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        WallSlideSurface wall = collision.collider.GetComponentInParent<WallSlideSurface>();
        if (wall == null)
            wall = collision.gameObject.GetComponentInParent<WallSlideSurface>();
        if (wall == null) return;

        if (wall == wallThatRestoredThisContact)
            wallThatRestoredThisContact = null;

        if (wall == contactedWall)
        {
            contactedWall = null;
            lastContactTime = -100f;
            SetSliding(false);
        }
    }

    private Vector3 SlideEffectPosition()
    {
        Vector2 p = contactPoint + new Vector2(wallNormalX * 0.04f, -0.06f);
        return new Vector3(p.x, p.y, transform.position.z);
    }

    private void SetSliding(bool sliding)
    {
        if (IsWallSliding == sliding) return;
        IsWallSliding = sliding;

        if (blobAnimator != null && hasWallSlideParam)
            blobAnimator.SetBool("WallSliding", sliding);

        if (sliding)
        {
            if (wallSlidePrefab == null) return;

            // Instantiate once, then reuse. The clone stays at the scene root,
            // not under Player, and older particles remain in world space.
            if (activeWallSlide == null)
            {
                activeWallSlide = Instantiate(
                    wallSlidePrefab, SlideEffectPosition(), Quaternion.identity);
            }

            activeWallSlide.transform.position = SlideEffectPosition();
            if (!activeWallSlide.isPlaying)
                activeWallSlide.Play(true);
        }
        else if (activeWallSlide != null)
        {
            // Existing particles finish naturally; no more are emitted.
            activeWallSlide.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void SpawnKickBurst()
    {
        if (wallKickPrefab == null) return;

        ParticleSystem burst = Instantiate(
            wallKickPrefab,
            new Vector3(contactPoint.x, contactPoint.y, transform.position.z),
            Quaternion.identity);

        burst.Play(true);
        burst.Emit(kickParticleCount);

        // Destroy this scene clone after its particles have finished.
        var main = burst.main;
        float life = main.startDelay.constantMax +
                     main.duration + main.startLifetime.constantMax + 0.5f;
        Destroy(burst.gameObject, Mathf.Max(0.5f, life));
    }

    private void DestroySlideInstance()
    {
        if (activeWallSlide == null) return;
        activeWallSlide.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Destroy(activeWallSlide.gameObject);
        activeWallSlide = null;
    }

    private void ResetWallState()
    {
        contactedWall = null;
        wallThatRestoredThisContact = null;
        lastContactTime = -100f;
        pressedAt = -100f;
        bufferedTapExpiresAt = -100f;
        lastKickAt = -100f;
        wallNormalX = 0f;
        pressActive = false;
        SetSliding(false);
    }
}
