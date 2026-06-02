using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LevelTransition : MonoBehaviour
{
    [Header("Level")]
    public string nextSceneName; // exact name of next scene

    [Header("Fade")]
    public float fadeDuration = 1f;

    private bool transitioning = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (transitioning) return;
        if (!other.CompareTag("Player")) return;
        StartCoroutine(FadeAndLoad());
    }

    IEnumerator FadeAndLoad()
    {
        transitioning = true;
        yield return StartCoroutine(FadeManager.Instance.FadeOut(fadeDuration));
        SceneManager.LoadScene(nextSceneName);
    }
}