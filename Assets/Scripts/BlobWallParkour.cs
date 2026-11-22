using UnityEngine;
using UnityEngine.InputSystem;
using Vector2 = UnityEngine.Vector2;

// Add to Player root, beside BlobSlingshot and BlobImpact.
// Reads Space for manual wall grip and quick-release jumps. No Input Actions needed.
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

    [Header("Optional Visuals")]
    [Tooltip("Put a looping, Play On Awake OFF Particle System on Player, outside VisualRoot.")]
    [SerializeField] private ParticleSystem wallSlideParticles;
    [SerializeField] private ParticleSystem wallKickBurst;
    [SerializeField] private Animator blobAnimator;

    public bool IsWallSliding { get; private set; }
    // +1 = wall on player's LEFT, -1 = wall on player's RIGHT.
    public float WallOutwardNormalX => wallNormalX;

    private BlobSlingshot motor;
    private Rigidbody2D body;
    private WallSlideSurface contactedWall;
    private Vector2 contactPoint;
    private float wallNormalX;
    private float lastContactTime = -100f;
    private float pressedAt = -100f;
    private float bufferedTapExpiresAt = -100f;
    private float lastKickAt = -100f;
    private bool pressActive;
    private bool hasWallSlideParam;
    private bool hasWallKickTrigger;

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
        if (wallSlideParticles != null)
            wallSlideParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

        // Forgives a quick tap just BEFORE reaching the wall.
        if (Time.time <= bufferedTapExpiresAt && !keys.spaceKey.isPressed)
        {
            if (TryKick()) return;
        }

        bool holdingSpace = keys.spaceKey.isPressed;
        bool fallingOrNearApex = body.linearVelocity.y <= slideStartsBelowVerticalSpeed;

        if (holdingSpace && fallingOrNearApex)
        {
            if (!IsWallSliding && contactedWall.RestoreAirShot)
                motor.RefreshAirShots();

            motor.ApplyWallSlide(contactedWall.MaximumSlideSpeed,
                wallNormalX, contactedWall.InwardClingSpeed);
            SetSliding(true);
            if (wallSlideParticles != null)
            {
                wallSlideParticles.transform.position =
                    contactPoint + new Vector2(wallNormalX * 0.04f, -0.06f);
            }
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
        if (!motor.RequestWallKick(normalX,
                wall.KickHorizontalSpeed, wall.KickVerticalSpeed,
                wall.RestoreAirShot))
            return false;

        lastKickAt = Time.time;
        bufferedTapExpiresAt = -100f;
        lastContactTime = -100f; // Prevent a second kick from the same contact.
        contactedWall = null;
        SetSliding(false);

        if (wallKickBurst != null)
        {
            wallKickBurst.transform.position = contactPoint;
            wallKickBurst.Emit(12);
        }
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
        if (wall != null && wall == contactedWall)
        {
            contactedWall = null;
            lastContactTime = -100f;
            SetSliding(false);
        }
    }

    private void SetSliding(bool sliding)
    {
        if (IsWallSliding == sliding) return;
        IsWallSliding = sliding;
        if (blobAnimator != null && hasWallSlideParam)
            blobAnimator.SetBool("WallSliding", sliding);

        if (wallSlideParticles == null) return;
        if (sliding)
        {
            if (!wallSlideParticles.isPlaying)
                wallSlideParticles.Play();
        }
        else
        {
            wallSlideParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void ResetWallState()
    {
        contactedWall = null;
        lastContactTime = -100f;
        pressedAt = -100f;
        bufferedTapExpiresAt = -100f;
        lastKickAt = -100f;
        wallNormalX = 0f;
        pressActive = false;
        SetSliding(false);
    }
}
