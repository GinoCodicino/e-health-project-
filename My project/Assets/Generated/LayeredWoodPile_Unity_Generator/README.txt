LAYERED LOW-POLY WOOD PILE

This package contains a Unity Editor generator, not an already built prefab or FBX.
Copy Assets into your project, wait for compilation, then run:
Tools > Low Poly Wood > Generate Layered Wood Pile
The prefab and meshes/materials are created under Assets/WildlifeWoodPile.
The previous saw generator/prefab is unchanged and not included.

Design: chunky base logs with central shelter opening; overlapping bark slabs;
crossed thin branches; three rows of genuinely hollow stems; posts and top rails.
No animals, plants, signs or text. Flat-shaded geometry and solid warm colors.
Approximate dimensions: width 1.1 m, depth 1 m, height 1.4 m; ground-level pivot.
Forward viewing direction: look from negative Z towards positive Z.

Colliders: closed logs have convex MeshColliders, slabs have BoxColliders,
hollow stems have compound wall BoxColliders to avoid filling their openings.
No Rigidbody is added. Add a single Rigidbody to root if the entire pile should move.
This is one prefab with many child meshes/colliders, not separate physical logs.
Many colliders: profile and simplify if spawning many instances.

Intended for Unity 2021.3+; URP Lit selected if present, otherwise Standard.
HDRP requires material conversion. No UVs, textures, LODs or animation.
Generator has not been compiled or tested inside Unity in this environment.
Check appearance, contacts, materials and scale before production use.
Re-running overwrites generated prefab and geometry in the output folder;
back up hand edits before regeneration.
