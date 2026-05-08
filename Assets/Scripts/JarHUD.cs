using UnityEngine;
using UnityEngine.UI;

public class JarHUD : MonoBehaviour
{
    [System.Serializable]
    public struct JarSlot
    {
        public JarType jarType;
        public Image icon;          // the UI Image
        public Sprite emptySprite;  // transparent version
        public Sprite fullSprite;   // solid version
    }

    public JarSlot[] slots; // set all 3 in Inspector
    private PlayerInventory inventory;

    void Start()
    {
        inventory = FindObjectOfType<PlayerInventory>();
        inventory.OnJarCollected += OnJarCollected;
        ResetHUD();
    }

    void OnDestroy()
    {
        if (inventory != null)
            inventory.OnJarCollected -= OnJarCollected;
    }

    void ResetHUD()
    {
        foreach (var slot in slots)
            slot.icon.sprite = slot.emptySprite;
    }

    void OnJarCollected(JarType type)
    {
        foreach (var slot in slots)
        {
            if (slot.jarType == type)
            {
                slot.icon.sprite = slot.fullSprite;
                return;
            }
        }
    }
}