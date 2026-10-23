using UnityEngine;
using UnityEngine.SceneManagement;

namespace NecromancerInfection
{
    /// <summary>Scene references and the autonomous route for the demo.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DefeatScreen))]
    [RequireComponent(typeof(VictoryScreen))]
    [RequireComponent(typeof(GameFeedback))]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private Camera levelCamera;
        [SerializeField] private Collider ground;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform captureTarget;
        [SerializeField] private Necromancer necromancer;
        [SerializeField] private Transform[] intermediateWaypoints = new Transform[0];
        [SerializeField] private string menuScenePath;

        public Camera LevelCamera => levelCamera;
        public Collider Ground => ground;
        public Transform StartPoint => startPoint;
        public Transform CaptureTarget => captureTarget;
        public NecromancerInfection.CaptureTarget Farm => captureTarget ? captureTarget.GetComponent<NecromancerInfection.CaptureTarget>() : null;
        public bool FinalPhase => Farm && Farm.FinalPhase;
        public Necromancer Necromancer => necromancer;
        public bool IsDefeated { get; private set; }
        public bool IsVictorious { get; private set; }
        public bool IsFinished => IsDefeated || IsVictorious;
        public int InfectedCount { get; private set; }
        public int GuardsKilled { get; private set; }
        public int SurvivingUndead { get; private set; }
        public double ElapsedSeconds => IsFinished ? completedSeconds : Time.timeAsDouble - attemptStartedAt;
        public bool CanReturnToMenu => !string.IsNullOrWhiteSpace(menuScenePath)
            && menuScenePath != gameObject.scene.path && Application.CanStreamedLevelBeLoaded(menuScenePath);
        public bool IsRestarting { get; private set; }
        private UnitCombat leaderCombat;
        private FarmAssault assault;
        private double attemptStartedAt;
        private double completedSeconds;

        private void Awake()
        {
            // Every new scene load starts a fresh attempt, including after defeat.
            Time.timeScale = 1;
            attemptStartedAt = Time.timeAsDouble;
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
            assault = Farm ? Farm.GetComponent<FarmAssault>() : null;
            if (assault) assault.Captured += OnFarmCaptured;
            Zombie.Raised += OnZombieRaised;
            UnitCombat.AnyDied += OnUnitDied;
        }

        private void OnDisable()
        {
            if (leaderCombat) leaderCombat.Died -= OnNecromancerDied;
            if (assault) assault.Captured -= OnFarmCaptured;
            Zombie.Raised -= OnZombieRaised;
            UnitCombat.AnyDied -= OnUnitDied;
        }

        private void OnDestroy()
        {
            // Do not leave the editor or a subsequently opened scene frozen.
            if (IsFinished) Time.timeScale = 1;
        }

        private void OnNecromancerDied(UnitCombat attacker)
        {
            if (IsFinished) return;
            completedSeconds = ElapsedSeconds;
            IsDefeated = true;
            Time.timeScale = 0;
        }

        private void OnZombieRaised(Zombie zombie)
        {
            if (!IsFinished && !zombie.GetComponent<RaisedGuard>() && !zombie.GetComponent<Skeleton>()
                && zombie.Leader == necromancer && zombie.gameObject.scene == gameObject.scene)
                InfectedCount++;
        }

        private void OnUnitDied(UnitCombat victim)
        {
            if (!IsFinished && victim.gameObject.scene == gameObject.scene
                && (victim.GetComponent<Guard>() || victim.GetComponent<Crossbowman>()))
                GuardsKilled++;
        }

        private void OnFarmCaptured()
        {
            if (IsFinished || IsRestarting || !necromancer || !necromancer.Combat.IsAlive) return;
            completedSeconds = ElapsedSeconds;
            SurvivingUndead = 1; // The living necromancer is part of the surviving undead.
            foreach (var zombie in FindObjectsByType<Zombie>(FindObjectsSortMode.None))
                if (zombie.gameObject.scene == gameObject.scene && zombie.Leader == necromancer && zombie.Combat.IsAlive)
                    SurvivingUndead++;
            IsVictorious = true;
            Time.timeScale = 0;
        }

        public bool TryRestartLevel()
        {
            if (!IsFinished || IsRestarting) return false;
            IsRestarting = true;
            // Keep the old attempt frozen until the new GameManager awakens.
            SceneManager.LoadSceneAsync(gameObject.scene.path, LoadSceneMode.Single);
            return true;
        }

        public bool TryReturnToMenu()
        {
            if (!IsFinished || IsRestarting || !CanReturnToMenu) return false;
            IsRestarting = true;
            Time.timeScale = 1;
            SceneManager.LoadSceneAsync(menuScenePath, LoadSceneMode.Single);
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
            if (!IsFinished && !IsRestarting && Farm) Farm.TryBeginFinalPhase(necromancer);
        }
    }
}
