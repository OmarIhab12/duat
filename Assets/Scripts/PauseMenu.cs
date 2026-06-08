using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;

public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;
    private bool isPaused = false;

    void Start()
    {
        pausePanel.SetActive(false);
    }

    public void OnPause(InputValue value)
    {
        if (!value.isPressed) return;
        if (isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        isPaused = true;
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        isPaused = false;
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnResumeButton() => Resume();

    public void OnMainMenuButton()
    {
        Time.timeScale = 1f;
        StartCoroutine(LoadMainMenu());
    }

    IEnumerator LoadMainMenu()
    {
        yield return StartCoroutine(FadeManager.Instance.FadeOut(1f));
        SceneManager.LoadScene("MainMenu");
    }
}