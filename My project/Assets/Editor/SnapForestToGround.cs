using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SnapForestToGround : MonoBehaviour
{
    // Aumentato leggermente l'affondamento per gruppi larghi come Rock Pile
    private const float SINK_AMOUNT = 0.25f;

    [MenuItem("Tools/Adatta Oggetti al Terreno")]
    private static void SnapSelectedObjects()
    {
        if (Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("Nessun oggetto selezionato!");
            return;
        }

        // Disattiva tutti i collider (inclusi tutti i figli di Rock Pile)
        var allColliders = new List<Collider>();
        foreach (GameObject go in Selection.gameObjects)
        {
            allColliders.AddRange(go.GetComponentsInChildren<Collider>());
        }

        foreach (var col in allColliders)
        {
            if (col != null) col.enabled = false;
        }

        try
        {
            Terrain terrain = Terrain.activeTerrain;

            foreach (GameObject go in Selection.gameObjects)
            {
                Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;

                // Calcola il bounding box globale unificato che include TUTTE le rocce del cumulo
                Bounds combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }

                // Consideriamo il centro orizzontale effettivo delle mesh, non solo il pivot del genitore
                Vector3 centerXZ = new Vector3(combinedBounds.center.x, go.transform.position.y, combinedBounds.center.z);
                float bottomOffset = go.transform.position.y - combinedBounds.min.y;

                float targetY = go.transform.position.y;
                bool hitGround = false;

                // Lancia il raggio partendo dal centro volumetrico dell'intero gruppo
                if (Physics.Raycast(centerXZ + Vector3.up * 500f, Vector3.down, out RaycastHit hit, 1000f))
                {
                    targetY = hit.point.y;
                    hitGround = true;
                }
                else if (terrain != null)
                {
                    targetY = terrain.SampleHeight(centerXZ) + terrain.transform.position.y;
                    hitGround = true;
                }

                if (hitGround)
                {
                    Undo.RecordObject(go.transform, "Snap to Ground");

                    // Applica la nuova altezza compensando il punto più basso di tutto il cumulo
                    go.transform.position = new Vector3(
                        go.transform.position.x,
                        targetY + bottomOffset - SINK_AMOUNT,
                        go.transform.position.z
                    );
                }
            }
        }
        finally
        {
            // Riattiva sempre i collider
            foreach (var col in allColliders)
            {
                if (col != null) col.enabled = true;
            }
        }
    }
}