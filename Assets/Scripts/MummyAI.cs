using UnityEngine;
using System.Collections;

public class MummyAI : EnemyAI
{
    [Header("Mummy — Stagger")]
    public int comboHitsForStagger = 3;
    public float comboWindow = 1.5f;

    [Header("Mummy — Healing")]
    public float healPercent = 0.5f;
    public int maxHealSessions = 2;
    public float healTickRate = 0.5f;
    public float healRange = 6f;

    [Header("Mummy — Enrage")]
    public float enrageCheckInterval = 60f;
    public float enrageChance = 0.2f;

    // Stagger tracking
    private int comboCount;
    private float comboStartTime;

    // Healing tracking
    private int healSessions;
    private bool isHealing;
    private Coroutine healCoroutine;

    // Enrage
    private bool isEnraged;
    private float enrageTimer;

    protected override void Start()
    {
        base.Start();
        enrageTimer = enrageCheckInterval;
    }

    protected override void Update()
    {
        base.Update();
        if (isDead) return;
        HandleEnrageTimer();
        HandleHealCheck();
    }

    protected override void HandleAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        lastAttackTime = Time.time;
        animator.SetTrigger(isEnraged ? "Attack2" : "Attack");

        if (player == null) return;
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc && Vector2.Distance(transform.position, player.position) < attackRange)
            pc.TakeDamage(attackDamage);
    }

    protected override void OnDamaged(int damage, Vector2 hitDirection)
    {
        // Stagger combo
        float now = Time.time;
        if (comboCount == 0) comboStartTime = now;
        comboCount++;
        if (now - comboStartTime > comboWindow) { comboCount = 1; comboStartTime = now; }
        if (comboCount >= comboHitsForStagger)
        {
            comboCount = 0;
            StartCoroutine(DoStagger());
        }

        // Cancel healing if hit
        if (isHealing && healCoroutine != null)
        {
            StopCoroutine(healCoroutine);
            isHealing = false;
            animator.SetTrigger("Enrage"); // WrapBreakOut → immediate enrage
        }
    }

    IEnumerator DoStagger()
    {
        animator.SetTrigger("Stagger");
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.6f);
        moveSpeed *= 1.3f; // speed boost on recovery
        yield return new WaitForSeconds(0.3f);
        moveSpeed /= 1.3f;
    }

    void HandleHealCheck()
    {
        if (isHealing || healSessions >= maxHealSessions) return;
        if (currentHealth >= maxHealth) return;
        if (currentState != EnemyState.Wander) return;
        if (player && Vector2.Distance(transform.position, player.position) > healRange)
            healCoroutine = StartCoroutine(DoHeal());
    }

    IEnumerator DoHeal()
    {
        isHealing = true;
        healSessions++;
        animator.SetTrigger("Heal");
        int healAmount = Mathf.RoundToInt(maxHealth * healPercent);
        for (int i = 0; i < healAmount; i++)
        {
            if (!isHealing) yield break;
            currentHealth = Mathf.Min(currentHealth + 1, maxHealth);
            yield return new WaitForSeconds(healTickRate);
        }
        animator.SetTrigger("Enrage"); // WrapBreakOut
        isHealing = false;
    }

    void HandleEnrageTimer()
    {
        enrageTimer -= Time.deltaTime;
        if (enrageTimer > 0) return;
        enrageTimer = enrageCheckInterval;
        if (Random.value < enrageChance && !isEnraged)
            StartCoroutine(DoEnrage());
    }

    IEnumerator DoEnrage()
    {
        isEnraged = true;
        float origSpeed = moveSpeed;
        moveSpeed *= 1.75f;
        attackDamage *= 2;
        detectionRange *= 1.6f;
        yield return new WaitForSeconds(20f); // MaxPower
        moveSpeed = origSpeed * 1.25f;
        attackDamage /= 2;
        detectionRange /= 1.6f;
        yield return new WaitForSeconds(20f); // FadingRage
        moveSpeed = origSpeed;
        isEnraged = false;
        yield return new WaitForSeconds(20f); // Cooldown
    }
}