using UnityEngine;

public class HeartPickup : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (player.CurrentHearts >= player.MaxHearts) return;

        player.Heal(1);
        Destroy(gameObject);
    }
}