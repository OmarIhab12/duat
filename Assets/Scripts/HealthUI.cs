using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    [Header("References")]
    public PlayerController player;
    public Image[] heartImages;

    [Header("Sprites")]
    public Sprite heartFull;
    public Sprite heartEmpty;

    void Update()
    {
        RefreshHearts();
    }

    void RefreshHearts()
    {
        int current = player.CurrentHearts;

        for (int i = 0; i < heartImages.Length; i++)
        {
            heartImages[i].sprite = i < current ? heartFull : heartEmpty;
        }
    }
}