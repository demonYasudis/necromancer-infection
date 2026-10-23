using UnityEngine;

namespace NecromancerInfection
{
    [RequireComponent(typeof(Civilian))]
    [DisallowMultipleComponent]
    public sealed class AlarmRunner : MonoBehaviour
    {
        [SerializeField] private AlarmBell bell;
        [SerializeField] private GameObject warningMarker;
        [SerializeField, Min(0.1f)] private float detectionRadius = 4.5f;
        private Civilian civilian;
        public bool HasStarted { get; private set; }
        public bool IsStopped { get; private set; }
        public bool IsRunning => isActiveAndEnabled && HasStarted && !IsStopped && bell && !bell.IsRaised
            && civilian && civilian.IsAvailableTarget;
        public AlarmBell Bell => bell;

        private void OnEnable()
        {
            civilian = GetComponent<Civilian>();
            civilian.InfectionStarted += Stop;
            civilian.Combat.Died += OnDeath;
            if (warningMarker) warningMarker.SetActive(false);
        }

        private void OnDisable()
        {
            if (civilian)
            {
                civilian.InfectionStarted -= Stop;
                civilian.Combat.Died -= OnDeath;
            }
            if (warningMarker) warningMarker.SetActive(false);
        }

        private void OnDeath(UnitCombat attacker) => Stop();

        private void Stop()
        {
            IsStopped = true;
            if (warningMarker) warningMarker.SetActive(false);
            if (bell) bell.Prevent(this);
        }

        public bool Tick()
        {
            if (!isActiveAndEnabled || !bell || !bell.CanRun || IsStopped || !civilian.IsAvailableTarget || Time.timeScale <= 0) return false;
            if (!HasStarted)
            {
                if (!UndeadPresence.FindNearest(transform.position, detectionRadius)) return false;
                HasStarted = true;
                if (warningMarker) warningMarker.SetActive(true);
            }
            if (warningMarker) warningMarker.SetActive(true);
            if (bell.TryRaise(this))
            {
                if (warningMarker) warningMarker.SetActive(false);
                return false;
            }
            civilian.RunTowardsBell(bell.ArrivalPoint.position);
            return true;
        }
    }
}
