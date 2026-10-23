using System.Collections.Generic;
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
        [SerializeField, Min(0.1f)] private float crowdSpacing = 0.68f;
        [SerializeField] private Rect movementArea = new Rect(-17, -10, 34, 20);
        [SerializeField] private BehaviourState state;
        [SerializeField] private UnitCombat currentTarget;

        private readonly RaycastHit[] obstacles = new RaycastHit[32];
        private static readonly List<Zombie> Active = new List<Zombie>();
        private static readonly float[] SteeringAngles = { 0, 30, -30, 60, -60, 90, -90, 120, -120, 150, -150, 180 };
        private UnitCombat combat;
        private bool spawnReported;
        private int followSlot = -1;
        private int attackSlot = -1;
        private UnitCombat slotTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCrowd() => Active.Clear();

        private void OnEnable() => Active.Add(this);

        private void OnDisable()
        {
            Active.Remove(this);
            followSlot = attackSlot = -1;
            slotTarget = null;
        }

        public static event System.Action<Zombie> Raised;

        public Necromancer Leader { get; private set; }
        public UnitCombat CurrentTarget => currentTarget;
        public BehaviourState State => state;
        public float MaxHordeDistance => maxHordeDistance;
        public UnitCombat Combat => combat ? combat : combat = GetComponent<UnitCombat>();

        public void Initialize(Necromancer leader)
        {
            Leader = leader;
            followSlot = attackSlot = -1;
            slotTarget = null;
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
            if (Time.deltaTime <= 0) return;
            if (!Combat.IsAlive || !Leader || !Leader.Combat.IsAlive || !Leader.gameObject.activeInHierarchy)
            {
                currentTarget = null;
                slotTarget = null;
                attackSlot = -1;
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
                slotTarget = null;
                attackSlot = -1;
                FollowLeader();
                return;
            }

            currentTarget = LivingTarget.FindNearest(transform.position, detectionRadius,
                Leader.transform.position, maxHordeDistance - 0.5f);
            if (currentTarget)
            {
                if (slotTarget != currentTarget)
                {
                    slotTarget = currentTarget;
                    attackSlot = -1;
                }
                // Reserve distinct places around the enemy. Outer ranks take a vacant
                // inner place as soon as a front-line zombie dies or changes targets.
                attackSlot = ReserveSlot(true, attackSlot);
                float radius = Mathf.Min(approachDistance, Combat.AttackRange * 0.9f);
                Vector3 destination = currentTarget.transform.position + SlotOffset(attackSlot, radius);
                MoveTowards(destination, 0.04f);
                state = Vector3.Distance(transform.position, currentTarget.transform.position) <= Combat.AttackRange
                    ? BehaviourState.Attacking : BehaviourState.PursuingTarget;
                if (state == BehaviourState.Attacking) Combat.TryAttack(currentTarget);
            }
            else
            {
                state = BehaviourState.Following;
                slotTarget = null;
                attackSlot = -1;
                FollowLeader();
            }
        }

        private void FollowLeader()
        {
            if (followSlot < 0) followSlot = ReserveSlot(false, -1);
            Vector3 destination = Leader.transform.position + SlotOffset(followSlot, followDistance);
            // In a doorway the formation compresses towards the leader, then opens
            // back out after the walls no longer obstruct its assigned position.
            Vector3 offset = destination - transform.position;
            offset.y = 0;
            if (!IsInsideArea(destination) || IsBlocked(transform.position, offset.normalized, offset.magnitude))
                destination = Leader.transform.position;
            MoveTowards(destination, 0.06f);
        }

        private int ReserveSlot(bool attacking, int existing)
        {
            int limit = existing >= 0 ? existing + 1 : Active.Count + 1;
            int best = existing;
            float bestDistance = float.PositiveInfinity;
            float radius = attacking ? Mathf.Min(approachDistance, Combat.AttackRange * 0.9f) : followDistance;
            Vector3 center = attacking ? currentTarget.transform.position : Leader.transform.position;
            for (int slot = 0; slot < limit; slot++)
            {
                bool occupied = false;
                foreach (var other in Active)
                {
                    if (other == this || !other || !other.Combat.IsAlive || other.Leader != Leader) continue;
                    if (attacking ? other.slotTarget == currentTarget && other.attackSlot == slot : other.followSlot == slot)
                    {
                        occupied = true;
                        break;
                    }
                }
                if (occupied) continue;
                // Keep a reservation stable, except that waiting ranks may advance.
                if (existing >= 0) return slot;
                Vector3 offset = SlotOffset(slot, radius);
                float distance = (center + offset - transform.position).sqrMagnitude + offset.sqrMagnitude * 2;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = slot;
            }
            return best < 0 ? 0 : best;
        }

        private Vector3 SlotOffset(int slot, float radius)
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

        private void MoveTowards(Vector3 destination, float stoppingDistance)
        {
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
                float spacing = Mathf.Max(crowdSpacing, other.crowdSpacing);
                if (gap >= spacing) continue;
                if (gap < 0.001f)
                {
                    // Deterministic opposite directions also separate exact-overlap spawns.
                    away = GetInstanceID() < other.GetInstanceID() ? Vector3.left : Vector3.right;
                    gap = 0;
                }
                else away /= gap;
                separation += away * (1 - gap / spacing);
            }
            Vector3 desired = distance > stoppingDistance ? offset / distance : Vector3.zero;
            Vector3 velocity = Vector3.ClampMagnitude(desired + separation * 6, 1);
            if (velocity.sqrMagnitude < 0.0001f) return;
            Vector3 direction = velocity.normalized;
            float step = moveSpeed * Time.deltaTime * velocity.magnitude;
            if (separation.sqrMagnitude < 0.0001f) step = Mathf.Min(step, Mathf.Max(0, distance - stoppingDistance));
            if (step <= 0) return;
            float currentHordeDistance = FlatDistance(transform.position, Leader.transform.position);

            foreach (float angle in SteeringAngles)
            {
                Vector3 steered = Quaternion.Euler(0, angle, 0) * direction;
                Vector3 nextPosition = transform.position + steered * step;
                if (!IsInsideArea(nextPosition)) continue;
                float nextHordeDistance = FlatDistance(nextPosition, Leader.transform.position);
                // A distant conversion walks home; a pursuing zombie cannot cross the leash.
                // Allow near-tangential detours outside the leash, even while the leader moves.
                // Inside the horde radius the boundary remains strict.
                float allowedRadius = currentHordeDistance > maxHordeDistance
                    ? currentHordeDistance + step * 0.1f : maxHordeDistance;
                if (nextHordeDistance > allowedRadius) continue;

                if (IsBlocked(transform.position, steered, step + 0.1f)) continue;
                transform.position = nextPosition;
                transform.rotation = Quaternion.LookRotation(steered);
                return;
            }
        }

        private bool IsInsideArea(Vector3 point) => movementArea.Contains(new Vector2(point.x, point.z));

        private bool IsBlocked(Vector3 origin, Vector3 direction, float distance)
        {
            if (distance <= 0) return false;
            int count = Physics.SphereCastNonAlloc(origin + Vector3.up * 0.7f,
                0.3f, direction, obstacles, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == obstacles.Length) return true;
            for (int i = 0; i < count; i++)
            {
                var hit = obstacles[i].collider;
                if (hit.transform.IsChildOf(transform) || hit.GetComponentInParent<LivingTarget>()
                    || hit.GetComponentInParent<UndeadPresence>()) continue;
                return true;
            }
            return false;
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
