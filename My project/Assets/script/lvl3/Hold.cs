using System.Collections.Generic;
using UnityEngine;

public class Hold : MonoBehaviour
{
    static readonly Color AimedColor = Color.yellow;
    static readonly Color TakenColor = new Color(1f, 0.15f, 0.1f);

    [Header("Voie")]
    [Tooltip("Prise de départ : attrapable quand aucune main ne tient.")]
    public bool isStart;
    [Tooltip("Prise du sommet : l'attraper = victoire.")]
    public bool isSummit;

    // Rempli automatiquement par Route (pas sauvegardé dans la scène).
    [System.NonSerialized] public List<Hold> Next = new List<Hold>();   // prises voisines
    [System.NonSerialized] public bool reachable = true;                // résultat du BFS

    Renderer rend;
    Material mat;
    bool aimed;

    public Hand Holder { get; private set; }
    public bool IsTaken => Holder != null;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend == null)
        {
            Debug.LogError($"Hold '{name}' : pas de Renderer.", this);
            return;
        }
        mat = rend.material;               // instance propre à cette prise
        mat.EnableKeyword("_EMISSION");
        Refresh();
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    public void SetHighlight(bool highlight)
    {
        aimed = highlight;
        Refresh();
    }

    public void SetTaken(Hand hand)
    {
        Holder = hand;
        Refresh();
    }

    void Refresh()
    {
        if (mat == null) return;
        Color c = IsTaken ? TakenColor : aimed ? AimedColor : Color.black;
        mat.SetColor("_EmissionColor", c);
    }
}
