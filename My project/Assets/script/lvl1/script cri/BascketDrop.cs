using System.Collections;
using UnityEngine;

public class BasketDrop : MonoBehaviour
{
    [Header("Riferimenti")]
    public Camera playerCamera;            // la camera del PlayerFPS
    public GameObject minigamePanel;       // il pannello/canvas del minigioco da chiudere
    public GameObject basketPrefab;        // prefab della cesta (con i legnetti dentro)
    public RaccoltaLegnetti minigiocoLegnetti;
    public PuntoMinigioco puntoMinigioco;  // punto che segnala il completamento del minigioco

    [Header("Completamento automatico")]
    public bool controllaPuntoMinigioco = true;

    [Header("Posizione di caduta")]
    public float distanceInFront = 2.2f;   // distanza davanti al giocatore
    public float lateralOffset = 1.4f;     // spostamento laterale rispetto alla camera
    public float dropHeight = 2.5f;        // altezza da cui cade
    public LayerMask groundMask = ~0;

    [Header("Audio (opzionale)")]
    public AudioClip impactSound;

    bool cadutaAvviata;

    void Start()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main;

        if (puntoMinigioco == null)
        {
            puntoMinigioco = GetComponent<PuntoMinigioco>();
            if (puntoMinigioco == null)
                puntoMinigioco = GetComponentInParent<PuntoMinigioco>();
            if (puntoMinigioco == null)
                puntoMinigioco = FindFirstObjectByType<PuntoMinigioco>();
        }

        if (minigiocoLegnetti == null)
        {
            minigiocoLegnetti = GetComponent<RaccoltaLegnetti>();
            if (minigiocoLegnetti == null)
                minigiocoLegnetti = GetComponentInParent<RaccoltaLegnetti>();
            if (minigiocoLegnetti == null)
                minigiocoLegnetti = FindFirstObjectByType<RaccoltaLegnetti>();
        }

        if (minigiocoLegnetti != null)
            minigiocoLegnetti.onVittoria.AddListener(OnMinigameCompleted);
    }

    void Update()
    {
        if (controllaPuntoMinigioco && puntoMinigioco != null && puntoMinigioco.completato)
            OnMinigameCompleted();
    }

    public void OnMinigameCompleted()
    {
        if (cadutaAvviata)
            return;

        cadutaAvviata = true;

        if (minigamePanel != null)
            minigamePanel.SetActive(false);

        // Riabilita il controllo del giocatore (vedi nota sotto)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;

        StartCoroutine(DropBasket());
    }

    private IEnumerator DropBasket()
    {
        yield return new WaitForSecondsRealtime(0.3f); // piccola pausa dopo la chiusura

        if (playerCamera == null)
        {
            Debug.LogWarning("BasketDrop: manca la camera del giocatore.");
            yield break;
        }

        // Direzione orizzontale davanti e di lato alla camera.
        Vector3 forward = playerCamera.transform.forward;
        forward.y = 0;
        forward.Normalize();
        Vector3 right = playerCamera.transform.right;
        right.y = 0;
        right.Normalize();

        Vector3 groundPos = playerCamera.transform.position +
                            forward * distanceInFront +
                            right * lateralOffset;

        // Trova il terreno sotto quel punto
        if (Physics.Raycast(groundPos + Vector3.up * 10f, Vector3.down,
                            out RaycastHit hit, 30f, groundMask))
            groundPos = hit.point;

        Vector3 spawnPos = groundPos + Vector3.up * dropHeight;
        Quaternion rot = Quaternion.Euler(Random.Range(-10f, 10f),
                                          Random.Range(0f, 360f),
                                          Random.Range(-10f, 10f));

        GameObject basket = basketPrefab != null
            ? Instantiate(basketPrefab, spawnPos, rot)
            : CreaCestaFallback(spawnPos, rot);

        // Fisica: serve un Rigidbody e un Collider
        Rigidbody rb = basket.GetComponent<Rigidbody>();
        if (rb == null) rb = basket.AddComponent<Rigidbody>();
        if (basket.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider col = basket.AddComponent<BoxCollider>();
        }
        rb.mass = 1.2f;
        rb.linearDamping = 1.8f;
        rb.angularDamping = 3f;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Random.insideUnitSphere * 0.35f;

        // Suono all'impatto
        if (impactSound != null)
            basket.AddComponent<ImpactSound>().clip = impactSound;
    }

    GameObject CreaCestaFallback(Vector3 posizione, Quaternion rotazione)
    {
        GameObject basket = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basket.name = "Cesta legnetti";
        basket.transform.SetPositionAndRotation(posizione, rotazione);
        basket.transform.localScale = new Vector3(1.3f, 0.45f, 1f);
        return basket;
    }
}

// Riproduce un suono al primo impatto, poi si rimuove
public class ImpactSound : MonoBehaviour
{
    public AudioClip clip;
    void OnCollisionEnter(Collision c)
    {
        AudioSource.PlayClipAtPoint(clip, transform.position);
        Destroy(this);
    }
}