using UnityEngine;
using UnityEngine.Events;

// Optional on each enemy. Gives future stasis targeting one common API.
// Leave other enemy components in place; they check IsFrozen before acting.
public sealed class EnemyStatus : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D physicsBody;
    [SerializeField] private UnityEvent onFrozen;
    [SerializeField] private UnityEvent onThawed;

    private float originalGravity;
    private float originalAnimationSpeed;
    private Vector2 savedVelocity;
    private bool frozen;

    public bool IsFrozen => frozen;

    private void Awake()
    {
        if (physicsBody == null) physicsBody = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public void SetFrozen(bool value)
    {
        if (frozen == value) return;
        frozen = value;

        if (physicsBody != null)
        {
            if (value)
            {
                originalGravity = physicsBody.gravityScale;
                savedVelocity = physicsBody.linearVelocity;
                physicsBody.gravityScale = 0f;
                physicsBody.linearVelocity = Vector2.zero;
            }
            else
            {
                physicsBody.gravityScale = originalGravity;
                physicsBody.linearVelocity = savedVelocity;
            }
        }

        if (animator != null)
        {
            if (value)
            {
                originalAnimationSpeed = animator.speed;
                animator.speed = 0f;
            }
            else animator.speed = originalAnimationSpeed;
        }

        if (value) onFrozen?.Invoke();
        else onThawed?.Invoke();
    }
}
