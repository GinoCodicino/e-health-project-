using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class AutoBoxCollider : MonoBehaviour
{
    [ContextMenu("Adatta Box Collider alle mesh")]
    public void FitBoxCollider()
    {
        MeshFilter[] meshFilters =
            GetComponentsInChildren<MeshFilter>(true);

        Bounds totalBounds = new Bounds();
        bool hasBounds = false;

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
                continue;

            Bounds meshBounds = meshFilter.sharedMesh.bounds;

            Matrix4x4 toLocal =
                transform.worldToLocalMatrix *
                meshFilter.transform.localToWorldMatrix;

            // Trasforma gli otto angoli dell'ingombro della mesh.
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = meshBounds.center +
                            Vector3.Scale(
                                meshBounds.extents,
                                new Vector3(x, y, z)
                            );

                        Vector3 localCorner =
                            toLocal.MultiplyPoint3x4(corner);

                        if (!hasBounds)
                        {
                            totalBounds =
                                new Bounds(localCorner, Vector3.zero);

                            hasBounds = true;
                        }
                        else
                        {
                            totalBounds.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }

        if (!hasBounds)
        {
            Debug.LogWarning(
                "Nessuna mesh trovata nel prefab.", this
            );
            return;
        }

        BoxCollider box = GetComponent<BoxCollider>();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (box == null)
                box = Undo.AddComponent<BoxCollider>(gameObject);

            Undo.RecordObject(box, "Adatta Box Collider");
        }
        else
#endif
        {
            if (box == null)
                box = gameObject.AddComponent<BoxCollider>();
        }

        box.center = totalBounds.center;
        box.size = totalBounds.size;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
            EditorUtility.SetDirty(box);
        }
#endif
    }
}
