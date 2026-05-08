using UnityEngine;

public class CanopicJar : MonoBehaviour
{
    public JarType jarType;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerInventory inventory = other.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        inventory.CollectJar(jarType);
        Destroy(gameObject);
    }
}