using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Per-scene tutorial progression and the timed objective for future survival maps.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameManager))]
    public sealed class LevelObjective : MonoBehaviour
    {
        public enum ObjectiveMode { Tutorial, Survival }
        [SerializeField] private ObjectiveMode mode;
        [SerializeField, Range(1, 4)] private int tutorialLevel = 1;
        [SerializeField, Min(1)] private float survivalDuration = 900;
        [SerializeField] private Guard knightPrefab;
        [SerializeField] private Crossbowman archerPrefab;
        [SerializeField] private AlarmBell bell;
        [SerializeField] private Transform[] encounterPoints = new Transform[0];
        private GameManager manager;
        private Transform encounter;
        internal Transform Encounter => encounter;
        public ObjectiveMode Mode => mode;
        public int TutorialLevel => tutorialLevel;
        public bool EncounterStarted { get; private set; }
        public float RemainingSeconds => Mathf.Max(0, survivalDuration - (float)manager.ElapsedSeconds);
        public string Title => mode == ObjectiveMode.Survival ? "ВЫЖИВАНИЕ"
            : $"ОБУЧЕНИЕ · {tutorialLevel}/4";
        public string Hint => mode == ObjectiveMode.Survival
            ? $"Продержитесь · {Mathf.CeilToInt(RemainingSeconds) / 60:00}:{Mathf.CeilToInt(RemainingSeconds) % 60:00}"
            : !EncounterStarted ? tutorialLevel == 1 ? "ЛКМ по крестьянам — соберите орду и дойдите до фермы"
                : tutorialLevel == 2 ? "Соберите орду · в конце один рыцарь · ПКМ — защита"
                : tutorialLevel == 3 ? "В конце рыцарь и лучник · защищайте орду от выстрелов"
                : "В конце колокол вызовет двух рыцарей и двух лучников"
            : "Устраните защитников или обратите их в нежить";

        private void Awake() => manager = GetComponent<GameManager>();

        private void Start()
        {
            // The tutorial has its own completion condition; legacy farm waves must never run.
            var assault = manager.Farm ? manager.Farm.GetComponent<FarmAssault>() : null;
            if (assault) assault.enabled = false;
        }

        private void LateUpdate()
        {
            if (manager.IsFinished || manager.IsRestarting || Time.timeScale <= 0
                || !manager.Necromancer || !manager.Necromancer.Combat.IsAlive) return;
            if (mode == ObjectiveMode.Survival)
            {
                if (RemainingSeconds <= 0) manager.CompleteLevel();
                return;
            }
            if (!manager.FinalPhase) return;
            if (!EncounterStarted)
            {
                if (tutorialLevel == 1) { manager.CompleteLevel(); return; }
                if (tutorialLevel == 4)
                {
                    if (!bell || !bell.TryRaiseTutorial()) return;
                    encounter = bell.Patrol;
                }
                else
                {
                    encounter = new GameObject("TutorialBosses").transform;
                    Instantiate(knightPrefab, encounterPoints[0].position, Quaternion.identity, encounter);
                    if (tutorialLevel == 3)
                        Instantiate(archerPrefab, encounterPoints[1].position, Quaternion.identity, encounter);
                }
                EncounterStarted = true;
            }
            // Pending infection remains an encounter until conversion completes, preserving rewards.
            foreach (var combat in encounter.GetComponentsInChildren<UnitCombat>(true))
                if (combat.IsAlive) return;
            manager.CompleteLevel();
        }
    }
}
