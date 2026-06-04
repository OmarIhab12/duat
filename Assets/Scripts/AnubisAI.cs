using UnityEngine;
using System.Collections;

public class AnubisAI : EnemyAI
{
    [Header("Anubis — Ranges")]
    public float meleeRange = 1.5f;
    public float rangedRange = 6f;

    [Header("Anubis — Melee")]
    public int meleeDamage = 1;
    public float meleeCooldown = 1.5f;

    [Header("Anubis — Ranged")]
    public GameObject ankhPrefab;
    public Transform throwPoint;         // empty child GO at Anubis's hand
    public float throwCooldown = 3f;
    public float phase3SpreadAngle = 20f; // angle between 2 ankhs in phase 3

    [Header("Anubis — Phases")]
    public float phase2HealthThreshold = 0.66f;
    public float phase3HealthThreshold = 0.33f;
    public float phase2SpeedMultiplier = 1.4f;
    public float phase3SpeedMultiplier = 1.8f;
    public Color phase3Tint = new Color(1f, 0.3f, 0.3f);

    [Header("Boss Health Bar")]
    public BossHealthBar bossHealthBar; // assign in inspector

    private int currentPhase = 1;
    private float baseSpeed;
    private float lastMeleeTime;
    private float lastThrowTime;
    private bool isActing;
    private Coroutine actionCoroutine;

    protected override void Awake()
    {
        base.Awake();
        detectionRange = 20f; // always aware in boss room
        baseSpeed = moveSpeed;
    }

    protected override void Start()
    {
        base.Start();
        lastThrowTime = -throwCooldown; // ready to throw immediately at game start
        lastMeleeTime = -meleeCooldown; // ready to melee immediately at game start
    }

    protected override void UpdateStateMachine()
    {
        if (isDead || isActing) return;
        if (player == null) return;

        CheckPhaseTransition();

        float dist = Vector2.Distance(transform.position, player.position);
        Debug.Log($"[Anubis] Phase:{currentPhase} dist:{dist:F2} meleeRange:{meleeRange} rangedRange:{rangedRange} isActing:{isActing}");

        switch (currentState)
        {
            case EnemyState.Wander:
            case EnemyState.Chase:
                HandleChase();
                if (dist < meleeRange)
                    ChangeState(EnemyState.Attack);
                else if (dist < rangedRange && currentPhase >= 2)
                {
                    Debug.Log("[Anubis] Trying to throw");
                    TryThrow();
                }
                break;

            case EnemyState.Attack:
                if (dist > meleeRange)
                    ChangeState(EnemyState.Chase);
                else
                    HandleAttack();
                break;
        }
    }

    void CheckPhaseTransition()
    {
        int phase2Threshold = Mathf.RoundToInt(maxHealth * phase2HealthThreshold);
        int phase3Threshold = Mathf.RoundToInt(maxHealth * phase3HealthThreshold);

        Debug.Log($"[Anubis] Health:{currentHealth} Phase2at:{phase2Threshold} Phase3at:{phase3Threshold}");

        if (currentHealth <= phase3Threshold && currentPhase < 3)
            EnterPhase(3);
        else if (currentHealth <= phase2Threshold && currentPhase < 2)
            EnterPhase(2);
    }

    void EnterPhase(int phase)
    {
        currentPhase = phase;
        Debug.Log($"[Anubis] Entering Phase {phase}");

        if (phase == 2)
        {
            moveSpeed = baseSpeed * phase2SpeedMultiplier;
            StartCoroutine(PhaseTransitionFlash(Color.yellow));
        }
        else if (phase == 3)
        {
            moveSpeed = baseSpeed * phase3SpeedMultiplier;
            meleeDamage = 2;
            sr.color = phase3Tint;
            StartCoroutine(PhaseTransitionFlash(Color.red));
        }
    }

    IEnumerator PhaseTransitionFlash(Color flashCol)
    {
        // Flash to signal phase change
        for (int i = 0; i < 6; i++)
        {
            sr.color = flashCol;
            yield return new WaitForSeconds(0.1f);
            sr.color = currentPhase == 3 ? phase3Tint : Color.white;
            yield return new WaitForSeconds(0.1f);
        }
    }

    protected override void HandleAttack()
    {
        if (isActing) return;
        if (Time.time - lastMeleeTime < meleeCooldown) return;
        actionCoroutine = StartCoroutine(DoMeleeAttack());
    }

    IEnumerator DoMeleeAttack()
    {
        isActing = true;
        lastMeleeTime = Time.time;

        animator.SetTrigger("Stab");

        // Wait for stab windup
        yield return new WaitForSeconds(0.3f);

        // Hit check
        Collider2D hit = Physics2D.OverlapCircle(
            transform.position, meleeRange,
            LayerMask.GetMask("Player"));
        if (hit != null && hit.CompareTag("Player"))
            hit.GetComponentInParent<PlayerController>()?.TakeDamage(meleeDamage);

        // Wait for animation to finish
        yield return new WaitForSeconds(0.4f);
        isActing = false;
    }

    void TryThrow()
    {
        Debug.Log($"[Anubis] TryThrow — isActing:{isActing} timeSinceLastThrow:{Time.time - lastThrowTime:F2} cooldown:{throwCooldown}");
        if (isActing) return;
        if (Time.time - lastThrowTime < throwCooldown) return;
        Debug.Log("[Anubis] Starting throw coroutine");
        actionCoroutine = StartCoroutine(DoThrowAttack());
        Debug.Log($"[Anubis] Coroutine started: {actionCoroutine != null}");
    }

    IEnumerator DoThrowAttack()
    {
        isActing = true;
        lastThrowTime = Time.time;

        // Stop and face player
        rb.linearVelocity = Vector2.zero;
        if (player)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            FlipSprite(dir.x);
        }

        animator.SetTrigger("Attack");

        // Windup
        yield return new WaitForSeconds(0.4f);

        // Spawn ankh(s)
        if (player != null)
        {
            Vector2 throwDir = (player.position - throwPoint.position).normalized;

            if (currentPhase < 3)
            {
                // Single ankh
                SpawnAnkh(throwDir);
            }
            else
            {
                // Two ankhs spread apart
                Vector2 dir1 = RotateVector(throwDir, phase3SpreadAngle);
                Vector2 dir2 = RotateVector(throwDir, -phase3SpreadAngle);
                SpawnAnkh(dir1);
                SpawnAnkh(dir2);
            }
        }

        yield return new WaitForSeconds(0.5f);
        isActing = false;
        ChangeState(EnemyState.Chase);
    }

    void SpawnAnkh(Vector2 direction)
    {
        Debug.Log($"[Anubis] SpawnAnkh — prefab:{ankhPrefab} throwPoint:{throwPoint}");
        if (ankhPrefab == null) { Debug.LogError("[Anubis] ankhPrefab is NULL"); return; }
        if (throwPoint == null) { Debug.LogError("[Anubis] throwPoint is NULL"); return; }
        GameObject ankh = Instantiate(ankhPrefab, throwPoint.position, Quaternion.identity);
        AnkhProjectile proj = ankh.GetComponent<AnkhProjectile>();
        if (proj == null) Debug.LogError("[Anubis] AnkhProjectile component missing on prefab");
        proj?.Init(direction);
    }

    Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(
            v.x * Mathf.Cos(rad) - v.y * Mathf.Sin(rad),
            v.x * Mathf.Sin(rad) + v.y * Mathf.Cos(rad)
        );
    }

    protected override void OnDamaged(int damage, Vector2 hitDirection)
    {
        bossHealthBar?.UpdateHealth(currentHealth, maxHealth);
    }

    protected override void Die()
    {
        if (actionCoroutine != null) StopCoroutine(actionCoroutine);
        sr.color = Color.white;
        bossHealthBar?.Hide();
        base.Die();
        // TODO: trigger win condition / end screen
    }

}