using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

// Attach to Player root. Assign VisualRoot (not the Rigidbody/Player root).
// Sprite frame animations belong on the BlobSprite child and will not fight this script.
[RequireComponent(typeof(BlobSlingshot))]
public class BlobFeel : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator blobAnimator;
    [SerializeField] private SpriteRenderer facingSprite;
    [SerializeField, Min(0.05f)] private float facingSpeedThreshold = 0.25f;

    [Header("Squash / Stretch")]
    [SerializeField] private float aimSquash = 0.16f;
    [SerializeField] private float flyingStretch = 0.10f;
    [SerializeField] private float springStrength = 110f;
    [SerializeField] private float springDamping = 13f;
    [SerializeField] private float visualSmoothing = 0.055f;
    [SerializeField] private float runningTiltDegrees = 8f;
    [SerializeField] private float flyingTiltDegrees = 14f;

    private BlobSlingshot slingshot;
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private float spring;
    private float springSpeed;
    private float visualAngle;
    private float visualAngleVelocity;

    private bool hasGrounded;
    private bool hasAiming;
    private bool hasSpeed;
    private bool hasVerticalSpeed;
    private bool hasCharged;
    private bool hasPerfect;
    private bool hasScampering;
    private BlobWallParkour wallParkour;

    private void Awake()
    {
        slingshot = GetComponent<BlobSlingshot>();
        if (visualRoot == null)
        {
            UnityEngine.Debug.LogWarning("BlobFeel: VisualRoot not assigned; visual deformation is disabled.", this);
            enabled = false;
            return;
        }

        originalScale = visualRoot.localScale;
        originalRotation = visualRoot.localRotation;
        if (facingSprite == null)
            facingSprite = visualRoot.GetComponentInChildren<SpriteRenderer>();
        if (blobAnimator == null)
            blobAnimator = visualRoot.GetComponentInChildren<Animator>();
        wallParkour = GetComponent<BlobWallParkour>();
        CacheAnimatorParameters();
    }

    private void OnEnable()
    {
        if (slingshot == null) slingshot = GetComponent<BlobSlingshot>();
        slingshot.Launched += OnLaunch;
        slingshot.Landed += OnLand;
        slingshot.Bounced += OnBounce;
        slingshot.Respawned += ResetVisual;
    }

    private void OnDisable()
    {
        if (slingshot != null)
        {
            slingshot.Launched -= OnLaunch;
            slingshot.Landed -= OnLand;
            slingshot.Bounced -= OnBounce;
            slingshot.Respawned -= ResetVisual;
        }
        if (visualRoot != null)
        {
            visualRoot.localScale = originalScale;
            visualRoot.localRotation = originalRotation;
        }
    }

    private void Update()
    {
        if (visualRoot == null) return;
        float dt = Mathf.Min(0.05f, Time.deltaTime);
        if (dt <= 0f) return;

        // A damped spring gives the blob follow-through after launches / impacts.
        springSpeed += (-springStrength * spring - springDamping * springSpeed) * dt;
        spring += springSpeed * dt;

        Vector2 velocity = slingshot.Velocity;
        if (facingSprite != null)
        {
            if (wallParkour != null && wallParkour.IsWallSliding)
                facingSprite.flipX = wallParkour.WallOutwardNormalX > 0f;
            else if (Mathf.Abs(velocity.x) > facingSpeedThreshold && !slingshot.IsAiming)
                facingSprite.flipX = velocity.x < 0f;
        }
        float aim = slingshot.IsAiming ? -aimSquash * slingshot.AimStrength : 0f;
        float flying = slingshot.IsGrounded ? 0f : flyingStretch * Mathf.Clamp01(velocity.magnitude / 23f);
        float stretch = Mathf.Clamp(aim + flying + spring, -0.32f, 0.32f);

        Vector3 targetScale = Vector3.Scale(originalScale,
            new Vector3(1f - stretch * 0.65f, 1f + stretch, 1f));
        float blend = 1f - Mathf.Exp(-dt / Mathf.Max(0.005f, visualSmoothing));
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, blend);

        float maxTilt = slingshot.IsGrounded ? runningTiltDegrees : flyingTiltDegrees;
        float angle = Mathf.Clamp(-velocity.x * 0.65f, -maxTilt, maxTilt);
        if (slingshot.IsAiming) angle *= 0.25f;
        visualAngle = Mathf.SmoothDampAngle(visualAngle, angle,
            ref visualAngleVelocity, 0.10f);
        visualRoot.localRotation = originalRotation * Quaternion.Euler(0f, 0f, visualAngle);

        // Animator parameters are optional. You can replace sprite clips freely.
        if (blobAnimator != null)
        {
            if (hasGrounded) blobAnimator.SetBool("Grounded", slingshot.IsGrounded);
            if (hasAiming) blobAnimator.SetBool("Aiming", slingshot.IsAiming);
            if (hasSpeed) blobAnimator.SetFloat("Speed", Mathf.Abs(velocity.x));
            if (hasVerticalSpeed) blobAnimator.SetFloat("VerticalSpeed", velocity.y);
            if (hasCharged) blobAnimator.SetBool("Charged", slingshot.IsCharged);
            if (hasPerfect) blobAnimator.SetBool("PerfectReady", slingshot.IsPerfectReady);
            if (hasScampering) blobAnimator.SetBool("Scampering",
                slingshot.IsGrounded && !slingshot.IsAiming &&
                Mathf.Abs(velocity.x) > facingSpeedThreshold);
        }
    }

    private void CacheAnimatorParameters()
    {
        if (blobAnimator == null) return;
        foreach (AnimatorControllerParameter p in blobAnimator.parameters)
        {
            if (p.name == "Grounded" && p.type == AnimatorControllerParameterType.Bool) hasGrounded = true;
            if (p.name == "Aiming" && p.type == AnimatorControllerParameterType.Bool) hasAiming = true;
            if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
            if (p.name == "VerticalSpeed" && p.type == AnimatorControllerParameterType.Float) hasVerticalSpeed = true;
            if (p.name == "Charged" && p.type == AnimatorControllerParameterType.Bool) hasCharged = true;
            if (p.name == "PerfectReady" && p.type == AnimatorControllerParameterType.Bool) hasPerfect = true;
            if (p.name == "Scampering" && p.type == AnimatorControllerParameterType.Bool) hasScampering = true;
        }
    }

    private void OnLaunch(bool perfect)
    {
        springSpeed += perfect ? 13f : 10f;
    }

    private void OnLand(float speed)
    {
        springSpeed -= Mathf.Clamp(speed * 0.50f, 2f, 11f);
    }

    private void OnBounce(float speed)
    {
        springSpeed += Mathf.Clamp(speed * 0.40f, 3f, 12f);
    }

    private void ResetVisual()
    {
        spring = 0f;
        springSpeed = 0f;
        visualAngle = 0f;
        visualAngleVelocity = 0f;
        visualRoot.localScale = originalScale;
        visualRoot.localRotation = originalRotation;
    }
}
