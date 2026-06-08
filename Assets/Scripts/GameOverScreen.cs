using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameOverScreen : MonoBehaviour
{
    [Header("References")]
    public GameObject gameOverPanel;

    public void Show()
    {
        gameOverPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void OnRetry()
    {
        Time.timeScale = 1f;
        StartCoroutine(Reload());
    }

    public void OnMainMenu()
    {
        Time.timeScale = 1f;
        StartCoroutine(LoadMainMenu());
    }

    IEnumerator Reload()
    {
        yield return StartCoroutine(FadeManager.Instance.FadeOut(1f));
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    IEnumerator LoadMainMenu()
    {
        yield return StartCoroutine(FadeManager.Instance.FadeOut(1f));
        SceneManager.LoadScene("MainMenu");
    }
}