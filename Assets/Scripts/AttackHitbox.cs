using UnityEngine;

// ─────────────────────────────────────────────
//  DUAT — AttackHitbox.cs
//  Attach to the AttackHitbox child GameObject
//  on the Player. Requires a Collider2D set as
//  trigger on the same GameObject.
//  The hitbox is enabled/disabled by
//  PlayerController during attacks.
// ─────────────────────────────────────────────

public class AttackHitbox : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private int damage = 1;

    // Root transform is the Player — used to
    // calculate knockback direction away from player
    private Transform playerRoot;

    private void Awake()
    {
        playerRoot = transform.root;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Try to get EnemyAI from the collided object
        EnemyAI enemy = other.GetComponent<EnemyAI>();

        if (enemy == null) return;
        if (enemy.IsDead) return;

        // Calculate knockback direction — away from player
        Vector2 hitDirection = (other.transform.position
            - playerRoot.position).normalized;

        enemy.TakeDamage(damage, hitDirection);
    }
}