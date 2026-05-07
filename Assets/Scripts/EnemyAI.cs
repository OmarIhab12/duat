using UnityEngine;

// ─────────────────────────────────────────────
//  DUAT — EnemyAI.cs  (v2)
//  Attach to any enemy GameObject.
//  Requires: Rigidbody2D, Animator, SpriteRenderer,
//  Collider2D, AudioSource on the same GameObject.
//
//  States:   Wander → Chase → Attack
//                        ↓ (3 fast hits)
//                     Stagger → Chase (+speed)
//                        ↓ (damaged + player gone)
//                     Healing → Idle (healed)
//                        ↓ (hp = 0 anywhere)
//                      Dead
//
//  Parallel: Enrage timer runs independently,
//            boosting stats every ~60s (20% chance).
// ─────────────────────────────────────────────

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(AudioSource))]
public class EnemyAI : MonoBehaviour
{
    // ══════════════════════════════════════════
    //  INSPECTOR FIELDS
    // ══════════════════════════════════════════

    [Header("Base Stats")]
    [SerializeField] private int maxHealth = 6;
    [SerializeField] private float baseMoveSpeed = 2f;
    [SerializeField] private int baseAttackDamage = 1;
    [SerializeField] private float baseDetectRange = 5f;
    [SerializeField] private float baseAttackRange = 0.6f;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float attackDelay = 0.3f;  // delay before damage applied

    [Header("Stagger")]
    [SerializeField] private int staggerHitCount = 3;     // hits needed to stagger
    [SerializeField] private float staggerWindow = 1.5f;  // time window for combo hits
    [SerializeField] private float staggerDuration = 1.2f;  // how long stagger lasts
    [SerializeField] private float staggerSpeedBoost = 0.3f;  // speed multiplier after stagger

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 3f;
    [SerializeField] private float knockbackDuration = 0.15f;

    [Header("Wandering")]
    [SerializeField] private float wanderSpeed = 1f;    // 50% of chase speed
    [SerializeField] private float wanderDirDuration = 1.5f;  // seconds per direction
    [SerializeField] private float wanderRayDistance = 1.5f;  // raycast ahead distance
    [SerializeField] private int wanderMaxAttempts = 5;     // max direction retries
    [SerializeField] private float wanderPausMin = 1f;    // min pause between moves
    [SerializeField] private float wanderPauseMax = 2f;    // max pause between moves
    [SerializeField] private LayerMask wallLayer;             // assign Wall layer in inspector

    [Header("Healing")]
    [SerializeField] private float healAmount = 0.5f;  // 50% of maxHealth per heal
    [SerializeField] private float healDuration = 2f;    // time per heal session
    [SerializeField] private int maxHealSessions = 2;     // max heals per retreat

    [Header("Enrage")]
    [SerializeField] private float enrageCheckInterval = 60f; // seconds between rolls
    [SerializeField] private float enrageChance = 0.2f; // 20% per roll
    [SerializeField] private float enrageRoundDelay = 60f;  // don't enrage before this
    [SerializeField] private float maxPowerDuration = 20f;
    [SerializeField] private float fadingRageDuration = 20f;
    [SerializeField] private float cooldownDuration = 20f;
    // Max power stat multipliers
    [SerializeField] private float enrageSpeedMult = 1.75f;
    [SerializeField] private float enrageDamageMult = 2f;
    [SerializeField] private float enrageDetectMult = 1.6f;
    // Fading rage stat multipliers
    [SerializeField] private float fadingSpeedMult = 1.25f;
    [SerializeField] private float fadingDamageMult = 1f;
    [SerializeField] private float fadingDetectMult = 1.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip attackSFX;
    [SerializeField] private AudioClip hurtSFX;
    [SerializeField] private AudioClip deathSFX;
    [SerializeField] private AudioClip enrageSFX;
    [SerializeField] private AudioClip healSFX;

    // ══════════════════════════════════════════
    //  ENUMS
    // ══════════════════════════════════════════

    public enum State
    {
        Wander,
        Chase,
        Attack,
        Stagger,
        Healing,
        Dead
    }

    private enum EnragePhase
    {
        Normal,
        MaxPower,
        FadingRage,
        Cooldown
    }

    // ══════════════════════════════════════════
    //  PRIVATE REFERENCES
    // ══════════════════════════════════════════

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sr;
    private AudioSource audioSource;
    private Transform playerTransform;
    private PlayerController playerController;

    // ══════════════════════════════════════════
    //  RUNTIME STATE
    // ══════════════════════════════════════════

    // Core state
    private State currentState = State.Wander;
    private EnragePhase enragePhase = EnragePhase.Normal;
    private int currentHealth;
    private bool hasTakenDamage = false;

    // Current stats (modified by enrage)
    private float currentMoveSpeed;
    private int currentAttackDamage;
    private float currentDetectRange;
    private float currentAttackRange;

    // Attack
    private float attackTimer;

    // Stagger / combo tracking
    private int recentHitCount;
    private float recentHitTimer;
    private float staggerTimer;

    // Knockback
    private bool isKnockedBack;
    private float knockbackTimer;

    // Wandering
    private Vector2 wanderDirection;
    private float wanderMoveTimer;
    private float wanderPauseTimer;
    private bool isWanderPausing;

    // Healing
    private int healSessionsDone;
    private float healTimer;
    private bool isHealing;

    // Enrage timing
    private float roundTimer;           // time since round started
    private float enrageCheckTimer;     // countdown to next roll
    private float enragePhaseTimer;     // timer within current phase

    // ══════════════════════════════════════════
    //  ANIMATOR HASHES
    // ══════════════════════════════════════════

    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashAttack2 = Animator.StringToHash("Attack2");
    private static readonly int HashHurt = Animator.StringToHash("Hurt");
    private static readonly int HashDead = Animator.StringToHash("Dead");
    private static readonly int HashStagger = Animator.StringToHash("Stagger");
    private static readonly int HashHeal = Animator.StringToHash("Heal");
    private static readonly int HashEnrage = Animator.StringToHash("Enrage");

    // ══════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ══════════════════════════════════════════

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();

        currentHealth = maxHealth;
        ApplyStats(EnragePhase.Normal);

        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerController = player.GetComponent<PlayerController>();
        }

        // Start with a random wander direction
        PickNewWanderDirection();
        enrageCheckTimer = enrageCheckInterval;
    }

    private void Update()
    {
        if (currentState == State.Dead) return;

        roundTimer += Time.deltaTime;
        enrageCheckTimer -= Time.deltaTime;

        HandleEnrageCycle();
        HandleComboWindow();
        HandleKnockback();
        UpdateStateMachine();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (currentState == State.Dead) return;
        if (isKnockedBack) return;
        if (currentState == State.Stagger) return;
        if (currentState == State.Healing) return;

        if (currentState == State.Wander)
            ExecuteWander();
        else if (currentState == State.Chase)
            MoveTowardsPlayer();
    }

    // ══════════════════════════════════════════
    //  STATE MACHINE
    // ══════════════════════════════════════════

    private void UpdateStateMachine()
    {
        if (playerTransform == null) return;

        float distToPlayer = Vector2.Distance(
            transform.position, playerTransform.position);

        switch (currentState)
        {
            case State.Wander:
                HandleWanderState(distToPlayer);
                break;

            case State.Chase:
                HandleChaseState(distToPlayer);
                break;

            case State.Attack:
                HandleAttackState(distToPlayer);
                break;

            case State.Stagger:
                HandleStaggerState();
                break;

            case State.Healing:
                HandleHealingState(distToPlayer);
                break;
        }
    }

    // ── Wander ────────────────────────────────
    private void HandleWanderState(float distToPlayer)
    {
        if (distToPlayer <= currentDetectRange)
        {
            currentState = State.Chase;
            return;
        }
    }

    // ── Chase ─────────────────────────────────
    private void HandleChaseState(float distToPlayer)
    {
        // Lost player — go heal if damaged, else wander
        if (distToPlayer > currentDetectRange)
        {
            if (hasTakenDamage && healSessionsDone < maxHealSessions)
                EnterHealing();
            else
            {
                currentState = State.Wander;
                PickNewWanderDirection();
            }
            return;
        }

        if (distToPlayer <= currentAttackRange)
            currentState = State.Attack;
    }

    // ── Attack ────────────────────────────────
    private void HandleAttackState(float distToPlayer)
    {
        if (distToPlayer > currentAttackRange)
        {
            currentState = State.Chase;
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = attackCooldown;

            // Use stronger attack animation when enraged
            if (enragePhase == EnragePhase.MaxPower)
                anim.SetTrigger(HashAttack2);
            else
                anim.SetTrigger(HashAttack);

            PlaySound(attackSFX);
            Invoke(nameof(DealDamage), attackDelay);
        }
    }

    // ── Stagger ───────────────────────────────
    private void HandleStaggerState()
    {
        staggerTimer -= Time.deltaTime;
        if (staggerTimer <= 0f)
        {
            // Get back up angry — speed boost
            currentMoveSpeed += currentMoveSpeed * staggerSpeedBoost;
            currentState = State.Chase;
        }
    }

    // ── Healing ───────────────────────────────
    private void HandleHealingState(float distToPlayer)
    {
        // Cancel healing if player comes back
        if (distToPlayer <= currentDetectRange)
        {
            isHealing = false;
            currentState = State.Chase;
            return;
        }

        if (!isHealing)
        {
            isHealing = true;
            healTimer = healDuration;
            anim.SetTrigger(HashHeal);
            PlaySound(healSFX);
        }

        healTimer -= Time.deltaTime;

        if (healTimer <= 0f)
        {
            // Apply heal — +50% of maxHealth capped at maxHealth
            int healValue = Mathf.RoundToInt(maxHealth * healAmount);
            currentHealth = Mathf.Min(currentHealth + healValue, maxHealth);
            healSessionsDone++;
            isHealing = false;

            // Healed twice or back to full — play WrapBreakOut then wander
            if (healSessionsDone >= maxHealSessions || currentHealth >= maxHealth)
            {
                hasTakenDamage = false;
                healSessionsDone = 0;
                anim.SetTrigger(HashEnrage); // WrapBreakOut animation
                currentState = State.Wander;
                PickNewWanderDirection();
            }
            // else — loop for second heal session
        }
    }

    private void EnterHealing()
    {
        currentState = State.Healing;
        isHealing = false;
        rb.linearVelocity = Vector2.zero;
    }

    // ══════════════════════════════════════════
    //  WANDERING
    // ══════════════════════════════════════════

    private void ExecuteWander()
    {
        if (isWanderPausing)
        {
            wanderPauseTimer -= Time.deltaTime;
            if (wanderPauseTimer <= 0f)
            {
                isWanderPausing = false;
                PickNewWanderDirection();
            }
            return;
        }

        wanderMoveTimer -= Time.deltaTime;

        // Check for wall ahead — if blocked pick new direction
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            wanderDirection,
            wanderRayDistance,
            wallLayer
        );

        if (hit.collider != null)
        {
            PickNewWanderDirection();
            return;
        }

        // Move in current direction
        rb.MovePosition(
            rb.position + wanderDirection * wanderSpeed * Time.fixedDeltaTime
        );

        // Flip sprite
        if (wanderDirection.x != 0)
            sr.flipX = wanderDirection.x < 0;

        // Time to pick a new direction?
        if (wanderMoveTimer <= 0f)
        {
            isWanderPausing = true;
            wanderPauseTimer = Random.Range(wanderPausMin, wanderPauseMax);
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void PickNewWanderDirection()
    {
        for (int i = 0; i < wanderMaxAttempts; i++)
        {
            // Pick a random angle and convert to direction
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            RaycastHit2D hit = Physics2D.Raycast(
                transform.position, dir, wanderRayDistance, wallLayer);

            if (hit.collider == null)
            {
                wanderDirection = dir;
                wanderMoveTimer = wanderDirDuration;
                return;
            }
        }

        // All attempts blocked — pause and try again later
        isWanderPausing = true;
        wanderPauseTimer = wanderPauseMax;
    }

    // ══════════════════════════════════════════
    //  MOVEMENT
    // ══════════════════════════════════════════

    private void MoveTowardsPlayer()
    {
        if (playerTransform == null) return;

        Vector2 dir = (playerTransform.position - transform.position).normalized;
        rb.MovePosition(rb.position + dir * currentMoveSpeed * Time.fixedDeltaTime);

        if (dir.x != 0)
            sr.flipX = dir.x < 0;
    }

    // ══════════════════════════════════════════
    //  ATTACK / DAMAGE
    // ══════════════════════════════════════════

    private void DealDamage()
    {
        if (currentState == State.Dead) return;
        if (playerController == null) return;

        float dist = Vector2.Distance(
            transform.position, playerTransform.position);

        if (dist <= currentAttackRange * 1.2f)
            playerController.TakeDamage(currentAttackDamage);
    }

    public void TakeDamage(int amount, Vector2 hitDirection)
    {
        if (currentState == State.Dead) return;

        // Interrupted during healing — partial heal + angry breakout
        if (currentState == State.Healing)
        {
            int partialHeal = Mathf.RoundToInt(maxHealth * 0.25f);
            currentHealth = Mathf.Min(currentHealth + partialHeal, maxHealth);
            isHealing = false;
            healSessionsDone = maxHealSessions; // prevent further healing
            anim.SetTrigger(HashEnrage);        // WrapBreakOut animation
            currentState = State.Chase;
            enragePhase = EnragePhase.MaxPower;
            enragePhaseTimer = maxPowerDuration;
            ApplyStats(EnragePhase.MaxPower);
            return;
        }

        currentHealth -= amount;
        hasTakenDamage = true;
        PlaySound(hurtSFX);

        // Track combo hits for stagger
        recentHitCount++;
        recentHitTimer = staggerWindow;

        ApplyKnockback(hitDirection);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        // Check stagger threshold
        if (recentHitCount >= staggerHitCount)
        {
            recentHitCount = 0;
            EnterStagger();
            return;
        }

        anim.SetTrigger(HashHurt);
    }

    // ══════════════════════════════════════════
    //  STAGGER
    // ══════════════════════════════════════════

    private void EnterStagger()
    {
        currentState = State.Stagger;
        staggerTimer = staggerDuration;
        rb.linearVelocity = Vector2.zero;
        anim.SetTrigger(HashStagger);
    }

    // ══════════════════════════════════════════
    //  KNOCKBACK
    // ══════════════════════════════════════════

    private void ApplyKnockback(Vector2 direction)
    {
        isKnockedBack = true;
        knockbackTimer = knockbackDuration;
        rb.linearVelocity = direction.normalized * knockbackForce;
    }

    private void HandleKnockback()
    {
        if (!isKnockedBack) return;

        knockbackTimer -= Time.deltaTime;
        if (knockbackTimer <= 0f)
        {
            isKnockedBack = false;
            rb.linearVelocity = Vector2.zero;
        }
    }

    // ══════════════════════════════════════════
    //  COMBO WINDOW
    // ══════════════════════════════════════════

    private void HandleComboWindow()
    {
        if (recentHitCount <= 0) return;

        recentHitTimer -= Time.deltaTime;
        if (recentHitTimer <= 0f)
            recentHitCount = 0; // combo window expired
    }

    // ══════════════════════════════════════════
    //  ENRAGE CYCLE
    // ══════════════════════════════════════════

    private void HandleEnrageCycle()
    {
        // Tick the active enrage phase timer
        if (enragePhase != EnragePhase.Normal)
        {
            enragePhaseTimer -= Time.deltaTime;

            if (enragePhaseTimer <= 0f)
                AdvanceEnragePhase();

            return;
        }

        // Still in cooldown / normal — wait for check interval
        // but don't enrage before the round delay has passed
        if (roundTimer < enrageRoundDelay) return;

        if (enrageCheckTimer <= 0f)
        {
            enrageCheckTimer = enrageCheckInterval;

            if (Random.value <= enrageChance)
                TriggerEnrage();
        }
    }

    private void TriggerEnrage()
    {
        Debug.Log("Mummy enraged!");
        enragePhase = EnragePhase.MaxPower;
        enragePhaseTimer = maxPowerDuration;

        ApplyStats(EnragePhase.MaxPower);
        anim.SetTrigger(HashEnrage);
        PlaySound(enrageSFX);
    }

    private void AdvanceEnragePhase()
    {
        switch (enragePhase)
        {
            case EnragePhase.MaxPower:
                enragePhase = EnragePhase.FadingRage;
                enragePhaseTimer = fadingRageDuration;
                ApplyStats(EnragePhase.FadingRage);
                break;

            case EnragePhase.FadingRage:
                enragePhase = EnragePhase.Cooldown;
                enragePhaseTimer = cooldownDuration;
                ApplyStats(EnragePhase.Normal);
                break;

            case EnragePhase.Cooldown:
                enragePhase = EnragePhase.Normal;
                enrageCheckTimer = enrageCheckInterval;
                break;
        }
    }

    // ══════════════════════════════════════════
    //  STAT APPLICATION
    // ══════════════════════════════════════════

    private void ApplyStats(EnragePhase phase)
    {
        switch (phase)
        {
            case EnragePhase.Normal:
                currentMoveSpeed = baseMoveSpeed;
                currentAttackDamage = baseAttackDamage;
                currentDetectRange = baseDetectRange;
                currentAttackRange = baseAttackRange;
                break;

            case EnragePhase.MaxPower:
                currentMoveSpeed = baseMoveSpeed * enrageSpeedMult;
                currentAttackDamage = Mathf.RoundToInt(baseAttackDamage * enrageDamageMult);
                currentDetectRange = baseDetectRange * enrageDetectMult;
                currentAttackRange = baseAttackRange * 1.3f;
                break;

            case EnragePhase.FadingRage:
                currentMoveSpeed = baseMoveSpeed * fadingSpeedMult;
                currentAttackDamage = Mathf.RoundToInt(baseAttackDamage * fadingDamageMult);
                currentDetectRange = baseDetectRange * fadingDetectMult;
                currentAttackRange = baseAttackRange;
                break;
        }
    }

    // ══════════════════════════════════════════
    //  DEATH
    // ══════════════════════════════════════════

    private void Die()
    {
        currentState = State.Dead;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;

        GetComponent<Collider2D>().enabled = false;

        PlaySound(deathSFX);
        anim.SetTrigger(HashDead);

        Destroy(gameObject, 2f);
    }

    // ══════════════════════════════════════════
    //  ANIMATOR
    // ══════════════════════════════════════════

    private void UpdateAnimator()
    {
        bool isMoving = currentState == State.Chase ||
                       (currentState == State.Wander && !isWanderPausing);

        anim.SetFloat(HashSpeed, isMoving ? 1f : 0f);
    }

    // ══════════════════════════════════════════
    //  AUDIO
    // ══════════════════════════════════════════

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    // ══════════════════════════════════════════
    //  GIZMOS
    // ══════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        // Detection range — yellow
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, baseDetectRange);

        // Attack range — red
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, baseAttackRange);

        // Wander raycast — cyan
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, wanderDirection * wanderRayDistance);
    }

    // ══════════════════════════════════════════
    //  PUBLIC GETTERS
    // ══════════════════════════════════════════

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => currentState == State.Dead;
    public bool IsEnraged => enragePhase == EnragePhase.MaxPower;
    public State CurrentState => currentState;
}