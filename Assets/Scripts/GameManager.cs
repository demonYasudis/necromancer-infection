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
        [SerializeField] private string nextScenePath;
        public LevelObjective Objective => GetComponent<LevelObjective>();
        public bool CanAdvance => IsVictorious && !string.IsNullOrEmpty(nextScenePath)
            && Application.CanStreamedLevelBeLoaded(nextScenePath);

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
        public bool IsPaused => (pause & PauseReason.Menu) != 0;
        public int InfectedCount { get; private set; }
        public int GuardsKilled { get; private set; }
        public int SurvivingUndead { get; private set; }
        public double ElapsedSeconds => IsFinished ? completedSeconds : Time.timeAsDouble - attemptStartedAt;
        private string MenuPath => string.IsNullOrWhiteSpace(menuScenePath) ? MainMenu.ScenePath : menuScenePath;
        public bool CanReturnToMenu => (IsFinished || IsPaused) && !IsRestarting
            && MenuPath != gameObject.scene.path && Application.CanStreamedLevelBeLoaded(MenuPath);
        public bool IsRestarting { get; private set; }
        internal bool BlocksAbilityInput => IsFinished || IsRestarting || pause != PauseReason.None
            || Time.frameCount == resumeFrame;
        internal bool IsUpgradePaused => (pause & PauseReason.Upgrade) != 0;
        [System.Flags]
        private enum PauseReason { None = 0, Menu = 1, Upgrade = 2 }
        private PauseReason pause;
        private float resumeScale;
        private int resumeFrame = -1;
        private UnitCombat leaderCombat;
        private FarmAssault assault;
        private double attemptStartedAt;
        private double completedSeconds;

        private void Awake()
        {
            // Every new scene load starts a fresh attempt, including after defeat.
            Time.timeScale = 1;
            attemptStartedAt = Time.timeAsDouble;
            // Also supports previously authored scenes without resaving them or their prefabs.
            if (!GetComponent<GameAudio>()) gameObject.AddComponent<GameAudio>();
            if (!GetComponent<AbilityPreview>()) gameObject.AddComponent<AbilityPreview>();
            if (!GetComponent<GameHUD>()) gameObject.AddComponent<GameHUD>();
            if (!GetComponent<GameNotifications>()) gameObject.AddComponent<GameNotifications>();
            if (!GetComponent<FinalePresentation>()) gameObject.AddComponent<FinalePresentation>();
            if (!GetComponent<SoulProgression>()) gameObject.AddComponent<SoulProgression>();
            if (!GetComponent<OnboardingGuide>()) gameObject.AddComponent<OnboardingGuide>();
            if (!GetComponent<ProgressionScreen>()) gameObject.AddComponent<ProgressionScreen>();
            if (!GetComponent<JourneyCamera>()) gameObject.AddComponent<JourneyCamera>();
            if (!GetComponent<PauseMenu>()) gameObject.AddComponent<PauseMenu>();
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
            if (IsFinished || IsRestarting || pause != PauseReason.None) Time.timeScale = 1;
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
            if (Objective) return;
            CompleteLevel();
        }

        internal void CompleteLevel()
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

        public bool TryPauseGame() => AcquirePause(PauseReason.Menu);
        public bool TryResumeGame() => !IsFinished && !IsRestarting && ReleasePause(PauseReason.Menu);
        internal bool TryPauseForUpgrade() => AcquirePause(PauseReason.Upgrade);
        internal void ResumeAfterUpgrade() => ReleasePause(PauseReason.Upgrade);

        private bool AcquirePause(PauseReason reason)
        {
            if (IsFinished || IsRestarting || (pause & reason) != 0) return false;
            if (pause == PauseReason.None)
            {
                if (Time.timeScale <= 0) return false;
                resumeScale = Time.timeScale;
            }
            pause |= reason;
            Time.timeScale = 0;
            return true;
        }

        private bool ReleasePause(PauseReason reason)
        {
            if ((pause & reason) == 0) return false;
            pause &= ~reason;
            if (pause == PauseReason.None && !IsFinished && !IsRestarting && Time.timeScale == 0)
            {
                Time.timeScale = resumeScale;
                resumeFrame = Time.frameCount;
            }
            return true;
        }

        public bool TryRestartLevel() => (IsFinished || IsPaused) && TryLoadScene(gameObject.scene.path);
        public bool TryAdvanceLevel() => CanAdvance && TryLoadScene(nextScenePath);
        public bool TryReturnToMenu() => CanReturnToMenu && TryLoadScene(MenuPath);

        private bool TryLoadScene(string path)
        {
            if (IsRestarting || !Application.CanStreamedLevelBeLoaded(path)) return false;
            IsRestarting = true;
            // The old world stays frozen until the destination initializes its clock.
            Time.timeScale = 0;
            SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
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
