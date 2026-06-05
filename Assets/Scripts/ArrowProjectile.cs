using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 10f;
    public int damage = 1;
    public float lifetime = 3f;

    private Vector2 moveDir;
    private bool hit;

    public void Init(Vector2 direction)
    {
        moveDir = direction.normalized;

        // Rotate sprite to face direction of travel
        float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (hit) return;
        transform.Translate(moveDir * speed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hit) return;

        // Hit enemy
        EnemyAI enemy = other.GetComponent<EnemyAI>()
            ?? other.GetComponentInParent<EnemyAI>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, moveDir);
            DestroyArrow();
            return;
        }

        // Hit wall
        if (other.gameObject.layer == LayerMask.NameToLayer("Wall"))
        {
            DestroyArrow();
            return;
        }

        // Ignore player and pickups
        if (other.CompareTag("Player") || other.CompareTag("PlayerBody")) return;
    }

    void DestroyArrow()
    {
        hit = true;
        Destroy(gameObject);
    }
}