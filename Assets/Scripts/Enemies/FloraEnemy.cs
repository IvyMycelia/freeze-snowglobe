using UnityEngine;
using UnityEngine.Events;

public sealed class FloraEnemy : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform eye;
    [SerializeField] private LayerMask solidTerrain;
    [SerializeField, Min(0.1f)] private float detectionRange = 6f;
    [SerializeField, Range(20f, 360f)] private float visionArc = 150f;
    [SerializeField] private bool facesRight = true;

    [Header("Alert")]
    [SerializeField, Min(0.1f)] private float broadcastRadius = 12f;
    [SerializeField, Min(0f)] private float windupSeconds = 0.6f;
    [SerializeField, Min(0.1f)] private float cooldownSeconds = 5f;
    [SerializeField, Min(0.1f)] private float alarmDuration = 4f;
    [SerializeField] private EnemyStatus status;
    [SerializeField] private UnityEvent onWarning;
    [SerializeField] private UnityEvent onAlarm;

    private BlobSlingshot playerMotor;
    private float readyAt;
    private float windupRemaining;
    private bool warning;

    private void Awake()
    {
        if (status == null) status = GetComponent<EnemyStatus>();
        if (eye == null) eye = transform;
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
        warning = false;
        readyAt = Time.time + 1f;
    }

    private void Update()
    {
        if (status != null && status.IsFrozen) return;
        if (warning)
        {
            // A hidden player cancels the alarm before it is broadcast.
            if (!CanSeePlayer()) { warning = false; return; }
            windupRemaining -= Time.deltaTime;
            if (windupRemaining <= 0f)
            {
                warning = false;
                Broadcast();
                readyAt = Time.time + cooldownSeconds;
            }
            return;
        }
        if (Time.time < readyAt || !CanSeePlayer()) return;
        warning = true;
        windupRemaining = windupSeconds;
        onWarning?.Invoke();
    }

    private bool CanSeePlayer()
    {
        if (player == null) return false;
        Vector2 origin = eye.position;
        Vector2 toPlayer = (Vector2)player.position - origin;
        if (toPlayer.sqrMagnitude > detectionRange * detectionRange) return false;
        float angle = Vector2.Angle(facesRight ? Vector2.right : Vector2.left, toPlayer);
        if (angle > visionArc * 0.5f) return false;
        return Physics2D.Linecast(origin, player.position, solidTerrain).collider == null;
    }

    private void Broadcast()
    {
        Collider2D[] candidates = Physics2D.OverlapCircleAll(transform.position, broadcastRadius);
        foreach (Collider2D collider in candidates)
        {
            FaunaEnemy fauna = collider.GetComponentInParent<FaunaEnemy>();
            if (fauna != null)
                fauna.AlertAt(player.position, alarmDuration);
        }
        onAlarm?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, broadcastRadius);
    }
}
