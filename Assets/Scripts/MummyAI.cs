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
    public float timeWithoutDamageToHeal = 4f;

    [Header("Mummy — Enrage")]
    public float enrageCheckInterval = 60f;
    public float enrageChance = 0.2f;
    public float enrageInterval = 40f; // how long the enrage phases last

    [Header("Mummy — Visual")]
    public Color normalColor = Color.white;
    public Color enragedColor = new Color(1f, 0.4f, 0.4f); // red tint
    public Color semiEnragedColor = new Color(1f, 0.6f, 0.6f); // lighter red tint

    [Header("Mummy — Attack")]
    private Coroutine attackCoroutine;
    private bool isAttacking;

    [Header("Mummy — Audio")]
    public AudioClip enrageSFX;        // roar when entering MaxPower
    public AudioClip normalGrowlSFX;     // loop during normal state
    public AudioClip enragedGrowlSFX;    // loop during enrage
    public AudioClip wrapBreakOutSFX;  // bandages tearing on WrapBreakOut
    public AudioClip staggerSFX;       // heavy daze on stagger
    public AudioClip healSFX;          // mystical healing sound
    public AudioClip attackSFX;        // standard attack hit
    public AudioClip attack2SFX;       // enraged attack hit
    public AudioSource growlSource;

    // Stagger tracking
    private int comboCount;
    private float comboStartTime;

    // Healing tracking
    private float lastDamageTime;
    private int healSessions;
    private bool isHealing;
    private Coroutine healCoroutine;

    // Enrage
    private bool isEnraged;
    private float enrageTimer;
    private AudioSource enrageLoopSource; // separate source for looping growl

    protected override void Start()
    {
        base.Start();
        enrageTimer = enrageCheckInterval;
        lastDamageTime = Time.time;



        if (growlSource != null)
        {
            // Start normal growl immediately
            PlayGrowl(normalGrowlSFX);
        }

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
        if (isAttacking) return;
        if (Time.time - lastAttackTime < attackCooldown) return;
        attackCoroutine = StartCoroutine(DoMeleeAttack());
    }

    IEnumerator DoMeleeAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        animator.SetTrigger(isEnraged ? "Attack2" : "Attack");
        if (isEnraged ? attack2SFX : attackSFX)
            PlaySFX(isEnraged ? attack2SFX : attackSFX);

        // Windup before damage
        yield return new WaitForSeconds(0.4f);

        // Only deal damage if still attacking (not interrupted)
        if (isAttacking && player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc && Vector2.Distance(transform.position, player.position) < attackRange)
                pc.TakeDamage(attackDamage);
        }

        yield return new WaitForSeconds(0.3f);
        isAttacking = false;
    }

    protected override void OnDamaged(int damage, Vector2 hitDirection)
    {
        // Record time of last hit — resets heal timer
        lastDamageTime = Time.time;

        // Interrupt heal if mid-heal
        if (isHealing && healCoroutine != null)
        {
            StopCoroutine(healCoroutine);
            isHealing = false;
            if (wrapBreakOutSFX) PlaySFX(wrapBreakOutSFX);
            animator.SetTrigger("Enrage");
            StartCoroutine(DoEnrage());
        }

        // Interrupt attack if mid-swing
        if (isAttacking)
        {
            if (attackCoroutine != null) StopCoroutine(attackCoroutine);
            isAttacking = false;
            lastAttackTime = Time.time;
        }

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
    }

    IEnumerator DoStagger()
    {
        if (staggerSFX) PlaySFX(staggerSFX);
        animator.SetTrigger("Stagger");
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.6f);
        moveSpeed *= 1.3f;
        yield return new WaitForSeconds(0.3f);
        moveSpeed /= 1.3f;
    }

    void HandleHealCheck()
    {
        if (isHealing) return;
        if (healSessions >= maxHealSessions) return;
        if (currentHealth >= maxHealth) return;
        if (currentState == EnemyState.Attack) return;

        // Only heal if enough time has passed since last hit
        if (Time.time - lastDamageTime < timeWithoutDamageToHeal) return;

        healCoroutine = StartCoroutine(DoHeal());
    }

    IEnumerator DoHeal()
    {
        isHealing = true;
        healSessions++;

        if (healSFX) PlaySFX(healSFX);
        animator.SetTrigger("Heal");

        int healAmount = Mathf.RoundToInt(maxHealth * healPercent);
        for (int i = 0; i < healAmount; i++)
        {
            if (!isHealing) yield break;
            currentHealth = Mathf.Min(currentHealth + 1, maxHealth);
            yield return new WaitForSeconds(healTickRate);
        }

        // Healing complete — WrapBreakOut
        if (wrapBreakOutSFX) PlaySFX(wrapBreakOutSFX);
        animator.SetTrigger("Enrage");
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

        // Mummy enrage roar
        if (enrageSFX) PlaySFX(enrageSFX);

        // Start enrage growl
        PlaySFX(enrageSFX);
        yield return new WaitForSeconds(enrageSFX != null ? enrageSFX.length : 0.5f);
        PlayGrowl(enragedGrowlSFX);

        // MaxPower phase (20s)
        sr.color = enragedColor;
        moveSpeed *= 1.75f;
        // attackDamage *= 2;
        detectionRange *= 1.6f;
        yield return new WaitForSeconds(enrageInterval / 2);

        // FadingRage phase (20s)
        sr.color = semiEnragedColor;
        moveSpeed = origSpeed * 1.25f;
        // attackDamage /= 2;
        detectionRange /= 1.6f;
        yield return new WaitForSeconds(enrageInterval / 2);

        // Stop enrage loop
        if (enrageLoopSource != null)
            enrageLoopSource.Stop();

        // Cooldown phase (20s)
        sr.color = normalColor;
        PlayGrowl(normalGrowlSFX);
        moveSpeed = origSpeed;
        isEnraged = false;
        yield return new WaitForSeconds(20f);
    }

    protected override void Die()
    {
        // Stop enrage loop if playing on death
        if (enrageLoopSource != null)
            enrageLoopSource.Stop();
        base.Die();
    }


    void PlayGrowl(AudioClip clip)
    {
        if (clip == null) return;
        if (growlSource.clip == clip && growlSource.isPlaying) return; // already playing
        growlSource.clip = clip;
        growlSource.Play();
    }

    void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        // Temporarily duck the growl while SFX plays
        StartCoroutine(DuckGrowlForSFX(clip));
    }

    IEnumerator DuckGrowlForSFX(AudioClip clip)
    {
        // Duck growl volume
        growlSource.volume = 0.4f;

        // Play one-shot SFX
        audioSource?.PlayOneShot(clip);

        // Wait for SFX to finish
        yield return new WaitForSeconds(clip.length * 0.8f);

        // Restore growl volume
        growlSource.volume = 0.8f;
    }
}