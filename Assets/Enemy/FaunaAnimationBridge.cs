using UnityEngine;

// Attach to P_Fauna root. No Animator transitions / parameters required.
// FaunaEnemy still owns the sprite's facing direction while it is alive.
public sealed class FaunaAnimationBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FaunaEnemy fauna;
    [SerializeField] private EnemyStatus status;
    [SerializeField] private Shatterable shatterable;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animator State Names (Base Layer)")]
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string patrolState = "Patrol";
    [SerializeField] private string telegraphState = "Telegraph";
    [SerializeField] private string chargeState = "Charge";
    [SerializeField] private string recoverState = "Idle";
    [SerializeField] private string frozenState = "Frozen";
    [SerializeField] private string deathState = "Shatter";

    [Header("After Death Animation")]
    [Tooltip("Optional. Assign a collapsed squirrel sprite to guarantee visible remains. If blank, the last Shatter frame is kept.")]
    [SerializeField] private Sprite remainsSprite;

    private string requestedState;
    private int missingStateHash;
    private bool deathStarted;
    private bool deathFinished;

    private void Awake()
    {
        if (fauna == null) fauna = GetComponent<FaunaEnemy>();
        if (status == null) status = GetComponent<EnemyStatus>();
        if (shatterable == null) shatterable = GetComponent<Shatterable>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (animator == null)
            return;

        if (shatterable != null && shatterable.IsShattered)
        {
            HandleDeathAnimation();
            return;
        }

        if (fauna == null)
            return;

        string desired = DetermineLivingState();
        string fullPath = "Base Layer." + desired;

        if (requestedState == desired &&
            animator.GetCurrentAnimatorStateInfo(0).IsName(fullPath))
            return;

        PlayState(desired);
    }

    private void HandleDeathAnimation()
    {
        if (deathFinished)
            return;

        if (!deathStarted)
        {
            deathStarted = true;
            animator.enabled = true;
            animator.speed = 1f;

            if (!PlayState(deathState))
                FinishDeathAnimation();
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Base Layer." + deathState) &&
            !animator.IsInTransition(0) &&
            state.normalizedTime >= 1f)
        {
            FinishDeathAnimation();
        }
    }

    private void FinishDeathAnimation()
    {
        deathFinished = true;

        // Freeze the last animation frame permanently.
        // If a separate 'fallen' sprite was assigned, show that instead.
        animator.enabled = false;
        if (remainsSprite != null && spriteRenderer != null)
            spriteRenderer.sprite = remainsSprite;
    }

    private string DetermineLivingState()
    {
        if (status != null && status.IsFrozen)
            return frozenState;

        switch (fauna.CurrentMode)
        {
            case FaunaEnemy.Mode.Telegraph: return telegraphState;
            case FaunaEnemy.Mode.Charge: return chargeState;
            case FaunaEnemy.Mode.Recover: return recoverState;
            case FaunaEnemy.Mode.Investigate:
            case FaunaEnemy.Mode.Patrol:
            default: return patrolState;
        }
    }

    private bool PlayState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName))
            return false;

        string fullPath = "Base Layer." + stateName;
        int hash = Animator.StringToHash(fullPath);

        if (!animator.HasState(0, hash))
        {
            if (missingStateHash != hash)
            {
                UnityEngine.Debug.LogWarning(
                    "Fauna Animator is missing '" + stateName +
                    "' on " + name, this);
                missingStateHash = hash;
            }
            return false;
        }

        animator.Play(hash, 0, 0f);
        requestedState = stateName;
        missingStateHash = 0;
        return true;
    }
}
