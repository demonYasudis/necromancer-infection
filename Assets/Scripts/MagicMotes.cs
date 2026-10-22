using UnityEngine;
using UnityEngine.Rendering;

namespace NecromancerInfection
{
    /// <summary>Small procedural magic effect; owns and releases its runtime material.</summary>
    public sealed class MagicMotes : MonoBehaviour
    {
        private Material material;
        private Light glow;
        private LineRenderer ring;
        private float bornAt;
        private bool burst;

        public static MagicMotes Infection(Transform host)
        {
            var effect = new GameObject("InfectionGlow").AddComponent<MagicMotes>();
            effect.transform.SetParent(host, false);
            effect.Build(false);
            return effect;
        }

        public static void Conversion(Vector3 position)
        {
            var effect = new GameObject("ConversionBurst").AddComponent<MagicMotes>();
            effect.transform.position = position;
            effect.Build(true);
        }

        private void Build(bool isBurst)
        {
            burst = isBurst;
            bornAt = Time.time;
            material = new Material(Shader.Find("NecromancerInfection/ProtectionArea"));
            material.SetColor("_Color", new Color(0.38f, 1, 0.15f, 0.9f));
            var particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = !burst;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, burst ? 2 : 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.startColor = Color.white;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = burst ? 0 : 16;
            if (burst) emission.SetBursts(new[] { new ParticleSystem.Burst(0, 24) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.4f;
            shape.angle = burst ? 55 : 15;
            shape.rotation = new Vector3(-90, 0, 0);
            shape.position = Vector3.up * 0.25f;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0.85f, 0), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var lamp = new GameObject("GreenGlow");
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = Vector3.up * 0.7f;
            glow = lamp.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.4f, 1, 0.12f);
            glow.range = 2.2f;
            glow.shadows = LightShadows.None;
            ring = new GameObject("InfectionRing").AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, false);
            ring.sharedMaterial = material;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 32;
            ring.widthMultiplier = 0.06f;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.55f, 0.1f, Mathf.Sin(angle) * 0.55f));
            }
            particles.Play();
        }

        private void Update()
        {
            float age = Time.time - bornAt;
            float strength = burst ? Mathf.Clamp01(1 - age / 0.9f) : 0.7f + Mathf.Sin(age * 9) * 0.2f;
            glow.intensity = strength * 1.5f;
            ring.startColor = ring.endColor = new Color(1, 1, 1, strength);
            if (burst)
            {
                ring.transform.localScale = Vector3.one * (1 + age * 1.5f);
                if (age >= 1) Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (material) Destroy(material);
        }
    }
}
