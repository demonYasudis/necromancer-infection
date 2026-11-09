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
        public event System.Action CastByPlayer;
        public void ReduceCost() => energyCost *= .8f;
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
            if (Time.timeScale <= 0 || !TryGetPlacement(screenPosition, out var point, out var reason)
                || reason != null || !Energy.TrySpend(energyCost)) return false;
            var area = Instantiate(areaPrefab, point, Quaternion.identity);
            area.name = "ProtectionArea";
            CastByPlayer?.Invoke();
            return true;
        }

        public bool TryGetPlacement(Vector2 screenPosition, out Vector3 point, out string reason)
        {
            point = default;
            reason = null;
            if (!isActiveAndEnabled || !levelCamera || !ground || !areaPrefab || !Energy
                || !necromancer || !necromancer.Combat.IsAlive || AbilityPointer.IsBlocked(screenPosition)) return false;
            Ray ray = levelCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, levelCamera.farClipPlane,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider.GetComponentInParent<Necromancer>() == necromancer)
            {
                // Target the ground beneath the leader when clicking his body.
                var down = new Ray(necromancer.transform.position + Vector3.up * 5, Vector3.down);
                if (!ground.Raycast(down, out var groundHit, 10)) return false;
                point = groundHit.point;
            }
            else
            {
                if (hit.collider != ground || Vector3.Dot(hit.normal, Vector3.up) < 0.5f) return false;
                point = hit.point;
            }
            if (!Energy.CanSpend(energyCost)) reason = "Недостаточно энергии";
            return true;
        }

        private void OnValidate() => energyCost = Mathf.Max(0, energyCost);
    }
}
