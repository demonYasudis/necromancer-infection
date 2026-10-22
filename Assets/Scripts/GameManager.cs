using UnityEngine;
using UnityEngine.SceneManagement;

namespace NecromancerInfection
{
    /// <summary>Scene references and the autonomous route for the demo.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DefeatScreen))]
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
        public NecromancerInfection.CaptureTarget Farm => captureTarget ? captureTarget.GetComponent<NecromancerInfection.CaptureTarget>() : null;
        public bool FinalPhase => Farm && Farm.FinalPhase;
        public Necromancer Necromancer => necromancer;
        public bool IsDefeated { get; private set; }
        public bool IsRestarting { get; private set; }
        private UnitCombat leaderCombat;

        private void Awake()
        {
            // Every new scene load starts a fresh attempt, including after defeat.
            Time.timeScale = 1;
            if (levelCamera == null || ground == null || startPoint == null || captureTarget == null || necromancer == null
                || System.Array.Exists(intermediateWaypoints, point => point == null))
            {
                Debug.LogError("DemoLevel is missing required scene references on GameManager.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!necromancer) return;
            leaderCombat = necromancer.Combat;
            leaderCombat.Died += OnNecromancerDied;
        }

        private void OnDisable()
        {
            if (leaderCombat) leaderCombat.Died -= OnNecromancerDied;
        }

        private void OnDestroy()
        {
            // Do not leave the editor or a subsequently opened scene frozen.
            if (IsDefeated) Time.timeScale = 1;
        }

        private void OnNecromancerDied(UnitCombat attacker)
        {
            if (IsDefeated) return;
            IsDefeated = true;
            Time.timeScale = 0;
        }

        public bool TryRestartLevel()
        {
            if (!IsDefeated || IsRestarting) return false;
            IsRestarting = true;
            // Keep the old attempt frozen until the new GameManager awakens.
            SceneManager.LoadSceneAsync(gameObject.scene.path, LoadSceneMode.Single);
            return true;
        }

        private void Start()
        {
            var route = new Transform[intermediateWaypoints.Length + 1];
            intermediateWaypoints.CopyTo(route, 0);
            route[route.Length - 1] = Farm ? Farm.ArrivalPoint : captureTarget;
            necromancer.BeginRoute(startPoint.position, route);
        }

        private void Update()
        {
            if (!IsDefeated && !IsRestarting && Farm) Farm.TryBeginFinalPhase(necromancer);
        }
    }
}
