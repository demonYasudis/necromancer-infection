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
            CrossbowChecks.DisableSceneArchers();
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            manager.Necromancer.BeginRoute(manager.Farm.ArrivalPoint.position, new[] { manager.Farm.ArrivalPoint });
            manager.Farm.GetComponent<FarmAssault>().WaveArrived += _ => {
                foreach (var guard in manager.Farm.GetComponent<FarmAssault>().Reinforcements.GetComponentsInChildren<Guard>())
                    guard.enabled = false;
            };
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
            // Isolate timer/ability checks; the separate scenario below uses real wave combat.
            assault.WaveArrived += _ => {
                foreach (var guard in assault.Reinforcements.GetComponentsInChildren<Guard>()) guard.enabled = false;
            };
            Require(assault && assault.enabled && assault.CorruptionDuration == 20 && !assault.HasStarted
                && !assault.IsCaptured && !assault.ClosedGate.activeSelf, "A fresh assault must have an open gate and a zeroed 20-second objective.");
            var leader = manager.Necromancer;
            leader.BeginRoute(farm.ArrivalPoint.position, new[] { farm.ArrivalPoint });
            yield return 0.2f;
            Require(manager.FinalPhase && assault.HasStarted && assault.ClosedGate.activeSelf
                && assault.ClosedGate.GetComponent<Collider>().enabled && assault.Reinforcements.childCount == 0
                && assault.HasPendingWave && assault.NextWaveSize == 2 && assault.NextWaveSeconds > 2.5f
                && assault.WarningMarkers.childCount == 2,
                "Final phase must close a solid gate and warn of two guards before spawning any.");
            CaptureWarning(manager.LevelCamera, farm.ArrivalPoint.position + Vector3.back * 4);
            float countdown = assault.NextWaveSeconds;
            Time.timeScale = 0;
            yield return 0.35f;
            Require(assault.NextWaveSeconds == countdown && assault.Reinforcements.childCount == 0,
                "Pause must freeze the warning and prevent wave spawns.");
            Time.timeScale = 1;
            yield return 3.1f;
            Require(assault.WavesSpawned == 1 && assault.Reinforcements.childCount == 2
                && assault.NextWaveDirection == "СПРАВА" && assault.WarningMarkers.childCount == 3,
                "First wave must contain two guards and announce the opposite flank.");
            foreach (var guard in assault.Reinforcements.GetComponentsInChildren<Guard>())
            {
                Require(guard.Combat.MaxHealth == 100 && guard.Combat.Damage == 15
                    && guard.Combat.IsAlive, "Final guards must have the normal combat stats.");
                guard.enabled = false; // Isolate timer and ability checks from the lethal wave.
            }
            Require(!farm.TryBeginFinalPhase(leader) && assault.Reinforcements.childCount == 2,
                "Repeated proximity checks must not spawn another wave.");
            Debug.Log("PASS: gate, advance warning, paused countdown and first two-guard wave without duplicate activation.");
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
            Require(assault.WavesSpawned == 1 && assault.AssaultSeconds > 3,
                "Leaving the radius must preserve the assault clock and completed waves.");
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
            Require(assault.WavesSpawned == 2 && assault.Reinforcements.childCount == 5
                && assault.NextWaveDirection == "ПО ДОРОГЕ", "Second wave must add three guards and warn of the road wave.");
            yield return 3f;
            Require(assault.WavesSpawned == 3 && assault.Reinforcements.childCount == 8 && !assault.HasPendingWave
                && !assault.WarningMarkers, "Third wave must bring the total to eight and remove warnings.");
            double deadline = EditorApplication.timeSinceStartup + 15;
            while (!assault.IsCaptured && EditorApplication.timeSinceStartup < deadline) yield return 0.1f;
            Require(assault.IsCaptured && assault.CorruptionSeconds == 20 && assault.Progress == 1
                && captureEvents == 1 && leader.Combat.IsAlive && !manager.IsDefeated,
                "Twenty uninterrupted seconds with a living necromancer must capture the farm exactly once.");
            yield return 0.2f;
            Require(captureEvents == 1, "Completed capture must not fire repeatedly.");
            Debug.Log("PASS: 2/3/3 waves, warnings, pause, separate assault/corruption clocks, paid LMB/RMB and single 20-second capture.");

            leader.Combat.TakeDamage(1000);
            Require(manager.TryRestartLevel(), "Reset the completed timer scenario for actual combat.");
            yield return 0.3f;
            manager = Object.FindFirstObjectByType<GameManager>();
            Prepare(manager);
            farm = manager.Farm;
            assault = farm.GetComponent<FarmAssault>();
            leader = manager.Necromancer;
            leader.BeginRoute(farm.ArrivalPoint.position, new[] { farm.ArrivalPoint });
            deadline = EditorApplication.timeSinceStartup + 22;
            while (!manager.IsDefeated && EditorApplication.timeSinceStartup < deadline) yield return 0.1f;
            Require(manager.IsDefeated && Time.timeScale == 0 && assault.HasStarted && !assault.IsCaptured
                && assault.CorruptionSeconds < 20 && assault.WavesSpawned > 0 && assault.WavesSpawned < 3,
                "Real waves must kill a lone necromancer before capture, leaving reinforcements pending.");
            float stoppedProgress = assault.CorruptionSeconds;
            int stoppedWaves = assault.WavesSpawned;
            float stoppedClock = assault.AssaultSeconds;
            Time.timeScale = 8; // Completion guards must work even if another system resumes time.
            yield return 0.25f;
            Require(assault.CorruptionSeconds == stoppedProgress && !assault.IsCaptured
                && assault.WavesSpawned == stoppedWaves && assault.AssaultSeconds == stoppedClock
                && !assault.HasPendingWave && !assault.WarningMarkers.gameObject.activeSelf,
                "Death must stop the wave clock and hide warnings even if time resumes.");
            Require(manager.TryRestartLevel(), "Retry must work after final-assault defeat.");
            yield return 0.3f;
            var fresh = Object.FindFirstObjectByType<GameManager>();
            var freshAssault = fresh.Farm.GetComponent<FarmAssault>();
            Require(!fresh.FinalPhase && !freshAssault.HasStarted && !freshAssault.IsCaptured
                && freshAssault.CorruptionSeconds == 0 && !freshAssault.ClosedGate.activeSelf
                && freshAssault.WavesSpawned == 0 && freshAssault.AssaultSeconds == 0 && !freshAssault.WarningMarkers
                && freshAssault.Reinforcements == null && Object.FindObjectsByType<Guard>(FindObjectsSortMode.None).Length == 2,
                "Retry must reopen gates, remove the wave and reset all corruption state.");
            Debug.Log("PASS: real wave combat defeats a lone necromancer; death cancels pending waves; retry clears wave clock, warnings, guards and capture state.");
        }

        private static void CaptureWarning(Camera source, Vector3 center)
        {
            var camera = new GameObject("FarmWarningCheckCamera").AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var output = RenderTexture.GetTemporary(1280, 720, 24);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographicSize = 9;
                camera.transform.position = center + new Vector3(0, 16, -12);
                camera.transform.LookAt(center);
                camera.targetTexture = output;
                camera.Render();
                RenderTexture.active = output;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.WriteAllBytes("Logs/farm-wave-warning.png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(output);
                Object.Destroy(texture);
                Object.Destroy(camera.gameObject);
            }
        }

        private static void Prepare(GameManager manager)
        {
            CrossbowChecks.DisableSceneArchers();
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var civilian in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) civilian.gameObject.SetActive(false);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
