using UnityEngine;
using UnityEngine.UI;

public class Vignette : MonoBehaviour
{
    [Header("Settings")]
    [Range(0f, 1f)] public float strength = 0.85f;    // how dark the edges get
    [Range(0f, 1f)] public float softness = 0.4f;     // how gradual the fade is
    public Color vignetteColor = new Color(0.05f, 0.02f, 0f, 1f);
    public int textureResolution = 256;

    private RawImage rawImage;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
        if (rawImage == null)
            rawImage = gameObject.AddComponent<RawImage>();

        rawImage.texture = GenerateVignetteTexture();
        rawImage.raycastTarget = false;

        // Stretch to fill entire screen
        RectTransform rt = GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Texture2D GenerateVignetteTexture()
    {
        int res = textureResolution;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[res * res];
        Vector2 centre = new Vector2(0.5f, 0.5f);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                Vector2 uv = new Vector2((float)x / res, (float)y / res);
                float dist = Vector2.Distance(uv, centre);

                // Remap dist to vignette alpha
                float vignette = Mathf.SmoothStep(
                    softness,
                    softness + (1f - softness) * strength,
                    dist);

                Color c = vignetteColor;
                c.a = Mathf.Clamp01(vignette);
                pixels[y * res + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}