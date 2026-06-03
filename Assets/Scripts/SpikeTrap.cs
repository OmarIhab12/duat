using UnityEngine;
using System.Collections;

public class SpikeTrap : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite retractedSprite;
    public Sprite emergingSprite;
    public Sprite outSprite;

    [Header("Timing")]
    public float retractedDuration = 2f;
    public float emergingDuration = 0.5f;
    public float outDuration = 0.5f;

    [Header("Damage")]
    public int damage = 1;
    public float immunityDuration = 1f;

    private SpriteRenderer sr;
    private CircleCollider2D col;
    private bool active = false;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.enabled = false;
    }

    void Start()
    {
        float randomOffset = Random.Range(0f, retractedDuration);
        StartCoroutine(StartWithDelay(randomOffset));
    }

    IEnumerator StartWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartCoroutine(SpikeLoop());
    }

    IEnumerator SpikeLoop()
    {
        while (true)
        {
            // Retracted
            sr.sprite = retractedSprite;
            col.enabled = false;
            yield return new WaitForSeconds(retractedDuration);

            // Emerging — warning
            sr.sprite = emergingSprite;
            col.enabled = false;
            yield return new WaitForSeconds(emergingDuration);

            // Out — dangerous
            sr.sprite = outSprite;
            col.enabled = true;
            yield return new WaitForSeconds(outDuration);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !other.GetComponentInParent<PlayerController>()) return;
        other.GetComponentInParent<PlayerController>()?.TakeDamage(damage);
    }
}