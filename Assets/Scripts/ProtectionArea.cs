using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NecromancerInfection
{
    [RequireComponent(typeof(LineRenderer), typeof(MeshFilter), typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class ProtectionArea : MonoBehaviour
    {
        private static readonly HashSet<ProtectionArea> Active = new HashSet<ProtectionArea>();
        [SerializeField, Min(0.1f)] private float radius = 3;
        [SerializeField, Min(0.01f)] private float duration = 5;
        [SerializeField, Range(0, 1)] private float damageReduction = 0.5f;
        private float expiresAt;
        private Mesh disc;
        private LineRenderer timerArc;
        private LineRenderer ring;
        private readonly Vector3[] arcPoints = new Vector3[97];

        public float Radius => radius;
        public float Duration => duration;
        public float DamageReduction => damageReduction;
        public bool IsActive => isActiveAndEnabled && Time.time < expiresAt;
        public float RemainingFraction => Mathf.Clamp01((expiresAt - Time.time) / duration);

        private void Awake()
        {
            expiresAt = Time.time + duration;
            BuildVisual();
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);
        private void OnDestroy()
        {
            Active.Remove(this);
            if (disc) Destroy(disc);
        }

        private void Update()
        {
            if (Time.time >= expiresAt) Destroy(gameObject);
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            float remaining = RemainingFraction;
            float alpha = Mathf.Clamp01(remaining * 5);
            float pulse = 0.75f + Mathf.Sin((duration - (expiresAt - Time.time)) * 5) * 0.15f;
            ring.startColor = ring.endColor = new Color(1, 1, 1, alpha * pulse);
            timerArc.startColor = timerArc.endColor = new Color(1, 1, 1, alpha);
            int count = Mathf.Max(2, Mathf.CeilToInt(96 * remaining) + 1);
            timerArc.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 0.5f + i / (float)(count - 1) * Mathf.PI * 2 * remaining;
                arcPoints[i] = new Vector3(Mathf.Cos(angle) * radius * 0.91f, 0.07f, Mathf.Sin(angle) * radius * 0.91f);
                timerArc.SetPosition(i, arcPoints[i]);
            }
        }

        public static float IncomingDamageMultiplier(UnitCombat target)
        {
            if (!target || (!target.GetComponent<Zombie>() && !target.GetComponent<Necromancer>())) return 1;
            float multiplier = 1;
            foreach (var area in Active)
            {
                if (!area || !area.IsActive || area.gameObject.scene != target.gameObject.scene) continue;
                Vector3 offset = target.transform.position - area.transform.position;
                offset.y = 0;
                if (offset.sqrMagnitude <= area.radius * area.radius)
                    multiplier = Mathf.Min(multiplier, 1 - area.damageReduction);
            }
            return multiplier;
        }

        private void BuildVisual()
        {
            const int segments = 96;
            ring = GetComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = segments;
            ring.widthMultiplier = 0.09f;
            ring.startColor = ring.endColor = Color.white;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            var vertices = new Vector3[segments + 1];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.up * 0.04f;
            colors[0] = new Color(1, 1, 1, 0.2f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector3 edge = new Vector3(Mathf.Cos(angle) * radius, 0.04f, Mathf.Sin(angle) * radius);
                vertices[i + 1] = edge;
                colors[i + 1] = new Color(1, 1, 1, 0.08f);
                ring.SetPosition(i, edge + Vector3.up * 0.01f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            disc = new Mesh { name = "ProtectionDisc" };
            disc.vertices = vertices;
            disc.colors = colors;
            disc.triangles = triangles;
            disc.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = disc;
            var fill = GetComponent<MeshRenderer>();
            fill.shadowCastingMode = ShadowCastingMode.Off;
            fill.receiveShadows = false;
            timerArc = new GameObject("ProtectionTimeArc").AddComponent<LineRenderer>();
            timerArc.transform.SetParent(transform, false);
            timerArc.useWorldSpace = false;
            timerArc.sharedMaterial = ring.sharedMaterial;
            timerArc.widthMultiplier = 0.14f;
            timerArc.shadowCastingMode = ShadowCastingMode.Off;
            timerArc.receiveShadows = false;
            UpdateVisual();
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.1f, radius);
            duration = Mathf.Max(0.01f, duration);
            damageReduction = Mathf.Clamp01(damageReduction);
        }
    }
}
