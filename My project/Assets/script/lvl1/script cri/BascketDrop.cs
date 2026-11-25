using System.Collections;
using UnityEngine;

public class BasketDrop : MonoBehaviour
{
    [Header("Riferimenti")]
    public Camera playerCamera;
    public GameObject oggettoDaFarComparire; // Può essere un Prefab OPPURE un oggetto già in scena (disattivato)
    public RaccoltaLegnetti minigiocoLegnetti;

    [Header("Posizionamento davanti al Player (solo se è un Prefab)")]
    public bool posizionaDavantiAlPlayer = true;
    public float distanceInFront = 2.2f;
    public float lateralOffset = 1.2f;
    public LayerMask groundMask = ~0;

    [Header("Audio (opzionale)")]
    public AudioClip spawnSound;

    private bool giaComparso;

    void Start()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main;

        if (minigiocoLegnetti == null)
            minigiocoLegnetti = FindFirstObjectByType<RaccoltaLegnetti>();

        // Se l'oggetto è già nella scena, lo nasconde all'avvio
        if (oggettoDaFarComparire != null && oggettoDaFarComparire.scene.IsValid())
            oggettoDaFarComparire.SetActive(false);

        if (minigiocoLegnetti != null)
            minigiocoLegnetti.onVittoria.AddListener(OnMinigameCompleted);
    }

    public void OnMinigameCompleted()
    {
        if (giaComparso) return;
        giaComparso = true;

        StartCoroutine(MaterializzaDopoChiusura());
    }

    private IEnumerator MaterializzaDopoChiusura()
    {
        // Aspetta un attimo che il pop-up del minigioco si chiuda
        yield return new WaitForSecondsRealtime(0.2f);

        if (oggettoDaFarComparire == null) yield break;

        Vector3 spawnPos = oggettoDaFarComparire.transform.position;

        // Calcola il punto sul terreno davanti al player se richiesto
        if (posizionaDavantiAlPlayer && playerCamera != null)
        {
            Vector3 forward = playerCamera.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = playerCamera.transform.right;
            right.y = 0f;
            right.Normalize();

            spawnPos = playerCamera.transform.position + (forward * distanceInFront) + (right * lateralOffset);

            // Trova l'altezza esatta del terreno sotto quel punto
            if (Physics.Raycast(spawnPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 30f, groundMask))
                spawnPos = hit.point;
        }

        // Controlla se è un oggetto già in scena oppure un Prefab da istanziare
        if (oggettoDaFarComparire.scene.IsValid())
        {
            if (posizionaDavantiAlPlayer)
                oggettoDaFarComparire.transform.position = spawnPos;

            oggettoDaFarComparire.SetActive(true);
        }
        else
        {
            Quaternion rot = playerCamera != null
                ? Quaternion.Euler(0f, playerCamera.transform.eulerAngles.y + 180f, 0f)
                : Quaternion.identity;

            Instantiate(oggettoDaFarComparire, spawnPos, rot);
        }

        if (spawnSound != null)
            AudioSource.PlayClipAtPoint(spawnSound, spawnPos);
    }
}