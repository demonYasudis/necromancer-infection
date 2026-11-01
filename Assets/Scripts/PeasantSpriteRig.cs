using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NecromancerInfection
{
    /// <summary>Articulates parts cut from one anatomically coherent reference pose.</summary>
    public sealed class PeasantSpriteRig : MonoBehaviour
    {
        [Serializable]
        public struct Piece
        {
            public string Name;
            public Vector2 RestStart, RestEnd, ImageStart, ImageEnd;
        }
        [Serializable] public sealed class Definition { public Piece[] Pieces; }
        public Sprite[] Sprites;
        public Piece[] Pieces;
        public bool PreviewWalking = true;
        public bool ShowPouch;
        [Range(0, 2)] public float Tempo = .85f;
        [Range(.1f, 1.6f)] public float Stride = .65f;
        public bool Paused;
        [NonSerialized] public bool MirrorX;
        [NonSerialized] public float InfectionBend;
        [NonSerialized] public Vector2 RecoilOffset;
        public float Phase { get; private set; }
        public bool ArmDetached => detached;
        public Transform PouchSocket { get { Build(); return parts[11].transform; } }
        private SpriteRenderer[] parts;
        private Transform canvas, looseArm;
        private Vector3 previous, looseStart;
        private Vector2 bodyOffset;
        private float weight, weightVelocity, fallAge;
        private float poseMovement;
        private readonly float[] legMotionScale = new float[2];
        private bool detached;
        private static readonly string[] Names = { "Head", "Torso", "Pelvis", "NearUpperArm", "NearForearm", "FarUpperArm", "FarForearm", "NearThigh", "NearShin", "FarThigh", "FarShin", "Pouch" };

        private void Awake() => Build();
        private void OnEnable() => previous = transform.position;
        private void Build()
        {
            if (parts != null || Pieces == null || Pieces.Length != 12) return;
            canvas = new GameObject("SpriteSkeleton").transform;
            canvas.SetParent(transform, false);
            canvas.gameObject.AddComponent<SortingGroup>();
            parts = new SpriteRenderer[12];
            // The right thigh covers the shin's small overlap at the painted knee.
            int[] order = { 11, 7, 6, 0, 1, 8, 9, 3, 2, 4, 5, 10 };
            for (int i = 0; i < 12; i++)
            {
                var item = new GameObject(Names[i]);
                item.transform.SetParent(canvas, false);
                parts[i] = item.AddComponent<SpriteRenderer>();
                parts[i].sprite = Sprites[i];
                parts[i].sortingOrder = order[i];
            }
            looseArm = new GameObject("DetachedArm").transform;
            looseArm.SetParent(canvas, false);
            legMotionScale[0] = CalibrateLegMotion(9, 10);
            legMotionScale[1] = CalibrateLegMotion(7, 8);
            DrawPose(0);
        }

        private void LateUpdate()
        {
            Build();
            Vector3 travel = transform.position - previous;
            previous = transform.position;
            if (parts == null || Paused || Time.deltaTime <= 0) return;
            if (PreviewWalking)
            {
                BlendMovement(Tempo > 0 ? 1 : 0);
                Phase = Mathf.Repeat(Phase + Time.deltaTime * Tempo, 1);
            }
            else
            {
                travel.y = 0;
                float distance = travel.magnitude;
                bool teleport = distance > Mathf.Max(.5f, Time.deltaTime * 12);
                float speed = teleport ? 0 : distance / Time.deltaTime;
                BlendMovement(Mathf.Clamp01(speed / .8f));
                if (!teleport) Phase = Mathf.Repeat(Phase + distance / Mathf.Max(.2f, Stride), 1);
            }
            if (detached)
            {
                fallAge = Mathf.Min(1.2f, fallAge + Time.deltaTime);
                looseArm.localPosition = looseStart + new Vector3(fallAge * .6f, -Mathf.Min(1.2f, fallAge * fallAge * 3), 0);
                looseArm.localRotation = Quaternion.Euler(0, 0, -Mathf.Min(85, fallAge * 110));
            }
            DrawPose(weight);
        }

        private void BlendMovement(float target)
        {
            weight = Mathf.SmoothDamp(weight, target, ref weightVelocity, .08f, Mathf.Infinity, Time.deltaTime);
            if (target == 0 && weight < .01f) { weight = 0; weightVelocity = 0; }
        }

        public void SetPhase(float value) { Build(); Phase = Mathf.Repeat(value, 1); DrawPose(1); }
        public void SetIdlePose() { Build(); weight = 0; weightVelocity = 0; DrawPose(0); }
        public void SetPouch(bool value) { ShowPouch = value; if (parts != null) parts[11].enabled = value; }
        public void DetachArm()
        {
            Build();
            if (detached) return;
            detached = true;
            fallAge = 0;
            looseStart = Pieces[5].RestStart + bodyOffset;
            looseArm.localPosition = looseStart;
            looseArm.localRotation = Quaternion.identity;
            parts[5].transform.SetParent(looseArm, true);
            parts[6].transform.SetParent(looseArm, true);
        }
        public void RestoreArm()
        {
            Build();
            parts[5].transform.SetParent(canvas, false);
            parts[6].transform.SetParent(canvas, false);
            detached = false;
            looseArm.localPosition = Vector3.zero;
            looseArm.localRotation = Quaternion.identity;
            DrawPose(weight);
        }

        public static Vector2 Foot(float cycle, float stride)
        {
            float u = Mathf.Repeat(cycle, 1);
            if (u < .6f) return new Vector2(stride * (.5f - u / .6f), 0);
            float v = (u - .6f) / .4f;
            // Match stance velocity and acceleration at both contacts. The old cubic
            // stopped the foot abruptly, while sin(v) began the lift with a sharp kick.
            float ease = v * v * v * (10 + v * (-15 + 6 * v));
            float forward = -.5f - (2f / 3) * v + (5f / 3) * ease;
            float lift = Mathf.Sin(Mathf.PI * v);
            return new Vector2(stride * forward, .23f * lift * lift * lift * lift);
        }

        public static Vector2 SolveJoint(Vector2 start, ref Vector2 end, float upper, float lower, float bend)
        {
            Vector2 delta = end - start;
            float length = Mathf.Clamp(delta.magnitude, Mathf.Abs(upper - lower) + .00001f, upper + lower - .00001f);
            Vector2 direction = delta.sqrMagnitude > .000001f ? delta.normalized : Vector2.down;
            end = start + direction * length;
            float along = (upper * upper - lower * lower + length * length) / (2 * length);
            float across = Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            return start + direction * along + new Vector2(-direction.y, direction.x) * (across * bend);
        }

        private void DrawPose(float movement)
        {
            if (parts == null) return;
            poseMovement = movement;
            bodyOffset = GaitBodyOffset(Phase) * movement;
            for (int i = 0; i < 12; i++)
            {
                if (detached && (i == 5 || i == 6)) continue;
                Segment(i, Pieces[i].RestStart + bodyOffset, Pieces[i].RestEnd + bodyOffset);
            }
            if (movement > 0)
            {
                for (int side = 0; side < 2; side++)
                {
                    Vector2 foot = Foot(Phase + side * .5f, Mathf.Clamp(Stride, .1f, 1.6f) * .42f) * movement;
                    Vector2 displacement = FootDisplacement(foot) * legMotionScale[side];
                    Limb(side == 0 ? 9 : 7, side == 0 ? 10 : 8, displacement);
                    if (!(detached && side == 0))
                        Limb(side == 0 ? 5 : 3, side == 0 ? 6 : 4, new Vector2(-foot.x * .20f, foot.x * .08f + foot.y * .02f) + bodyOffset);
                }
            }
            parts[11].enabled = ShowPouch;
            canvas.localScale = new Vector3(MirrorX ? -1 : 1, 1, 1);
            if (Camera.main) canvas.rotation = Camera.main.transform.rotation;
        }

        private void Limb(int upperIndex, int lowerIndex, Vector2 displacement)
        {
            Piece upper = Pieces[upperIndex], lower = Pieces[lowerIndex];
            Vector2 start = upper.RestStart + bodyOffset;
            Vector2 end = lower.RestEnd + displacement;
            Vector2 original = lower.RestEnd - upper.RestStart;
            Vector2 perpendicular = new Vector2(-original.y, original.x);
            float bend = Vector2.Dot(upper.RestEnd - upper.RestStart, perpendicular) < 0 ? -1 : 1;
            float upperLength = Vector2.Distance(upper.RestStart, upper.RestEnd);
            float lowerLength = Vector2.Distance(lower.RestStart, lower.RestEnd);
            if (upperIndex >= 7) end = SoftenLegReach(start, end, upperLength + lowerLength, poseMovement);
            Vector2 joint = SolveJoint(start, ref end, upperLength, lowerLength, bend);
            Segment(upperIndex, start, joint);
            Segment(lowerIndex, joint, end);
        }

        private static Vector2 GaitBodyOffset(float phase) => Vector2.up * (-.002f + .001f * Mathf.Cos(phase * 4 * Mathf.PI));

        // Forward/back on two narrow parallel lanes; avoid swinging the ankles across the body.
        private static Vector2 FootDisplacement(Vector2 foot) => new Vector2(foot.x * .10f, -foot.x * .20f + foot.y * .12f);

        private float CalibrateLegMotion(int upperIndex, int lowerIndex)
        {
            Piece upper = Pieces[upperIndex], lower = Pieces[lowerIndex];
            Vector2 original = lower.RestEnd - upper.RestStart;
            float bend = Vector2.Dot(upper.RestEnd - upper.RestStart, new Vector2(-original.y, original.x)) < 0 ? -1 : 1;
            float upperLength = Vector2.Distance(upper.RestStart, upper.RestEnd);
            float lowerLength = Vector2.Distance(lower.RestStart, lower.RestEnd);
            float low = 0, high = 1;
            // Fit one amplitude for the entire cycle, instead of clipping individual
            // frames against a joint limit and producing long stationary plateaus.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float amplitude = (low + high) * .5f;
                bool fits = true;
                for (int frame = 0; frame < 120; frame++)
                {
                    float phase = frame / 120f;
                    Vector2 start = upper.RestStart + GaitBodyOffset(phase);
                    Vector2 end = lower.RestEnd + FootDisplacement(Foot(phase, 1.6f * .42f)) * amplitude;
                    end = SoftenLegReach(start, end, upperLength + lowerLength, 1);
                    Vector2 joint = SolveJoint(start, ref end, upperLength, lowerLength, bend);
                    if (!FitsPaintedLeg(upper, lower, start, joint, end)) { fits = false; break; }
                }
                if (fits) low = amplitude; else high = amplitude;
            }
            return low * .95f;
        }

        private static Vector2 SoftenLegReach(Vector2 start, Vector2 end, float reach, float movement)
        {
            // Stay smoothly inside full extension, avoiding the IK singularity where
            // a fully straight knee used to snap into or out of the next pose.
            Vector2 delta = end - start;
            float distance = delta.magnitude;
            float softness = .0015f * movement;
            if (softness < .0000001f || distance < .000001f) return end;
            float ceiling = reach - .0001f * movement;
            float slack = (ceiling - distance) / softness;
            float softplus = slack > 20 ? slack : Mathf.Log(1 + Mathf.Exp(slack));
            float softened = ceiling - softness * softplus;
            return start + delta * (softened / distance);
        }

        private static bool FitsPaintedLeg(Piece upper, Piece lower, Vector2 start, Vector2 joint, Vector2 end)
        {
            const float limit = 18;
            return Vector2.Angle(upper.RestEnd - upper.RestStart, joint - start) <= limit
                && Vector2.Angle(lower.RestEnd - lower.RestStart, end - joint) <= limit;
        }

        private void Segment(int index, Vector2 from, Vector2 to)
        {
            var item = parts[index];
            Piece piece = Pieces[index];
            if (InfectionBend > 0 && index != 2 && index != 7 && index != 8 && index != 9 && index != 10)
            {
                Vector2 pivot = Pieces[2].RestEnd + bodyOffset;
                Quaternion fold = Quaternion.Euler(0, 0, -24 * Mathf.Clamp01(InfectionBend));
                from = pivot + (Vector2)(fold * (from - pivot));
                to = pivot + (Vector2)(fold * (to - pivot));
            }
            from += RecoilOffset;
            to += RecoilOffset;
            Vector2 size = item.sprite.bounds.size;
            Vector2 start = Vector2.Scale(piece.ImageStart - Vector2.one * .5f, size);
            Vector2 end = Vector2.Scale(piece.ImageEnd - Vector2.one * .5f, size);
            Vector2 source = end - start, target = to - from;
            float scale = target.magnitude / source.magnitude;
            Quaternion rotation = Quaternion.Euler(0, 0, (Mathf.Atan2(target.y, target.x) - Mathf.Atan2(source.y, source.x)) * Mathf.Rad2Deg);
            item.transform.localPosition = (Vector3)from - rotation * ((Vector3)start * scale);
            item.transform.localRotation = rotation;
            item.transform.localScale = new Vector3(scale, scale, 1);
        }
    }
}

