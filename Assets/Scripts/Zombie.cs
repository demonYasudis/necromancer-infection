using System.Collections.Generic;
using UnityEngine;

namespace NecromancerInfection
{
    [RequireComponent(typeof(UndeadPresence))]
    [RequireComponent(typeof(UnitCombat))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public sealed class Zombie : MonoBehaviour
    {
        public enum BehaviourState { Idle, Following, PursuingTarget, Attacking, Returning }

        [SerializeField, Min(0.1f)] private float moveSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float detectionRadius = 4;
        [SerializeField, Min(0.1f)] private float followDistance = 1.5f;
        [SerializeField, Min(0.1f)] private float approachDistance = 0.9f;
        [SerializeField, Min(0.1f)] private float maxHordeDistance = 8;
        [SerializeField, Min(0.1f)] private float returnDistance = 3;
        [SerializeField, Min(0.1f)] private float crowdSpacing = 0.68f;
        [SerializeField] private Rect movementArea = new Rect(-17, -10, 34, 20);
        [SerializeField] private BehaviourState state;
        [SerializeField] private UnitCombat currentTarget;

        private readonly RaycastHit[] obstacles = new RaycastHit[64];
        private readonly HashSet<int> occupiedAttackSlots = new HashSet<int>();
        private static readonly List<Zombie> Active = new List<Zombie>();
        private static readonly float[] SteeringAngles = { 0, 30, -30, 60, -60, 90, -90, 120, -120, 150, -150, 180 };
        private UnitCombat combat;
        private bool spawnReported;
        private int attackSlot = -1;
        private UnitCombat slotTarget;
        private Vector3 velocity;
        private Vector3 detour;
        private bool hasDetour;
        private float nextPathCheck;
        private int detourSide = 1;
        internal int FormationSlot { get; set; } = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCrowd() => Active.Clear();

        private void OnEnable()
        {
            Active.Add(this);
            if (Leader) Leader.Horde.Join(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
            if (Leader) Leader.Horde.Leave(this);
            FormationSlot = attackSlot = -1;
            slotTarget = null;
            velocity = Vector3.zero;
            nextPathCheck = 0;
        }

        public static event System.Action<Zombie> Raised;

        public Necromancer Leader { get; private set; }
        public UnitCombat CurrentTarget => currentTarget;
        public BehaviourState State => state;
        public float MaxHordeDistance => maxHordeDistance;
        public UnitCombat Combat => combat ? combat : combat = GetComponent<UnitCombat>();

        public void Initialize(Necromancer leader)
        {
            movementArea = GameManager.MovementArea(this, movementArea);
            if (Leader != leader)
            {
                if (Leader) Leader.Horde.Leave(this);
                Leader = leader;
                if (Leader && isActiveAndEnabled) Leader.Horde.Join(this);
            }
            attackSlot = -1;
            slotTarget = null;
            currentTarget = null;
            velocity = Vector3.zero;
            nextPathCheck = 0;
            state = BehaviourState.Following;
            if (!spawnReported && leader && Combat.IsAlive)
            {
                spawnReported = true;
                Raised?.Invoke(this);
            }
        }

        private void Update()
        {
            if (Time.deltaTime <= 0) return;
            if (!Combat.IsAlive || !Leader || !Leader.Combat.IsAlive || !Leader.gameObject.activeInHierarchy)
            {
                currentTarget = null;
                slotTarget = null;
                attackSlot = -1;
                velocity = Vector3.zero;
                state = BehaviourState.Idle;
                return;
            }

            var horde = Leader.Horde;
            Vector3 leaderPosition = Leader.transform.position;
            Vector3 formationPosition = horde.Destination(this, followDistance, movementArea);
            float distanceFromLeader = FlatDistance(transform.position, leaderPosition);
            float formationDistance = FlatDistance(formationPosition, leaderPosition);
            float returnThreshold = Mathf.Clamp(formationDistance + .5f, returnDistance, maxHordeDistance - .75f);
            if (distanceFromLeader >= maxHordeDistance - .25f
                || (state == BehaviourState.Returning && distanceFromLeader > returnThreshold))
            {
                state = BehaviourState.Returning;
                currentTarget = null;
                slotTarget = null;
                attackSlot = -1;
                FollowFormation(horde, formationPosition, formationDistance);
                return;
            }

            currentTarget = LivingTarget.FindNearest(transform.position, detectionRadius, leaderPosition,
                maxHordeDistance - .5f, horde.Forward, currentTarget);
            if (currentTarget)
            {
                nextPathCheck = 0;
                if (slotTarget != currentTarget)
                {
                    slotTarget = currentTarget;
                    attackSlot = -1;
                }
                // Reserve distinct places around the enemy. Outer ranks take a vacant
                // inner place as soon as a front-line zombie dies or changes targets.
                attackSlot = ReserveAttackSlot();
                float radius = Mathf.Min(approachDistance, Combat.AttackRange * 0.9f);
                Vector3 destination = currentTarget.transform.position + AttackOffset(attackSlot, radius);
                MoveTowards(destination, .04f, maxHordeDistance);
                state = Vector3.Distance(transform.position, currentTarget.transform.position) <= Combat.AttackRange
                    ? BehaviourState.Attacking : BehaviourState.PursuingTarget;
                if (state == BehaviourState.Attacking) Combat.TryAttack(currentTarget);
            }
            else
            {
                state = BehaviourState.Following;
                slotTarget = null;
                attackSlot = -1;
                FollowFormation(horde, formationPosition, formationDistance);
            }
        }

        private void FollowFormation(HordeFormation horde, Vector3 position, float formationDistance)
        {
            if (Time.time >= nextPathCheck)
            {
                hasDetour = horde.TryDetour(transform.position, position, obstacles, out detour);
                nextPathCheck = Time.time + .18f;
            }
            MoveTowards(hasDetour ? detour : position, .12f, Mathf.Max(maxHordeDistance, formationDistance + .3f));
        }

        private int ReserveAttackSlot()
        {
            occupiedAttackSlots.Clear();
            foreach (var other in Active)
                if (other != this && other.slotTarget == currentTarget && other.Leader == Leader
                    && other.attackSlot >= 0 && other.Combat.IsAlive)
                    occupiedAttackSlots.Add(other.attackSlot);

            int limit = attackSlot >= 0 ? attackSlot + 1 : Active.Count + 1;
            int best = attackSlot;
            float bestDistance = float.PositiveInfinity;
            float radius = Mathf.Min(approachDistance, Combat.AttackRange * .9f);
            Vector3 center = currentTarget.transform.position;
            for (int slot = 0; slot < limit; slot++)
            {
                if (occupiedAttackSlots.Contains(slot)) continue;
                // Keep a reservation stable, except that waiting ranks may advance.
                if (attackSlot >= 0) return slot;
                Vector3 offset = AttackOffset(slot, radius);
                float distance = (center + offset - transform.position).sqrMagnitude + offset.sqrMagnitude * 2;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = slot;
            }
            return best < 0 ? 0 : best;
        }

        private Vector3 AttackOffset(int slot, float radius)
        {
            int ring = 0;
            int capacity;
            while (true)
            {
                // Chord spacing, rather than arc length, keeps adjacent bodies apart.
                capacity = radius > crowdSpacing * 0.5f
                    ? Mathf.Max(1, Mathf.FloorToInt(Mathf.PI / Mathf.Asin(crowdSpacing / (2 * radius)))) : 1;
                if (slot < capacity) break;
                slot -= capacity;
                radius += crowdSpacing;
                ring++;
            }
            float angle = slot * Mathf.PI * 2 / capacity + ring * 0.37f;
            return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
        }

        private void MoveTowards(Vector3 destination, float stoppingDistance, float leash)
        {
            if (Combat.IsMeleeContact)
            {
                velocity = Vector3.zero;
                return;
            }
            destination.y = transform.position.y;
            Vector3 offset = destination - transform.position;
            float distance = offset.magnitude;
            Vector3 separation = Vector3.zero;
            foreach (var other in Active)
            {
                if (other == this || !other || !other.Combat.IsAlive || other.gameObject.scene != gameObject.scene) continue;
                Vector3 away = transform.position - other.transform.position;
                away.y = 0;
                float gap = away.magnitude;
                // Contact correction ends well inside the gap between reserved places.
                float spacing = Mathf.Max(crowdSpacing, other.crowdSpacing) * .85f;
                if (gap >= spacing) continue;
                if (gap < 0.001f)
                {
                    // Deterministic opposite directions also separate exact-overlap spawns.
                    away = GetInstanceID() < other.GetInstanceID() ? Vector3.left : Vector3.right;
                    gap = 0;
                }
                else away /= gap;
                separation += away * (spacing - gap);
            }
            float approachSpeed = Mathf.Min(moveSpeed, Mathf.Max(0, distance - stoppingDistance) * 6);
            Vector3 desired = offset.normalized * approachSpeed + separation * 5;
            if (approachSpeed == 0 && separation.sqrMagnitude < .0001f)
            {
                velocity = Vector3.zero;
                return;
            }
            velocity = Vector3.MoveTowards(velocity, Vector3.ClampMagnitude(desired, moveSpeed), 16 * Time.deltaTime);
            if (velocity.sqrMagnitude < .0004f) return;
            Vector3 direction = velocity.normalized;
            float step = Time.deltaTime * velocity.magnitude;
            if (separation.sqrMagnitude < .0001f) step = Mathf.Min(step, Mathf.Max(0, distance - stoppingDistance));
            if (step <= 0) return;
            Vector3 leaderPosition = Leader.transform.position;
            float currentHordeDistance = FlatDistance(transform.position, leaderPosition);
            float clearance = Mathf.Min(.85f, currentHordeDistance);
            float allowedRadius = currentHordeDistance > leash ? currentHordeDistance + step * .1f : leash;

            foreach (float angle in SteeringAngles)
            {
                float turn = angle * detourSide;
                Vector3 steered = Quaternion.Euler(0, turn, 0) * direction;
                Vector3 nextPosition = transform.position + steered * step;
                if (!IsInsideArea(nextPosition)) continue;
                float nextHordeDistance = FlatDistance(nextPosition, leaderPosition);
                if (nextHordeDistance < clearance || nextHordeDistance > allowedRadius) continue;

                float lookAhead = step + .1f;
                if (ObstacleClearance(transform.position, steered, lookAhead, obstacles) < lookAhead) continue;
                if (turn != 0 && angle != 180) detourSide = turn > 0 ? 1 : -1;
                transform.position = nextPosition;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(steered),
                    720 * Time.deltaTime);
                return;
            }
        }

        private bool IsInsideArea(Vector3 point) => movementArea.Contains(new Vector2(point.x, point.z));

        internal static float ObstacleClearance(Vector3 origin, Vector3 direction, float distance, RaycastHit[] hits)
        {
            if (distance <= 0) return 0;
            int count = Physics.SphereCastNonAlloc(origin + Vector3.up * 0.7f,
                .3f, direction.normalized, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return 0;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.GetComponentInParent<LivingTarget>() || hit.collider.GetComponentInParent<UndeadPresence>()) continue;
                distance = Mathf.Min(distance, hit.distance);
            }
            return distance;
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
            crowdSpacing = Mathf.Max(0.1f, crowdSpacing);
            returnDistance = Mathf.Max(followDistance + 0.5f, returnDistance);
            maxHordeDistance = Mathf.Max(returnDistance + 1, maxHordeDistance);
        }
    }
}
