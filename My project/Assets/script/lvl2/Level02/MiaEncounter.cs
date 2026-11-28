using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Level02.DeerRescue
{
    // Distanze orizzontali lungo lo stesso sentiero per Mia e per chi la segue.
    internal sealed class MiaTrail
    {
        readonly List<Vector3> points = new List<Vector3>();
        readonly List<float> distances = new List<float>();

        public float Length => distances.Count == 0 ? 0f : distances[distances.Count - 1];

        public static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction;
        }

        public void Add(Vector3 point)
        {
            float next = 0f;
            if (points.Count > 0)
            {
                float distance = Flat(point - points[points.Count - 1]).magnitude;
                if (distance < .02f) return;
                next = Length + distance;
            }
            points.Add(point);
            distances.Add(next);
        }

        public Vector3 At(float distance)
        {
            for (int i = 1; i < points.Count; ++i)
            {
                if (distance <= distances[i])
                    return Vector3.Lerp(points[i - 1], points[i],
                        Mathf.InverseLerp(distances[i - 1], distances[i], distance));
            }
            return points[points.Count - 1];
        }

        // Se il giocatore ha seguito un tratto prima di accettare, si rientra sul
        // sentiero dal punto più vicino. Si può anche riprendere dopo una disattivazione.
        public MiaTrail From(Vector3 start)
        {
            var remaining = new MiaTrail();
            remaining.Add(start);
            if (points.Count == 0) return remaining;
            float nearest = float.PositiveInfinity;
            Vector3 entry = points[0];
            int next = 0;
            for (int i = 1; i < points.Count; ++i)
            {
                Vector3 segment = Flat(points[i] - points[i - 1]);
                float u = Mathf.Clamp01(Vector3.Dot(Flat(start - points[i - 1]), segment) / segment.sqrMagnitude);
                Vector3 projection = Vector3.Lerp(points[i - 1], points[i], u);
                float d = Flat(projection - start).sqrMagnitude;
                if (d >= nearest) continue;
                nearest = d;
                entry = projection;
                next = i;
            }
            remaining.Add(entry);
            for (int i = next; i < points.Count; ++i) remaining.Add(points[i]);
            return remaining;
        }
    }

    // Sovrappone piccoli movimenti alle ossa effettive. La clip già presente su MIA
    // continua a funzionare. Ogni offset viene tolto prima del fotogramma successivo.
    internal sealed class MiaMotion
    {
        sealed class Joint
        {
            public readonly Transform bone;
            Quaternion originalRotation, appliedRotation;
            Vector3 originalPosition, appliedPosition;
            bool applied;
            Vector3 stepAxis;

            public Joint(Transform transform) { bone = transform; }

            // Nei rig low-poly le braccia possono essere modellate in posa a T.
            // In quel caso una rotazione intorno alla spalla deve prima abbassarle.
            public void FitArm(Vector3 forward)
            {
                if (!bone) return;
                Vector3 limb = Vector3.zero;
                for (int i = 0; i < bone.childCount; ++i)
                {
                    Vector3 v = bone.GetChild(i).position - bone.position;
                    if (v.sqrMagnitude > limb.sqrMagnitude) limb = v;
                }
                if (limb.sqrMagnitude < .00001f) return;
                stepAxis = Quaternion.Inverse(Quaternion.LookRotation(forward)) * limb.normalized;
            }

            public Quaternion LowerArm(Vector3 forward, float weight)
            {
                if (stepAxis.sqrMagnitude < .1f) return Quaternion.identity;
                Vector3 limb = Quaternion.LookRotation(forward) * stepAxis;
                Quaternion down = Quaternion.FromToRotation(limb, Vector3.down);
                return Quaternion.RotateTowards(Quaternion.identity, down, 58f * weight);
            }

            public void Pose(Quaternion worldTurn, Vector3 worldOffset)
            {
                if (!bone) return;
                originalRotation = bone.localRotation;
                originalPosition = bone.localPosition;
                Quaternion parent = bone.parent ? bone.parent.rotation : Quaternion.identity;
                appliedRotation = Quaternion.Inverse(parent) * worldTurn * parent * originalRotation;
                appliedPosition = originalPosition + (bone.parent ? bone.parent.InverseTransformVector(worldOffset) : worldOffset);
                bone.SetLocalPositionAndRotation(appliedPosition, appliedRotation);
                applied = true;
            }

            public void Clear()
            {
                if (!applied || !bone) return;
                // Se l'Animator ha già scritto una posa nuova (anche in FixedUpdate), la conserva.
                if (Quaternion.Angle(bone.localRotation, appliedRotation) < .03f) bone.localRotation = originalRotation;
                if ((bone.localPosition - appliedPosition).sqrMagnitude < 1e-9f) bone.localPosition = originalPosition;
                applied = false;
            }
        }

        readonly Transform actor;
        readonly float height;
        readonly Joint torso, head, leftArm, rightArm, leftThigh, rightThigh, leftShin, rightShin, leftFoot, rightFoot;
        readonly Joint[] joints;
        float phase, gait, run, speechWeight;

        static void Remember(Dictionary<string, Transform> bones, Transform t)
        {
            if (!t) return;
            string n = t.name;
            int prefix = n.LastIndexOf(':');
            if (prefix >= 0) n = n.Substring(prefix + 1);
            n = n.Replace("_", "").Replace("-", "").Replace(".", "").Replace(" ", "").ToLowerInvariant();
            if (n.StartsWith("mixamorig", System.StringComparison.Ordinal)) n = n.Substring(9);
            if (!bones.ContainsKey(n)) bones.Add(n, t);
        }

        static Transform Find(Dictionary<string, Transform> bones, Animator human, HumanBodyBones id, params string[] names)
        {
            if (human)
            {
                Transform t = human.GetBoneTransform(id);
                if (t) return t;
            }
            foreach (string name in names)
                if (bones.TryGetValue(name, out Transform match)) return match;
            return null;
        }

        public MiaMotion(Transform actor, Vector3 forward)
        {
            this.actor = actor;
            var bones = new Dictionary<string, Transform>();
            // Prima le ossa pesate sulla mesh: un modello importato può avere due rig omonimi.
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (var bone in skin.bones) Remember(bones, bone);
            foreach (var t in actor.GetComponentsInChildren<Transform>(true)) Remember(bones, t);
            Animator human = null;
            foreach (var a in actor.GetComponentsInChildren<Animator>(true))
                if (a.avatar && a.avatar.isValid && a.isHuman) { human = a; break; }

            Renderer largest = null;
            Transform visualRoot = null;
            foreach (var r in actor.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (!largest || r.bounds.size.y > largest.bounds.size.y) largest = r;
                if (!visualRoot) visualRoot = r.transform;
                while (visualRoot != actor && !r.transform.IsChildOf(visualRoot)) visualRoot = visualRoot.parent;
            }
            height = largest ? Mathf.Clamp(largest.bounds.size.y, .7f, 3f) : 1.65f;
            Transform chest = Find(bones, human, HumanBodyBones.Chest, "spine00", "chest", "upperchest", "spine2", "spine", "hips", "pelvis");
            // Un modello rigido oscilla comunque con i passi. Si muove la sua radice
            // visiva quando possibile, lasciando al controller lo spostamento lungo il sentiero.
            if (!chest)
                chest = visualRoot ? visualRoot : actor;
            torso = new Joint(chest);
            Transform headBone = Find(bones, human, HumanBodyBones.Head, "head", "testa", "neck");
            head = new Joint(headBone != chest ? headBone : null);
            leftArm = new Joint(Find(bones, human, HumanBodyBones.LeftUpperArm, "arml", "leftarm", "leftupperarm", "upperarml", "lupperarm", "larm"));
            rightArm = new Joint(Find(bones, human, HumanBodyBones.RightUpperArm, "armr", "rightarm", "rightupperarm", "upperarmr", "rupperarm", "rarm"));
            if (forward.sqrMagnitude > .001f)
            {
                leftArm.FitArm(forward);
                rightArm.FitArm(forward);
            }
            leftThigh = new Joint(Find(bones, human, HumanBodyBones.LeftUpperLeg, "thighl", "leftupleg", "leftupperleg", "legl", "lthigh"));
            rightThigh = new Joint(Find(bones, human, HumanBodyBones.RightUpperLeg, "thighr", "rightupleg", "rightupperleg", "legr", "rthigh"));
            leftShin = new Joint(Find(bones, human, HumanBodyBones.LeftLowerLeg, "shinl", "leftleg", "leftlowerleg", "calfl"));
            rightShin = new Joint(Find(bones, human, HumanBodyBones.RightLowerLeg, "shinr", "rightleg", "rightlowerleg", "calfr"));
            leftFoot = new Joint(Find(bones, human, HumanBodyBones.LeftFoot, "footl", "leftfoot", "lfoot"));
            rightFoot = new Joint(Find(bones, human, HumanBodyBones.RightFoot, "footr", "rightfoot", "rfoot"));
            joints = new[] { torso, head, leftArm, rightArm, leftThigh, rightThigh, leftShin, rightShin, leftFoot, rightFoot };
        }

        public Vector3 DialogueTarget
        {
            get
            {
                // Si mira appena sotto il volto: occhi e bocca restano sopra il pannello del dialogo.
                return head.bone ? head.bone.position - Vector3.up * (.055f * height)
                    : actor.position + Vector3.up * (.79f * height);
            }
        }

        public void ClearFrame() { foreach (var joint in joints) joint.Clear(); }

        public void Rest()
        {
            ClearFrame();
            gait = run = speechWeight = 0f;
        }

        static Quaternion Turn(Vector3 right, Vector3 forward, float pitch, float roll = 0f, float yaw = 0f)
        {
            return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(roll, forward)
                * Quaternion.AngleAxis(pitch, right);
        }

        void Foot(Joint foot, Joint thigh, float step, float lift, Vector3 right, Vector3 forward)
        {
            Vector3 offset = thigh.bone ? Vector3.zero :
                (forward * (.105f * step) + Vector3.up * (.075f * Mathf.Max(0f, lift) * gait)) * height;
            foot.Pose(Turn(right, forward, -step * 10f), offset);
        }

        public void Animate(float speed, bool talking, bool deliveringLine, Vector3 forward, float dt)
        {
            gait = Mathf.MoveTowards(gait, Mathf.Clamp01(speed / 1.2f), dt * 7f);
            run = Mathf.MoveTowards(run, Mathf.InverseLerp(2.5f, 5.2f, speed), dt * 5f);
            speechWeight = Mathf.MoveTowards(speechWeight, talking ? (deliveringLine ? 1f : .36f) : 0f, dt * 5f);
            if (gait + speechWeight <= .001f) return;
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            phase += dt * Mathf.Lerp(6f, 11.6f, Mathf.Clamp01(speed / 5f)) * gait;
            float stride = Mathf.Sin(phase) * gait;
            float time = Time.unscaledTime;
            float beat = Mathf.Sin(time * 3.15f) * speechWeight;
            float gesture = (.5f + .5f * Mathf.Sin(time * 2.1f)) * speechWeight;
            float swing = Mathf.Lerp(21f, 34f, run);

            torso.Pose(Turn(right, forward, (3f + 5f * run) * gait + beat * 1.8f, stride * 3f + gesture * 2f),
                Vector3.up * (Mathf.Sin(phase) * Mathf.Sin(phase) * gait * .021f * height));
            head.Pose(Turn(right, forward, beat * 4.2f - gait * 2f, gesture * 2.5f, gesture * 5f), Vector3.zero);
            float armWeight = Mathf.Max(gait, speechWeight);
            leftArm.Pose(Turn(right, forward, stride * swing - gesture * 12f) * leftArm.LowerArm(forward, armWeight), Vector3.zero);
            rightArm.Pose(Turn(right, forward, -stride * swing - gesture * 26f, -gesture * 8f) * rightArm.LowerArm(forward, armWeight), Vector3.zero);
            leftThigh.Pose(Turn(right, forward, -stride * swing), Vector3.zero);
            rightThigh.Pose(Turn(right, forward, stride * swing), Vector3.zero);
            leftShin.Pose(Turn(right, forward, Mathf.Max(0f, stride) * (18f + 24f * run)), Vector3.zero);
            rightShin.Pose(Turn(right, forward, Mathf.Max(0f, -stride) * (18f + 24f * run)), Vector3.zero);
            Foot(leftFoot, leftThigh, stride, Mathf.Cos(phase), right, forward);
            Foot(rightFoot, rightThigh, -stride, -Mathf.Cos(phase), right, forward);
        }
    }

    // Usa la camera già collegata al giocatore, con un'inquadratura dietro e di lato.
    internal sealed class MiaEscortShot
    {
        readonly Camera camera;
        readonly Transform player, ranger, mount;
        readonly Vector3 homePosition;
        readonly Quaternion homeRotation;
        readonly int homeMask;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly List<Renderer> shadowBodies = new List<Renderer>();
        readonly int bodyLayers;
        bool wasWide, showingBody;

        public MiaEscortShot(Camera camera, Transform player, Transform ranger)
        {
            this.camera = camera;
            this.player = player;
            this.ranger = ranger;
            mount = camera.transform.parent;
            homePosition = camera.transform.localPosition;
            homeRotation = camera.transform.localRotation;
            homeMask = camera.cullingMask;
            int layers = 0;
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled || r.transform.IsChildOf(camera.transform)) continue;
                layers |= 1 << r.gameObject.layer;
                if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) shadowBodies.Add(r);
            }
            bodyLayers = layers;
        }

        void ShowBody(bool show)
        {
            if (show == showingBody) return;
            showingBody = show;
            camera.cullingMask = show ? homeMask | bodyLayers : homeMask;
            foreach (var r in shadowBodies)
                if (r) r.shadowCastingMode = show ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        }

        public void Frame(float blend, Vector3 heading)
        {
            if (!camera) return;
            if (blend <= 0f) { Restore(); return; }
            wasWide = true;
            Vector3 eye = mount ? mount.TransformPoint(homePosition) : player.position + Vector3.up * 1.5f;
            Quaternion firstPerson = mount ? mount.rotation * homeRotation : player.rotation;
            Vector3 side = Vector3.Cross(Vector3.up, heading);
            Vector3 offset = -heading * 3.7f + side * 1.9f + Vector3.up * 1.15f;
            float distance = offset.magnitude;
            Vector3 direction = offset / distance;
            int count = Physics.SphereCastNonAlloc(eye, .19f, direction, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; ++i)
            {
                Transform t = hits[i].collider.transform;
                if (!t.IsChildOf(player) && !t.IsChildOf(ranger))
                    distance = Mathf.Min(distance, Mathf.Max(0f, hits[i].distance - .09f));
            }
            Vector3 position = eye + direction * (distance * blend);
            Vector3 focus = Vector3.Lerp(player.position, ranger.position, .52f) + Vector3.up * 1.05f;
            Vector3 look = focus - position;
            Quaternion wide = look.sqrMagnitude < .001f ? firstPerson : Quaternion.LookRotation(look);
            camera.transform.SetPositionAndRotation(position, Quaternion.Slerp(firstPerson, wide, blend));
            ShowBody(blend * distance > .75f);
        }

        public void Restore()
        {
            if (!wasWide || !camera) return;
            camera.transform.SetLocalPositionAndRotation(homePosition, homeRotation);
            ShowBody(false);
            wasWide = false;
        }
    }
}
