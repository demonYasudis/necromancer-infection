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

        public float Radius => radius;
        public float Duration => duration;
        public float DamageReduction => damageReduction;
        public bool IsActive => isActiveAndEnabled && Time.time < expiresAt;

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
            var ring = GetComponent<LineRenderer>();
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
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.1f, radius);
            duration = Mathf.Max(0.01f, duration);
            damageReduction = Mathf.Clamp01(damageReduction);
        }
    }
}
