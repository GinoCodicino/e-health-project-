using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [SerializeField] float durata = 0.25f;
    [SerializeField] float intensita = 0.05f;

    Vector3 posizioneOriginale;
    Coroutine shakeInCorso;

    void Awake()
    {
        posizioneOriginale = transform.localPosition;
    }

    public void Shake()
    {
        if (shakeInCorso != null) StopCoroutine(shakeInCorso);
        shakeInCorso = StartCoroutine(ShakeRoutine());
    }

    IEnumerator ShakeRoutine()
    {
        float tempo = 0f;
        while (tempo < durata)
        {
            // l'intensità diminuisce col tempo
            float attenuazione = 1f - (tempo / durata);
            float spostamentoOrizzontale = Random.Range(-1f, 1f) * intensita * attenuazione;
            transform.localPosition = posizioneOriginale +
                new Vector3(spostamentoOrizzontale, 0f, 0f);
            tempo += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = posizioneOriginale;
        shakeInCorso = null;
    }
}