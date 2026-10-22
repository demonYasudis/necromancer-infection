using UnityEngine;

namespace NecromancerInfection
{
    [RequireComponent(typeof(UndeadPresence))]
    [RequireComponent(typeof(UnitCombat))]
    [DisallowMultipleComponent]
    public sealed class Zombie : MonoBehaviour
    {
        public enum BehaviourState { Idle, Following, PursuingTarget, Attacking, Returning }

        [SerializeField, Min(0.1f)] private float moveSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float detectionRadius = 4;
        [SerializeField, Min(0.1f)] private float followDistance = 1.5f;
        [SerializeField, Min(0.1f)] private float approachDistance = 0.9f;
        [SerializeField, Min(0.1f)] private float maxHordeDistance = 8;
        [SerializeField, Min(0.1f)] private float returnDistance = 3;
        [SerializeField] private Rect movementArea = new Rect(-17, -10, 34, 20);
        [SerializeField] private BehaviourState state;
        [SerializeField] private UnitCombat currentTarget;

        private readonly RaycastHit[] obstacles = new RaycastHit[32];
        private static readonly float[] SteeringAngles = { 0, 30, -30, 60, -60, 90, -90, 120, -120 };
        private UnitCombat combat;
        private bool spawnReported;

        public static event System.Action<Zombie> Raised;

        public Necromancer Leader { get; private set; }
        public UnitCombat CurrentTarget => currentTarget;
        public BehaviourState State => state;
        public float MaxHordeDistance => maxHordeDistance;
        public UnitCombat Combat => combat ? combat : combat = GetComponent<UnitCombat>();

        public void Initialize(Necromancer leader)
        {
            Leader = leader;
            currentTarget = null;
            state = BehaviourState.Following;
            if (!spawnReported && leader && Combat.IsAlive)
            {
                spawnReported = true;
                Raised?.Invoke(this);
            }
        }

        private void Update()
        {
            if (!Combat.IsAlive || !Leader || !Leader.Combat.IsAlive || !Leader.gameObject.activeInHierarchy)
            {
                currentTarget = null;
                state = BehaviourState.Idle;
                return;
            }

            float distanceFromLeader = FlatDistance(transform.position, Leader.transform.position);
            if (distanceFromLeader >= maxHordeDistance - 0.25f && state != BehaviourState.Returning)
            {
                state = BehaviourState.Returning;
            }
            if (state == BehaviourState.Returning && distanceFromLeader > returnDistance)
            {
                currentTarget = null;
                MoveTowards(Leader.transform.position, followDistance);
                return;
            }

            currentTarget = LivingTarget.FindNearest(transform.position, detectionRadius,
                Leader.transform.position, maxHordeDistance - 0.5f);
            if (currentTarget)
            {
                MoveTowards(currentTarget.transform.position, Mathf.Min(approachDistance, Combat.AttackRange * 0.9f));
                state = Vector3.Distance(transform.position, currentTarget.transform.position) <= Combat.AttackRange
                    ? BehaviourState.Attacking : BehaviourState.PursuingTarget;
                if (state == BehaviourState.Attacking) Combat.TryAttack(currentTarget);
            }
            else
            {
                state = BehaviourState.Following;
                MoveTowards(Leader.transform.position, followDistance);
            }
        }

        private void MoveTowards(Vector3 destination, float stoppingDistance)
        {
            destination.y = transform.position.y;
            Vector3 offset = destination - transform.position;
            float distance = offset.magnitude;
            if (distance <= stoppingDistance) return;
            Vector3 direction = offset / distance;
            float step = Mathf.Min(moveSpeed * Time.deltaTime, distance - stoppingDistance);
            if (step <= 0) return;
            float currentHordeDistance = FlatDistance(transform.position, Leader.transform.position);

            foreach (float angle in SteeringAngles)
            {
                Vector3 steered = Quaternion.Euler(0, angle, 0) * direction;
                Vector3 nextPosition = transform.position + steered * step;
                if (!movementArea.Contains(new Vector2(nextPosition.x, nextPosition.z))) continue;
                float nextHordeDistance = FlatDistance(nextPosition, Leader.transform.position);
                // A distant conversion walks home; a pursuing zombie cannot cross the leash.
                // Allow near-tangential detours outside the leash, even while the leader moves.
                // Inside the horde radius the boundary remains strict.
                float allowedRadius = currentHordeDistance > maxHordeDistance
                    ? currentHordeDistance + step * 0.1f : maxHordeDistance;
                if (nextHordeDistance > allowedRadius) continue;

                int count = Physics.SphereCastNonAlloc(transform.position + Vector3.up * 0.7f,
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
            followDistance = Mathf.Max(0.1f, followDistance);
            approachDistance = Mathf.Max(0.1f, approachDistance);
            returnDistance = Mathf.Max(followDistance + 0.5f, returnDistance);
            maxHordeDistance = Mathf.Max(returnDistance + 1, maxHordeDistance);
        }
    }
}
