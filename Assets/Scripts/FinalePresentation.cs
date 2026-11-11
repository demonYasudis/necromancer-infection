using UnityEngine;
using UnityEngine.Rendering;

namespace NecromancerInfection
{
    /// <summary>Presentation of the farm's real capture progress and the completed attempt.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(150)]
    public sealed class FinalePresentation : MonoBehaviour
    {
        private const int RuneCount = 24;
        private GameManager manager;
        private FarmAssault assault;
        private Transform effects;
        private Material material;
        private LineRenderer[] runes;
        private LineRenderer seal, burst;
        private Light glow;
        private float pulseClock;
        private float endedAt = -1;
        private bool won;

        public float ResultAge => endedAt < 0 ? 0 : Mathf.Max(0, Time.unscaledTime - endedAt);
        public float CaptureProgress { get; private set; }
        public int VisibleRunes { get; private set; }
        public float LightIntensity => glow && glow.enabled && glow.gameObject.activeInHierarchy ? glow.intensity : 0;
        public float BurstProgress { get; private set; }
        public bool BurstVisible => burst && burst.enabled && effects && effects.gameObject.activeInHierarchy;
        public Transform Effects => effects;
        public float DefeatFade => Mathf.Lerp(.12f, .92f, Mathf.SmoothStep(0, 1, ResultAge / 1.2f));
        public float StatisticAlpha(int row) => Mathf.SmoothStep(0, 1,
            Mathf.Clamp01((ResultAge - .12f - Mathf.Clamp(row, 0, 3) * .18f) / .28f));

        private void Awake() => manager = GetComponent<GameManager>();

        private void Start()
        {
            if (!manager || !manager.Farm) return;
            assault = manager.Farm.GetComponent<FarmAssault>();
            if (!assault) return;
            effects = new GameObject("FarmCorruptionEffects").transform;
            effects.SetParent(transform, false);
            effects.position = manager.Farm.transform.position + Vector3.up * .075f;
            material = new Material(Shader.Find("Sprites/Default")) { name = "FarmCorruptionGlow" };
            runes = new LineRenderer[RuneCount];
            for (int i = 0; i < RuneCount; i++)
            {
                int ring = i / 8;
                float angle = (i % 8 + ring * .4f) * Mathf.PI / 4;
                var rune = MakeLine("CorruptionRune_" + i, 5, .045f);
                rune.transform.localPosition = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (1.2f + ring * 1.35f);
                rune.transform.localRotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
                rune.SetPositions(new[] { new Vector3(-.16f, 0, -.16f), new Vector3(.16f, 0, .16f),
                    new Vector3(0, 0, .3f), new Vector3(0, 0, -.3f), new Vector3(-.16f, 0, -.16f) });
                runes[i] = rune;
            }
            seal = MakeLine("CorruptionSeal", 65, .035f);
            Circle(seal, .7f);
            burst = MakeLine("VictoryShockwave", 65, .12f);
            var lightObject = new GameObject("CorruptionLight");
            lightObject.transform.SetParent(effects, false);
            lightObject.transform.localPosition = Vector3.up * 2;
            glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(.4f, 1, .15f);
            glow.range = 12;
            glow.shadows = LightShadows.None;
            Apply();
        }

        private LineRenderer MakeLine(string label, int points, float width)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(effects, false);
            line.useWorldSpace = false;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private static void Circle(LineRenderer line, float radius)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / (line.positionCount - 1);
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), .015f, Mathf.Sin(angle)) * radius);
            }
        }

        private void LateUpdate()
        {
            if (!manager) return;
            if (manager.IsFinished && endedAt < 0)
            {
                endedAt = Time.unscaledTime;
                won = manager.IsVictorious;
            }
            if (!manager.IsFinished) pulseClock += Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            if (!effects || !assault) return;
            effects.gameObject.SetActive(isActiveAndEnabled);
            bool active = assault.isActiveAndEnabled && manager.Farm.isActiveAndEnabled && assault.HasStarted;
            CaptureProgress = active ? assault.Progress : 0;
            float fade = manager.IsDefeated ? 1 - Mathf.Clamp01(ResultAge / 1.1f) : 1;
            float pulse = .88f + .12f * Mathf.Sin(pulseClock * 3);
            VisibleRunes = 0;
            for (int i = 0; i < RuneCount; i++)
            {
                float reveal = Mathf.Clamp01((CaptureProgress * RuneCount - i) / .8f);
                Color color = Color.Lerp(new Color(.48f, .24f, 1), new Color(.48f, 1, .12f), (float)i / RuneCount);
                color.a = reveal * fade * pulse;
                runes[i].startColor = runes[i].endColor = color;
                runes[i].enabled = active && color.a > .01f;
                if (runes[i].enabled) VisibleRunes++;
            }
            var sealColor = new Color(.45f, 1, .16f, CaptureProgress * fade * pulse);
            seal.startColor = seal.endColor = sealColor;
            seal.enabled = active && sealColor.a > .01f;
            BurstProgress = won && endedAt >= 0 ? Mathf.Clamp01(ResultAge / .9f) : 0;
            burst.enabled = won && endedAt >= 0 && BurstProgress < 1;
            if (burst.enabled)
            {
                Circle(burst, Mathf.Lerp(.7f, 7, Mathf.Sqrt(BurstProgress)));
                burst.startColor = burst.endColor = new Color(.65f, 1, .3f, (1 - BurstProgress) * .9f);
            }
            glow.enabled = active && (CaptureProgress > 0 || burst.enabled) && fade > 0;
            glow.intensity = (CaptureProgress * 2 * pulse + (burst.enabled ? 3 * (1 - BurstProgress) : 0)) * fade;
        }

        private void OnDisable()
        {
            if (effects) effects.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (effects) Destroy(effects.gameObject);
            if (material) Destroy(material);
        }
    }
}
