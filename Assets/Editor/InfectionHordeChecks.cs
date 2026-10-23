using System;
using System.Collections;
using NecromancerInfection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    /// <summary>Play Mode integration checks using the real scene, prefabs, raycasts and Update loops.</summary>
    [InitializeOnLoad]
    public static class InfectionHordeChecks
    {
        private const string RunningKey = "NecromancerInfection.ChecksRunning";
        private const string BatchKey = "NecromancerInfection.ChecksBatch";
        private const string CrowdKey = "NecromancerInfection.CrowdChecksOnly";
        private const string CrossbowKey = "NecromancerInfection.CrossbowChecksOnly";
        private const string RaisedGuardKey = "NecromancerInfection.RaisedGuardChecksOnly";
        private const string AlarmKey = "NecromancerInfection.AlarmChecksOnly";
        private const string GraveyardKey = "NecromancerInfection.GraveyardChecksOnly";
        private static IEnumerator routine;
        private static double resumeAt;
        private static double deadline;
        private static string runtimeError;

        static InfectionHordeChecks()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RunningKey, false))
                    Finish(false, "Play Mode was stopped before checks completed.");
            };
        }

        [MenuItem("Necromancer Infection/Run Prototype Checks")]
        public static void Run() => RunChecks(false);

        private static void RunChecks(bool crowdOnly, bool crossbowOnly = false, bool raisedGuardOnly = false, bool alarmOnly = false, bool graveyardOnly = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before starting checks.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save scene changes before starting checks.");
            EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            SessionState.SetBool(CrowdKey, crowdOnly);
            SessionState.SetBool(CrossbowKey, crossbowOnly);
            SessionState.SetBool(RaisedGuardKey, raisedGuardOnly);
            SessionState.SetBool(AlarmKey, alarmOnly);
            SessionState.SetBool(GraveyardKey, graveyardOnly);
            SessionState.SetBool(RunningKey, true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(BatchKey, true);
            Run();
        }

        [MenuItem("Necromancer Infection/Run Crowd Checks")]
        public static void RunCrowd() => RunChecks(true);

        public static void RunCrowdBatch()
        {
            SessionState.SetBool(BatchKey, true);
            RunCrowd();
        }

        [MenuItem("Necromancer Infection/Run Crossbow Checks")]
        public static void RunCrossbow() => RunChecks(false, true);

        public static void RunCrossbowBatch()
        {
            SessionState.SetBool(BatchKey, true);
            RunCrossbow();
        }

        [MenuItem("Necromancer Infection/Run Raised Guard Checks")]
        public static void RunRaisedGuard() => RunChecks(false, false, true);

        public static void RunRaisedGuardBatch()
        {
            SessionState.SetBool(BatchKey, true);
            RunRaisedGuard();
        }

        [MenuItem("Necromancer Infection/Run Alarm Checks")]
        public static void RunAlarm() => RunChecks(false, false, false, true);

        public static void RunAlarmBatch()
        {
            SessionState.SetBool(BatchKey, true);
            RunAlarm();
        }

        [MenuItem("Necromancer Infection/Run Graveyard Checks")]
        public static void RunGraveyard() => RunChecks(false, false, false, false, true);

        public static void RunGraveyardBatch()
        {
            SessionState.SetBool(BatchKey, true);
            RunGraveyard();
        }

        private static void Update()
        {
            if (!SessionState.GetBool(RunningKey, false) || !EditorApplication.isPlaying
                || EditorApplication.isPaused) return;
            if (routine == null)
            {
                routine = SessionState.GetBool(GraveyardKey, false) ? GraveyardChecks.Checks()
                    : SessionState.GetBool(AlarmKey, false) ? AlarmChecks.Checks()
                    : SessionState.GetBool(RaisedGuardKey, false) ? RaisedGuardChecks.Checks()
                    : SessionState.GetBool(CrossbowKey, false) ? CrossbowChecks.Checks()
                    : SessionState.GetBool(CrowdKey, false) ? CrowdChecks.Checks() : Checks();
                resumeAt = EditorApplication.timeSinceStartup + 0.5;
                deadline = EditorApplication.timeSinceStartup + 360;
                Application.logMessageReceived += WatchErrors;
            }
            if (EditorApplication.timeSinceStartup < resumeAt) return;
            try
            {
                Require(runtimeError == null, runtimeError);
                Require(EditorApplication.timeSinceStartup < deadline, "Checks timed out.");
                if (routine.MoveNext())
                    resumeAt = EditorApplication.timeSinceStartup + (float)routine.Current;
                else
                    Finish(true, SessionState.GetBool(GraveyardKey, false) ? "All graveyard checks passed."
                        : SessionState.GetBool(AlarmKey, false)
                        ? "All village alarm checks passed (runner detection/movement, paid infection, death, pause, single patrol, combat and retry)."
                        : SessionState.GetBool(RaisedGuardKey, false)
                        ? "All raised-guard checks passed (corpses, paid LMB raising, heavy combat, resource/statistics semantics, protection and retry)."
                        : SessionState.GetBool(CrossbowKey, false)
                        ? "All crossbow integration checks passed (warning, bolts, protection, cooldown, walls/range, pause, target lifecycle, defender rewards and retry)."
                        : SessionState.GetBool(CrowdKey, false)
                        ? "All crowd integration checks passed (30-member formation, narrow passage, walls/leash, attack ranks, pause and reload)."
                        : "All prototype integration checks passed (infection, horde, combat, guards, dark energy, protection, defeat/retry, farm, final assault, victory/statistics/retry, visual feedback/UI, crowd movement, crossbowmen).");
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static IEnumerator Checks()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            var ability = Object.FindFirstObjectByType<InfectionAbility>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InfectionSetup.PrefabPath).GetComponent<Zombie>();
            Require(manager && ability && prefab, "Scene references and prefabs must exist.");
            var leader = manager.Necromancer;
            CrossbowChecks.DisableSceneArchers();
            Require(leader.Energy && leader.Energy.Current == 100 && leader.Energy.Maximum == 100
                && ability.InfectionCost == 15 && ability.GetComponent<DarkEnergyHUD>(),
                "DemoLevel must start at 100/100 energy with infection cost 15 and a HUD.");
            foreach (var defender in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None))
                defender.gameObject.SetActive(false);
            leader.enabled = false;
            leader.transform.position = Vector3.zero;
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None))
                human.gameObject.SetActive(false);
            Time.timeScale = 1;
            var subject = Human(new Vector3(-4, 0, 0));
            yield return 0.1f;

            Physics.SyncTransforms();
            Vector2 ground = manager.LevelCamera.WorldToScreenPoint(new Vector3(-15, 0, -8));
            Require(!ability.TryInfectAtScreenPosition(ground), "Ground click must do nothing.");
            Vector2 screen = manager.LevelCamera.WorldToScreenPoint(subject.transform.position + Vector3.up * 0.75f);
            Require(ability.TryInfectAtScreenPosition(screen), "Raycast must select a human and start infection.");
            Require(!ability.TryInfectAtScreenPosition(screen), "A second click must reject the infected human.");
            Require(subject.State == Civilian.BehaviourState.Infected, "Infection state must be immediate.");
            Vector3 infectionPosition = subject.transform.position;
            yield return ability.InfectionDelay * 0.4f;
            Require(subject && subject.transform.position == infectionPosition, "Infected human must stop moving.");
            Require(Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 0, "Conversion must wait for its delay.");
            yield return ability.InfectionDelay * 0.8f;
            var zombies = Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None);
            Require(!subject && zombies.Length == 1, "One human must become exactly one zombie.");
            var zombie = zombies[0];
            Require(zombie.Leader == leader && zombie.GetComponent<UndeadPresence>(), "Zombie must join the horde and count as undead.");
            Debug.Log("PASS: ground click, human raycast, infection delay, frozen infected state and duplicate rejection.");

            zombie.transform.position = new Vector3(-4, 0, 0);
            yield return 0.3f;
            Require(zombie.State == Zombie.BehaviourState.Following && zombie.transform.position.x > -3.8f,
                "Zombie without humans must approach its leader.");
            leader.transform.position = new Vector3(1, 0, 0);
            float before = zombie.transform.position.x;
            yield return 0.2f;
            Require(zombie.transform.position.x > before, "Zombie must follow a moving horde center.");

            leader.transform.position = Vector3.zero;
            zombie.transform.position = Vector3.zero;
            var near = Human(new Vector3(2, 0, 0));
            var far = Human(new Vector3(3.5f, 0, 0));
            yield return 0.1f;
            Require(zombie.CurrentTarget == near.Combat && zombie.State == Zombie.BehaviourState.PursuingTarget,
                "Zombie must select the nearest available human.");
            zombie.transform.position = near.transform.position - Vector3.right * 0.8f;
            yield return 0.1f;
            Require(zombie.State == Zombie.BehaviourState.Attacking, "Zombie must enter combat at attack distance.");
            near.gameObject.SetActive(false);
            yield return 0.1f;
            Require(zombie.CurrentTarget == far.Combat, "Disabled humans must be dropped as targets.");
            Require(far.TryInfect(prefab, leader, ability.InfectionDelay), "The remaining target must accept infection.");
            yield return 0.1f;
            Require(!zombie.CurrentTarget, "Infected humans must immediately stop being attack targets.");
            Object.Destroy(far.gameObject);
            yield return 0.1f;
            Require(!zombie.CurrentTarget && zombie.State == Zombie.BehaviourState.Following,
                "After losing its last human, zombie must return to the leader.");
            Debug.Log("PASS: following, nearest target, approach distance, disabled/destroyed target and return to leader.");

            var boundaryHuman = Human(new Vector3(7, 0, 0));
            zombie.transform.position = new Vector3(7.9f, 0, 0);
            yield return 0.3f;
            Require(!zombie.CurrentTarget && zombie.State == Zombie.BehaviourState.Returning
                && zombie.transform.position.x < 7.7f, "Leash must override a nearby human and move the zombie home.");
            boundaryHuman.transform.position = new Vector3(10, 0, 0);
            zombie.Initialize(leader);
            zombie.transform.position = new Vector3(7, 0, 0);
            yield return 0.1f;
            Require(!zombie.CurrentTarget && Vector3.Distance(zombie.transform.position, leader.transform.position) <= zombie.MaxHordeDistance,
                "Humans outside the horde radius must not lure zombies away.");
            Object.Destroy(boundaryHuman.gameObject);
            zombie.transform.position = new Vector3(-12, 0, 0);
            yield return 0.3f;
            Require(zombie.State == Zombie.BehaviourState.Returning && zombie.transform.position.x > -11.8f,
                "A remote conversion must head home instead of pursuing humans.");
            Debug.Log("PASS: leash priority, out-of-radius target rejection and distant spawn return.");

            leader.transform.position = new Vector3(11, 0, 3.5f);
            zombie.Initialize(leader);
            zombie.transform.position = new Vector3(-10.6f, 0, 7.5f);
            float blockedDistance = Vector3.Distance(zombie.transform.position, leader.transform.position);
            yield return 3.5f;
            Require(zombie.transform.position.x > -9
                && Vector3.Distance(zombie.transform.position, leader.transform.position) < blockedDistance - 1.5f,
                "A remote zombie must detour around the village house and continue returning.");
            Debug.Log("PASS: remote zombie navigates around the actual village house.");

            var cancelled = Human(new Vector3(3, 0, -2));
            Require(cancelled.TryInfect(prefab, leader, ability.InfectionDelay), "Cancellation setup must infect a human.");
            cancelled.gameObject.SetActive(false);
            yield return ability.InfectionDelay + 0.2f;
            Require(Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 1,
                "Disabling a human during infection must cancel conversion.");
            Debug.Log("PASS: cancelled infection cannot create a ghost zombie.");

            var combatChecks = CombatChecks(leader, prefab);
            while (combatChecks.MoveNext()) yield return combatChecks.Current;
            var guardChecks = GuardChecks.Checks(leader, prefab);
            while (guardChecks.MoveNext()) yield return guardChecks.Current;
            var energyChecks = DarkEnergyChecks.Checks(manager, ability, prefab);
            while (energyChecks.MoveNext()) yield return energyChecks.Current;
            var protectionChecks = ProtectionChecks.Checks(manager, ability, prefab);
            while (protectionChecks.MoveNext()) yield return protectionChecks.Current;
            var defeatChecks = DefeatChecks.Checks(manager, ability, prefab);
            while (defeatChecks.MoveNext()) yield return defeatChecks.Current;
            var farmChecks = FarmChecks.Checks(Object.FindFirstObjectByType<GameManager>());
            while (farmChecks.MoveNext()) yield return farmChecks.Current;
            var assaultChecks = FarmAssaultChecks.Checks(Object.FindFirstObjectByType<GameManager>());
            while (assaultChecks.MoveNext()) yield return assaultChecks.Current;
            var victoryChecks = VictoryChecks.Checks(Object.FindFirstObjectByType<GameManager>(), prefab);
            while (victoryChecks.MoveNext()) yield return victoryChecks.Current;
            var feedbackChecks = FeedbackChecks.Checks(Object.FindFirstObjectByType<GameManager>(), prefab);
            while (feedbackChecks.MoveNext()) yield return feedbackChecks.Current;
            var crowdChecks = CrowdChecks.Checks();
            while (crowdChecks.MoveNext()) yield return crowdChecks.Current;
            var crossbowChecks = CrossbowChecks.Checks();
            while (crossbowChecks.MoveNext()) yield return crossbowChecks.Current;
            var raisedGuardChecks = RaisedGuardChecks.Checks();
            while (raisedGuardChecks.MoveNext()) yield return raisedGuardChecks.Current;
            var alarmChecks = AlarmChecks.Checks();
            while (alarmChecks.MoveNext()) yield return alarmChecks.Current;
            var graveyardChecks = GraveyardChecks.Checks();
            while (graveyardChecks.MoveNext()) yield return graveyardChecks.Current;
        }

        private static IEnumerator CombatChecks(Necromancer leader, Zombie prefab)
        {
            foreach (var unit in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None)) Object.Destroy(unit.gameObject);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) Object.Destroy(human.gameObject);
            leader.transform.position = Vector3.zero;
            yield return 0.1f;

            var attacker = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            attacker.Initialize(leader);
            attacker.enabled = false; // Exercise the shared attack API without AI issuing extra attacks.
            var secondAttacker = Object.Instantiate(prefab, new Vector3(-0.1f, 0, 0), Quaternion.identity);
            secondAttacker.Initialize(leader);
            secondAttacker.enabled = false;
            var victim = Human(new Vector3(2, 0, 0));
            var alternate = Human(new Vector3(0.7f, 0, 0));
            alternate.gameObject.SetActive(false);
            int deaths = 0;
            victim.Combat.Died += source => deaths++;
            yield return 0.1f;

            Require(attacker.Combat.CurrentHealth == 50 && attacker.Combat.Damage == 10
                && victim.Combat.CurrentHealth == 30 && victim.Combat.Damage == 0
                && leader.CurrentHealth == 100, "Prefabs must have the intended combat stats.");
            Require(!attacker.Combat.TryAttack(victim.Combat) && victim.Combat.CurrentHealth == 30,
                "Out-of-range attack must neither hit nor reserve cooldown.");
            victim.transform.position = new Vector3(0.8f, 0, 0);
            Require(attacker.Combat.TryAttack(victim.Combat) && victim.Combat.CurrentHealth == 20,
                "An in-range hit must deal exactly 10 damage.");
            Require(!attacker.Combat.TryAttack(victim.Combat), "Repeated attack in the same frame must fail.");
            yield return attacker.Combat.AttackCooldown * 0.4f;
            Require(!attacker.Combat.TryAttack(victim.Combat) && victim.Combat.CurrentHealth == 20,
                "Attacks during cooldown must not deal damage.");
            victim.transform.position = new Vector3(2, 0, 0);
            yield return attacker.Combat.AttackCooldown * 0.7f;
            Require(!attacker.Combat.TryAttack(victim.Combat), "Ready attacker still cannot hit beyond its range.");
            victim.transform.position = new Vector3(0.8f, 0, 0);
            Require(attacker.Combat.TryAttack(victim.Combat) && victim.Combat.CurrentHealth == 10,
                "Returning to range after cooldown must allow the second hit.");
            alternate.gameObject.SetActive(true);
            Require(!attacker.Combat.TryAttack(alternate.Combat), "Switching targets must not bypass attacker cooldown.");
            alternate.gameObject.SetActive(false);
            yield return attacker.Combat.AttackCooldown + 0.05f;
            var victimCombat = victim.Combat;
            Require(attacker.Combat.TryAttack(victimCombat) && !victimCombat.IsAlive
                && !victim.IsAvailableTarget, "The third hit must kill and immediately invalidate the human.");
            Require(!secondAttacker.Combat.TryAttack(victimCombat) && !victimCombat.TakeDamage(100, secondAttacker.Combat)
                && deaths == 1, "Additional lethal hits must not fire death or conversion twice.");
            yield return 0.1f;
            Require(!victim && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 3,
                "One killed human must create exactly one additional zombie.");
            foreach (var unit in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None))
            {
                Require(unit.Leader == leader && unit.Combat.CurrentHealth == 50,
                    "Converted zombie must have full prefab health and the killer's leader.");
                unit.enabled = false;
            }
            Debug.Log("PASS: melee range, damage, cooldown across targets, three-hit kill and one conversion under multiple hits.");

            var unrelated = Human(new Vector3(-3, 0, 0));
            Require(!unrelated.Combat.TakeDamage(0) && !unrelated.Combat.TakeDamage(-5)
                && unrelated.Combat.CurrentHealth == 30, "Zero and negative damage must have no effect.");
            unrelated.Combat.TakeDamage(100);
            yield return 0.1f;
            Require(!unrelated && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 3,
                "A human killed without a zombie must not convert.");

            var pending = Human(new Vector3(3, 0, -2));
            Require(pending.TryInfect(prefab, leader, 0.5f), "Concurrent infection setup must succeed.");
            pending.Combat.TakeDamage(30, attacker.Combat);
            Require(!pending.IsAvailableTarget, "A killed infected human must be invalid immediately.");
            yield return 0.7f;
            Require(!pending && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 4,
                "Lethal zombie damage during infection must create one zombie and cancel the pending timer.");

            attacker.transform.position = new Vector3(0, 0, 8);
            attacker.Combat.TakeDamage(100, secondAttacker.Combat);
            Require(!attacker.Combat.IsAlive, "Zombie health must also reach zero.");
            Require(!UndeadPresence.FindNearest(attacker.transform.position, 0.1f), "Dead undead must stop frightening humans immediately.");
            yield return 0.1f;
            Require(!attacker && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 3,
                "Dead zombie must disappear without producing another zombie.");
            Require(leader.Combat.TakeDamage(25) && leader.CurrentHealth == 75,
                "Necromancer HP facade must use the shared health component.");
            Debug.Log("PASS: invalid damage, non-zombie death, infection/death race, zombie death and shared necromancer HP.");

            foreach (var unit in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None)) Object.Destroy(unit.gameObject);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) Object.Destroy(human.gameObject);
            yield return 0.1f;
            var seed = Object.Instantiate(prefab, new Vector3(-1, 0, 0), Quaternion.identity);
            seed.Initialize(leader);
            var first = Human(Vector3.zero);
            var second = Human(new Vector3(2, 0, 0));
            var third = Human(new Vector3(4, 0, 0));
            double timeout = EditorApplication.timeSinceStartup + 12;
            while ((first || second || third) && EditorApplication.timeSinceStartup < timeout)
            {
                yield return 0.1f;
                int livingHumans = (first ? 1 : 0) + (second ? 1 : 0) + (third ? 1 : 0);
                Require(Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 4 - livingHumans,
                    "Each autonomous kill must add exactly one zombie to the horde.");
            }
            Require(!first && !second && !third && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 4,
                "Autonomous kills must grow the horde from one zombie to four without player infection.");
            yield return 2f;
            foreach (var unit in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None))
                Require(unit.State == Zombie.BehaviourState.Following && !unit.CurrentTarget
                    && Vector3.Distance(unit.transform.position, leader.transform.position) <= 1.6f,
                    "After actual combat, surviving zombies must return to their leader.");
            Debug.Log("PASS: autonomous chain infection grows 1 -> 4 zombies and all return after combat.");
        }

        private static Civilian Human(Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CivilianSetup.PrefabPath);
            var human = Object.Instantiate(prefab, position, Quaternion.identity).GetComponent<Civilian>();
            // Keep fixtures stationary while exercising the real target and infection lifecycle.
            var settings = new SerializedObject(human);
            settings.FindProperty("movementArea").rectValue = new Rect(position.x, position.z, 0, 0);
            settings.ApplyModifiedPropertiesWithoutUndo();
            return human;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void WatchErrors(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                runtimeError = message;
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(RunningKey, false);
            SessionState.SetBool(CrowdKey, false);
            SessionState.SetBool(CrossbowKey, false);
            SessionState.SetBool(RaisedGuardKey, false);
            SessionState.SetBool(AlarmKey, false);
            SessionState.SetBool(GraveyardKey, false);
            Application.logMessageReceived -= WatchErrors;
            routine = null;
            runtimeError = null;
            if (success) Debug.Log("PASS: " + message);
            else Debug.LogError("FAIL: " + message);
            bool batch = SessionState.GetBool(BatchKey, false);
            SessionState.SetBool(BatchKey, false);
            if (batch) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
