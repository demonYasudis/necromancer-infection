using System;
using System.Collections.Generic;
using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LevelObjective))]
    [DefaultExecutionOrder(30)]
    public sealed class SurvivalWaves : MonoBehaviour
    {
        [Serializable]
        private sealed class Sector
        {
            public string label;
            public Transform anchor;
        }

        [Serializable]
        private sealed class Wave
        {
            [Min(1)] public float arrivalSeconds;
            [Min(0)] public int knights;
            [Min(0)] public int archers;
            [Min(0)] public int sector;
            public int Size => knights + archers;
        }

        private const float LeaderClearance = 4;
        private const float SpawnRadius = .45f;
        [SerializeField] private Guard knightPrefab;
        [SerializeField] private Crossbowman archerPrefab;
        [SerializeField, Min(1)] private float warningSeconds = 8;
        [SerializeField, Min(1)] private int enemyLimit = 20;
        [SerializeField, Min(1)] private float corpseLifetime = 20;
        [SerializeField] private Sector[] sectors = Array.Empty<Sector>();
        [SerializeField] private Vector2[] spawnOffsets = Array.Empty<Vector2>();
        [SerializeField] private Wave[] waves = Array.Empty<Wave>();

        private readonly List<Vector3> reserved = new List<Vector3>();
        private GameManager manager;
        private LevelObjective objective;
        private Rect movementArea;
        private double nextAttempt;
        private double previousArrival = double.NegativeInfinity;
        private bool waitingForCapacity;

        public int WavesSpawned { get; private set; }
        public int WaveCount => waves.Length;
        public Transform Reinforcements { get; private set; }
        internal float CorpseLifetime => corpseLifetime;
        public int LivingEnemyCount
        {
            get
            {
                if (!Reinforcements) return 0;
                int count = 0;
                foreach (Transform unit in Reinforcements)
                    if (unit.TryGetComponent<UnitCombat>(out var combat) && combat.IsAlive) count++;
                return count;
            }
        }
        private Wave NextWave => WavesSpawned < WaveCount ? waves[WavesSpawned] : null;
        private bool IsRunning => isActiveAndEnabled && manager.isActiveAndEnabled && !manager.IsFinished && !manager.IsRestarting
            && objective.RemainingSeconds > 0;
        public double SecondsUntilWave => NextWave != null
            ? Math.Max(0, Math.Max(NextWave.arrivalSeconds, previousArrival + warningSeconds) - manager.ElapsedSeconds) : 0;
        public bool HasWarning => IsRunning && NextWave != null && SecondsUntilWave <= warningSeconds;
        internal Vector3 WarningPosition => sectors[NextWave.sector].anchor.position;
        internal string WarningText
        {
            get
            {
                Wave wave = NextWave;
                string timing = SecondsUntilWave > 0 ? $"{Math.Ceiling(SecondsUntilWave):0} с"
                    : waitingForCapacity ? $"лимит врагов {enemyLimit}" : "ждёт места";
                return $"ВОЛНА {WavesSpawned + 1}/{WaveCount} · {sectors[wave.sector].label} · {timing}";
            }
        }

        private void Awake()
        {
            manager = GetComponent<GameManager>();
            objective = GetComponent<LevelObjective>();
            if (manager.IsSurvival && knightPrefab && archerPrefab && ValidSchedule()) return;
            Debug.LogError("Survival waves require both prefabs, spawn sectors and an ordered schedule that fits the enemy limit.", this);
            enabled = false;
        }

        private void Start()
        {
            movementArea = GameManager.MovementArea(this, default);
            Reinforcements = new GameObject("SurvivalReinforcements").transform;
            Reinforcements.SetParent(transform, false);
        }

        private bool ValidSchedule()
        {
            if (waves.Length == 0 || sectors.Length == 0) return false;
            foreach (var sector in sectors)
                if (sector == null || !sector.anchor || string.IsNullOrWhiteSpace(sector.label)) return false;

            float previous = 0;
            foreach (var wave in waves)
            {
                if (wave == null || !(wave.arrivalSeconds > previous) || float.IsInfinity(wave.arrivalSeconds)
                    || wave.knights < 0 || wave.archers < 0 || wave.Size == 0 || wave.Size > spawnOffsets.Length
                    || wave.Size > enemyLimit
                    || wave.sector < 0 || wave.sector >= sectors.Length) return false;
                previous = wave.arrivalSeconds;
            }
            return true;
        }

        private void LateUpdate()
        {
            if (Time.timeScale <= 0 || !IsRunning || NextWave == null) return;
            double elapsed = manager.ElapsedSeconds;
            if (SecondsUntilWave > 0 || elapsed < nextAttempt) return;
            nextAttempt = elapsed + .25;

            var leader = manager.Necromancer.transform;
            Wave wave = NextWave;
            waitingForCapacity = LivingEnemyCount > enemyLimit - wave.Size;
            if (waitingForCapacity) return;
            Transform anchor = sectors[wave.sector].anchor;
            if (!ReservePositions(anchor, leader.position, wave.Size)) return;

            // Commit the whole wave only once every member has a safe position.
            for (int i = 0; i < reserved.Count; i++)
            {
                Vector3 position = reserved[i];
                Vector3 forward = leader.position - position;
                forward.y = 0;
                var rotation = Quaternion.LookRotation(forward);
                if (i < wave.knights) Instantiate(knightPrefab, position, rotation, Reinforcements);
                else Instantiate(archerPrefab, position, rotation, Reinforcements);
            }
            // A delayed wave still leaves time to warn the player about the next one.
            previousArrival = elapsed;
            WavesSpawned++;
        }

        private bool ReservePositions(Transform anchor, Vector3 leader, int count)
        {
            reserved.Clear();
            // Units and arena objects move by transform, so query their latest positions.
            Physics.SyncTransforms();
            foreach (Vector2 offset in spawnOffsets)
            {
                Vector3 position = anchor.TransformPoint(new Vector3(offset.x, 0, offset.y));
                Vector2 flat = new Vector2(position.x, position.z);
                Vector2 fromLeader = flat - new Vector2(leader.x, leader.z);
                if (!movementArea.Contains(flat) || fromLeader.sqrMagnitude < LeaderClearance * LeaderClearance) continue;

                var ray = new Ray(position + Vector3.up * 2, Vector3.down);
                if (!manager.Ground.Raycast(ray, out var groundHit, 4)) continue;
                position = groundHit.point;
                if (Physics.CheckCapsule(position + Vector3.up * .5f, position + Vector3.up * 1.45f,
                    SpawnRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;

                if (reserved.Exists(other => (other - position).sqrMagnitude < 4 * SpawnRadius * SpawnRadius)) continue;
                reserved.Add(position);
                if (reserved.Count == count) return true;
            }
            return false;
        }
    }
}
