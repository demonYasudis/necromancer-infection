using System;
using UnityEngine;

namespace NecromancerInfection
{
    [RequireComponent(typeof(CaptureTarget))]
    [DisallowMultipleComponent]
    public sealed class FarmAssault : MonoBehaviour
    {
        [Serializable]
        public sealed class Wave
        {
            public string direction;
            [Min(0)] public float arrivalSeconds;
            public int[] pointIndices;

            public Wave(string direction, float arrivalSeconds, params int[] pointIndices)
            { this.direction = direction; this.arrivalSeconds = arrivalSeconds; this.pointIndices = pointIndices; }
        }

        [SerializeField] private GameManager manager;
        [SerializeField] private GameObject closedGate;
        [SerializeField] private Guard guardPrefab;
        [SerializeField] private Transform[] reinforcementPoints = new Transform[0];
        [SerializeField] private Wave[] waves = {
            new Wave("СЛЕВА", 3, 0, 4),
            new Wave("СПРАВА", 9, 2, 3, 7),
            new Wave("ПО ДОРОГЕ", 14, 1, 5, 6)
        };
        [SerializeField, Min(0.1f)] private float corruptionDuration = 20;
        [SerializeField] private float corruptionSeconds;
        [SerializeField] private bool isCaptured;
        private CaptureTarget target;
        private bool started;
        private float assaultSeconds;
        private int wavesSpawned;
        private Transform warnings;
        private Material warningMaterial;
        private GUIStyle warningStyle;

        public float CorruptionDuration => corruptionDuration;
        public float CorruptionSeconds => corruptionSeconds;
        public float Progress => Mathf.Clamp01(corruptionSeconds / corruptionDuration);
        public bool IsCaptured => isCaptured;
        public bool HasStarted => started;
        public int WavesSpawned => wavesSpawned;
        public int WaveCount => waves.Length;
        public float AssaultSeconds => assaultSeconds;
        public bool HasPendingWave => started && wavesSpawned < waves.Length && !isCaptured
            && manager && !manager.IsFinished && !manager.IsRestarting;
        public float NextWaveSeconds => HasPendingWave ? Mathf.Max(0, waves[wavesSpawned].arrivalSeconds - assaultSeconds) : 0;
        public string NextWaveDirection => HasPendingWave ? waves[wavesSpawned].direction : "";
        public int NextWaveSize => HasPendingWave ? waves[wavesSpawned].pointIndices.Length : 0;
        public Transform WarningMarkers => warnings;
        public GameObject ClosedGate => closedGate;
        public Transform Reinforcements { get; private set; }
        public event Action Captured;
        public event Action<int> WaveArrived;

        private void Awake()
        {
            target = GetComponent<CaptureTarget>();
            corruptionSeconds = 0;
            isCaptured = false;
            if (closedGate) closedGate.SetActive(false);
            if (manager && closedGate && guardPrefab && reinforcementPoints.Length > 0
                && !Array.Exists(reinforcementPoints, point => !point) && ValidWaves()) return;
            Debug.LogError("FarmAssault requires a manager, gate, guard prefab and reinforcement points.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (!target) target = GetComponent<CaptureTarget>();
            target.FinalPhaseStarted += Begin;
        }

        private void OnDisable()
        {
            if (target) target.FinalPhaseStarted -= Begin;
            if (warnings) warnings.gameObject.SetActive(false);
        }

        private void OnDestroy() { if (warningMaterial) Destroy(warningMaterial); }

        private bool ValidWaves()
        {
            if (waves == null || waves.Length == 0) return false;
            float previous = -1;
            var used = new System.Collections.Generic.HashSet<int>();
            foreach (var wave in waves)
            {
                if (wave == null || wave.arrivalSeconds < 0 || wave.arrivalSeconds <= previous
                    || wave.pointIndices == null || wave.pointIndices.Length == 0) return false;
                foreach (int index in wave.pointIndices)
                    if (index < 0 || index >= reinforcementPoints.Length || !used.Add(index)) return false;
                previous = wave.arrivalSeconds;
            }
            return true;
        }

        private void Begin()
        {
            if (started || !manager || manager.IsFinished || manager.IsRestarting) return;
            started = true;
            closedGate.SetActive(true);
            Reinforcements = new GameObject("FinalReinforcements").transform;
            Reinforcements.SetParent(transform, false);
            ShowWarning();
        }

        private void Update()
        {
            if (!started) return;
            if (warnings) warnings.gameObject.SetActive(isActiveAndEnabled && HasPendingWave && target.isActiveAndEnabled);
            if (isCaptured || !target.isActiveAndEnabled || !manager || manager.IsFinished
                || manager.IsRestarting || Time.timeScale <= 0) return;
            var leader = manager.Necromancer;
            if (!leader || !leader.isActiveAndEnabled || !leader.Combat.IsAlive) return;
            // The assault clock never resets on leaving the farm, unlike uninterrupted corruption.
            assaultSeconds += Time.deltaTime;
            // Capture takes precedence if a long frame crosses both the victory and a wave deadline.
            Vector3 offset = leader.transform.position - target.transform.position;
            offset.y = 0;
            if (offset.sqrMagnitude > target.ActivationRadius * target.ActivationRadius)
            {
                corruptionSeconds = 0;
            }
            else corruptionSeconds = Mathf.Min(corruptionDuration, corruptionSeconds + Time.deltaTime);
            if (corruptionSeconds >= corruptionDuration)
            {
                isCaptured = true;
                if (warnings) warnings.gameObject.SetActive(false);
                Captured?.Invoke();
                return;
            }
            while (HasPendingWave && assaultSeconds >= waves[wavesSpawned].arrivalSeconds)
            {
                foreach (int index in waves[wavesSpawned].pointIndices)
                {
                    var point = reinforcementPoints[index];
                    var guard = Instantiate(guardPrefab, point.position, point.rotation, Reinforcements);
                    guard.name = $"FinalGuard_Wave{wavesSpawned + 1}";
                }
                wavesSpawned++;
                WaveArrived?.Invoke(wavesSpawned);
                ShowWarning();
            }
        }

        private void ShowWarning()
        {
            if (warnings) { warnings.gameObject.SetActive(false); Destroy(warnings.gameObject); }
            warnings = null;
            if (!HasPendingWave) return;
            if (!warningMaterial) warningMaterial = new Material(Shader.Find("Sprites/Default"));
            warnings = new GameObject("NextWaveWarning").transform;
            warnings.SetParent(transform, false);
            foreach (int index in waves[wavesSpawned].pointIndices)
            {
                var ring = new GameObject("ArrivalMarker").AddComponent<LineRenderer>();
                ring.transform.SetParent(warnings, false);
                ring.transform.position = reinforcementPoints[index].position + Vector3.up * 0.08f;
                ring.useWorldSpace = false;
                ring.sharedMaterial = warningMaterial;
                ring.loop = true;
                ring.widthMultiplier = 0.1f;
                ring.startColor = ring.endColor = new Color(1, 0.55f, 0.12f);
                ring.positionCount = 32;
                ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ring.receiveShadows = false;
                for (int i = 0; i < 32; i++)
                {
                    float angle = i * Mathf.PI * 2 / 32;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.7f);
                }
            }
        }

        private void OnGUI()
        {
            if (!HasPendingWave || !target.isActiveAndEnabled || !manager.LevelCamera) return;
            if (warningStyle == null) warningStyle = new GUIStyle(GUI.skin.box) {
                fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1, 0.7f, 0.3f) }
            };
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            if (scale <= 0) return;
            var original = GUI.matrix;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                Vector3 center = Vector3.zero;
                foreach (int index in waves[wavesSpawned].pointIndices) center += reinforcementPoints[index].position;
                center /= NextWaveSize;
                Vector3 screen = manager.LevelCamera.WorldToScreenPoint(center + Vector3.up * 1.5f);
                float width = Screen.width / scale, height = Screen.height / scale;
                float x = screen.z > 0 ? Mathf.Clamp(screen.x / scale - 140, 8, width - 288) : width - 288;
                float y = screen.z > 0 ? Mathf.Clamp((Screen.height - screen.y) / scale - 50, 155, height - 110) : 160;
                GUI.Box(new Rect(x, y, 280, 64),
                    $"ВОЛНА {wavesSpawned + 1}/{WaveCount} · {NextWaveDirection}\n{NextWaveSize} стражника · через {NextWaveSeconds:0.0} сек", warningStyle);
            }
            finally { GUI.matrix = original; }
        }

        private void OnValidate() => corruptionDuration = Mathf.Max(0.1f, corruptionDuration);
    }
}
