using UnityEngine;
using System.Collections;

public class ScarabAI : EnemyAI
{
    [Header("Scarab — Ranges")]
    public float chaseRange = 8f;
    public float lungeRange = 4f;

    [Header("Scarab — Lunge")]
    public float lungeForce = 10f;
    public float lungeWindupTime = 0.4f;
    public float attackCoolDown = 2.5f;

    private enum AttackPhase { Ready, Windup, Lunge, Cooldown }
    private AttackPhase attackPhase = AttackPhase.Ready;
    private Coroutine attackCoroutine;

    protected override void Awake()
    {
        base.Awake();
        maxHealth = 2;
        moveSpeed = 3.5f;
        detectionRange = chaseRange;
        attackRange = lungeRange;
    }

    protected override void UpdateStateMachine()
    {
        if (isDead) return;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        switch (currentState)
        {
            case EnemyState.Wander:
                HandleWander();
                animator.SetBool("IsChasing", false);
                if (dist < chaseRange)
                    ChangeState(EnemyState.Chase);
                break;

            case EnemyState.Chase:
                // Only move and check transitions when not attacking
                if (attackPhase == AttackPhase.Ready)
                {
                    HandleChase();
                    animator.SetBool("IsChasing", true);
                    if (dist > chaseRange * 1.2f)
                        ChangeState(EnemyState.Wander);
                    else if (dist < lungeRange)
                        ChangeState(EnemyState.Attack);
                }
                break;

            case EnemyState.Attack:
                // Never touch rb or change state here — coroutine owns everything
                // Only trigger attack if ready
                if (attackPhase == AttackPhase.Ready)
                    HandleAttack();
                break;
        }
    }

    protected override void HandleAttack()
    {
        if (attackPhase != AttackPhase.Ready) return;
        attackCoroutine = StartCoroutine(DoWindupAndLunge());
    }

    IEnumerator DoWindupAndLunge()
    {
        attackPhase = AttackPhase.Windup;
        rb.linearVelocity = Vector2.zero;

        Vector2 targetPos = player.position;
        Vector2 lungeDir = (targetPos - rb.position).normalized;
        FlipSprite(lungeDir.x);

        Debug.Log($"[Scarab] Windup start — pos:{rb.position} target:{targetPos} dir:{lungeDir}");

        float windupTimer = 0f;
        while (windupTimer < lungeWindupTime)
        {
            float pulse = Mathf.Abs(Mathf.Sin(windupTimer * 15f));
            sr.color = Color.Lerp(Color.white, Color.red, pulse);
            windupTimer += Time.deltaTime;
            yield return null;
        }
        sr.color = Color.white;

        attackPhase = AttackPhase.Lunge;
        animator.SetBool("IsChasing", true);

        Debug.Log($"[Scarab] Lunge start — pos:{rb.position} velocity about to set:{lungeDir * lungeForce}");

        float timeout = 2f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            rb.linearVelocity = lungeDir * lungeForce;

            float dot = Vector2.Dot(targetPos - rb.position, lungeDir);
            Debug.Log($"[Scarab] Lunge frame — pos:{rb.position} vel:{rb.linearVelocity} dot:{dot}");

            if (dot < 0f)
            {
                Debug.Log("[Scarab] Passed target — stopping");
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;

            // Check AFTER yield — position has actually updated now
            Collider2D hit = Physics2D.OverlapCircle(
                transform.position, 0.6f,
                LayerMask.GetMask("Player"));
            if (hit != null && hit.GetComponent<PlayerController>())
            {
                Debug.Log("[Scarab] Hit player");
                hit.GetComponentInParent<PlayerController>()?.TakeDamage(attackDamage);
                break;
            }
        }

        Debug.Log($"[Scarab] Lunge ended — pos:{rb.position}");
        rb.linearVelocity = Vector2.zero;

        attackPhase = AttackPhase.Cooldown;
        animator.SetBool("IsChasing", false);

        rb.linearVelocity = Vector2.zero;
        yield return StartCoroutine(AttackCooldown());

        attackPhase = AttackPhase.Ready;
        ChangeState(EnemyState.Chase);
    }

    protected override void OnDamaged(int damage, Vector2 hitDirection)
    {
        if (attackPhase == AttackPhase.Windup || attackPhase == AttackPhase.Lunge)
        {
            if (attackCoroutine != null) StopCoroutine(attackCoroutine);
            rb.linearVelocity = Vector2.zero;
            sr.color = Color.white;
            animator.SetTrigger("Hurt");

            // Start cooldown coroutine instead of going straight to Chase
            attackCoroutine = StartCoroutine(AttackCooldown());
        }
    }

    IEnumerator AttackCooldown()
    {
        attackPhase = AttackPhase.Cooldown;
        animator.SetBool("IsChasing", false);

        float cooldownTimer = attackCoolDown;
        while (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            yield return null;
        }

        attackPhase = AttackPhase.Ready;
        ChangeState(EnemyState.Chase);
    }

    protected override void Die()
    {
        if (attackCoroutine != null) StopCoroutine(attackCoroutine);
        sr.color = Color.white;
        animator.SetBool("IsChasing", false);
        base.Die();
    }

}