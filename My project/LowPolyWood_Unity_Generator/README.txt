LOW-POLY WOOD ASSETS — procedural Unity generator

1. Copy the Assets folder from this archive into your Unity project.
2. Wait for compilation, then choose Tools > Low Poly Wood > Generate Both Prefabs.
3. Find HandSaw.prefab and WoodPile.prefab in Assets/LowPolyWoodGenerated.

This archive contains an Editor C# generator, NOT pre-exported FBX/OBJ or prebuilt prefabs.
The generator writes reusable mesh/material assets and two separate prefabs inside Unity.
No external packages or textures are required. Intended for Unity 2021.3+; not executed in Unity here.
Materials select URP/Lit when available, otherwise Standard. HDRP needs manual material conversion.
The reference image is interpreted stylistically, not reconstructed exactly.

HandSaw: approximately 68 cm long; XY blade plane, Z thickness; origin close to handle.
WoodPile: 22 stylized hexagonal logs; origin on ground near pile center; unified prefab,
individual log children. No leaves or exact split-wood reconstruction in this initial version.
Saw uses compound BoxColliders (coarse around teeth); handle opening mostly preserved.
Pile uses one closed convex MeshCollider per log. Both are generated without Rigidbody.
For dynamic physics, add ONE Rigidbody to the prefab root, not to individual log children.
All collider children are invisible; no collision render geometry is displayed.

Regenerating updates assets in the same output directory: back up custom edits first.
Meshes have flat normals and solid colors; no UVs, textures, animation or LODs.
Verify geometry, scale, normals, collision and materials in your Unity version before production use.
