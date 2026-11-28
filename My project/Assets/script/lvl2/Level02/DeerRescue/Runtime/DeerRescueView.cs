using System.Collections.Generic;
using UnityEngine;
using static Level02.DeerRescue.DeerRescueGeometry;

namespace Level02.DeerRescue
{
    public sealed class DeerRescueView : MonoBehaviour
    {
        public Transform animalVisual;
        public GameObject fallenBranches;
        public GameObject bandageVisual;
        // Older scenes store a 90-degree Euler angle on the empty container.
        [HideInInspector] public Vector3 restingEuler = new Vector3(90, 0, 0);
        [SerializeField, HideInInspector] internal DeerRescueSceneData sceneData;

        enum State { Trapped, Freed, Recovering, Standing }
        State state;
        Transform deer, neck, leftEar, rightEar, dressedJoint;
        Quaternion upright, groundTilt, neckRest, leftEarRest, rightEarRest, legRest;
        Vector3 forward, originalRoot, pivot, anchoredPivot, floorPoint, floorNormal;
        Vector3 localLeg;
        Vector3[] support, openFabric, foldedFabric;
        Bounds body;
        Mesh wrapMesh;
        bool ready, dressingComplete;
        float restAngle, elapsed;
        const float RiseSeconds = 2.1f;
        const float DressingSeconds = .4f;

        [SerializeField] AnimationClip passiEInchino;

        Transform giocatoreSaluto;
        Quaternion giroSaluto = Quaternion.identity;
        Vector3 partenzaSaluto, arrivoSaluto;
        bool salutoAttivo;

        void IniziaSaluto()
        {
            if (!passiEInchino || passiEInchino.length <= 0f) return;

            var missione = FindFirstObjectByType<DeerRescueController>();
            if (!missione || !missione.player) return;

            giocatoreSaluto = missione.player.transform;
            partenzaSaluto = anchoredPivot;

            Vector3 verso = Vector3.ProjectOnPlane(
                giocatoreSaluto.position - partenzaSaluto, Vector3.up);

            // Avanza al massimo mezzo metro e lascia spazio davanti al giocatore.
            float distanza = Mathf.Clamp(verso.magnitude - 1.8f, 0f, .5f);
            arrivoSaluto = partenzaSaluto + verso.normalized * distanza;
            salutoAttivo = true;
        }

        void AggiornaSaluto()
        {
            float t = Mathf.Min(elapsed, passiEInchino.length);

            // Lo spostamento coincide con i fotogrammi 18–51 della clip.
            float passo = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.6f, 1.7f, t));
            Vector3 nuovoPivot = Vector3.Lerp(partenzaSaluto, arrivoSaluto, passo);

            if (nuovoPivot != anchoredPivot)
            {
                anchoredPivot = nuovoPivot;
                MeasureFloor(anchoredPivot);
            }

            if (giocatoreSaluto)
            {
                Vector3 verso = Vector3.ProjectOnPlane(
                    giocatoreSaluto.position - anchoredPivot, Vector3.up);

                if (verso.sqrMagnitude > .001f)
                {
                    float angolo = Vector3.SignedAngle(forward, verso, Vector3.up);
                    Quaternion mira = Quaternion.AngleAxis(angolo, Vector3.up);
                    giroSaluto = Quaternion.RotateTowards(
                        giroSaluto, mira, 240f * Time.deltaTime);
                }
            }

            passiEInchino.SampleAnimation(deer.gameObject, t);
            ApplyGroundedPose(0f);

            if (t < passiEInchino.length) return;

            salutoAttivo = false;
            elapsed = 0f;
        }



        internal Transform FindDeer()
        {
            if (!animalVisual) return null;
            Transform named = animalVisual.Find("Deer_001");
            if (named) return named;
            if (animalVisual.GetComponent<Renderer>() || animalVisual.GetComponent<Animator>()) return animalVisual;
            var skin = animalVisual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin)
            {
                Transform part = skin.transform;
                while (part != animalVisual && part.parent != animalVisual) part = part.parent;
                return part;
            }
            return animalVisual;
        }

        internal MeshFilter FindTimber()
        {
            if (!fallenBranches) return null;
            MeshFilter best = null;
            float distance = float.PositiveInfinity;
            var model = FindDeer();
            Vector3 at = model ? model.position : fallenBranches.transform.position;
            foreach (var filter in fallenBranches.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                string label = filter.name.ToLowerInvariant();
                if (label == "trunk") return filter;
                if (label.Contains("leav") || label.Contains("leaf") || label.Contains("foliage")) continue;
                var renderer = filter.GetComponent<Renderer>();
                float next = (renderer ? renderer.bounds.center - at : filter.transform.position - at).sqrMagnitude;
                if (next < distance) { distance = next; best = filter; }
            }
            return best;
        }

        void Awake()
        {
            if (bandageVisual) bandageVisual.SetActive(false);
            deer = FindDeer();
            if (!deer) return;
#if UNITY_EDITOR
            sceneData = DeerRescueSceneBaker.Capture(this);
#endif
            if (sceneData == null) sceneData = new DeerRescueSceneData();
            // The pose is driven here. Imported clips or rigidbodies must not drive the same bones.
            foreach (var animator in deer.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var animation in deer.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
            foreach (var rb in deer.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;

            var reference = sceneData.referenceDeer ? sceneData.referenceDeer.transform : null;
            Quaternion assetRotation = reference ? reference.localRotation : Quaternion.identity;
            Vector3 heading = Vector3.ProjectOnPlane((deer.rotation * Quaternion.Inverse(assetRotation)) * Vector3.forward, Vector3.up);
            if (heading.sqrMagnitude < .001f) heading = Vector3.forward;
            upright = Quaternion.LookRotation(heading, Vector3.up) * assetRotation;
            if (reference) RestoreChildren(deer, reference);
            deer.rotation = upright;
            originalRoot = deer.position;

            var bones = new HashSet<Transform>();
            var parts = ReadDeer(bones);
            if (parts.Count == 0)
            {
                Debug.LogWarning("DeerRescueView: no visible deer mesh on " + deer.name, this);
                return;
            }
            var vertices = new List<Vector3>();
            body = new Bounds(parts[0].vertices[0], Vector3.zero);
            foreach (var part in parts)
                foreach (var p in part.vertices) { vertices.Add(p); body.Encapsulate(p); }
            if (body.size.y < .001f) return;
            FindHead(vertices, bones);
            pivot = new Vector3(body.center.x, body.min.y + body.size.y * .42f, body.center.z);
            anchoredPivot = pivot;
            support = vertices.ToArray();
            for (int i = 0; i < support.Length; i++) support[i] -= pivot;

            Vector3 leg = FitBandage(parts, bones);
            localLeg = deer.InverseTransformPoint(leg);
            // Roll about the animal's nose-to-tail axis, with the dressed leg on the exposed side.
            float side = Vector3.Dot(leg - body.center, Vector3.Cross(Vector3.up, forward));
            restAngle = RestOnFlank(side > 0 ? 1f : -1f);
            MeasureFloor(anchoredPivot);
            ApplyGroundedPose(restAngle);
            PlaceUnderTimber();
            ready = true;
        }

        static void RestoreChildren(Transform instance, Transform source)
        {
            // Imported hierarchies can contain multiple siblings with the same name.
            for (int i = 0; i < source.childCount; i++)
            {
                var from = source.GetChild(i);
                var to = i < instance.childCount ? instance.GetChild(i) : null;
                if (!to || to.name != from.name) to = instance.Find(from.name);
                if (!to) continue;
                to.localPosition = from.localPosition;
                to.localRotation = from.localRotation;
                RestoreChildren(to, from);
            }
        }

        List<Part> ReadDeer(HashSet<Transform> bones)
        {
            var parts = new List<Part>();
            var lowerLods = new HashSet<Renderer>();
            foreach (var group in deer.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                for (int n = 1; n < lods.Length; n++)
                    foreach (var r in lods[n].renderers) if (r) lowerLods.Add(r);
                if (lods.Length > 0) foreach (var r in lods[0].renderers) lowerLods.Remove(r);
            }
            foreach (var r in deer.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || lowerLods.Contains(r)) continue;
                if (bandageVisual && r.transform.IsChildOf(bandageVisual.transform)) continue;
                var skin = r as SkinnedMeshRenderer;
                if (skin && skin.sharedMesh)
                {
                    foreach (var b in skin.bones) if (b) bones.Add(b);
                    var neutral = sceneData.Get(skin.sharedMesh);
                    var snapshot = new Mesh();
                    try
                    {
                        skin.BakeMesh(snapshot, false);
                        var vertices = snapshot.vertices;
                        // Use the FBX bind shape to distinguish authored mesh scale from a
                        // scale already baked by the importer. Apply that scale exactly once.
                        bool local = IsRendererLocal(r.transform, vertices, neutral);
                        for (int i = 0; i < vertices.Length; i++)
                            vertices[i] = local ? r.transform.TransformPoint(vertices[i])
                                : r.transform.position + r.transform.rotation * vertices[i];
                        var indices = snapshot.triangles;
                        if (indices.Length == 0 && neutral != null && neutral.vertices.Length == vertices.Length)
                            indices = neutral.triangles;
                        if (vertices.Length > 0) parts.Add(new Part { owner = r.transform, vertices = vertices, triangles = indices });
                    }
                    finally { Destroy(snapshot); }
                }
                else
                {
                    var filter = r.GetComponent<MeshFilter>();
                    var part = filter ? WorldPart(filter) : null;
                    if (part != null) parts.Add(part);
                }
            }
            return parts;
        }

        static bool IsRendererLocal(Transform owner, Vector3[] baked, DeerRescueSceneData.Surface neutral)
        {
            if (neutral == null || neutral.vertices.Length != baked.Length) return true;
            float localError = 0f, unscaledError = 0f;
            for (int i = 0; i < baked.Length; i += Mathf.Max(1, baked.Length / 96))
            {
                var bind = owner.TransformPoint(neutral.vertices[i]);
                localError += (owner.TransformPoint(baked[i]) - bind).sqrMagnitude;
                unscaledError += (owner.position + owner.rotation * baked[i] - bind).sqrMagnitude;
            }
            return localError <= unscaledError;
        }

        Part WorldPart(MeshFilter filter)
        {
            var src = sceneData.Get(filter.sharedMesh);
            if (src == null || src.vertices.Length == 0) return null;
            var world = new Vector3[src.vertices.Length];
            for (int i = 0; i < world.Length; i++) world[i] = filter.transform.TransformPoint(src.vertices[i]);
            return new Part { owner = filter.transform, vertices = world, triangles = src.triangles };
        }

        void FindHead(List<Vector3> vertices, HashSet<Transform> bones)
        {
            Transform head = null;
            // Some FBX assets omit intermediary neck/ear transforms from the weighted bone list.
            var joints = deer.GetComponentsInChildren<Transform>(true);
            foreach (var joint in joints)
            {
                string label = joint.name.ToLowerInvariant();
                if (label.Contains("end") || label.Contains("tip") || label.Contains("antler")) continue;
                if (neck == null && label.Contains("neck")) neck = joint;
                if (head == null && label.Contains("head") && !label.Contains("forehead")) head = joint;
                if (!label.Contains("ear") || label.Contains("rear") || label.Contains("gear")) continue;
                if (!leftEar) leftEar = joint;
                else if (!rightEar && !joint.IsChildOf(leftEar) && !leftEar.IsChildOf(joint)) rightEar = joint;
            }
            Vector3 top = Vector3.zero;
            int count = 0;
            foreach (var v in vertices)
                if (v.y >= body.min.y + body.size.y * .85f) { top += v; count++; }
            top = count > 0 ? top / count : body.center;
            var landmark = head ? head.position : top;
            forward = Vector3.ProjectOnPlane(landmark - body.center, Vector3.up);
            if (forward.sqrMagnitude < .003f) forward = Vector3.ProjectOnPlane(top - body.center, Vector3.up);
            if (forward.sqrMagnitude < .003f) forward = Vector3.ProjectOnPlane(deer.forward, Vector3.up);
            forward = forward.sqrMagnitude > .0001f ? forward.normalized : Vector3.forward;
            if (!neck) neck = head;
            if (!neck && bones.Count > 0)
            {
                float closest = body.size.y * .24f;
                foreach (var bone in bones)
                {
                    float d = Vector3.Distance(bone.position, top);
                    if (d < closest && bone.childCount > 0 && bone != deer)
                    { closest = d; neck = bone; }
                }
            }
            if (neck) neckRest = neck.localRotation;
            if (leftEar) leftEarRest = leftEar.localRotation;
            if (rightEar) rightEarRest = rightEar.localRotation;
        }

        Vector3 FitBandage(List<Part> parts, HashSet<Transform> bones)
        {
            float height = body.size.y;
            Section leg = null;
            foreach (float fraction in new[] { .18f, .14f, .22f, .10f, .27f })
            {
                var cuts = Sections(parts, body.min.y + height * fraction, height);
                leg = Foreleg(cuts, body, forward);
                if (leg != null) break;
            }
            if (leg == null) leg = FromHoof(parts, bones, body, forward);
            if (!bandageVisual) return leg.Center;
            var nearLow = Sections(parts, leg.Center.y - height * .037f, height);
            var nearHigh = Sections(parts, leg.Center.y + height * .037f, height);
            var sections = new[] { Nearest(nearLow, leg, height * .15f), leg, Nearest(nearHigh, leg, height * .15f) };
            var anchor = LegBone(bones, leg.Center, leg.owner, height);
            if (bones.Contains(anchor)) { dressedJoint = anchor; legRest = anchor.localRotation; }
            var wrap = bandageVisual.transform;
            wrap.SetParent(anchor, false);
            wrap.localScale = Vector3.one;
            wrap.localRotation = Quaternion.identity;
            wrap.position = leg.Center;
            var filter = bandageVisual.GetComponent<MeshFilter>();
            if (filter)
            {
                wrapMesh = MakeWrap(wrap, sections, leg.Center.y, height);
                filter.sharedMesh = wrapMesh;
                openFabric = wrapMesh.vertices;
                foldedFabric = new Vector3[openFabric.Length];
            }
            return leg.Center;
        }

        float RestOnFlank(float side)
        {
            float height = body.size.y;
            // A hoof or antler can touch the ground while the entire torso is still
            // hanging in the air. Increase the bank until the flank is supported too.
            float angle = Mathf.Clamp(restingEuler.magnitude * .86f, 65f, 83f);
            while (angle < 87f)
            {
                Vector3 up = Quaternion.AngleAxis(-side * angle, forward) * Vector3.up;
                float lowest = float.PositiveInfinity, flank = float.PositiveInfinity;
                foreach (var p in support)
                {
                    float y = Vector3.Dot(p, up);
                    lowest = Mathf.Min(lowest, y);
                    float h = p.y + pivot.y - body.min.y;
                    if (h > height * .30f && h < height * .66f && Mathf.Abs(Vector3.Dot(p, forward)) < height * .29f)
                        flank = Mathf.Min(flank, y);
                }
                if (float.IsInfinity(flank) || flank - lowest <= height * .025f) break;
                angle += 2f;
            }
            return side * Mathf.Min(87f, angle);
        }

        void MeasureFloor(Vector3 at)
        {
            float baseline = transform.position.y;
            floorPoint = new Vector3(at.x, baseline, at.z);
            floorNormal = Vector3.up;
            float best = float.PositiveInfinity;
            var ray = new Vector3(at.x, baseline + 12f, at.z);
            foreach (var hit in Physics.RaycastAll(ray, Vector3.down, 36f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < .55f || hit.transform.IsChildOf(transform) ||
                    hit.transform.IsChildOf(deer) ||
                    (fallenBranches && hit.transform.IsChildOf(fallenBranches.transform))) continue;
                float score = Mathf.Abs(hit.point.y - baseline);
                if (score < best) { best = score; floorPoint = hit.point; floorNormal = hit.normal.normalized; }
            }
            if (float.IsInfinity(best)) foreach (var terrain in Terrain.activeTerrains)
            {
                Vector3 p = at - terrain.transform.position;
                var size = terrain.terrainData.size;
                if (p.x < 0 || p.z < 0 || p.x > size.x || p.z > size.z) continue;
                floorPoint.y = terrain.SampleHeight(at) + terrain.transform.position.y;
                floorNormal = terrain.terrainData.GetInterpolatedNormal(p.x / size.x, p.z / size.z);
                break;
            }
            groundTilt = Quaternion.FromToRotation(Vector3.up, floorNormal);
        }

        void ApplyGroundedPose(float angle)
        {
            Quaternion bank = groundTilt * giroSaluto * Quaternion.AngleAxis(angle, forward);
            Vector3 normal = Quaternion.Inverse(bank) * floorNormal;
            float min = float.PositiveInfinity;
            foreach (var p in support) min = Mathf.Min(min, Vector3.Dot(p, normal));
            float lift = (Vector3.Dot(floorPoint - anchoredPivot, floorNormal) - min) / floorNormal.y + .003f;
            deer.SetPositionAndRotation(anchoredPivot + bank * (originalRoot - pivot) + Vector3.up * lift, bank * upright);
        }

        void PlaceUnderTimber()
        {
            var filter = FindTimber();
            var wood = filter ? WorldPart(filter) : null;
            if (wood == null) return;
            var at = TopOfInjuredLeg();
            Vector3 head = neck ? neck.position : at + forward * body.size.y * .45f;
            Vector3 contact;
            if (!ChooseTimberContact(wood, at, head, floorNormal, body.size.y, out contact)) return;
            anchoredPivot += Vector3.ProjectOnPlane(contact - at, Vector3.up);
            MeasureFloor(anchoredPivot);
            ApplyGroundedPose(restAngle);
            float targetY = TopOfInjuredLeg().y;
            // Only the visible piece gets this initial vertical adjustment. The rope game
            // moves FallenBranches independently and the deer stays on the ground.
            var piece = filter.transform;
            while (piece != fallenBranches.transform && piece.parent != fallenBranches.transform)
                piece = piece.parent;
            piece.position += Vector3.up * (targetY - contact.y);
        }

        Vector3 TopOfInjuredLeg()
        {
            var p = deer.TransformPoint(localLeg);
            if (wrapMesh)
            {
                var vertices = wrapMesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = bandageVisual.transform.TransformPoint(vertices[i]);
                var sleeve = new Part { vertices = vertices, triangles = wrapMesh.triangles };
                float low, high;
                if (TimberSpan(sleeve, p, out low, out high)) { p.y = high; return p; }
            }
            return p + Vector3.up * body.size.y * .025f;
        }

        public void SetClearedBranches(int count)
        {
            if (!fallenBranches || state != State.Trapped) return;
            for (int i = 0; i < fallenBranches.transform.childCount; i++)
                fallenBranches.transform.GetChild(i).gameObject.SetActive(i >= count);
        }

        public void Free()
        {
            if (fallenBranches) fallenBranches.SetActive(false);
            if (state == State.Trapped) state = State.Freed;
        }

        public void Heal()
        {
            if (state == State.Recovering || state == State.Standing) return;
            Free();
            if (bandageVisual && wrapMesh)
            {
                ShowFabric(0f);
                bandageVisual.SetActive(true);
            }
            state = State.Recovering;
            elapsed = 0f;
        }

        void ShowFabric(float progress)
        {
            // Keep the middle turn on the leg while the edges open to the final width.
            int sides = openFabric.Length / 5;
            float width = .12f + .88f * Mathf.SmoothStep(0f, 1f, progress);
            for (int i = 0; i < openFabric.Length; i++)
            {
                Vector3 middle = openFabric[2 * sides + i % sides];
                foldedFabric[i] = middle + (openFabric[i] - middle) * width;
            }
            wrapMesh.vertices = foldedFabric;
        }

        void LateUpdate()
        {
            if (!ready || (state != State.Recovering && state != State.Standing)) return;
            elapsed += Time.deltaTime;
            if (state == State.Recovering)
            {
                if (!dressingComplete)
                {
                    if (wrapMesh) ShowFabric(Mathf.Clamp01(elapsed / DressingSeconds));
                    if (elapsed < DressingSeconds) return;
                    dressingComplete = true;
                }
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - DressingSeconds) / RiseSeconds));
                ApplyGroundedPose(restAngle * (1f - rise));
                // Lift the head and briefly flex the exposed foreleg while rising.
                float shift = Mathf.Sin(rise * Mathf.PI);
                Vector3 pitchAxis = Vector3.Cross(Vector3.up, forward);
                if (neck && neck.parent) SetWorldOffset(neck, neckRest, Quaternion.AngleAxis(-7f * shift, pitchAxis));
                if (dressedJoint && dressedJoint.parent)
                    SetWorldOffset(dressedJoint, legRest, Quaternion.AngleAxis(8f * shift, pitchAxis));
                if (elapsed < DressingSeconds + RiseSeconds) return;
                            state = State.Standing;
                elapsed = 0f;
                if (dressedJoint) dressedJoint.localRotation = legRest;
                IniziaSaluto();
            }

            if (salutoAttivo)
            {
                AggiornaSaluto();
                return;
            }

            float ease = Mathf.Clamp01(elapsed / .75f);
            float breath = Mathf.Sin(elapsed * 1.75f);
            float look = Mathf.Sin(elapsed * .86f);
            // Neck, ears and hooves use the original rig. Rest-relative rotations never accumulate.
            if (neck && neck.parent)
            {
                Vector3 side = Vector3.Cross(Vector3.up, forward);
                Quaternion nod = Quaternion.AngleAxis((2.2f * breath - 2f) * ease, side);
                Quaternion glance = Quaternion.AngleAxis(4.5f * look * ease, Vector3.up);
                SetWorldOffset(neck, neckRest, glance * nod);
            }
            else ApplyGroundedPose(.65f * look * ease);
            Quaternion earMotion = Quaternion.AngleAxis(2.5f * Mathf.Sin(elapsed * 2.3f) * ease, forward);
            if (leftEar && leftEar != neck) SetWorldOffset(leftEar, leftEarRest, earMotion);
            if (rightEar && rightEar != neck) SetWorldOffset(rightEar, rightEarRest, Quaternion.Inverse(earMotion));
        }

        static void SetWorldOffset(Transform bone, Quaternion rest, Quaternion offset)
        {
            var parent = bone.parent.rotation;
            bone.localRotation = Quaternion.Inverse(parent) * offset * parent * rest;
        }

        void OnDestroy() { if (wrapMesh) Destroy(wrapMesh); }
    }
}
