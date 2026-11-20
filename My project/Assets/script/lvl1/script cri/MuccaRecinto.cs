using System.Collections;
using System.Collections;
using UnityEngine;

public class MuccaRecinto : MonoBehaviour
{
    [System.Serializable]
    public class Azione
    {
        public string stato = "Idle";     // nome esatto dello stato nell'Animator
        public bool muovi = false;        // se è true, la mucca cammina verso un punto
        public float peso = 1f;           // più alto = più frequente
        public Vector2 durata = new Vector2(3f, 7f);   // secondi (solo se non si muove)
        public float velocita = 1f;       // metri al secondo (solo se si muove)
        public float velocitaAnimazione = 1f;   // se i piedi scivolano, aggiusta questo o la velocità
    }

    [Header("Area (se vuoto, cerca l'AreaRecinto nella scena)")]
    public AreaRecinto area;

    [Header("Cosa fa")]
    public Azione[] azioni = new Azione[]
    {
        new Azione { stato = "Idle", muovi = false, peso = 3f,   durata = new Vector2(3f, 7f) },
        new Azione { stato = "Walk", muovi = true,  peso = 3f,   velocita = 1f },
        new Azione { stato = "Run",  muovi = true,  peso = 0.7f, velocita = 2.5f },
        new Azione { stato = "Jump", muovi = false, peso = 0.3f, durata = new Vector2(1f, 1.3f) },
    };

    [Header("Modello")]
    public float correzioneRotazione = 0f;   // se va di lato o all'indietro, prova 90, 180 o 270
    public float velocitaRotazione = 120f;   // gradi al secondo
    public float angoloPartenza = 25f;       // parte a camminare solo quando è girata meno di così
    public float sfumatura = 0.25f;

    Animator animator;
    float quotaY;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("MuccaRecinto: nessun Animator trovato su " + name);
            enabled = false;
            return;
        }

        if (area == null) area = FindFirstObjectByType<AreaRecinto>();
        if (area == null || !area.Valido)
            Debug.LogWarning("MuccaRecinto: manca un AreaRecinto con gli angoli assegnati. " + name + " si muoverà senza limiti.");

        animator.applyRootMotion = false;
        quotaY = transform.position.y;

        ControllaLoop();
        StartCoroutine(Ciclo());
    }

    // Avvisa se le clip principali non sono in loop (causa del "trascinamento")
    void ControllaLoop()
    {
        RuntimeAnimatorController ctrl = animator.runtimeAnimatorController;
        if (ctrl == null)
        {
            Debug.LogWarning("MuccaRecinto: l'Animator di " + name + " non ha un Controller.");
            return;
        }

        foreach (AnimationClip c in ctrl.animationClips)
        {
            string n = c.name.ToLower();
            bool serveLoop = n.Contains("idle") || n.Contains("walk") || n.Contains("run");
            if (serveLoop && !c.isLooping)
                Debug.LogWarning("La clip '" + c.name + "' non è in loop: dopo pochi passi si ferma sull'ultima posa. " +
                                 "Seleziona il file FBX, tab Animation, spunta Loop Time e premi Apply.");
        }
    }

    IEnumerator Ciclo()
    {
        yield return new WaitForSeconds(Random.Range(0f, 3f));   // non partono tutte insieme

        while (true)
        {
            Azione a = Scegli();
            animator.speed = Mathf.Max(0.1f, a.velocitaAnimazione);

            if (a.muovi)
            {
                yield return Muovi(a);
            }
            else
            {
                animator.CrossFadeInFixedTime(a.stato, sfumatura);
                yield return new WaitForSeconds(Random.Range(a.durata.x, a.durata.y));
            }
        }
    }

    IEnumerator Muovi(Azione a)
    {
        Vector3 meta = area != null ? area.PuntoCasuale(quotaY) : transform.position;
        bool camminando = false;
        float limiteTempo = 20f;

        while (limiteTempo > 0f)
        {
            Vector3 dir = meta - transform.position;
            dir.y = 0f;
            if (dir.magnitude < 0.3f) break;
            dir.Normalize();

            Quaternion voluta = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, correzioneRotazione, 0f);
            float angolo = Quaternion.Angle(transform.rotation, voluta);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, voluta, velocitaRotazione * Time.deltaTime);

            if (angolo < angoloPartenza)
            {
                if (!camminando)
                {
                    camminando = true;
                    animator.CrossFadeInFixedTime(a.stato, sfumatura);
                }

                Vector3 prossima = transform.position + dir * a.velocita * Time.deltaTime;
                prossima.y = quotaY;

                // si ferma se il passo successivo uscirebbe dal recinto
                if (area != null && !area.Permesso(prossima, 0.5f)) break;
                transform.position = prossima;
            }
            else if (camminando)
            {
                camminando = false;
                animator.CrossFadeInFixedTime("Idle", sfumatura);
            }

            limiteTempo -= Time.deltaTime;
            yield return null;
        }

        animator.speed = 1f;
        animator.CrossFadeInFixedTime("Idle", sfumatura);
    }

    Azione Scegli()
    {
        float totale = 0f;
        foreach (Azione a in azioni) totale += a.peso;

        float r = Random.value * totale;
        foreach (Azione a in azioni)
        {
            r -= a.peso;
            if (r <= 0f) return a;
        }
        return azioni[0];
    }
}