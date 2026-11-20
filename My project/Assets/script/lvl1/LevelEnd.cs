using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PassaggioLivello : MonoBehaviour
{
    [Header("Riferimenti")]
    [SerializeField] private Transform giocatore;
    [SerializeField] private GameObject testoProssimoLivello;

    [Header("Scena da caricare")]
    [SerializeField] private string nomeScenaSuccessiva;

    private readonly HashSet<Collider> colliderDentro = new();
    private bool caricamentoInCorso;

    private void Awake()
    {
        if (testoProssimoLivello != null)
            testoProssimoLivello.SetActive(false);
    }

    private void Update()
    {
        if (caricamentoInCorso || colliderDentro.Count == 0)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            CaricaScenaSuccessiva();
        }
    }

    private bool AppartieneAlGiocatore(Collider other)
    {
        if (giocatore == null)
            return false;

        return other.transform == giocatore ||
               other.transform.IsChildOf(giocatore);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!AppartieneAlGiocatore(other))
            return;

        colliderDentro.Add(other);

        if (testoProssimoLivello != null)
            testoProssimoLivello.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!colliderDentro.Remove(other))
            return;

        if (colliderDentro.Count == 0 &&
            testoProssimoLivello != null)
        {
            testoProssimoLivello.SetActive(false);
        }
    }

    private void CaricaScenaSuccessiva()
    {
        if (string.IsNullOrWhiteSpace(nomeScenaSuccessiva))
        {
            Debug.LogError(
                "Inserisci il nome della scena successiva nell'Inspector.",
                this
            );
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nomeScenaSuccessiva))
        {
            Debug.LogError(
                $"La scena '{nomeScenaSuccessiva}' non è caricabile. " +
                "Controlla il nome e la Scene List del Build Profile.",
                this
            );
            return;
        }

        caricamentoInCorso = true;

        if (testoProssimoLivello != null)
            testoProssimoLivello.SetActive(false);

        SceneManager.LoadSceneAsync(nomeScenaSuccessiva);
    }

    private void OnDisable()
    {
        colliderDentro.Clear();

        if (testoProssimoLivello != null)
            testoProssimoLivello.SetActive(false);
    }
}
