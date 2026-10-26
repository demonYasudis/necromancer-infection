using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Presentation and movement bounds for the extended, authored demo.</summary>
    [DisallowMultipleComponent]
    public sealed class JourneyRoute : MonoBehaviour
    {
        [SerializeField] private Transform cameraRig;
        [SerializeField] private Transform[] route;
        [SerializeField] private float villageStartsAt = 80;
        [SerializeField] private float approachStartsAt = 440;
        private GameManager manager;
        private float length;
        public float DistanceTravelled { get; private set; }
        public float Length => length;
        public int Stage => DistanceTravelled < villageStartsAt ? 0 : DistanceTravelled < approachStartsAt ? 1 : 2;
        public string StageTitle => Stage == 0 ? "ОКРАИНА · СБОР ОРДЫ" : Stage == 1
            ? "ДЕРЕВНЯ · ПАТРУЛИ" : "ПОДХОД К ФЕРМЕ";
        public string StageHint => Stage == 0 ? "ЛКМ по жителям · могила даёт подкрепление"
            : Stage == 1 ? "Остановите посыльного · пополняйте орду между боями"
            : "Защищайтесь от арбалетов · берегите энергию для штурма";

        private void Awake()
        {
            manager = GetComponent<GameManager>();
            for (int i = 1; i < route.Length; i++) length += Vector3.Distance(route[i - 1].position, route[i].position);
        }

        private void LateUpdate()
        {
            if (!manager || !manager.Necromancer || manager.IsFinished || Time.timeScale <= 0) return;
            Vector3 position = manager.Necromancer.transform.position;
            // Move the parent rig, so GameFeedback retains its local camera shake.
            if (cameraRig) cameraRig.position = position;
            float nearest = float.PositiveInfinity, travelled = 0;
            for (int i = 1; i < route.Length; i++)
            {
                Vector3 start = route[i - 1].position;
                Vector3 segment = route[i].position - start;
                float distance = segment.magnitude;
                float t = distance > 0 ? Mathf.Clamp01(Vector3.Dot(position - start, segment) / segment.sqrMagnitude) : 0;
                float squared = (position - (start + segment * t)).sqrMagnitude;
                if (squared < nearest) { nearest = squared; DistanceTravelled = travelled + distance * t; }
                travelled += distance;
            }
        }

        public static Rect MovementArea(Component unit, Rect fallback)
        {
            var journey = FindFirstObjectByType<JourneyRoute>();
            if (!journey || journey.gameObject.scene != unit.gameObject.scene) return fallback;
            var game = journey.GetComponent<GameManager>();
            if (!game || !game.Ground) return fallback;
            Bounds bounds = game.Ground.bounds;
            return Rect.MinMaxRect(bounds.min.x + 1, bounds.min.z + 1, bounds.max.x - 1, bounds.max.z - 1);
        }
    }
}
