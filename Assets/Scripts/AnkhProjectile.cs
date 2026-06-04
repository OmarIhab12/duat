using UnityEngine;

public class AnkhProjectile : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 6f;
    public float trackingStrength = 2f;  // how much it homes toward player
    public float maxTrackingTime = 1f;   // stops tracking after this long

    [Header("Damage")]
    public int damage = 1;

    [Header("Visuals")]
    public float rotationSpeed = 180f;   // degrees per second visual spin

    private Vector2 moveDir;
    private Transform player;
    private float trackingTimer;
    private bool hitSomething;
    public float lifetime = 5f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    public void Init(Vector2 direction)
    {
        moveDir = direction.normalized;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj) player = playerObj.transform;
        trackingTimer = maxTrackingTime;
    }

    void Update()
    {
        // Visual spin
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);

        // Homing — gradually steer toward player
        if (player != null && trackingTimer > 0)
        {
            Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
            moveDir = Vector2.Lerp(moveDir, toPlayer, trackingStrength * Time.deltaTime).normalized;
            trackingTimer -= Time.deltaTime;
        }

        // Move
        transform.Translate(moveDir * speed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hitSomething) return;

        Debug.Log($"[Ankh] Hit: {other.gameObject.name} tag:{other.tag} layer:{LayerMask.LayerToName(other.gameObject.layer)}");

        // Hit player
        if (other.CompareTag("Player") || other.CompareTag("PlayerBody"))
        {
            PlayerController pc = other.GetComponent<PlayerController>()
                ?? other.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(damage);
                DestroyProjectile();
                return;
            }
        }

        // Hit wall
        if (other.gameObject.layer == LayerMask.NameToLayer("Wall"))
        {
            DestroyProjectile();
            return;
        }

        // Ignore Anubis itself and other enemies
        if (other.CompareTag("Enemy")) return;
    }

    void DestroyProjectile()
    {
        hitSomething = true;
        // Could add a small flash effect here later
        Destroy(gameObject);
    }
}