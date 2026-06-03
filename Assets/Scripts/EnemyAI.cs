using UnityEngine;
using System.Collections;

public abstract class EnemyAI : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 3;
    public int attackDamage = 1;
    public float moveSpeed = 2f;
    public float detectionRange = 5f;
    public float attackRange = 0.8f;
    public float attackCooldown = 1.2f;

    [Header("Knockback")]
    public float knockbackForce = 3f;
    public float knockbackDuration = 0.15f;

    [Header("References")]
    public Animator animator;
    public AudioSource audioSource;
    public AudioClip hurtSFX;
    public AudioClip deathSFX;

    protected int currentHealth;
    protected Rigidbody2D rb;
    protected SpriteRenderer sr;
    protected Transform player;
    protected float lastAttackTime;
    protected bool isDead;

    protected enum EnemyState { Wander, Chase, Attack, Dead }
    protected EnemyState currentState = EnemyState.Wander;

    // Wander
    protected Vector2 wanderDir;
    protected float wanderTimer;
    protected float wanderDuration = 2f;
    protected float wanderPauseTimer;
    protected float wanderPauseDuration = 1f;
    protected bool isWanderPausing;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
    }

    protected virtual void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj) player = playerObj.transform;
        PickNewWanderDir();
    }

    protected virtual void Update()
    {
        if (isDead) return;
        UpdateStateMachine();
    }

    protected virtual void UpdateStateMachine()
    {
        float distToPlayer = player ? Vector2.Distance(transform.position, player.position) : Mathf.Infinity;

        switch (currentState)
        {
            case EnemyState.Wander:
                HandleWander();
                if (distToPlayer < detectionRange)
                    ChangeState(EnemyState.Chase);
                break;

            case EnemyState.Chase:
                HandleChase();
                if (distToPlayer > detectionRange * 1.2f)
                    ChangeState(EnemyState.Wander);
                else if (distToPlayer < attackRange)
                    ChangeState(EnemyState.Attack);
                break;

            case EnemyState.Attack:
                if (distToPlayer > attackRange)
                    ChangeState(EnemyState.Chase);
                else
                    HandleAttack();
                break;
        }
    }

    protected virtual void HandleWander()
    {
        if (isWanderPausing)
        {
            wanderPauseTimer -= Time.deltaTime;
            rb.linearVelocity = Vector2.zero;
            animator.SetFloat("Speed", 0);
            if (wanderPauseTimer <= 0) { isWanderPausing = false; PickNewWanderDir(); }
            return;
        }

        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0) { isWanderPausing = true; wanderPauseTimer = wanderPauseDuration; return; }

        // Raycast wall detection
        RaycastHit2D hit = Physics2D.Raycast(transform.position, wanderDir, 0.8f, LayerMask.GetMask("Wall"));
        if (hit.collider != null) PickNewWanderDir();

        rb.linearVelocity = wanderDir * moveSpeed * 0.5f;
        FlipSprite(wanderDir.x);
        animator.SetFloat("Speed", rb.linearVelocity.magnitude);
    }

    protected virtual void HandleChase()
    {
        if (player == null) return;
        Vector2 dir = (player.position - transform.position).normalized;
        rb.linearVelocity = dir * moveSpeed;
        FlipSprite(dir.x);
        animator.SetFloat("Speed", rb.linearVelocity.magnitude);
    }

    // Override in subclasses for custom attack behaviour
    protected abstract void HandleAttack();

    protected virtual void ChangeState(EnemyState newState)
    {
        currentState = newState;
        if (newState == EnemyState.Wander) PickNewWanderDir();
        if (newState != EnemyState.Chase && newState != EnemyState.Attack)
            rb.linearVelocity = Vector2.zero;
    }

    public virtual void TakeDamage(int damage, Vector2 hitDirection)
    {
        if (isDead) return;
        currentHealth -= damage;

        if (hurtSFX) audioSource?.PlayOneShot(hurtSFX);
        animator.SetTrigger("Hurt");
        StartCoroutine(ApplyKnockback(hitDirection));

        if (currentHealth <= 0) Die();
        else OnDamaged(damage, hitDirection);
    }

    // Hook for subclasses to react to damage (stagger, enrage etc)
    protected virtual void OnDamaged(int damage, Vector2 hitDirection) { }

    protected virtual void Die()
    {
        isDead = true;
        currentState = EnemyState.Dead;
        rb.linearVelocity = Vector2.zero;
        GetComponent<Collider2D>().enabled = false;
        if (deathSFX) audioSource?.PlayOneShot(deathSFX);
        animator.SetTrigger("Dead");
        Destroy(gameObject, 2f);
    }

    protected IEnumerator ApplyKnockback(Vector2 dir)
    {
        rb.linearVelocity = dir * knockbackForce;
        yield return new WaitForSeconds(knockbackDuration);
        rb.linearVelocity = Vector2.zero;
    }

    protected void FlipSprite(float xDir)
    {
        if (xDir != 0) sr.flipX = xDir < 0;
    }

    protected void PickNewWanderDir()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        wanderDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        wanderTimer = wanderDuration;
    }

    public bool IsDead()
    {
        return isDead;
    }
}