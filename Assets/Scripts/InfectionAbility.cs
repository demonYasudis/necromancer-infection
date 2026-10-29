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

        public float InfectionDelay => infectionDelay;
        public float InfectionCost => infectionCost;
        public float RaiseGuardCost => raiseGuardCost;
        public DarkEnergy Energy => necromancer ? necromancer.Energy : null;

        private void Awake()
        {
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
            if (Time.timeScale <= 0 || !TryGetTarget(screenPosition, out var target, out _, out _, out var reason) || reason != null) return false;
            if (target.TryGetComponent<GuardCorpse>(out var corpse)) return corpse.TryRaise(necromancer, raiseGuardCost);
            if (target.TryGetComponent<Grave>(out var grave)) return grave.TryRaise(necromancer);
            if (!target.GetComponent<Civilian>().TryInfect(zombiePrefab, necromancer, infectionDelay)) return false;
            return Energy.TrySpend(infectionCost);
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

        private void OnValidate()
        {
            infectionDelay = Mathf.Max(0.01f, infectionDelay);
            infectionCost = Mathf.Max(0, infectionCost);
            raiseGuardCost = Mathf.Max(0, raiseGuardCost);
        }
    }
}
