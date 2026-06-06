using UnityEngine;

public abstract class Pickup : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip pickupSFX;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (!CanPickup(other)) return;

        OnPickedUp(other);

        PlayPickupSound();

        Destroy(gameObject);
    }

    protected virtual void PlayPickupSound()
    {
        if (pickupSFX == null) return;

        // Spawn a temporary AudioSource that plays in 2D — always full volume
        GameObject tempAudio = new GameObject("PickupSFX");
        AudioSource src = tempAudio.AddComponent<AudioSource>();
        src.clip = pickupSFX;
        src.spatialBlend = 0f;  // 2D — not affected by distance
        src.volume = 1f;
        src.Play();
        Destroy(tempAudio, pickupSFX.length);
    }

    // Override to add conditions — e.g. health must not be full
    protected virtual bool CanPickup(Collider2D player) => true;

    // Override to define what happens on pickup
    protected abstract void OnPickedUp(Collider2D player);
}