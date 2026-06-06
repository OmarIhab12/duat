using UnityEngine;

public class CanopicJar : Pickup
{
    public JarType jarType;

    protected override void OnPickedUp(Collider2D player)
    {
        player.GetComponent<PlayerInventory>()?.CollectJar(jarType);
    }
}