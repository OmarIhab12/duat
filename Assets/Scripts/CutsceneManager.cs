using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class CutsceneManager : MonoBehaviour
{
    [Header("Panels")]
    public Sprite[] panels;
    public string nextSceneName;

    [Header("UI References")]
    public Image panelImage;
    public Image blackOverlay;
    public GameObject nextButton;
    public GameObject prevButton;
    public GameObject skipButton;

    [Header("Animation Settings")]
    public float fadeDuration = 0.3f;
    public AnimationType animationType = AnimationType.Fade;

    [Header("Next Button Text")]
    public string nextButtonTextOnLastPanel = "Start Game";

    public enum AnimationType { Fade, SlideLeft, SlideRight, ZoomIn }

    private int currentIndex = 0;
    private bool isAnimating = false;
    private RectTransform panelRect;

    void Start()
    {
        panelRect = panelImage.GetComponent<RectTransform>();
        blackOverlay.color = new Color(0, 0, 0, 1);
        panelImage.sprite = panels[0];
        UpdateButtons();
        StartCoroutine(FadeFromBlack());
    }

    // Called by Next button
    public void OnNext()
    {
        if (isAnimating) return;
        if (currentIndex >= panels.Length - 1)
        {
            // Last panel — load next scene
            StartCoroutine(EndCutscene());
            return;
        }
        StartCoroutine(TransitionTo(currentIndex + 1, true));
    }

    // Called by Prev button
    public void OnPrev()
    {
        if (isAnimating || currentIndex <= 0) return;
        StartCoroutine(TransitionTo(currentIndex - 1, false));
    }

    // Called by Skip button
    public void OnSkip()
    {
        if (isAnimating) return;
        StartCoroutine(EndCutscene());
    }

    IEnumerator TransitionTo(int newIndex, bool goingForward)
    {
        isAnimating = true;

        yield return StartCoroutine(AnimateOut(goingForward));

        currentIndex = newIndex;
        panelImage.sprite = panels[currentIndex];
        UpdateButtons();

        yield return StartCoroutine(AnimateIn(goingForward));

        isAnimating = false;
    }

    IEnumerator AnimateOut(bool goingForward)
    {
        float t = 0;
        Vector2 startPos = Vector2.zero;
        Vector2 endPos = goingForward ?
            new Vector2(-Screen.width, 0) :
            new Vector2(Screen.width, 0);

        switch (animationType)
        {
            case AnimationType.Fade:
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    panelImage.color = new Color(1, 1, 1,
                        1 - Mathf.Clamp01(t / fadeDuration));
                    yield return null;
                }
                panelImage.color = new Color(1, 1, 1, 0);
                break;

            case AnimationType.SlideLeft:
            case AnimationType.SlideRight:
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    panelRect.anchoredPosition = Vector2.Lerp(
                        startPos, endPos,
                        Mathf.SmoothStep(0, 1, t / fadeDuration));
                    yield return null;
                }
                panelRect.anchoredPosition = endPos;
                break;

            case AnimationType.ZoomIn:
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    float scale = Mathf.Lerp(1f, 1.1f, t / fadeDuration);
                    panelRect.localScale = new Vector3(scale, scale, 1);
                    panelImage.color = new Color(1, 1, 1,
                        1 - Mathf.Clamp01(t / fadeDuration));
                    yield return null;
                }
                break;
        }
    }

    IEnumerator AnimateIn(bool goingForward)
    {
        float t = 0;
        Vector2 startPos = goingForward ?
            new Vector2(Screen.width, 0) :
            new Vector2(-Screen.width, 0);
        Vector2 endPos = Vector2.zero;

        switch (animationType)
        {
            case AnimationType.Fade:
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    panelImage.color = new Color(1, 1, 1,
                        Mathf.Clamp01(t / fadeDuration));
                    yield return null;
                }
                panelImage.color = Color.white;
                break;

            case AnimationType.SlideLeft:
            case AnimationType.SlideRight:
                panelRect.anchoredPosition = startPos;
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    panelRect.anchoredPosition = Vector2.Lerp(
                        startPos, endPos,
                        Mathf.SmoothStep(0, 1, t / fadeDuration));
                    yield return null;
                }
                panelRect.anchoredPosition = endPos;
                break;

            case AnimationType.ZoomIn:
                panelRect.localScale = new Vector3(1.1f, 1.1f, 1);
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    float scale = Mathf.Lerp(1.1f, 1f, t / fadeDuration);
                    panelRect.localScale = new Vector3(scale, scale, 1);
                    panelImage.color = new Color(1, 1, 1,
                        Mathf.Clamp01(t / fadeDuration));
                    yield return null;
                }
                panelRect.localScale = Vector3.one;
                panelImage.color = Color.white;
                break;
        }
    }

    void UpdateButtons()
    {
        // Hide prev on first panel
        prevButton.SetActive(currentIndex > 0);

        // Change next button text on last panel
        Button nextBtn = nextButton.GetComponent<Button>();
        TMPro.TextMeshProUGUI nextText = nextButton
            .GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (nextText != null)
            nextText.text = currentIndex >= panels.Length - 1 ?
                nextButtonTextOnLastPanel : "Next ›";
    }

    IEnumerator FadeFromBlack()
    {
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            blackOverlay.color = new Color(0, 0, 0,
                1 - Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        blackOverlay.color = new Color(0, 0, 0, 0);
    }

    IEnumerator EndCutscene()
    {
        isAnimating = true;
        float t = 0;
        while (t < fadeDuration * 2)
        {
            t += Time.deltaTime;
            blackOverlay.color = new Color(0, 0, 0,
                Mathf.Clamp01(t / (fadeDuration * 2)));
            yield return null;
        }
        SceneManager.LoadScene(nextSceneName);
    }
}