using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI Panels")]
    public GameOverScreen gameOverScreen;
    public WinScreen winScreen;
    public PauseMenu pauseMenu;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void TriggerGameOver() => gameOverScreen?.Show();
    public void TriggerWin() => winScreen?.Show();
}