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

    [Header("Wall Following")]
    public float wallDetectDist = 0.5f;   // how close to wall before following
    public float wallFollowDist = 0.4f;   // how close to hug the wall
    public LayerMask wallLayer;

    private bool isWallFollowing;
    private Vector2 wallFollowDir;
    private float wallFollowTimer;
    private float maxWallFollowTime = 3f; // give up and try direct again after this

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
    protected bool isKnockedBack = false;

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
        if (isKnockedBack) return;
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

        Vector2 dirToPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
        float distToPlayer = Vector2.Distance(transform.position, player.position);

        // Check if direct path is clear
        RaycastHit2D directHit = Physics2D.Raycast(
            transform.position, dirToPlayer, wallDetectDist, wallLayer);

        if (directHit.collider == null)
        {
            // Path is clear — go directly to player
            isWallFollowing = false;
            wallFollowTimer = 0f;
            MoveInDirection(dirToPlayer);
            return;
        }

        // Wall ahead — start or continue wall following
        if (!isWallFollowing)
        {
            isWallFollowing = true;
            // Pick a wall follow direction — try left first, then right
            Vector2 leftDir = new Vector2(-dirToPlayer.y, dirToPlayer.x);
            Vector2 rightDir = new Vector2(dirToPlayer.y, -dirToPlayer.x);

            RaycastHit2D leftHit = Physics2D.Raycast(
                transform.position, leftDir, wallDetectDist, wallLayer);

            wallFollowDir = leftHit.collider == null ? leftDir : rightDir;
            wallFollowTimer = 0f;
        }

        wallFollowTimer += Time.deltaTime;

        // Give up wall following if stuck too long
        if (wallFollowTimer > maxWallFollowTime)
        {
            isWallFollowing = false;
            wallFollowTimer = 0f;
        }

        // Wall follow — hug the wall and slide along it
        RaycastHit2D wallHit = Physics2D.Raycast(
            transform.position, -wallFollowDir, wallFollowDist + 0.1f, wallLayer);

        if (wallHit.collider == null)
        {
            // Lost the wall — turn toward it
            wallFollowDir = new Vector2(-wallFollowDir.y, wallFollowDir.x);
        }

        // Check wall ahead in follow direction
        RaycastHit2D followHit = Physics2D.Raycast(
            transform.position, wallFollowDir, wallDetectDist, wallLayer);

        if (followHit.collider != null)
        {
            // Corner — turn away from wall
            wallFollowDir = new Vector2(wallFollowDir.y, -wallFollowDir.x);
        }

        MoveInDirection(wallFollowDir);
    }

    void MoveInDirection(Vector2 dir)
    {
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

    protected IEnumerator ApplyKnockback(Vector2 dir)
    {
        isKnockedBack = true;
        rb.linearVelocity = dir * knockbackForce;
        yield return new WaitForSeconds(knockbackDuration);
        rb.linearVelocity = Vector2.zero;
        isKnockedBack = false;
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

    // protected IEnumerator ApplyKnockback(Vector2 dir)
    // {
    //     rb.linearVelocity = dir * knockbackForce;
    //     yield return new WaitForSeconds(knockbackDuration);
    //     rb.linearVelocity = Vector2.zero;
    // }

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