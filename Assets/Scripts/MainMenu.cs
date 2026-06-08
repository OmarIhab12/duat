using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject creditsPanel;

    [Header("Settings")]
    public string firstSceneName = "CutsceneIntro";

    void Start()
    {
        mainPanel.SetActive(true);
        creditsPanel.SetActive(false);
        // MusicManager.Instance?.PlayMenuMusic();
    }

    public void OnPlay()
    {
        StartCoroutine(LoadScene(firstSceneName));
    }

    public void OnCredits()
    {
        mainPanel.SetActive(false);
        creditsPanel.SetActive(true);
    }

    public void OnBackFromCredits()
    {
        creditsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void OnQuit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    IEnumerator LoadScene(string sceneName)
    {
        yield return StartCoroutine(FadeManager.Instance.FadeOut(1f));
        SceneManager.LoadScene(sceneName);
    }
}