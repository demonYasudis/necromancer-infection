using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class AlarmBell : MonoBehaviour
    {
        [SerializeField] private GameManager manager;
        [SerializeField] private AlarmRunner runner;
        [SerializeField] private Transform arrivalPoint;
        [SerializeField] private Guard guardPrefab;
        [SerializeField] private Transform[] patrolPoints = new Transform[0];
        [SerializeField, Min(0.1f)] private float activationRadius = 0.75f;
        [SerializeField] private Renderer bellVisual;
        public bool IsRaised { get; private set; }
        public static event System.Action<AlarmBell> Rang;
        public bool IsPrevented { get; private set; }
        public Transform ArrivalPoint => arrivalPoint ? arrivalPoint : transform;
        public AlarmRunner Runner => runner;
        public Transform Patrol { get; private set; }
        public bool CanRun => isActiveAndEnabled && manager && !manager.IsFinished && !manager.IsRestarting
            && !IsRaised && !IsPrevented;

        public void Prevent(AlarmRunner messenger)
        {
            if (messenger != runner || IsRaised) return;
            IsPrevented = true;
            SetColor(new Color(0.4f, 0.85f, 0.3f));
        }

        public bool TryRaise(AlarmRunner messenger)
        {
            if (!CanRun || Time.timeScale <= 0 || !messenger || messenger != runner || !messenger.IsRunning
                || !guardPrefab || patrolPoints.Length == 0 || System.Array.Exists(patrolPoints, point => !point)) return false;
            Vector3 offset = messenger.transform.position - ArrivalPoint.position;
            offset.y = 0;
            if (offset.sqrMagnitude > activationRadius * activationRadius) return false;
            IsRaised = true;
            Rang?.Invoke(this);
            Patrol = new GameObject("AlarmPatrol").transform;
            Patrol.SetParent(transform, false);
            foreach (var point in patrolPoints)
            {
                var guard = Instantiate(guardPrefab, point.position, point.rotation, Patrol);
                guard.name = "AlarmGuard";
            }
            SetColor(new Color(1, 0.3f, 0.1f));
            return true;
        }

        private void SetColor(Color color)
        {
            if (!bellVisual) return;
            var tint = new MaterialPropertyBlock();
            bellVisual.GetPropertyBlock(tint);
            tint.SetColor("_Color", color);
            bellVisual.SetPropertyBlock(tint);
        }
    }
}
