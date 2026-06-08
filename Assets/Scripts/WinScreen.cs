using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class WinScreen : MonoBehaviour
{
    [Header("References")]
    public GameObject winPanel;

    public void Show()
    {
        winPanel.SetActive(true);
        Time.timeScale = 0f;
        // MusicManager.Instance?.PlayMenuMusic();
    }

    public void OnMainMenu()
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