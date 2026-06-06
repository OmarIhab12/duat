using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.Tilemaps;

public class PillarAltar : MonoBehaviour
{
    [Header("Puzzle Config")]
    public JarType[] correctOrder = { JarType.Eye, JarType.Ankh, JarType.Scarab };

    [Header("Altar Visuals")]
    public SpriteRenderer altarRenderer;
    public Sprite stateEmpty;
    public Sprite stateOne;
    public Sprite stateTwo;
    public Sprite stateThree;

    [Header("Door")]
    public Tilemap doorTilemap;

    [Header("Interaction")]
    public GameObject interactPrompt;
    public float wrongFlashDuration = 0.5f;

    [Header("Audio")]

    public AudioSource audioSource;
    public AudioClip depositSFX;      // jar placed in slot
    public AudioClip wrongOrderSFX;   // wrong jar flash
    public AudioClip doorOpenSFX;     // door rumbles open

    private int depositCount = 0;
    private bool playerInRange = false;
    private bool solved = false;
    private PlayerInventory playerInventory;


    void Awake()
    {
        if (audioSource == null && GetComponent<AudioSource>())
            audioSource = GetComponent<AudioSource>();
    }
    // Called by PlayerInput component via SendMessages
    public void OnDeposit(InputValue value)
    {
        Debug.Log($"[Altar] OnDeposit called with value: {value}");
        if (!value.isPressed) return;
        if (!playerInRange || solved) return;
        TryDeposit();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        playerInventory = other.GetComponent<PlayerInventory>();
        other.GetComponent<PlayerController>()?.SetNearbyAltar(this);
        if (interactPrompt) interactPrompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        playerInventory = null;
        other.GetComponent<PlayerController>()?.ClearNearbyAltar();
        if (interactPrompt) interactPrompt.SetActive(false);
    }

    void TryDeposit()
    {
        if (depositCount >= 3 || playerInventory == null) return;

        JarType expected = correctOrder[depositCount];

        if (!playerInventory.HasJar(expected))
        {
            Debug.Log($"[Altar] Need {expected} next but player doesn't have it.");
            StartCoroutine(FlashWrong());
            return;
        }

        playerInventory.UseJar(expected);
        depositCount++;
        // Play deposit sound
        if (depositSFX) audioSource.PlayOneShot(depositSFX);
        Debug.Log($"[Altar] Deposited {expected} — state {depositCount}/3");

        UpdateVisual();

        if (depositCount == 3)
            StartCoroutine(OpenDoor());
    }

    void UpdateVisual()
    {
        if (altarRenderer == null) return;
        altarRenderer.sprite = depositCount switch
        {
            1 => stateOne,
            2 => stateTwo,
            3 => stateThree,
            _ => stateEmpty
        };
    }

    IEnumerator FlashWrong()
    {
        // Play wrong order sound
        if (wrongOrderSFX) audioSource.PlayOneShot(wrongOrderSFX);
        if (altarRenderer == null) yield break;
        Color original = altarRenderer.color;
        altarRenderer.color = new Color(1f, 0.2f, 0.2f);
        yield return new WaitForSeconds(wrongFlashDuration);
        altarRenderer.color = original;
    }

    IEnumerator OpenDoor()
    {
        solved = true;
        if (interactPrompt) interactPrompt.SetActive(false);
        yield return new WaitForSeconds(0.8f);
        doorTilemap.ClearAllTiles();
        // Play door open sound
        if (doorOpenSFX) audioSource.PlayOneShot(doorOpenSFX);
        doorTilemap.GetComponent<TilemapCollider2D>().enabled = false;
        Debug.Log("[Altar] Door opened!");
    }

    public void ResetAltar()
    {
        depositCount = 0;
        solved = false;
        UpdateVisual();
    }
}