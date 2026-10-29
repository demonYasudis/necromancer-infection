using System.Collections.Generic;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Composes distance-driven gait, attacks and recoil on the existing model parts.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatMotion : MonoBehaviour
    {
        public enum Transformation { None, Infection, ZombieRise, HeavyRise, SkeletonAssembly }
        private sealed class Part
        {
            public Transform Transform;
            public Vector3 Position;
            public Quaternion Rotation;
            public bool Weapon, Arm, Shield, Leg, Staff, Robe;
            public float Side;
            public int Index;
        }

        private readonly List<Part> parts = new List<Part>();
        private UnitCombat combat;
        private bool guard, heavy;
        private bool necromancer, walker, skeleton;
        private Vector3 previousPosition;
        private Vector3 staffPivot;
        private float gaitPhase, gaitWeight;
        public float GaitPhase => gaitPhase;
        public float GaitWeight => gaitWeight;
        public float GaitStrideLength => heavy ? 2.1f : necromancer ? 1.65f : skeleton ? 1.3f : 1.5f;
        private float reactionRemaining;
        private Vector3 recoil;
        public float ReactionRemaining => reactionRemaining;
        private bool initialized;
        private Transformation transformation;
        private float transformationStartedAt, transformationDuration;
        public Transformation CurrentTransformation => transformation;
        public float TransformationProgress => transformation == Transformation.None ? 1
            : Mathf.Clamp01((Time.time - transformationStartedAt) / transformationDuration);

        private void Start() => InitializeParts();

        private void InitializeParts()
        {
            if (initialized) return;
            initialized = true;
            combat = GetComponent<UnitCombat>();
            guard = GetComponent<Guard>();
            heavy = GetComponent<RaisedGuard>();
            necromancer = GetComponent<Necromancer>();
            skeleton = GetComponent<Skeleton>();
            walker = necromancer || GetComponent<Zombie>();
            previousPosition = transform.position;
            // An instance-specific phase without consuming Unity's gameplay random stream.
            uint seed = unchecked((uint)GetInstanceID() * 2654435761u);
            gaitPhase = (seed & 0xffff) / 65536f * Mathf.PI * 2;
            var staff = transform.Find("Staff");
            staffPivot = staff ? new Vector3(staff.localPosition.x, 0.65f, staff.localPosition.z) : Vector3.zero;
            foreach (Transform child in transform)
            {
                if (!child.GetComponent<Renderer>() || child.GetComponent<LineRenderer>()) continue;
                string label = child.name;
                parts.Add(new Part { Transform = child, Position = child.localPosition, Rotation = child.localRotation,
                    Weapon = label.StartsWith("Sword"), Arm = label.StartsWith("Arm"),
                    Shield = label.StartsWith("Shield"), Leg = label.StartsWith("Leg"),
                    Staff = label.StartsWith("Staff"), Robe = label == "Robe",
                    Side = child.localPosition.x < 0 ? -1 : 1, Index = parts.Count });
            }
            combat.HitReceived += OnHit;
        }

        public void BeginInfection(float duration) => BeginTransformation(Transformation.Infection, Mathf.Max(0.01f, duration));

        public void BeginRising()
        {
            InitializeParts();
            BeginTransformation(heavy ? Transformation.HeavyRise : skeleton ? Transformation.SkeletonAssembly : Transformation.ZombieRise,
                heavy ? 0.65f : skeleton ? 0.6f : 0.45f);
        }

        private void BeginTransformation(Transformation kind, float duration)
        {
            InitializeParts();
            if (!isActiveAndEnabled || !combat.IsAlive || transformation != Transformation.None) return;
            transformation = kind;
            transformationDuration = duration;
            transformationStartedAt = Time.time;
            // Newborn models start in the first pose before their first rendered frame.
            foreach (var part in parts)
            {
                Vector3 position = part.Position;
                Quaternion rotation = part.Rotation;
                ApplyTransformation(part, ref position, ref rotation);
                part.Transform.localPosition = position;
                part.Transform.localRotation = rotation;
            }
        }

        public void CancelTransformation()
        {
            if (transformation == Transformation.None) return;
            transformation = Transformation.None;
            RestoreParts();
        }

        private void OnHit(Vector3 direction)
        {
            direction.y = 0;
            recoil = transform.InverseTransformDirection(direction.normalized);
            reactionRemaining = 0.16f;
        }

        private void LateUpdate()
        {
            Vector3 travel = transform.position - previousPosition;
            previousPosition = transform.position;
            if (!combat || Time.deltaTime <= 0) return;
            if (!combat.IsAlive || !combat.isActiveAndEnabled
                || (transformation != Transformation.Infection && TransformationProgress >= 1))
                transformation = Transformation.None;
            UpdateGait(travel);
            reactionRemaining = Mathf.Max(0, reactionRemaining - Time.deltaTime);
            float reaction = Mathf.Sin(reactionRemaining / 0.16f * Mathf.PI);
            float pose = 0;
            if (combat.IsMeleeAttacking)
            {
                transform.rotation = Quaternion.LookRotation(combat.MeleeDirection);
                float elapsed = combat.MeleeElapsed;
                float windup = combat.MeleeWindup;
                float strike = Mathf.Min(0.08f, (combat.MeleeDuration - windup) * 0.4f);
                // Contact pose and damage share the same timestamp; recovery follows through.
                if (elapsed < windup)
                {
                    float progress = elapsed / windup;
                    pose = progress < 0.68f ? -Mathf.Sin(progress / 0.68f * Mathf.PI * 0.5f)
                        : Mathf.Lerp(-1, 1, Mathf.SmoothStep(0, 1, (progress - 0.68f) / 0.32f));
                }
                else if (elapsed < windup + strike) pose = Mathf.Lerp(1, 0.75f, (elapsed - windup) / strike);
                else pose = 0.75f * (1 - Mathf.Clamp01((elapsed - windup - strike)
                    / (combat.MeleeDuration - windup - strike)));
            }
            foreach (var part in parts)
            {
                if (!part.Transform) continue;
                Vector3 offset = recoil * (reaction * (heavy ? 0.035f : 0.075f));
                Vector3 angles = new Vector3(-recoil.z * reaction * 9, 0, recoil.x * reaction * 9);
                ApplyGait(part, ref offset, ref angles);
                if (!part.Leg)
                {
                    offset.z += pose * (heavy ? 0.10f : guard ? 0.06f : 0.13f);
                    angles.x += pose * (heavy ? 8 : guard ? 5 : 13);
                }
                if (part.Weapon)
                {
                    offset.z += pose * (heavy ? 0.24f : 0.30f);
                    angles.x += pose * (heavy ? 55 : 35);
                    angles.z += pose * (guard ? -28 : 16);
                }
                if (part.Arm)
                {
                    offset.z += pose * 0.22f;
                    angles.x += pose * 48;
                    angles.z += pose * (part.Position.x < 0 ? -12 : 20);
                }
                if (part.Shield) { offset.z += pose * 0.04f; angles.y -= pose * 12; }
                Vector3 position = part.Position + offset;
                Quaternion rotation = part.Rotation * Quaternion.Euler(angles);
                ApplyTransformation(part, ref position, ref rotation);
                part.Transform.localPosition = position;
                part.Transform.localRotation = rotation;
            }
        }

        private void ApplyTransformation(Part part, ref Vector3 position, ref Quaternion rotation)
        {
            if (transformation == Transformation.None) return;
            float progress = TransformationProgress;
            if (transformation == Transformation.Infection || transformation == Transformation.ZombieRise)
            {
                float bend = transformation == Transformation.Infection
                    ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / 0.7f)) : 1 - Mathf.SmoothStep(0, 1, progress);
                if (part.Leg)
                {
                    position.y -= bend * 0.04f;
                    rotation = Quaternion.Euler(bend * 8, 0, 0) * rotation;
                }
                else
                {
                    Vector3 pivot = Vector3.up * 0.65f;
                    Quaternion fold = Quaternion.Euler(bend * 65, 0, 0);
                    position = pivot + fold * (position - pivot) + Vector3.down * (bend * 0.08f);
                    rotation = fold * rotation;
                }
            }
            else if (transformation == Transformation.HeavyRise)
            {
                float rise = Mathf.SmoothStep(0, 1, progress);
                Vector3 pivot = Vector3.up * 0.15f;
                Quaternion lift = Quaternion.Euler((1 - rise) * 85, 0, 0);
                position = pivot + lift * (position - pivot);
                rotation = lift * rotation;
            }
            else
            {
                // Each piece starts close to its own spawn point, never at a shared world origin.
                float stagger = part.Index % 4 * 0.055f;
                float assemble = Mathf.SmoothStep(0, 1, Mathf.Clamp01((progress - stagger) / (1 - stagger)));
                float angle = part.Index * 2.399963f;
                Vector3 scattered = new Vector3(Mathf.Cos(angle) * 0.38f,
                    0.32f + part.Index % 3 * 0.22f, Mathf.Sin(angle) * 0.38f);
                position = Vector3.Lerp(scattered, position, assemble);
                rotation = Quaternion.Euler((1 - assemble) * (part.Index % 2 == 0 ? 70 : -55),
                    (1 - assemble) * part.Index * 37, 0) * rotation;
            }
        }

        private void UpdateGait(Vector3 travel)
        {
            travel.y = 0;
            float distance = travel.magnitude;
            if (!walker || !combat.IsAlive || !combat.isActiveAndEnabled
                || distance > Mathf.Max(0.5f, Time.deltaTime * 12))
            {
                gaitWeight = 0; // Teleports, reactivation and retries never count as footsteps.
                return;
            }
            float speed = distance / Time.deltaTime;
            float target = Mathf.Clamp01(speed / (heavy ? 2.4f : necromancer ? 2 : 3.2f));
            gaitWeight = Mathf.MoveTowards(gaitWeight, target, Time.deltaTime * 7);
            gaitPhase = Mathf.Repeat(gaitPhase + distance / GaitStrideLength * Mathf.PI * 2, Mathf.PI * 2);
        }

        private void ApplyGait(Part part, ref Vector3 offset, ref Vector3 angles)
        {
            if (gaitWeight <= 0) return;
            float step = Mathf.Sin(gaitPhase);
            float alternating = Mathf.Sin(gaitPhase * 2);
            float upper = gaitWeight * (combat.IsMeleeAttacking ? 0.18f : 1);
            if (part.Leg)
            {
                float foot = step * part.Side;
                // The heavy keeps its feet low and plants them for longer.
                if (heavy) foot = Mathf.Sign(foot) * Mathf.Pow(Mathf.Abs(foot), 0.7f);
                offset.z += foot * (heavy ? 0.085f : 0.13f) * gaitWeight;
                offset.y += Mathf.Max(0, foot) * (heavy ? 0.045f : 0.07f) * gaitWeight;
                angles.x += foot * (heavy ? 13 : 24) * gaitWeight;
                return;
            }
            float bob = heavy ? Mathf.Abs(step) * 0.045f : necromancer ? alternating * 0.018f
                : (alternating * 0.022f + step * 0.014f);
            offset.y += bob * upper;
            float sway = heavy ? step * 3.5f : necromancer ? step * 1.5f
                : step * 4 + Mathf.Sin(gaitPhase * 2 + 0.7f) * 1.5f;
            if (!part.Staff) angles.z += sway * upper;
            if (part.Staff)
            {
                // Staff and crystal rotate about one grip, preserving their attachment.
                Vector3 staffAngles = new Vector3(Mathf.Sin(gaitPhase - 0.4f) * 5, 0, step * 3) * upper;
                staffAngles.z += sway * upper;
                offset += (Quaternion.Euler(staffAngles) * (part.Position - staffPivot)) - (part.Position - staffPivot);
                angles += staffAngles;
            }
            if (part.Robe)
            {
                angles.x += Mathf.Sin(gaitPhase - 0.65f) * 3 * upper;
                angles.z += Mathf.Sin(gaitPhase - 0.5f) * 2 * upper;
            }
            if (part.Arm)
            {
                float drag = Mathf.Sin(gaitPhase - part.Side * 0.4f);
                angles.x += drag * part.Side * (skeleton ? 13 : 9) * upper;
                offset.z -= step * part.Side * 0.045f * upper;
            }
            if (part.Weapon || part.Shield) angles.x += Mathf.Sin(gaitPhase - 0.55f) * 4 * upper;
        }

        private void OnEnable() => previousPosition = transform.position;

        private void OnDisable()
        {
            reactionRemaining = 0;
            gaitWeight = 0;
            transformation = Transformation.None;
            RestoreParts();
        }

        private void RestoreParts()
        {
            foreach (var part in parts)
                if (part.Transform) { part.Transform.localPosition = part.Position; part.Transform.localRotation = part.Rotation; }
        }

        private void OnDestroy()
        {
            if (combat) combat.HitReceived -= OnHit;
        }
    }
}
