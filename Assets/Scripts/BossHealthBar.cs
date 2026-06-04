using UnityEngine;
using UnityEngine.UI;

public class BossHealthBar : MonoBehaviour
{
    public Image fillBar;
    public GameObject container;

    public void UpdateHealth(int current, int max)
    {
        container.SetActive(true);
        fillBar.fillAmount = (float)current / max;
    }

    public void Hide()
    {
        container.SetActive(false);
        fillBar.gameObject.SetActive(false);
    }
}