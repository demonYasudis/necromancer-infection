using System.Collections;
using UnityEngine;

namespace NecromancerInfection
{
    [RequireComponent(typeof(UnitCombat))]
    [RequireComponent(typeof(LivingTarget))]
    [DisallowMultipleComponent]
    public sealed class Civilian : MonoBehaviour
    {
        public enum BehaviourState { Wandering, Fleeing, Infected, RunningToBell }

        [SerializeField, Min(0.1f)] private float walkSpeed = 0.8f;
        [SerializeField, Min(0.1f)] private float runSpeed = 2.8f;
        [SerializeField, Min(0.1f)] private float wanderRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float detectionRadius = 4.5f;
        [SerializeField, Min(0.1f)] private float safeRadius = 6.5f;
        [SerializeField] private Rect movementArea = new Rect(-17, -10, 34, 20);
        [SerializeField] private BehaviourState state;
        [SerializeField] private Zombie conversionPrefab;
        private UnitCombat combat;

        private readonly RaycastHit[] obstacles = new RaycastHit[32];
        private static readonly float[] SteeringAngles = { 0, 35, -35, 70, -70, 110, -110 };
        private Vector3 home;
        private Vector3 wanderTarget;
        private float nextWanderChoice;
        private Renderer[] infectedRenderers;
        private MaterialPropertyBlock[] originalAppearance;
        private MagicMotes infectionEffect;
        public event System.Action InfectionStarted;

        public BehaviourState State => state;
        public UnitCombat Combat => combat ? combat : combat = GetComponent<UnitCombat>();
        public bool IsAvailableTarget => isActiveAndEnabled && Combat.IsAlive && state != BehaviourState.Infected;

        private void OnEnable()
        {
            Combat.Died += OnDeath;
        }

        private void OnDisable()
        {
            Combat.Died -= OnDeath;
            StopAllCoroutines();
            RestoreAppearance();
            state = BehaviourState.Wandering;
        }

        public bool TryInfect(Zombie zombiePrefab, Necromancer leader, float delay)
        {
            if (!IsAvailableTarget || !zombiePrefab || !leader || !leader.Combat.IsAlive || delay <= 0) return false;
            state = BehaviourState.Infected;
            GetComponent<CombatMotion>().BeginInfection(delay);
            InfectionStarted?.Invoke();
            infectionEffect = MagicMotes.Infection(transform);
            infectedRenderers = GetComponentsInChildren<Renderer>();
            originalAppearance = new MaterialPropertyBlock[infectedRenderers.Length];
            for (int i = 0; i < infectedRenderers.Length; i++)
            {
                originalAppearance[i] = new MaterialPropertyBlock();
                infectedRenderers[i].GetPropertyBlock(originalAppearance[i]);
                var tint = new MaterialPropertyBlock();
                infectedRenderers[i].GetPropertyBlock(tint);
                tint.SetColor("_Color", new Color(0.35f, 1, 0.18f));
                infectedRenderers[i].SetPropertyBlock(tint);
            }
            StartCoroutine(TransformIntoZombie(zombiePrefab, leader, delay));
            return true;
        }

        private IEnumerator TransformIntoZombie(Zombie zombiePrefab, Necromancer leader, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!Combat.IsAlive) yield break;
            if (!leader || !leader.Combat.IsAlive || !zombiePrefab)
            {
                RestoreAppearance();
                state = BehaviourState.Wandering;
                yield break;
            }
            SpawnZombie(zombiePrefab, leader);
            Destroy(gameObject);
        }

        private void OnDeath(UnitCombat attacker)
        {
            StopAllCoroutines();
            if (!attacker || !attacker.TryGetComponent<Zombie>(out var killer) || !killer.Leader
                || !killer.Leader.Combat.IsAlive) return;
            if (!conversionPrefab)
            {
                Debug.LogError("Civilian requires a Zombie conversion prefab for chain infection.", this);
                return;
            }
            SpawnZombie(conversionPrefab, killer.Leader);
        }

        private void SpawnZombie(Zombie prefab, Necromancer leader)
        {
            var zombie = Instantiate(prefab, transform.position, transform.rotation);
            zombie.name = "Zombie";
            zombie.GetComponent<CombatMotion>().BeginRising();
            zombie.Initialize(leader);
        }

        private void RestoreAppearance()
        {
            if (TryGetComponent<CombatMotion>(out var motion)) motion.CancelTransformation();
            if (infectionEffect) Destroy(infectionEffect.gameObject);
            if (infectedRenderers == null) return;
            for (int i = 0; i < infectedRenderers.Length; i++)
                if (infectedRenderers[i]) infectedRenderers[i].SetPropertyBlock(originalAppearance[i]);
            infectedRenderers = null;
            originalAppearance = null;
        }

        private void Start()
        {
            movementArea = JourneyRoute.MovementArea(this, movementArea);
            home = transform.position;
            ChooseWanderTarget();
        }

        private void Update()
        {
            if (!Combat.IsAlive || state == BehaviourState.Infected) return;
            if (TryGetComponent<AlarmRunner>(out var runner) && runner.isActiveAndEnabled && runner.Tick())
            {
                state = BehaviourState.RunningToBell;
                return;
            }
            // A wider exit radius prevents rapid switching at the edge of detection.
            Transform threat = UndeadPresence.FindNearest(transform.position,
                state == BehaviourState.Fleeing ? safeRadius : detectionRadius);
            state = threat != null ? BehaviourState.Fleeing : BehaviourState.Wandering;

            if (state == BehaviourState.Fleeing)
            {
                Vector3 away = transform.position - threat.position;
                away.y = 0;
                if (away.sqrMagnitude < 0.001f) away = transform.forward;
                Move(away.normalized, runSpeed);
                // Resume wandering around the safe location after escaping.
                home = transform.position;
                nextWanderChoice = 0;
            }
            else
            {
                if (Time.time >= nextWanderChoice || (wanderTarget - transform.position).sqrMagnitude < 0.04f)
                    ChooseWanderTarget();
                Vector3 direction = wanderTarget - transform.position;
                direction.y = 0;
                if (!Move(direction.normalized, walkSpeed, direction.magnitude))
                    nextWanderChoice = 0;
            }
        }

        public void RunTowardsBell(Vector3 destination)
        {
            if (!IsAvailableTarget || Time.timeScale <= 0) return;
            Vector3 offset = destination - transform.position;
            offset.y = 0;
            Move(offset.normalized, runSpeed, offset.magnitude);
        }

        private void ChooseWanderTarget()
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            wanderTarget = new Vector3(
                Mathf.Clamp(home.x + offset.x, movementArea.xMin, movementArea.xMax), home.y,
                Mathf.Clamp(home.z + offset.y, movementArea.yMin, movementArea.yMax));
            nextWanderChoice = Time.time + Random.Range(2f, 5f);
        }

        private bool Move(Vector3 direction, float speed, float maxDistance = float.PositiveInfinity)
        {
            float distance = Mathf.Min(speed * Time.deltaTime, maxDistance);
            if (distance <= 0 || direction.sqrMagnitude < 0.001f) return false;
            foreach (float angle in SteeringAngles)
            {
                Vector3 steered = Quaternion.Euler(0, angle, 0) * direction;
                Vector3 destination = transform.position + steered * distance;
                if (!movementArea.Contains(new Vector2(destination.x, destination.z))) continue;
                int count = Physics.SphereCastNonAlloc(transform.position + Vector3.up * 0.8f,
                    0.3f, steered, obstacles, distance + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                bool blocked = count == obstacles.Length;
                for (int i = 0; i < count; i++)
                    if (!obstacles[i].transform.IsChildOf(transform)) blocked = true;
                if (blocked) continue;
                transform.position = destination;
                transform.rotation = Quaternion.LookRotation(steered);
                return true;
            }
            return false;
        }

        private void OnValidate()
        {
            walkSpeed = Mathf.Max(0.1f, walkSpeed);
            runSpeed = Mathf.Max(walkSpeed, runSpeed);
            wanderRadius = Mathf.Max(0.1f, wanderRadius);
            detectionRadius = Mathf.Max(0.1f, detectionRadius);
            safeRadius = Mathf.Max(detectionRadius + 0.1f, safeRadius);
        }
    }
}
