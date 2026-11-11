using System.Collections.Generic;
using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LevelObjective))]
    [DefaultExecutionOrder(20)]
    public sealed class SurvivalPopulation : MonoBehaviour
    {
        [SerializeField] private Transform civilianGroup;
        [SerializeField] private Civilian civilianPrefab;
        [SerializeField, Min(1)] private float vacancyDelay = 25;
        [SerializeField, Min(1)] private float arrivalInterval = 12;
        [SerializeField, Min(1)] private float undeadLifetime = 120;

        private sealed class Home
        {
            public Civilian Resident;
            public Vector3 Position;
            public Quaternion Rotation;
            public double ReturnAt = double.PositiveInfinity;
        }

        private readonly List<Home> homes = new List<Home>();
        private readonly List<(Zombie unit, double expiresAt)> undead = new List<(Zombie, double)>();
        private GameManager manager;
        private double nextCheck;
        private double nextArrival;

        private void Awake()
        {
            manager = GetComponent<GameManager>();
            if (manager.IsSurvival && civilianGroup && civilianPrefab) return;
            Debug.LogError("Survival population requires a civilian group and prefab on a survival level.", this);
            enabled = false;
        }

        private void OnEnable() => Zombie.Raised += OnRaised;
        private void OnDisable() => Zombie.Raised -= OnRaised;

        private void Start()
        {
            foreach (var resident in civilianGroup.GetComponentsInChildren<Civilian>())
                homes.Add(new Home { Resident = resident, Position = resident.transform.position,
                    Rotation = resident.transform.rotation });
        }

        private void OnRaised(Zombie unit)
        {
            if (unit.Leader == manager.Necromancer && unit.gameObject.scene == gameObject.scene
                && !manager.IsFinished && !manager.IsRestarting)
                undead.Add((unit, manager.ElapsedSeconds + undeadLifetime));
        }

        private void LateUpdate()
        {
            if (Time.timeScale <= 0 || !manager.isActiveAndEnabled || manager.IsFinished || manager.IsRestarting
                || manager.Objective.RemainingSeconds <= 0) return;

            double now = manager.ElapsedSeconds;
            if (now < nextCheck) return;
            nextCheck = now + .25;

            for (int i = undead.Count - 1; i >= 0; i--)
            {
                var (unit, expiresAt) = undead[i];
                if (unit && unit.Combat.IsAlive && now < expiresAt) continue;
                if (unit && unit.Combat.IsAlive)
                {
                    unit.gameObject.SetActive(false);
                    Destroy(unit.gameObject);
                }
                undead.RemoveAt(i);
            }

            bool hasDueHome = false;
            foreach (var home in homes)
            {
                if (!home.Resident && double.IsPositiveInfinity(home.ReturnAt)) home.ReturnAt = now + vacancyDelay;
                hasDueHome |= now >= home.ReturnAt;
            }

            if (!hasDueHome || now < nextArrival) return;
            Physics.SyncTransforms();
            Home nextHome = null;
            foreach (var home in homes)
            {
                if (now < home.ReturnAt || (nextHome != null && home.ReturnAt >= nextHome.ReturnAt)
                    || !IsClear(home.Position)) continue;
                nextHome = home;
            }
            if (nextHome == null) return;

            nextHome.Resident = Instantiate(civilianPrefab, nextHome.Position, nextHome.Rotation, civilianGroup);
            nextHome.ReturnAt = double.PositiveInfinity;
            nextArrival = now + arrivalInterval;
        }

        private bool IsClear(Vector3 position)
        {
            Vector3 offset = manager.Necromancer.transform.position - position;
            offset.y = 0;
            return offset.sqrMagnitude >= 25 && !Physics.CheckCapsule(position + Vector3.up * .5f,
                position + Vector3.up * 1.45f, .45f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
