using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance;

    [Header("References")]
    public Image fadeImage;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Subscribe to scene loaded event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Fires every time any scene finishes loading — including the first
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(FadeIn(1f));
    }

    public IEnumerator FadeOut(float duration)
    {
        float t = 0;
        fadeImage.color = new Color(0, 0, 0, 0);
        while (t < duration)
        {
            t += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Clamp01(t / duration));
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 1);
    }

    public IEnumerator FadeIn(float duration)
    {
        float t = 0;
        fadeImage.color = new Color(0, 0, 0, 1);
        while (t < duration)
        {
            t += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, 1 - Mathf.Clamp01(t / duration));
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 0);
    }
}