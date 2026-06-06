using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

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

    [Header("Minions")]
    public GameObject mummyPrefab;
    public GameObject scarabPrefab;
    public Transform[] minionsSpawnPoints;   // 3 spawn points around arena

    [Header("Door")]
    public Tilemap doorTilemap;

    [Header("Audio")]
    public AudioSource growlingAudioSource;
    public AudioSource DoorOpeningAudioSource;
    public AudioClip ambientBreathSFX;    // low ambient loop always playing
    public AudioClip phase2EntrySFX;      // roar when entering phase 2
    public AudioClip phase3EntrySFX;      // powerful roar when entering phase 3
    public AudioClip stabWindupSFX;       // windup before stab
    public AudioClip stabHitSFX;          // stab connects
    public AudioClip throwSFX;            // ankh throw sound
    public AudioClip ankhImpactSFX;       // ankh hits wall (add to AnkhProjectile too)
    public AudioClip doorOpenSFX;     // door rumbles open

    private List<GameObject> activeMinions = new List<GameObject>();
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

        // Start ambient breath loop immediately
        PlayLoop(ambientBreathSFX);
        SpawnMinions();

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
            PlaySFX(phase2EntrySFX);
            StartCoroutine(PhaseTransitionFlash(Color.yellow));
            StartCoroutine(SpawnMinionsDelayed(1f));
        }
        else if (phase == 3)
        {
            moveSpeed = baseSpeed * phase3SpeedMultiplier;
            PlaySFX(phase3EntrySFX);
            meleeDamage = 2;
            sr.color = phase3Tint;
            StartCoroutine(PhaseTransitionFlash(Color.red));
            StartCoroutine(SpawnMinionsDelayed(1f));
        }
    }

    void KillAllMinions()
    {
        foreach (GameObject minion in activeMinions)
        {
            if (minion != null)
                Destroy(minion);
        }
        activeMinions.Clear();
    }

    IEnumerator SpawnMinionsDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnMinions();
    }

    void SpawnMinions()
    {
        // Clean up dead minions from previous tracking list before spawning new ones
        activeMinions.RemoveAll(m => m == null);

        // Spawn mummies and scarabs at each spawn point, with adjustments for phase difficulty
        for (int i = 0; i < minionsSpawnPoints.Length; i++)
        {
            if (mummyPrefab == null || minionsSpawnPoints[i] == null) continue;
            GameObject mummy = Instantiate(mummyPrefab,
                minionsSpawnPoints[i].position, Quaternion.identity);
            activeMinions.Add(mummy);

            // Set aggressive mode — always chase
            MummyAI mummyAi = mummy.GetComponent<MummyAI>();
            if (mummyAi != null)
            {
                mummyAi.detectionRange = 999f;
                mummyAi.moveSpeed *= currentPhase == 3 ? 1.5f : 1.2f; // faster per phase
                mummyAi.enrageChance = 0f; // 0% chance to enrage.
                mummyAi.healPercent = 0f; // disable healing
            }

            if (scarabPrefab == null || minionsSpawnPoints[i] == null) continue;
            GameObject scarab = Instantiate(scarabPrefab,
                minionsSpawnPoints[i].position, Quaternion.identity);
            activeMinions.Add(scarab);

            // Set aggressive mode
            ScarabAI scarabAi = scarab.GetComponent<ScarabAI>();
            if (scarabAi != null)
            {
                scarabAi.chaseRange = 999f;        // always chases
                scarabAi.moveSpeed *= currentPhase == 3 ? 1.5f : 1.2f;
                scarabAi.attackCoolDown *= 0.7f;    // more frequent lunges
            }
        }

        Debug.Log($"[Anubis] Spawned minion wave for phase {currentPhase}");
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

        PlaySFX(stabWindupSFX);
        animator.SetTrigger("Stab");

        // Wait for stab windup
        yield return new WaitForSeconds(0.3f);

        // Hit check
        Collider2D hit = Physics2D.OverlapCircle(
            transform.position, meleeRange,
            LayerMask.GetMask("Player"));
        if (hit != null && hit.CompareTag("Player"))
        {
            PlaySFX(stabHitSFX);
            hit.GetComponentInParent<PlayerController>()?.TakeDamage(meleeDamage);
        }

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
            PlaySFX(throwSFX);
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
        growlingAudioSource?.Stop();
        PlaySFX(deathSFX);
        KillAllMinions();
        doorTilemap.ClearAllTiles();
        // Play door open sound
        DoorOpeningAudioSource?.PlayOneShot(doorOpenSFX);
        doorTilemap.GetComponent<TilemapCollider2D>().enabled = false;
        base.Die();
        // TODO: trigger win condition / end screen
    }

    void PlayLoop(AudioClip clip)
    {
        if (clip == null) return;
        if (growlingAudioSource.clip == clip && growlingAudioSource.isPlaying) return;
        growlingAudioSource.clip = clip;
        growlingAudioSource.Play();
    }

    void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        StartCoroutine(DuckLoopForSFX(clip));
    }

    IEnumerator DuckLoopForSFX(AudioClip clip)
    {
        growlingAudioSource.volume = 0.5f;
        audioSource.PlayOneShot(clip);
        yield return new WaitForSeconds(clip.length);
        growlingAudioSource.volume = 0.1f;
    }

}