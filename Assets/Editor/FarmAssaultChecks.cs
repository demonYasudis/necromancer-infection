using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    [InitializeOnLoad]
    public static class FarmAssaultChecks
    {
        private const string PreviewKey = "NecromancerInfection.PreviewAssault";
        private static double previewReadyAt;

        static FarmAssaultChecks()
        {
            EditorApplication.update += UpdatePreview;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredEditMode) return;
                SessionState.SetBool(PreviewKey, false);
                previewReadyAt = 0;
            };
        }

        [MenuItem("Necromancer Infection/Preview Farm Assault")]
        public static void Preview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save scene changes before previewing the assault.");
            EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            SessionState.SetBool(PreviewKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void UpdatePreview()
        {
            if (!SessionState.GetBool(PreviewKey, false) || !EditorApplication.isPlaying) return;
            if (previewReadyAt == 0) previewReadyAt = EditorApplication.timeSinceStartup + 0.5;
            if (EditorApplication.timeSinceStartup < previewReadyAt) return;
            SessionState.SetBool(PreviewKey, false);
            previewReadyAt = 0;
            var manager = Object.FindFirstObjectByType<GameManager>();
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            manager.Necromancer.BeginRoute(manager.Farm.ArrivalPoint.position, new[] { manager.Farm.ArrivalPoint });
            manager.Farm.TryBeginFinalPhase(manager.Necromancer);
            foreach (var guard in manager.Farm.GetComponent<FarmAssault>().Reinforcements.GetComponentsInChildren<Guard>())
                guard.enabled = false;
            Debug.Log("Assault visual preview: reinforcement AI temporarily disabled; stop Play Mode to restore the authored scene.");
        }

        public static IEnumerator Checks(GameManager previous)
        {
            previous.Necromancer.Combat.TakeDamage(1000);
            Require(previous.TryRestartLevel(), "Assault checks must begin with a clean attempt.");
            yield return 0.3f;
            var manager = Object.FindFirstObjectByType<GameManager>();
            Prepare(manager);
            var farm = manager.Farm;
            var assault = farm.GetComponent<FarmAssault>();
            Require(assault && assault.enabled && assault.CorruptionDuration == 20 && !assault.HasStarted
                && !assault.IsCaptured && !assault.ClosedGate.activeSelf, "A fresh assault must have an open gate and a zeroed 20-second objective.");
            var leader = manager.Necromancer;
            leader.BeginRoute(farm.ArrivalPoint.position, new[] { farm.ArrivalPoint });
            yield return 0.2f;
            Require(manager.FinalPhase && assault.HasStarted && assault.ClosedGate.activeSelf
                && assault.ClosedGate.GetComponent<Collider>().enabled && assault.Reinforcements.childCount == 8,
                "Final phase must close a solid gate and spawn exactly eight final guards.");
            foreach (var guard in assault.Reinforcements.GetComponentsInChildren<Guard>())
            {
                Require(guard.Combat.MaxHealth == 100 && guard.Combat.Damage == 15
                    && guard.CurrentTarget, "Final guards must be live combatants which detect the approaching undead.");
                guard.enabled = false; // Isolate timer and ability checks from the lethal wave.
            }
            Require(!farm.TryBeginFinalPhase(leader) && assault.Reinforcements.childCount == 8,
                "Repeated proximity checks must not spawn another wave.");
            Debug.Log("PASS: final phase closes the gate and creates exactly eight active guards without duplicate waves.");
            Physics.SyncTransforms();
            Vector3 segment = farm.ArrivalPoint.position - new Vector3(11, 0, 0);
            foreach (var hit in Physics.SphereCastAll(new Vector3(11, 0.8f, 0), 0.35f, segment.normalized, segment.magnitude))
                Require(hit.collider.GetComponentInParent<UnitCombat>(), "The closed gate must not intersect the necromancer's remaining approach route.");
            float frozen = assault.CorruptionSeconds;
            Time.timeScale = 0;
            yield return 0.35f;
            Require(assault.CorruptionSeconds == frozen && !assault.IsCaptured, "Pause must stop corruption progress.");
            Time.timeScale = 1;
            leader.transform.position = farm.transform.position + Vector3.left * (farm.ActivationRadius + 1);
            yield return 0.1f;
            // Stop the route from immediately moving the test fixture back into the radius.
            leader.enabled = false;
            Require(assault.CorruptionSeconds == 0, "Leaving the farm must reset the uninterrupted corruption timer.");
            leader.transform.position = farm.ArrivalPoint.position;
            yield return 0.15f;
            Require(assault.CorruptionSeconds == 0, "A disabled necromancer must not corrupt the farm.");
            leader.enabled = true;
            leader.BeginRoute(farm.ArrivalPoint.position, new[] { farm.ArrivalPoint });
            var energy = leader.Energy;
            var settings = new SerializedObject(energy);
            settings.FindProperty("regenerationPerSecond").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            energy.Restore(100);
            var ability = Object.FindFirstObjectByType<InfectionAbility>();
            var human = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CivilianSetup.PrefabPath),
                new Vector3(-4, 0, 0), Quaternion.identity).GetComponent<Civilian>();
            Physics.SyncTransforms();
            Vector2 screen = manager.LevelCamera.WorldToScreenPoint(human.transform.position + Vector3.up * 0.75f);
            Require(ability.TryInfectAtScreenPosition(screen) && energy.Current == 85,
                "Paid LMB infection must remain available during the final assault.");
            yield return ability.InfectionDelay + 0.15f;
            var zombie = Object.FindFirstObjectByType<Zombie>();
            Require(!human && zombie && energy.Current == 90, "Final-phase infection must convert and grant its normal zombie reward.");
            zombie.enabled = false;
            Physics.SyncTransforms();
            screen = manager.LevelCamera.WorldToScreenPoint(farm.ArrivalPoint.position + Vector3.left);
            Require(ability.GetComponent<ProtectionAbility>().TryCastAtScreenPosition(screen) && energy.Current == 65,
                "Paid RMB protection must remain available during the final assault.");
            int captureEvents = 0;
            assault.Captured += () => captureEvents++;
            yield return 8f;
            Require(!assault.IsCaptured && assault.Progress > 0 && assault.Progress < 1,
                "The farm must not be captured before twenty living seconds near it.");
            double deadline = EditorApplication.timeSinceStartup + 15;
            while (!assault.IsCaptured && EditorApplication.timeSinceStartup < deadline) yield return 0.1f;
            Require(assault.IsCaptured && assault.CorruptionSeconds == 20 && assault.Progress == 1
                && captureEvents == 1 && leader.Combat.IsAlive && !manager.IsDefeated,
                "Twenty uninterrupted seconds with a living necromancer must capture the farm exactly once.");
            yield return 0.2f;
            Require(captureEvents == 1, "Completed capture must not fire repeatedly.");
            Debug.Log("PASS: assault spawns eight guards and closes gate; pause/exit/disabled leader stop corruption; LMB/RMB work; living 20-second hold captures once.");

            leader.Combat.TakeDamage(1000);
            Require(manager.TryRestartLevel(), "Reset the completed timer scenario for actual combat.");
            yield return 0.3f;
            manager = Object.FindFirstObjectByType<GameManager>();
            Prepare(manager);
            farm = manager.Farm;
            assault = farm.GetComponent<FarmAssault>();
            leader = manager.Necromancer;
            leader.BeginRoute(farm.ArrivalPoint.position, new[] { farm.ArrivalPoint });
            deadline = EditorApplication.timeSinceStartup + 12;
            while (!manager.IsDefeated && EditorApplication.timeSinceStartup < deadline) yield return 0.1f;
            Require(manager.IsDefeated && Time.timeScale == 0 && assault.HasStarted && !assault.IsCaptured
                && assault.CorruptionSeconds < 20 && assault.Reinforcements.childCount == 8,
                "The real eight-guard wave must kill an unprotected necromancer before capture and trigger defeat.");
            float stoppedProgress = assault.CorruptionSeconds;
            yield return 0.25f;
            Require(assault.CorruptionSeconds == stoppedProgress && !assault.IsCaptured,
                "Death must stop corruption permanently for that attempt.");
            Require(manager.TryRestartLevel(), "Retry must work after final-assault defeat.");
            yield return 0.3f;
            var fresh = Object.FindFirstObjectByType<GameManager>();
            var freshAssault = fresh.Farm.GetComponent<FarmAssault>();
            Require(!fresh.FinalPhase && !freshAssault.HasStarted && !freshAssault.IsCaptured
                && freshAssault.CorruptionSeconds == 0 && !freshAssault.ClosedGate.activeSelf
                && freshAssault.Reinforcements == null && Object.FindObjectsByType<Guard>(FindObjectsSortMode.None).Length == 2,
                "Retry must reopen gates, remove the wave and reset all corruption state.");
            Debug.Log("PASS: actual final-wave combat defeats a lone necromancer; death stops corruption; retry clears eight guards, gate and capture state.");
        }

        private static void Prepare(GameManager manager)
        {
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var civilian in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) civilian.gameObject.SetActive(false);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
