using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(500)]
public sealed class InquadraturaDialogo : MonoBehaviour
{
    private static InquadraturaDialogo corrente;
    private Transform occhio;
    private Vector3 posizioneLocale;
    private Quaternion rotazioneLocale;

    [SerializeField] private bool nascondiGiocatore;
    private readonly List<Renderer> partiNascoste = new List<Renderer>();


    public bool Attiva => corrente == this;

    public void Apri(playercontroller giocatore)
    {
        if (Attiva || !isActiveAndEnabled || !giocatore) return;

        Camera cam = Camera.main;
        if (!cam || !cam.transform.IsChildOf(giocatore.transform))
        {
            Camera cameraDelGiocatore = giocatore.GetComponentInChildren<Camera>();
            if (cameraDelGiocatore && cameraDelGiocatore.isActiveAndEnabled)
                cam = cameraDelGiocatore;
        }

        if (!cam) return;
        if (corrente) corrente.Chiudi();

        occhio = cam.transform;
        posizioneLocale = occhio.localPosition;
        rotazioneLocale = occhio.localRotation;
        corrente = this;
        if (nascondiGiocatore)
        {
            foreach (Renderer parte in giocatore.GetComponentsInChildren<Renderer>(true))
            {
                if (!(parte is MeshRenderer || parte is SkinnedMeshRenderer) || parte.forceRenderingOff)
                    continue;

                partiNascoste.Add(parte);
                parte.forceRenderingOff = true;
            }
        }


    }

    private void LateUpdate()
    {
        if (occhio)
            occhio.SetPositionAndRotation(transform.position, transform.rotation);
    }

    public void Chiudi()
    {
        if (!Attiva) return;


        foreach (Renderer parte in partiNascoste)
        {
            if (parte) parte.forceRenderingOff = false;
        }
        partiNascoste.Clear();


        if (occhio)
            occhio.SetLocalPositionAndRotation(posizioneLocale, rotazioneLocale);

        occhio = null;
        corrente = null;
    }

    private void OnDisable() => Chiudi();
}
