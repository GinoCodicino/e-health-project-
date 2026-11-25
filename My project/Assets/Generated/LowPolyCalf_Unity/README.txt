LOW-POLY CALF — static mesh asset

Included: Calf.obj, Calf.mtl and Unity Editor prefab builder.
Approximate height 1.23 m, width 0.78 m including ears, length 1.52 m.
Brown/cream calf with pink muzzle, ears, hooves, collar and gold bell.
No udder, horns, rig, animation, textures or UV mapping.
Stylized approximation, not an exact reconstruction of the reference photo.
Mesh contains 1302 triangles across disconnected mesh parts; flat shaded.

UNITY
1. Copy the Assets folder into the Unity project.
2. Wait for import and C# compilation.
3. Run Tools > Low Poly Wood > Generate Calf Prefab.
4. Use Assets/LowPolyCalf/LowPolyCalf.prefab.
The builder assigns materials (URP Lit if available, otherwise Standard)
and adds compound BoxColliders for body, head and four legs.
No Rigidbody is added. Add one to the root if required for dynamic physics.
HDRP requires material conversion. Unity may convert the OBJ coordinate system
on import; inspect front direction, scale and collider alignment in the Editor.
Intended for Unity 2021.3+; the builder has NOT been compiled/executed here.
Collider boxes are intentionally coarse, excluding ears, tail and bell.
Re-running overwrites the generated prefab; back up manual changes first.

OBJ can also be opened directly in Blender or other 3D software;
keep Calf.mtl alongside Calf.obj. The model file is usable without the script.
