using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class ProtectionAbility : MonoBehaviour
    {
        [SerializeField] private Camera levelCamera;
        [SerializeField] private Collider ground;
        [SerializeField] private Necromancer necromancer;
        [SerializeField] private ProtectionArea areaPrefab;
        [SerializeField, Min(0)] private float energyCost = 25;

        public float EnergyCost => energyCost;
        public ProtectionArea AreaPrefab => areaPrefab;
        private DarkEnergy Energy => necromancer ? necromancer.Energy : null;

        private void Awake()
        {
            if (levelCamera && ground && Energy && areaPrefab) return;
            Debug.LogError("ProtectionAbility requires a camera, ground, necromancer with energy and area prefab.", this);
            enabled = false;
        }

        private void Update()
        {
            if (Time.timeScale > 0 && Input.GetMouseButtonDown(1))
                TryCastAtScreenPosition(Input.mousePosition);
        }

        public bool TryCastAtScreenPosition(Vector2 screenPosition)
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0 || !levelCamera || !ground || !areaPrefab
                || !Energy || !Energy.CanSpend(energyCost) || screenPosition.x < 0 || screenPosition.y < 0
                || screenPosition.x >= Screen.width || screenPosition.y >= Screen.height) return false;
            var hud = GetComponent<DarkEnergyHUD>();
            if (hud && hud.isActiveAndEnabled && hud.ContainsScreenPosition(screenPosition)) return false;
            var feedback = FindFirstObjectByType<GameFeedback>();
            if (feedback && feedback.isActiveAndEnabled && feedback.ContainsScreenPosition(screenPosition)) return false;
            var objective = FindFirstObjectByType<FarmObjectiveHUD>();
            if (objective && objective.isActiveAndEnabled && objective.ContainsScreenPosition(screenPosition)) return false;
            Ray ray = levelCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, levelCamera.farClipPlane,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || hit.collider != ground || Vector3.Dot(hit.normal, Vector3.up) < 0.5f) return false;
            if (!Energy.TrySpend(energyCost)) return false;
            var area = Instantiate(areaPrefab, hit.point, Quaternion.identity);
            area.name = "ProtectionArea";
            return true;
        }

        private void OnValidate() => energyCost = Mathf.Max(0, energyCost);
    }
}
