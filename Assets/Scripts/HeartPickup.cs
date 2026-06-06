using UnityEngine;

public class HeartPickup : Pickup
{
    protected override bool CanPickup(Collider2D player)
    {
        PlayerController pc = player.GetComponent<PlayerController>();
        return pc != null && pc.CurrentHearts < pc.MaxHearts;
    }

    protected override void OnPickedUp(Collider2D player)
    {
        player.GetComponent<PlayerController>()?.Heal(1);
    }
}