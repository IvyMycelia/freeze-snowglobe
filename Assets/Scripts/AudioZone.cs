using UnityEngine;

/// <summary>
/// A region that selects music and ambience. Handles respawn teleports reliably
/// by checking the player's current position, not only trigger-enter events.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class SnowglobeAudioZone : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip music;
    [SerializeField] private AudioClip ambience;
    [SerializeField] private int priority = 0;
    [Tooltip("-1 uses the manager's crossfade duration")]
    [SerializeField] private float fadeSeconds = -1f;

    [Header("Player")]
    [SerializeField] private Transform player;

    public AudioClip Music => music;
    public AudioClip Ambience => ambience;
    public int Priority => priority;
    public float FadeSeconds => fadeSeconds;

    private Collider2D area;
    private Rigidbody2D playerBody;
    private bool isInside;

    private void Awake()
    {
        area = GetComponent<Collider2D>();
        area.isTrigger = true;
    }

    private void Start()
    {
        FindPlayerIfNeeded();
        CheckPlayerInside();
    }

    private void Update()
    {
        FindPlayerIfNeeded();
        CheckPlayerInside();
    }

    private void FindPlayerIfNeeded()
    {
        if (player != null) return;
        BlobSlingshot blob = FindFirstObjectByType<BlobSlingshot>();
        if (blob == null) return;
        player = blob.transform;
        playerBody = blob.GetComponent<Rigidbody2D>();
    }

    private void CheckPlayerInside()
    {
        if (area == null || player == null) return;
        if (playerBody == null) playerBody = player.GetComponent<Rigidbody2D>();

        Vector2 point = playerBody != null
            ? playerBody.worldCenterOfMass
            : (Vector2)player.position;

        bool nowInside = area.OverlapPoint(point);
        if (nowInside == isInside) return;
        isInside = nowInside;

        SnowglobeZoneAudioManager manager = SnowglobeZoneAudioManager.Instance;
        if (manager == null) return;
        if (isInside) manager.EnterZone(this);
        else manager.ExitZone(this);
    }

    private void OnDisable()
    {
        if (!isInside) return;
        isInside = false;
        if (SnowglobeZoneAudioManager.Instance != null)
            SnowglobeZoneAudioManager.Instance.ExitZone(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        // Select the Collider2D and use Edit Collider for precise zone boundaries.
    }
}
