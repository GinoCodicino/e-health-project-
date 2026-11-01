using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Une voie d'escalade : contient toutes les prises (en enfants) et calcule
/// automatiquement le graphe des voisines (Hold.Next) à partir des positions.
/// Le graphe est aussi calculé dans l'éditeur pour être visible avec les gizmos.
/// </summary>
[ExecuteAlways]
public class Route : MonoBehaviour
{
    [Header("Règle de voisinage")]
    [Tooltip("Distance max entre deux prises voisines (m).")]
    [Min(0f)] public float reach = 1.2f;
    [Tooltip("Une voisine peut être plus basse que la prise de départ, au plus de cette valeur (m).")]
    [Min(0f)] public float downTolerance = 0.3f;

    [Header("Gizmos")]
    public float gizmoRadius = 0.15f;

    List<Hold> holds = new List<Hold>();

    // ---------- Cycle de vie ----------

    void Awake()
    {
        if (Application.isPlaying)
            Refresh(true);
    }

    // Appelé dans l'éditeur quand on change une valeur dans l'Inspector.
    void OnValidate()
    {
        Refresh(true);
    }

    /// <summary>Recalcule le graphe puis le vérifie.</summary>
    public void Refresh(bool logWarnings)
    {
        BuildGraph();
        Validate(logWarnings);
    }

    // ---------- 1. Construction du graphe ----------

    void BuildGraph()
    {
        holds.Clear();
        holds.AddRange(GetComponentsInChildren<Hold>());

        foreach (Hold a in holds)
        {
            a.Next.Clear();
            foreach (Hold b in holds)
            {
                if (IsNeighbour(a, b))
                    a.Next.Add(b);
            }
        }
    }

    // B est une suivante de A ?
    bool IsNeighbour(Hold a, Hold b)
    {
        if (a == b) return false;

        Vector3 pa = a.transform.position;
        Vector3 pb = b.transform.position;

        bool closeEnough = Vector3.Distance(pa, pb) <= reach;
        bool notTooLow = pb.y >= pa.y - downTolerance;
        return closeEnough && notTooLow;
    }

    // ---------- 2. Validation par BFS ----------

    /// <summary>
    /// Parcours en largeur depuis toutes les prises de départ.
    /// Marque chaque prise (reachable) et renvoie vrai si un sommet est atteignable.
    /// </summary>
    public bool Validate(bool logWarnings)
    {
        var visited = new HashSet<Hold>();
        var queue = new Queue<Hold>();

        // On démarre de TOUTES les prises de départ en même temps.
        foreach (Hold h in holds)
        {
            if (h.isStart)
            {
                visited.Add(h);
                queue.Enqueue(h);
            }
        }

        // On défile une prise, on ajoute ses voisines pas encore vues.
        while (queue.Count > 0)
        {
            Hold current = queue.Dequeue();
            foreach (Hold next in current.Next)
            {
                if (!visited.Contains(next))
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        // Résultat
        bool hasStart = false, hasSummit = false, summitReached = false;
        var unreachable = new List<string>();
        foreach (Hold h in holds)
        {
            h.reachable = visited.Contains(h);
            if (h.isStart) hasStart = true;
            if (h.isSummit) hasSummit = true;
            if (h.isSummit && h.reachable) summitReached = true;
            if (!h.reachable) unreachable.Add(h.name);
        }

        if (logWarnings)
        {
            if (holds.Count == 0)
                Debug.LogWarning($"Route '{name}' : aucune prise en enfant.", this);
            else if (!hasStart)
                Debug.LogWarning($"Route '{name}' : aucune prise de départ (isStart).", this);
            else if (!hasSummit)
                Debug.LogWarning($"Route '{name}' : aucune prise de sommet (isSummit).", this);
            else if (!summitReached)
                Debug.LogWarning($"Route '{name}' : le sommet n'est PAS atteignable depuis le départ. " +
                                 $"Prises inatteignables : {string.Join(", ", unreachable)}", this);
        }

        return summitReached;
    }

    // ---------- 3. Gizmos ----------

    void OnDrawGizmos()
    {
        // Hors Play : on recalcule à chaque dessin pour suivre les prises qu'on déplace.
        // (sans warning, sinon la Console serait inondée)
        if (!Application.isPlaying)
            Refresh(false);

        foreach (Hold a in holds)
        {
            if (a == null) continue;
            Vector3 pa = a.transform.position;

            // Traits vers les voisines + petit point côté arrivée pour voir le sens.
            Gizmos.color = Color.white;
            foreach (Hold b in a.Next)
            {
                Vector3 pb = b.transform.position;
                Gizmos.DrawLine(pa, pb);
                Gizmos.DrawSphere(Vector3.Lerp(pa, pb, 0.8f), gizmoRadius * 0.3f);
            }

            // Couleur de la prise
            if (!a.reachable)     Gizmos.color = Color.red;
            else if (a.isSummit)  Gizmos.color = Color.cyan;
            else if (a.isStart)   Gizmos.color = Color.green;
            else                  Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(pa, gizmoRadius * 2f);
        }
    }
}
