using System;
using UnityEngine;

namespace NecromancerInfection
{
    [RequireComponent(typeof(CaptureTarget))]
    [DisallowMultipleComponent]
    public sealed class FarmAssault : MonoBehaviour
    {
        [SerializeField] private GameManager manager;
        [SerializeField] private GameObject closedGate;
        [SerializeField] private Guard guardPrefab;
        [SerializeField] private Transform[] reinforcementPoints = new Transform[0];
        [SerializeField, Min(0.1f)] private float corruptionDuration = 20;
        [SerializeField] private float corruptionSeconds;
        [SerializeField] private bool isCaptured;
        private CaptureTarget target;
        private bool started;

        public float CorruptionDuration => corruptionDuration;
        public float CorruptionSeconds => corruptionSeconds;
        public float Progress => Mathf.Clamp01(corruptionSeconds / corruptionDuration);
        public bool IsCaptured => isCaptured;
        public bool HasStarted => started;
        public GameObject ClosedGate => closedGate;
        public Transform Reinforcements { get; private set; }
        public event Action Captured;

        private void Awake()
        {
            target = GetComponent<CaptureTarget>();
            corruptionSeconds = 0;
            isCaptured = false;
            if (closedGate) closedGate.SetActive(false);
            if (manager && closedGate && guardPrefab && reinforcementPoints.Length > 0
                && !Array.Exists(reinforcementPoints, point => !point)) return;
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
        }

        private void Begin()
        {
            if (started || !manager || manager.IsDefeated) return;
            started = true;
            closedGate.SetActive(true);
            Reinforcements = new GameObject("FinalReinforcements").transform;
            Reinforcements.SetParent(transform, false);
            foreach (var point in reinforcementPoints)
            {
                var guard = Instantiate(guardPrefab, point.position, point.rotation, Reinforcements);
                guard.name = "FinalGuard";
            }
        }

        private void Update()
        {
            if (!started || isCaptured || !target.isActiveAndEnabled || !manager || manager.IsDefeated
                || manager.IsRestarting || Time.timeScale <= 0) return;
            var leader = manager.Necromancer;
            if (!leader || !leader.isActiveAndEnabled || !leader.Combat.IsAlive) return;
            Vector3 offset = leader.transform.position - target.transform.position;
            offset.y = 0;
            if (offset.sqrMagnitude > target.ActivationRadius * target.ActivationRadius)
            {
                corruptionSeconds = 0;
                return;
            }
            corruptionSeconds = Mathf.Min(corruptionDuration, corruptionSeconds + Time.deltaTime);
            if (corruptionSeconds < corruptionDuration) return;
            isCaptured = true;
            Captured?.Invoke();
        }

        private void OnValidate() => corruptionDuration = Mathf.Max(0.1f, corruptionDuration);
    }
}
