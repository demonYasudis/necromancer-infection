using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Scene references and the autonomous route for the demo.</summary>
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private Camera levelCamera;
        [SerializeField] private Collider ground;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform captureTarget;
        [SerializeField] private Necromancer necromancer;
        [SerializeField] private Transform[] intermediateWaypoints = new Transform[0];

        public Camera LevelCamera => levelCamera;
        public Collider Ground => ground;
        public Transform StartPoint => startPoint;
        public Transform CaptureTarget => captureTarget;
        public Necromancer Necromancer => necromancer;

        private void Awake()
        {
            if (levelCamera == null || ground == null || startPoint == null || captureTarget == null || necromancer == null
                || System.Array.Exists(intermediateWaypoints, point => point == null))
            {
                Debug.LogError("DemoLevel is missing required scene references on GameManager.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            var route = new Transform[intermediateWaypoints.Length + 1];
            intermediateWaypoints.CopyTo(route, 0);
            route[route.Length - 1] = captureTarget;
            necromancer.BeginRoute(startPoint.position, route);
        }
    }
}
