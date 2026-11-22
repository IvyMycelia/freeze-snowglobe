using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class FaunaEnemy : MonoBehaviour
{
    public enum Mode { Patrol, Investigate, Telegraph, Charge, Recover }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform feetProbe;
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private EnemyStatus status;
    [SerializeField] private LayerMask solidTerrain;

    [Header("Patrol")]
    [SerializeField, Min(0f)] private float patrolSpeed = 2f;
    [SerializeField, Min(0f)] private float investigateSpeed = 3f;
    [SerializeField, Min(0.1f)] private float wallProbeDistance = 0.45f;
    [SerializeField, Min(0.1f)] private float ledgeLookAhead = 0.45f;
    [SerializeField, Min(0.1f)] private float ledgeProbeDepth = 0.9f;
    [SerializeField, Min(0f)] private float patrolTurnCooldown = 0.25f;
    [SerializeField] private bool faceRightInitially = true;

    [Header("Visual Orientation")]
    [Tooltip("OFF for the Snowglobe squirrel sprites, which are drawn facing LEFT.")]
    [SerializeField] private bool spriteFacesRightByDefault = false;

    [Header("Detection & Charge")]
    [SerializeField, Min(0.1f)] private float sightRange = 5f;
    [SerializeField, Min(0.1f)] private float chargeRange = 6f;
    [SerializeField, Min(0f)] private float windupSeconds = 0.5f;
    [SerializeField, Min(1f)] private float chargeSpeed = 11f;
    [SerializeField, Min(0.05f)] private float chargeSeconds = 0.48f;
    [SerializeField, Min(0f)] private float recoverySeconds = 0.9f;
    [SerializeField, Min(0f)] private float searchSeconds = 4f;

    [Header("Feedback (optional)")]
    [SerializeField] private UnityEvent onSpotted;
    [SerializeField] private UnityEvent onCharge;
    [SerializeField] private UnityEvent onRecovered;

    private Rigidbody2D body;
    private BlobSlingshot playerMotor;
    private Mode mode = Mode.Patrol;
    private float stateTime;
    private float alarmExpires;
    private float searchTargetX;
    private float nextPatrolTurnTime;
    private int facing;
    private bool wasFrozen;

    public Mode CurrentMode => mode;
    public int Facing => facing;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (status == null) status = GetComponent<EnemyStatus>();
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        if (feetProbe == null) feetProbe = transform;
        facing = faceRightInitially ? 1 : -1;
        body.freezeRotation = true;
        ApplySpriteFacing();
    }

    private void Start()
    {
        playerMotor = player != null ? player.GetComponent<BlobSlingshot>() : null;
        if (playerMotor == null) playerMotor = FindFirstObjectByType<BlobSlingshot>();
        if (playerMotor != null)
        {
            player = playerMotor.transform;
            playerMotor.Respawned += OnPlayerRespawned;
        }
    }

    private void OnDestroy()
    {
        if (playerMotor != null) playerMotor.Respawned -= OnPlayerRespawned;
    }

    private void OnPlayerRespawned()
    {
        alarmExpires = 0f;
        nextPatrolTurnTime = Time.time + patrolTurnCooldown;
        ChangeMode(Mode.Patrol);
        StopHorizontal();
    }

    private void FixedUpdate()
    {
        if (status != null && status.IsFrozen)
        {
            wasFrozen = true;
            body.linearVelocity = Vector2.zero;
            return;
        }

        if (wasFrozen)
        {
            wasFrozen = false;
            stateTime = Time.time;
        }

        bool canSee = CanSeePlayer();
        if (canSee && player != null)
            searchTargetX = player.position.x;

        switch (mode)
        {
            case Mode.Patrol:
                if (canSee && WithinChargeRange())
                {
                    BeginTelegraph();
                    break;
                }
                if (Time.time < alarmExpires)
                {
                    ChangeMode(Mode.Investigate);
                    break;
                }
                Patrol();
                break;

            case Mode.Investigate:
                if (canSee && WithinChargeRange())
                {
                    BeginTelegraph();
                    break;
                }
                if (Time.time >= alarmExpires ||
                    Mathf.Abs(searchTargetX - transform.position.x) < 0.25f)
                {
                    ChangeMode(Mode.Patrol);
                    StopHorizontal();
                    break;
                }

                int towardTarget = searchTargetX > transform.position.x ? 1 : -1;
                if (CanTravel(towardTarget))
                {
                    SetFacing(towardTarget);
                    SetHorizontal(towardTarget * investigateSpeed);
                }
                else
                {
                    // Don't flip back and forth when the alarm target is unreachable.
                    StopHorizontal();
                    ChangeMode(Mode.Patrol);
                    nextPatrolTurnTime = Time.time + patrolTurnCooldown;
                }
                break;

            case Mode.Telegraph:
                // Direction is deliberately locked during wind-up.
                StopHorizontal();
                if (Time.time - stateTime >= windupSeconds)
                {
                    ChangeMode(Mode.Charge);
                    onCharge?.Invoke();
                }
                break;

            case Mode.Charge:
                // Charge goes where the squirrel aimed during its telegraph.
                SetHorizontal(facing * chargeSpeed);
                if (Time.time - stateTime >= chargeSeconds || WallAhead(facing))
                    ChangeMode(Mode.Recover);
                break;

            case Mode.Recover:
                StopHorizontal();
                if (Time.time - stateTime >= recoverySeconds)
                {
                    onRecovered?.Invoke();
                    ChangeMode(Mode.Patrol);
                    nextPatrolTurnTime = Time.time + patrolTurnCooldown;
                }
                break;
        }
    }

    private void LateUpdate()
    {
        // One owner of facing: the AI. The Animator should animate Sprite only.
        ApplySpriteFacing();
    }

    private void ApplySpriteFacing()
    {
        if (sprite == null) return;
        sprite.flipX = spriteFacesRightByDefault ? facing < 0 : facing > 0;
    }

    private void SetFacing(int direction)
    {
        if (direction == 0) return;
        facing = direction > 0 ? 1 : -1;
    }

    private bool WithinChargeRange()
    {
        return player != null &&
               Mathf.Abs(player.position.x - transform.position.x) <= chargeRange;
    }

    public void AlertAt(Vector2 targetLocation, float duration)
    {
        searchTargetX = targetLocation.x;
        alarmExpires = Mathf.Max(
            alarmExpires,
            Time.time + (duration > 0f ? duration : searchSeconds)
        );
        if (mode == Mode.Patrol) ChangeMode(Mode.Investigate);
    }

    private void BeginTelegraph()
    {
        if (player != null)
        {
            float deltaX = player.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) > 0.08f)
                SetFacing(deltaX > 0f ? 1 : -1);
        }

        ChangeMode(Mode.Telegraph);
        StopHorizontal();
        onSpotted?.Invoke();
    }

    private bool CanSeePlayer()
    {
        if (player == null) return false;
        Vector2 origin = body.worldCenterOfMass;
        Vector2 goal = player.position;
        if ((goal - origin).sqrMagnitude > sightRange * sightRange) return false;
        return Physics2D.Linecast(origin, goal, solidTerrain).collider == null;
    }

    private void Patrol()
    {
        if (CanTravel(facing))
        {
            SetHorizontal(facing * patrolSpeed);
            return;
        }

        StopHorizontal();
        if (Time.time < nextPatrolTurnTime) return;

        // Only reverse if the opposite direction is actually navigable.
        // Otherwise wait instead of flickering between left and right.
        int opposite = -facing;
        if (CanTravel(opposite))
            SetFacing(opposite);

        nextPatrolTurnTime = Time.time + patrolTurnCooldown;
    }

    private bool CanTravel(int direction)
    {
        return !WallAhead(direction) && GroundAhead(direction);
    }

    private bool WallAhead(int direction)
    {
        Vector2 origin = body.worldCenterOfMass;
        return Physics2D.Raycast(
            origin,
            Vector2.right * direction,
            wallProbeDistance,
            solidTerrain
        ).collider != null;
    }

    private bool GroundAhead(int direction)
    {
        Vector2 origin = feetProbe.position;
        origin.x += direction * ledgeLookAhead;
        origin.y += 0.12f;
        return Physics2D.Raycast(
            origin,
            Vector2.down,
            ledgeProbeDepth,
            solidTerrain
        ).collider != null;
    }

    private void SetHorizontal(float speed)
    {
        body.linearVelocity = new Vector2(speed, body.linearVelocity.y);
    }

    private void StopHorizontal()
    {
        if (body == null) return;
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void ChangeMode(Mode next)
    {
        if (mode == next) return;
        mode = next;
        stateTime = Time.time;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        Transform probe = feetProbe != null ? feetProbe : transform;
        int dir = Application.isPlaying ? facing : (faceRightInitially ? 1 : -1);
        Vector3 start = probe.position + Vector3.right * (dir * ledgeLookAhead) + Vector3.up * 0.12f;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(start, start + Vector3.down * ledgeProbeDepth);
    }
}
