using UnityEngine;

namespace NecromancerInfection
{
    [RequireComponent(typeof(UnitCombat), typeof(LivingTarget))]
    [DisallowMultipleComponent]
    public sealed class Guard : MonoBehaviour
    {
        public enum BehaviourState { Idle, Pursuing, Attacking, Returning }
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.6f;
        [SerializeField, Min(0.1f)] private float detectionRadius = 6;
        [SerializeField, Min(0.1f)] private float necromancerPriorityRadius = 5;
        [SerializeField, Min(0.1f)] private float chaseRadius = 7;
        [SerializeField] private Rect movementArea = new Rect(-17, -10, 34, 20);
        [SerializeField] private BehaviourState state;
        [SerializeField] private UnitCombat currentTarget;
        private Vector3 home;
        private UnitCombat combat;
        private readonly RaycastHit[] obstacles = new RaycastHit[32];
        private static readonly float[] SteeringAngles = { 0, 30, -30, 60, -60, 90, -90, 120, -120 };

        public UnitCombat Combat => combat ? combat : combat = GetComponent<UnitCombat>();
        public UnitCombat CurrentTarget => currentTarget;
        public BehaviourState State => state;
        private void Start() => home = transform.position;

        private void Update()
        {
            if (!Combat.IsAlive) return;
            float fromHome = FlatDistance(transform.position, home);
            if (fromHome >= chaseRadius - 0.25f) state = BehaviourState.Returning;
            if (state == BehaviourState.Returning && fromHome > 0.5f)
            {
                currentTarget = null;
                MoveTowards(home, 0.2f);
                return;
            }

            currentTarget = UndeadPresence.FindCombatTarget(transform.position, detectionRadius,
                necromancerPriorityRadius, home, chaseRadius);
            if (!currentTarget)
            {
                state = fromHome > 0.2f ? BehaviourState.Returning : BehaviourState.Idle;
                MoveTowards(home, 0.2f);
                return;
            }
            MoveTowards(currentTarget.transform.position, Combat.AttackRange * 0.9f);
            state = Vector3.Distance(transform.position, currentTarget.transform.position) <= Combat.AttackRange
                ? BehaviourState.Attacking : BehaviourState.Pursuing;
            if (state == BehaviourState.Attacking) Combat.TryAttack(currentTarget);
        }

        private void MoveTowards(Vector3 destination, float stoppingDistance)
        {
            destination.y = transform.position.y;
            Vector3 offset = destination - transform.position;
            if (offset.magnitude <= stoppingDistance) return;
            Vector3 direction = offset.normalized;
            float step = Mathf.Min(moveSpeed * Time.deltaTime, offset.magnitude - stoppingDistance);
            if (step <= 0) return;
            foreach (float angle in SteeringAngles)
            {
                Vector3 steered = Quaternion.Euler(0, angle, 0) * direction;
                Vector3 nextPosition = transform.position + steered * step;
                if (!movementArea.Contains(new Vector2(nextPosition.x, nextPosition.z))
                    || FlatDistance(nextPosition, home) > chaseRadius) continue;
                int count = Physics.SphereCastNonAlloc(transform.position + Vector3.up * 0.8f,
                    0.3f, steered, obstacles, step + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                bool blocked = count == obstacles.Length;
                for (int i = 0; i < count; i++)
                {
                    var hit = obstacles[i].collider;
                    if (hit.transform.IsChildOf(transform) || hit.GetComponentInParent<LivingTarget>()
                        || hit.GetComponentInParent<UndeadPresence>()) continue;
                    blocked = true;
                }
                if (blocked) continue;
                transform.position = nextPosition;
                transform.rotation = Quaternion.LookRotation(steered);
                return;
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0;
            return Vector3.Distance(a, b);
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            detectionRadius = Mathf.Max(0.1f, detectionRadius);
            necromancerPriorityRadius = Mathf.Clamp(necromancerPriorityRadius, 0.1f, detectionRadius);
            chaseRadius = Mathf.Max(detectionRadius, chaseRadius);
        }
    }
}
