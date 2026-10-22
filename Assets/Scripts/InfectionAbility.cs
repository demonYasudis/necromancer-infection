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

        public float InfectionDelay => infectionDelay;
        public float InfectionCost => infectionCost;
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
            if (!isActiveAndEnabled || Time.timeScale <= 0 || !levelCamera || !necromancer || !zombiePrefab
                || !Energy || !Energy.CanSpend(infectionCost)) return false;
            var hud = GetComponent<DarkEnergyHUD>();
            if (hud && hud.isActiveAndEnabled && hud.ContainsScreenPosition(screenPosition)) return false;
            var feedback = FindFirstObjectByType<GameFeedback>();
            if (feedback && feedback.isActiveAndEnabled && feedback.ContainsScreenPosition(screenPosition)) return false;
            Ray ray = levelCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, levelCamera.farClipPlane,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            var civilian = hit.collider.GetComponentInParent<Civilian>();
            if (!civilian || !civilian.TryInfect(zombiePrefab, necromancer, infectionDelay)) return false;
            // TryInfect changes the target synchronously; rejected clicks never spend energy.
            return Energy.TrySpend(infectionCost);
        }

        private void OnValidate()
        {
            infectionDelay = Mathf.Max(0.01f, infectionDelay);
            infectionCost = Mathf.Max(0, infectionCost);
        }
    }
}
