using UnityEngine;
using UnityEditor;

public class SnapForestToGround : MonoBehaviour
{
    [MenuItem("Tools/Adatta Alberi al Terreno")]
    private static void SnapSelectedTrees()
    {
        // Raccoglie tutti i collider degli oggetti selezionati (compresi i figli)
        var allColliders = new System.Collections.Generic.List<Collider>();
        foreach (GameObject go in Selection.gameObjects)
        {
            allColliders.AddRange(go.GetComponentsInChildren<Collider>());
        }

        // Li disattiva temporaneamente per evitare che il raggio colpisca l'oggetto stesso
        foreach (var col in allColliders)
        {
            if (col != null) col.enabled = false;
        }

        try
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                // Calcola il punto più basso della mesh (se presente) per correggere pivot errati
                Renderer rend = go.GetComponentInChildren<Renderer>();
                float bottomOffset = 0f;
                if (rend != null)
                {
                    bottomOffset = go.transform.position.y - rend.bounds.min.y;
                }

                // Lancia il raggio verso il basso
                if (Physics.Raycast(go.transform.position + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 500f))
                {
                    Undo.RecordObject(go.transform, "Snap to Terrain");
                    // Posiziona l'oggetto compensando l'altezza del fondo
                    go.transform.position = new Vector3(go.transform.position.x, hit.point.y + bottomOffset, go.transform.position.z);
                }
            }
        }
        finally
        {
            // Riattiva tutti i collider
            foreach (var col in allColliders)
            {
                if (col != null) col.enabled = true;
            }
        }
    }
}