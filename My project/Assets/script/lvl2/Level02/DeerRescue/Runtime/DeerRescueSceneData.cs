using System;
using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
#endif

namespace Level02.DeerRescue
{
    [Serializable]
    internal sealed class DeerRescueSceneData
    {
        [Serializable]
        internal sealed class Surface
        {
            public Mesh mesh;
            public Vector3[] vertices;
            public int[] triangles;
        }

        public GameObject referenceDeer;
        public Surface[] surfaces;

        public Surface Get(Mesh mesh)
        {
            if (mesh == null) return null;
            if (surfaces != null)
                foreach (var surface in surfaces)
                    if (surface.mesh == mesh) return surface;
            return mesh.isReadable ? new Surface {
                mesh = mesh, vertices = mesh.vertices, triangles = mesh.triangles
            } : null;
        }
    }

#if UNITY_EDITOR
    // In Play leggiamo gli asset del progetto. Per il player salviamo gli stessi dati
    // nella copia della scena preparata per la build, anche con Read/Write disattivato.
    public sealed class DeerRescueSceneBaker : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var view in root.GetComponentsInChildren<DeerRescueView>(true))
                    view.sceneData = Capture(view);
        }

        internal static DeerRescueSceneData Capture(DeerRescueView view)
        {
            var data = new DeerRescueSceneData();
            var meshes = new HashSet<Mesh>();
            Transform deer = view.FindDeer();
            if (deer != null)
            {
                data.referenceDeer = PrefabUtility.GetCorrespondingObjectFromOriginalSource(deer.gameObject);
                // In a build copy Unity can already have unpacked the scene instance.
                if (data.referenceDeer == null && deer.name == "Deer_001")
                    data.referenceDeer = AssetDatabase.LoadAssetAtPath<GameObject>(
                        AssetDatabase.GUIDToAssetPath("f612768321ebc364fa742fa8bbfadb5e"));
                foreach (var skin in deer.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (skin.sharedMesh != null) meshes.Add(skin.sharedMesh);
                foreach (var shape in deer.GetComponentsInChildren<MeshFilter>(true))
                    if (shape.sharedMesh != null) meshes.Add(shape.sharedMesh);
            }
            var wood = view.FindTimber();
            if (wood != null) meshes.Add(wood.sharedMesh);
            var surfaces = new List<DeerRescueSceneData.Surface>();
            foreach (var mesh in meshes)
            {
                using (var read = MeshUtility.AcquireReadOnlyMeshData(mesh))
                {
                    var src = read[0];
                    var triangles = new List<int>();
                    using (var vertices = new NativeArray<Vector3>(src.vertexCount, Allocator.Temp))
                    {
                        src.GetVertices(vertices);
                        for (int i = 0; i < src.subMeshCount; i++)
                        {
                            var sub = src.GetSubMesh(i);
                            if (sub.topology != MeshTopology.Triangles) continue;
                            using (var indices = new NativeArray<int>(sub.indexCount, Allocator.Temp))
                            {
                                src.GetIndices(indices, i, true);
                                triangles.AddRange(indices.ToArray());
                            }
                        }
                        surfaces.Add(new DeerRescueSceneData.Surface {
                            mesh = mesh, vertices = vertices.ToArray(), triangles = triangles.ToArray()
                        });
                    }
                }
            }
            data.surfaces = surfaces.ToArray();
            return data;
        }
    }
#endif
}
