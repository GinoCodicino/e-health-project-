using System.Collections.Generic;
using UnityEngine;

namespace Level02.DeerRescue
{
    internal static class DeerRescueGeometry
    {
        internal sealed class Part
        {
            public Transform owner;
            public Vector3[] vertices;
            public int[] triangles;
        }

        internal sealed class Section
        {
            public Transform owner;
            public readonly List<Vector3> ends = new List<Vector3>();
            public Bounds bounds;
            public Vector3 Center => bounds.center;

            public bool Touches(Vector3 a, Vector3 b, float tolerance)
            {
                foreach (var p in ends)
                    if ((p - a).sqrMagnitude < tolerance || (p - b).sqrMagnitude < tolerance) return true;
                return false;
            }

            public void Add(Vector3 a, Vector3 b)
            {
                if (ends.Count == 0) bounds = new Bounds(a, Vector3.zero);
                ends.Add(a);
                ends.Add(b);
                bounds.Encapsulate(a);
                bounds.Encapsulate(b);
            }

            public Vector3 Edge(Vector3 direction, float padding)
            {
                Vector3 origin = Center;
                float farthest = 0;
                for (int i = 0; i < ends.Count; i += 2)
                {
                    Vector3 v = ends[i] - origin, edge = ends[i + 1] - ends[i];
                    float cross = CrossXZ(direction, edge);
                    if (Mathf.Abs(cross) < 1e-8f) continue;
                    float fraction = CrossXZ(v, direction) / cross;
                    if (fraction >= -.001f && fraction <= 1.001f)
                        farthest = Mathf.Max(farthest, CrossXZ(v, edge) / cross);
                }
                if (farthest <= 0f) farthest = Mathf.Max(bounds.extents.x, bounds.extents.z);
                return origin + direction * (farthest + padding);
            }
        }

        // Intersects the standing mesh with a horizontal plane. Disjoint contours are
        // different legs, even when an imported skeleton uses anonymous bone names.
        internal static List<Section> Sections(List<Part> parts, float y, float height)
        {
            var result = new List<Section>();
            float tolerance = Mathf.Pow(Mathf.Max(.0001f, height * .0006f), 2);
            foreach (var part in parts)
            {
                var points = part.vertices;
                var tris = part.triangles;
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    Vector3 a = points[tris[i]], b = points[tris[i + 1]], c = points[tris[i + 2]];
                    bool aboveA = a.y > y, aboveB = b.y > y, aboveC = c.y > y;
                    if (aboveA == aboveB && aboveB == aboveC) continue;
                    // The odd vertex lies on the opposite side from both others.
                    Vector3 odd = aboveA == aboveB ? c : aboveA == aboveC ? b : a;
                    Vector3 first = aboveA == aboveB ? a : aboveA == aboveC ? a : b;
                    Vector3 second = aboveA == aboveB ? b : c;
                    Vector3 p = odd + (first - odd) * ((y - odd.y) / (first.y - odd.y));
                    Vector3 q = odd + (second - odd) * ((y - odd.y) / (second.y - odd.y));
                    if ((p - q).sqrMagnitude < 1e-12f) continue;
                    Section joined = null;
                    for (int k = result.Count - 1; k >= 0; k--)
                    {
                        var group = result[k];
                        if (!group.Touches(p, q, tolerance)) continue;
                        if (joined == null) { joined = group; continue; }
                        joined.ends.AddRange(group.ends);
                        joined.bounds.Encapsulate(group.bounds);
                        result.RemoveAt(k);
                    }
                    if (joined == null) { joined = new Section { owner = part.owner }; result.Add(joined); }
                    joined.Add(p, q);
                }
            }
            return result;
        }

        internal static Section Foreleg(List<Section> sections, Bounds body, Vector3 forward)
        {
            Section best = null;
            float score = float.NegativeInfinity;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            foreach (var s in sections)
            {
                Vector3 size = s.bounds.size, offset = s.Center - body.center;
                if (s.ends.Count < 4 || Mathf.Max(size.x, size.z) > body.size.y * .35f) continue;
                float value = Vector3.Dot(offset, forward) - .65f * Vector3.Dot(offset, right);
                if (value > score) { score = value; best = s; }
            }
            return best;
        }

        internal static Section Nearest(List<Section> sections, Section reference, float reach)
        {
            Section best = reference;
            float distance = reach * reach;
            foreach (var section in sections)
            {
                float d = (section.Center - reference.Center).sqrMagnitude;
                if (d < distance) { best = section; distance = d; }
            }
            return best;
        }

        // Open or simplified LOD meshes may not have a closed contour. Measure a
        // single visible hoof and place the cuff on the lower limb above that hoof.
        internal static Section FromHoof(List<Part> parts, HashSet<Transform> bones, Bounds body, Vector3 forward)
        {
            float h = body.size.y, score = float.NegativeInfinity;
            Vector3 right = Vector3.Cross(Vector3.up, forward), seed = body.min;
            Part limb = parts[0];
            foreach (var part in parts)
                foreach (var p in part.vertices)
                {
                    if (p.y > body.min.y + h * .13f) continue;
                    float s = Vector3.Dot(p - body.center, forward - .65f * right);
                    if (s > score) { score = s; seed = p; limb = part; }
                }
            Bounds hoof = new Bounds(seed, Vector3.zero);
            foreach (var p in limb.vertices)
                if (p.y <= body.min.y + h * .13f && Vector3.ProjectOnPlane(p - seed, Vector3.up).sqrMagnitude < h * h * .012f)
                    hoof.Encapsulate(p);
            var center = hoof.center;
            center.y = body.min.y + .16f * h;
            float distance = .14f * h;
            foreach (var b in bones)
            {
                Transform a = b.parent;
                if (!a || !bones.Contains(a) || (a.position.y - center.y) * (b.position.y - center.y) > 0) continue;
                float dy = b.position.y - a.position.y;
                if (Mathf.Abs(dy) < .0001f) continue;
                var axis = a.position + (b.position - a.position) * ((center.y - a.position.y) / dy);
                float d = Vector3.Distance(hoof.center, new Vector3(axis.x, hoof.center.y, axis.z));
                if (d < distance) { distance = d; center = axis; }
            }
            var section = new Section { owner = limb.owner };
            float rx = Mathf.Clamp(hoof.extents.x, h * .018f, h * .055f);
            float rz = Mathf.Clamp(hoof.extents.z, h * .018f, h * .055f);
            Vector3 first = center + new Vector3(rx, 0, 0), prev = first;
            for (int i = 1; i <= 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                Vector3 next = i == 16 ? first : center + new Vector3(rx * Mathf.Cos(a), 0, rz * Mathf.Sin(a));
                section.Add(prev, next);
                prev = next;
            }
            return section;
        }

        internal static Transform LegBone(HashSet<Transform> bones, Vector3 point, Transform fallback, float height)
        {
            Transform best = fallback;
            float distance = Mathf.Pow(height * .12f, 2);
            foreach (var child in bones)
            {
                var parent = child.parent;
                if (parent == null || !bones.Contains(parent)) continue;
                Vector3 delta = child.position - parent.position;
                if (delta.sqrMagnitude < 1e-8f) continue;
                float t = Mathf.Clamp01(Vector3.Dot(point - parent.position, delta) / delta.sqrMagnitude);
                float d = (parent.position + t * delta - point).sqrMagnitude;
                if (d < distance) { distance = d; best = parent; }
            }
            return best;
        }

        internal static Mesh MakeWrap(Transform bandage, Section[] sections, float centerY, float height)
        {
            const int sides = 20;
            float[] levels = { -.037f, -.029f, 0f, .029f, .037f };
            var rings = new Vector3[levels.Length * sides];
            var faces = new int[(levels.Length - 1) * sides * 6];
            for (int r = 0; r < levels.Length; r++)
            {
                var section = sections[r < 2 ? 0 : r == 2 ? 1 : 2];
                float margin = height * (r == 0 || r == levels.Length - 1 ? .0035f : .006f);
                for (int j = 0; j < sides; j++)
                {
                    float angle = (2f * Mathf.PI * j) / sides;
                    Vector3 surface = section.Edge(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)), margin);
                    surface.y = centerY + levels[r] * height;
                    rings[r * sides + j] = bandage.InverseTransformPoint(surface);
                    if (r == levels.Length - 1) continue;
                    int a = r * sides + j, b = r * sides + (j + 1) % sides;
                    int f = (r * sides + j) * 6;
                    faces[f] = a; faces[f + 1] = a + sides; faces[f + 2] = b;
                    faces[f + 3] = b; faces[f + 4] = a + sides; faces[f + 5] = b + sides;
                }
            }
            Vector3 scale = bandage.lossyScale;
            if (scale.x * scale.y * scale.z < 0f)
                for (int i = 0; i < faces.Length; i += 3)
                { int swap = faces[i + 1]; faces[i + 1] = faces[i + 2]; faces[i + 2] = swap; }
            var mesh = new Mesh { name = "Bandage fitted to injured leg", vertices = rings, triangles = faces };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float CrossXZ(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;

        // Lowest and highest wood surface on a world-space vertical line.
        internal static bool TimberSpan(Part wood, Vector3 at, out float low, out float high)
        {
            low = float.PositiveInfinity;
            high = float.NegativeInfinity;
            var tris = wood.triangles;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                var a = wood.vertices[tris[i]];
                var u = wood.vertices[tris[i + 1]] - a;
                var v = wood.vertices[tris[i + 2]] - a;
                float det = CrossXZ(u, v);
                if (Mathf.Abs(det) < 1e-8f) continue;
                var offset = at - a;
                float b = CrossXZ(offset, v) / det, c = CrossXZ(u, offset) / det;
                if (b < -.0001f || c < -.0001f || b + c > 1.0001f) continue;
                float y = a.y + b * u.y + c * v.y;
                low = Mathf.Min(low, y);
                high = Mathf.Max(high, y);
            }
            return low <= high;
        }

        internal static bool ChooseTimberContact(Part wood, Vector3 leg, Vector3 head, Vector3 normal,
                                                float height, out Vector3 contact)
        {
            contact = leg;
            if (wood.vertices.Length == 0) return false;
            var bounds = new Bounds(wood.vertices[0], Vector3.zero);
            foreach (var v in wood.vertices) bounds.Encapsulate(v);
            float step = Mathf.Clamp(height * .1f, .10f, .28f);
            float radius = Mathf.Clamp(height * 2.6f, 3.5f, 7f);
            float best = float.PositiveInfinity;
            Vector3 selected = leg;
            // Privilege the broad shaft, the part near the deer, and a free head.
            System.Action<Vector3> consider = p => {
                float low, high;
                if (!TimberSpan(wood, p, out low, out high)) return;
                Vector3 offset = Vector3.ProjectOnPlane(p - leg, Vector3.up);
                float desiredY = leg.y - Vector3.Dot(offset, normal) / normal.y;
                float thickness = high - low;
                float score = .42f * offset.magnitude + 2.4f * Mathf.Abs(desiredY - low)
                            + 6f * Mathf.Max(0, height * .11f - thickness);
                if (score >= best) return;
                float underHead, overHead;
                Vector3 movedHead = head + offset;
                if (TimberSpan(wood, movedHead, out underHead, out overHead)
                    && overHead > movedHead.y - height * .2f && underHead < movedHead.y + height * .2f)
                    score += 2f * height;
                if (score < best) { best = score; selected = new Vector3(p.x, low, p.z); }
            };
            consider(leg);
            for (float x = Mathf.Max(bounds.min.x, leg.x - radius); x <= Mathf.Min(bounds.max.x, leg.x + radius); x += step)
                for (float z = Mathf.Max(bounds.min.z, leg.z - radius); z <= Mathf.Min(bounds.max.z, leg.z + radius); z += step)
                    consider(new Vector3(x, 0, z));
            // Also catch narrow or nearly vertical pieces falling between grid lines.
            if (float.IsInfinity(best))
                for (int i = 0; i + 2 < wood.triangles.Length; i += 3)
                    consider((wood.vertices[wood.triangles[i]] + wood.vertices[wood.triangles[i + 1]]
                              + wood.vertices[wood.triangles[i + 2]]) / 3f);
            contact = selected;
            return !float.IsInfinity(best);
        }
    }
}
