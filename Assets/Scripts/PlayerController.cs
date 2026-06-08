using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// ─────────────────────────────────────────────
//  DUAT — PlayerController.cs
//  Attach to the Player GameObject.
//  Requires: Rigidbody2D, Animator, SpriteRenderer,
//  AudioSource on the same GameObject, plus a
//  PlayerInput component with an Input Action Asset.
//  Child GameObject "AttackHitbox" with Collider2D (trigger).
// ─────────────────────────────────────────────

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    // ── Inspector fields ──────────────────────
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private GameObject attackHitbox;
    [SerializeField] private float hitboxActiveTime = 0.15f;

    [Header("Combo")]
    public float comboWindow = 0.6f; // time to input next attack
    private int comboStep = 0;
    private float lastAttackTime;
    private bool comboQueued;

    [Header("Arrow")]
    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint; // empty child GO near player's hand


    [Header("Health")]
    [SerializeField] private int maxHearts = 3;
    [SerializeField] private float invincibilityDuration = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip attackSFX;
    [SerializeField] private AudioClip throwSFX;
    [SerializeField] private AudioClip hurtSFX;
    [SerializeField] private AudioClip deathSFX;

    // ── Private references ────────────────────
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sr;
    private AudioSource audioSource;
    private PauseMenu pauseMenu;

    // ── State ─────────────────────────────────
    private Vector2 moveInput;
    private Vector2 lastMoveDir = Vector2.down;

    private int currentHearts;
    private bool isAttacking;
    private bool isDead;
    private bool isInvincible;

    private float attackTimer;
    private float invincibilityTimer;

    private PillarAltar nearbyAltar;

    // ── Animator parameter hashes ─────────────
    private static readonly int HashMoveX = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY = Animator.StringToHash("MoveY");
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashHurt = Animator.StringToHash("Hurt");
    private static readonly int HashDead = Animator.StringToHash("Dead");

    // ── Unity lifecycle ───────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();

        currentHearts = maxHearts;

        if (attackHitbox != null)
            attackHitbox.SetActive(false);
    }

    void Start()
    {
        pauseMenu = FindFirstObjectByType<PauseMenu>();
    }

    private void Update()
    {
        if (isDead) return;

        UpdateAnimator();
        HandleAttackCooldown();
        HandleInvincibility();
        UpdateHitboxFacing();
    }

    private void FixedUpdate()
    {
        if (isDead || isAttacking) return;
        Move();
    }

    // ── New Input System callbacks ─────────────
    // Called automatically by PlayerInput component
    // via "Send Messages" behaviour mode.
    // Method names must match your Action names exactly.

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>().normalized;

        if (moveInput != Vector2.zero)
            lastMoveDir = moveInput;
    }

    public void OnAttack(InputValue value)
    {
        if (!value.isPressed || isDead) return;

        // If currently attacking, queue the next hit
        if (isAttacking)
        {
            comboQueued = true;
            return;
        }

        TriggerAttack();
    }

    void TriggerAttack()
    {
        isAttacking = true;
        attackTimer = attackCooldown;
        comboQueued = false;

        float timeSinceLastAttack = Time.time - lastAttackTime;
        if (timeSinceLastAttack > comboWindow) comboStep = 0;
        comboStep++;
        if (comboStep > 3) comboStep = 1;
        lastAttackTime = Time.time;

        switch (comboStep)
        {
            case 1:
                anim.ResetTrigger("Attack2");
                anim.ResetTrigger("Attack3");
                anim.SetTrigger("Attack1");
                break;
            case 2:
                anim.ResetTrigger("Attack1");
                anim.ResetTrigger("Attack3");
                anim.SetTrigger("Attack2");
                break;
            case 3:
                anim.ResetTrigger("Attack1");
                anim.ResetTrigger("Attack2");
                anim.SetTrigger("Attack3");
                comboStep = 0;
                break;
        }

        PlaySound(attackSFX);
        PositionHitbox();

        if (attackHitbox != null)
        {
            attackHitbox.SetActive(true);
            Invoke(nameof(DisableHitbox), hitboxActiveTime);
        }

        Invoke(nameof(EndAttack), attackCooldown * 0.6f);
    }

    void EndAttack()
    {
        isAttacking = false;
        attackTimer = 0f;

        // Fire queued combo hit immediately
        if (comboQueued && comboStep < 3)
            TriggerAttack();
        else
            comboQueued = false;
    }


    public void OnThrow(InputValue value)
    {
        Debug.Log($"[Player] OnThrow called with value: {value}");
        if (!value.isPressed || isDead) return;
        if (isAttacking || attackTimer > 0f) return; // blocked during melee
        if (arrowPrefab == null) return;

        // Use attack cooldown so melee and throw share the same window
        isAttacking = true;
        attackTimer = attackCooldown;

        anim.SetTrigger("Throw");
        PlaySound(throwSFX);

        Vector2 throwDir = new Vector2(FacingDirection.x, FacingDirection.y);
        GameObject arrow = Instantiate(arrowPrefab,
            arrowSpawnPoint.position, Quaternion.identity);
        arrow.layer = LayerMask.NameToLayer("Arrow");
        arrow.GetComponent<ArrowProjectile>()?.Init(throwDir);

        Invoke(nameof(EndAttack), attackCooldown * 0.6f);
    }

    // ── Animator update ───────────────────────
    private void UpdateAnimator()
    {
        anim.SetFloat(HashMoveX, lastMoveDir.x);
        anim.SetFloat(HashMoveY, lastMoveDir.y);
        anim.SetFloat(HashSpeed, moveInput.magnitude);
        FlipPlayer(lastMoveDir.x);
    }

    // private void FlipSprite()
    // {
    //     if (moveInput.x > 0)
    //         sr.flipX = false; // facing right
    //     else if (moveInput.x < 0)
    //         sr.flipX = true;  // facing left
    // }

    // ── Movement ──────────────────────────────
    private void Move()
    {
        rb.MovePosition(rb.position + moveInput * moveSpeed * Time.fixedDeltaTime);
    }


    private void PositionHitbox()
    {
        if (attackHitbox == null) return;
        attackHitbox.transform.localPosition = lastMoveDir * 0.6f;
    }

    private void DisableHitbox()
    {
        if (attackHitbox != null)
            attackHitbox.SetActive(false);
    }

    private void HandleAttackCooldown()
    {
        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;
    }

    // ── Health & Damage ───────────────────────
    public void TakeDamage(int amount)
    {
        if (isDead || isInvincible) return;

        currentHearts -= amount;
        PlaySound(hurtSFX);

        if (currentHearts <= 0)
        {
            Die();
            return;
        }

        isInvincible = true;
        invincibilityTimer = invincibilityDuration;

        anim.SetTrigger(HashHurt);
        InvokeRepeating(nameof(FlashSprite), 0f, 0.1f);
    }

    public void Heal(int amount)
    {
        currentHearts = Mathf.Min(currentHearts + amount, maxHearts);
    }

    private void HandleInvincibility()
    {
        if (!isInvincible) return;

        invincibilityTimer -= Time.deltaTime;

        if (invincibilityTimer <= 0f)
        {
            isInvincible = false;
            sr.enabled = true;
            CancelInvoke(nameof(FlashSprite));
        }
    }

    private void FlashSprite()
    {
        sr.enabled = !sr.enabled;
    }

    private void Die()
    {
        isDead = true;
        currentHearts = 0;

        PlaySound(deathSFX);
        anim.SetTrigger(HashDead);

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;

        StartCoroutine(ShowGameOverAfterAnimation());
    }

    IEnumerator ShowGameOverAfterAnimation()
    {
        // Wait for death animation to finish
        // Get the length of the Dead animation clip
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        float animLength = stateInfo.length;
        yield return new WaitForSeconds(animLength + 0.5f); // small buffer after
        GameManager.Instance?.TriggerGameOver();
    }

    // ── Audio ─────────────────────────────────
    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    //── Interaction with Altar ─────────────────     
    public void SetNearbyAltar(PillarAltar altar) => nearbyAltar = altar;
    public void ClearNearbyAltar() => nearbyAltar = null;

    public void OnDeposit(InputValue value)
    {
        Debug.Log($"[Player] OnDeposit called with value: {value}");
        if (!value.isPressed) return;
        nearbyAltar?.OnDeposit(value);
    }


    // ── Hitbox ───────────────────────────────
    void UpdateHitboxFacing()
    {
        AttackHitbox hitbox = GetComponentInChildren<AttackHitbox>();
        if (hitbox == null) return;
        Vector3 pos = hitbox.transform.localPosition;
        // Mirror X based on facing

        Debug.Log($"[Player] UpdateHitboxFacing — lastMoveDir:{lastMoveDir} pos:{pos}");
        pos.x = Mathf.Abs(pos.x) * FacingDirection.x;
        hitbox.transform.localPosition = pos;
    }

    void FlipPlayer(float xDir)
    {
        if (xDir == 0) return;
        Vector3 scale = transform.localScale;
        scale.x = xDir > 0 ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    public void OnPause(InputValue value)
    {
        if (!value.isPressed) return;
        pauseMenu?.OnPause(value);
    }

    // ── Public getters ────────────────────────
    public int CurrentHearts => currentHearts;
    public int MaxHearts => maxHearts;
    public bool IsDead => isDead;
    public Vector2 FacingDirection => lastMoveDir;
}