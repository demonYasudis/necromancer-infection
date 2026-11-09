using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class InfectionAbility : MonoBehaviour
    {
        [SerializeField] private Camera levelCamera;
        [SerializeField] private Necromancer necromancer;
        [SerializeField] private Zombie zombiePrefab;
        [SerializeField, Min(0.01f)] private float infectionDelay = 1.2f;
        [SerializeField, Min(0)] private float infectionCost = 15;
        [SerializeField, Min(0)] private float raiseGuardCost = 35;
        [SerializeField, Min(0)] private float soldierInfectionCost = 35;
        [SerializeField, Min(0)] private float archerInfectionCost = 30;
        private LivingTarget[] pointerCandidates = new LivingTarget[0];
        private float nextPointerRefresh;
        private GameManager manager;

        public float InfectionDelay => infectionDelay;
        public float InfectionCost => infectionCost;
        public event System.Action InfectedByPlayer;
        public float SoldierInfectionCost => soldierInfectionCost;
        public float ArcherInfectionCost => archerInfectionCost;
        public void ReduceCost()
        {
            infectionCost *= .8f;
            soldierInfectionCost *= .8f;
            archerInfectionCost *= .8f;
        }
        public float RaiseGuardCost => raiseGuardCost;
        public DarkEnergy Energy => necromancer ? necromancer.Energy : null;

        private void Awake()
        {
            manager = FindFirstObjectByType<GameManager>();
            if (!levelCamera || !necromancer || !zombiePrefab || !Energy)
            {
                Debug.LogError("InfectionAbility requires a camera, necromancer with DarkEnergy and zombie prefab.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0 || !Input.GetMouseButtonDown(0)) return;
            TryInfectAtScreenPosition(Input.mousePosition);
        }

        public bool TryInfectAtScreenPosition(Vector2 screenPosition)
        {
            if (Time.timeScale <= 0 || !TryGetTarget(screenPosition, out var target, out _, out var cost, out var reason) || reason != null) return false;
            if (target.TryGetComponent<GuardCorpse>(out var corpse)) return corpse.TryRaise(necromancer, raiseGuardCost);
            if (target.TryGetComponent<Grave>(out var grave)) return grave.TryRaise(necromancer);
            bool isDefender = target.TryGetComponent<DefenderInfection>(out var defender);
            bool infected = isDefender ? defender.TryClick(zombiePrefab, necromancer, infectionDelay)
                : target.GetComponent<Civilian>().TryInfect(zombiePrefab, necromancer, infectionDelay);
            if (!infected) return false;
            if (isDefender && !defender.IsInfected) return true;
            bool paid = Energy.TrySpend(cost);
            if (paid) InfectedByPlayer?.Invoke();
            return paid;
        }

        public bool TryGetTarget(Vector2 screenPosition, out Transform target, out string action, out float cost, out string reason)
        {
            target = null;
            action = reason = null;
            cost = 0;
            if (!isActiveAndEnabled || !levelCamera || !necromancer || !necromancer.Combat.IsAlive
                || !zombiePrefab || !Energy || AbilityPointer.IsBlocked(screenPosition)) return false;
            Ray ray = levelCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, levelCamera.farClipPlane,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return false;
            var corpse = hit.collider.GetComponentInParent<GuardCorpse>();
            var grave = hit.collider.GetComponentInParent<Grave>();
            var civilian = hit.collider.GetComponentInParent<Civilian>();
            var defender = hit.collider.GetComponentInParent<DefenderInfection>();
            // Exact hits retain priority. Near misses on the ground get a forgiving screen-space target.
            if (!corpse && !grave && !civilian && !defender && manager && hit.collider == manager.Ground)
            {
                var nearby = FindNearbyLiving(screenPosition);
                civilian = nearby ? nearby.GetComponent<Civilian>() : null;
                defender = nearby ? nearby.GetComponent<DefenderInfection>() : null;
            }
            if (corpse)
            {
                target = corpse.transform;
                action = "Поднять стражника";
                cost = raiseGuardCost;
                reason = corpse.UnavailableReason(necromancer);
            }
            else if (grave)
            {
                target = grave.transform;
                action = $"Поднять скелетов: {grave.SkeletonCount}";
                cost = grave.Cost;
                reason = grave.UnavailableReason(necromancer);
            }
            else if (defender)
            {
                target = defender.transform;
                bool archer = defender.GetComponent<Crossbowman>();
                action = (archer ? "Заразить лучника" : "Заразить рыцаря")
                    + $" · {defender.AcceptedClicks}/{defender.RequiredClicks}";
                cost = defender.IsFinalClick ? (archer ? archerInfectionCost : soldierInfectionCost) : 0;
                if (!defender.IsAvailableTarget) reason = "Цель уже заражена или недоступна";
            }
            else if (civilian)
            {
                target = civilian.transform;
                action = "Заразить жителя";
                cost = infectionCost;
                if (!civilian.IsAvailableTarget) reason = "Цель уже заражена или недоступна";
            }
            else return false;
            if (reason == null && !Energy.CanSpend(cost)) reason = "Недостаточно энергии";
            return true;
        }

        private LivingTarget FindNearbyLiving(Vector2 pointer)
        {
            if (Time.unscaledTime >= nextPointerRefresh)
            {
                pointerCandidates = FindObjectsByType<LivingTarget>(FindObjectsSortMode.None);
                nextPointerRefresh = Time.unscaledTime + .5f;
            }
            float radius = Mathf.Clamp(Screen.height / 24f, 22, 40);
            float best = radius * radius;
            LivingTarget selected = null;
            foreach (var candidate in pointerCandidates)
            {
                if (!candidate || !candidate.isActiveAndEnabled || !IsAvailable(candidate)
                    || candidate.gameObject.scene != gameObject.scene) continue;
                var collider = candidate.GetComponent<Collider>();
                if (!collider || !collider.enabled) continue;
                Vector3 center = collider.bounds.center;
                Vector3 screen = levelCamera.WorldToScreenPoint(center);
                if (screen.z < levelCamera.nearClipPlane || screen.z > levelCamera.farClipPlane
                    || screen.x < 0 || screen.x >= Screen.width || screen.y < 0 || screen.y >= Screen.height) continue;
                float distance = ((Vector2)screen - pointer).sqrMagnitude;
                if (distance >= best) continue;
                Ray sight = levelCamera.ScreenPointToRay(screen);
                if (!Physics.Raycast(sight, out var visible, levelCamera.farClipPlane,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                    || visible.collider.GetComponentInParent<LivingTarget>() != candidate) continue;
                best = distance;
                selected = candidate;
            }
            return selected;
        }

        private static bool IsAvailable(LivingTarget target)
        {
            if (target.TryGetComponent<Civilian>(out var civilian)) return civilian.IsAvailableTarget;
            return target.TryGetComponent<DefenderInfection>(out var defender) && defender.IsAvailableTarget;
        }

        private void OnValidate()
        {
            infectionDelay = Mathf.Max(0.01f, infectionDelay);
            infectionCost = Mathf.Max(0, infectionCost);
            raiseGuardCost = Mathf.Max(0, raiseGuardCost);
            soldierInfectionCost = Mathf.Max(infectionCost, soldierInfectionCost);
            archerInfectionCost = Mathf.Max(infectionCost, archerInfectionCost);
        }
    }
}
