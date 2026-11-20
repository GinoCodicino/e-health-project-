using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// Carnet du village : le joueur appuie sur une touche, le livre monte dans sa main
/// (devant la caméra) puis s'ouvre. Nouvelle pression : il se ferme et redescend.
///
/// Mise en place :
///  1. Glisser Assets/JournalBook/Models/village_journal_book.fbx comme enfant de la Main Camera du joueur.
///  2. Ajouter ce script sur le livre (la racine importée).
///  3. Dans "Open Clip", mettre le clip d'animation contenu dans le FBX.
///  Le reste se règle tout seul ; ajuster handOffset / handEuler si besoin.
public class JournalBookController : MonoBehaviour
{
    [Header("Animation (clip du FBX : fermé -> ouvert)")]
    [SerializeField] private AnimationClip openClip;

    [Header("Touche")]
    [SerializeField] private Key toggleKey = Key.B;

    [Header("Position dans la main (repère de la caméra)")]
    [SerializeField] private Vector3 handOffset = new Vector3(0f, -0.12f, 0.42f);
    [SerializeField] private Vector3 handEuler = new Vector3(-60f, 180f, 0f);

    [Header("Position rangée (hors écran, en bas)")]
    [SerializeField] private Vector3 stowedOffset = new Vector3(0.15f, -0.6f, 0.25f);
    [SerializeField] private Vector3 stowedEuler = new Vector3(10f, 200f, 30f);

    [Header("Durées")]
    [SerializeField] private float moveDuration = 0.45f;
    [SerializeField] private float openDuration = 0.9f;

    [Header("Optionnel : bloquer le joueur pendant la lecture")]
    [SerializeField] private playercontroller player;
    [SerializeField] private bool blockPlayerWhileReading = false;

    public bool IsOpen { get; private set; }
    private bool busy;
    private Renderer[] renderers;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        if (player == null) player = GetComponentInParent<playercontroller>();
    }

    private void Start()
    {
        SetPose(stowedOffset, stowedEuler);
        Sample(0f);
        SetVisible(false);
    }

    private void Update()
    {
        if (busy || Keyboard.current == null) return;
        if (Keyboard.current[toggleKey].wasPressedThisFrame)
            StartCoroutine(IsOpen ? PutAway() : TakeOut());
    }

    private IEnumerator TakeOut()
    {
        busy = true;
        if (blockPlayerWhileReading && player != null) player.canMove = false;
        Sample(0f);
        SetVisible(true);
        yield return Move(stowedOffset, stowedEuler, handOffset, handEuler, moveDuration);
        yield return PlayOpen(0f, 1f, openDuration);
        IsOpen = true;
        busy = false;
    }

    private IEnumerator PutAway()
    {
        busy = true;
        yield return PlayOpen(1f, 0f, openDuration * 0.7f);
        yield return Move(handOffset, handEuler, stowedOffset, stowedEuler, moveDuration);
        SetVisible(false);
        if (blockPlayerWhileReading && player != null) player.canMove = true;
        IsOpen = false;
        busy = false;
    }

    private IEnumerator Move(Vector3 fromPos, Vector3 fromEuler, Vector3 toPos, Vector3 toEuler, float duration)
    {
        Quaternion a = Quaternion.Euler(fromEuler), b = Quaternion.Euler(toEuler);
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            transform.localPosition = Vector3.Lerp(fromPos, toPos, k);
            transform.localRotation = Quaternion.Slerp(a, b, k);
            yield return null;
        }
        SetPose(toPos, toEuler);
    }

    private IEnumerator PlayOpen(float from, float to, float duration)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            Sample(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        Sample(to);
    }

    // 0 = fermé, 1 = ouvert
    private void Sample(float amount)
    {
        if (openClip == null) return;
        // SampleAnimation écrase aussi la position/rotation de la racine : on la préserve.
        Vector3 p = transform.localPosition; Quaternion r = transform.localRotation;
        openClip.SampleAnimation(gameObject, Mathf.Clamp01(amount) * openClip.length);
        transform.localPosition = p; transform.localRotation = r;
    }

    private void SetPose(Vector3 pos, Vector3 euler)
    {
        transform.localPosition = pos;
        transform.localRotation = Quaternion.Euler(euler);
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers) r.enabled = visible;
    }
}
