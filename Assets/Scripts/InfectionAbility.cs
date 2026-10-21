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

        public float InfectionDelay => infectionDelay;

        private void Awake()
        {
            if (!levelCamera || !necromancer || !zombiePrefab)
            {
                Debug.LogError("InfectionAbility requires a camera, necromancer and zombie prefab.", this);
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
            if (!isActiveAndEnabled || !levelCamera || !necromancer || !zombiePrefab) return false;
            Ray ray = levelCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, levelCamera.farClipPlane,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            var civilian = hit.collider.GetComponentInParent<Civilian>();
            return civilian && civilian.TryInfect(zombiePrefab, necromancer, infectionDelay);
        }

        private void OnValidate() => infectionDelay = Mathf.Max(0.01f, infectionDelay);
    }
}
