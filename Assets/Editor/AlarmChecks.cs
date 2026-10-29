using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class AlarmChecks
    {
        public static IEnumerator Checks()
        {
            SceneManager.LoadSceneAsync(DemoLevelSetup.ScenePath);
            yield return 0.4f;
            var manager = Object.FindFirstObjectByType<GameManager>();
            var bell = Object.FindFirstObjectByType<AlarmBell>();
            Require(bell && bell.Runner && Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None).Length == 18,
                "The authored alarm must reuse one of the original 18 villagers.");
            Prepare(manager, bell);
            var runner = bell.Runner;
            Require(runner.IsRunning && runner.GetComponent<Civilian>().State == Civilian.BehaviourState.RunningToBell
                && runner.transform.Find("AlarmWarning").gameObject.activeSelf,
                "The nearby starting necromancer must trigger a marked villager running to the bell.");
            Require(!bell.TryRaise(runner) && !bell.IsRaised, "A distant runner cannot ring the bell remotely.");
            var ability = Object.FindFirstObjectByType<InfectionAbility>();
            var settings = new SerializedObject(manager.Necromancer.Energy);
            settings.FindProperty("regenerationPerSecond").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Capture(manager.LevelCamera, new Vector3(-7, 0, 2.5f));
            Physics.SyncTransforms();
            Vector2 screen = manager.LevelCamera.WorldToScreenPoint(runner.transform.position + Vector3.up * 1.1f);
            Require(ability.TryInfectAtScreenPosition(screen) && manager.Necromancer.Energy.Current == 85
                && bell.IsPrevented && !runner.IsRunning,
                $"Actual paid LMB infection must synchronously stop the runner and prevent alarm. Pointer={screen}, viewport={Screen.width}x{Screen.height}, blocked={AbilityPointer.IsBlocked(screen)}, runner={runner.transform.position}, energy={manager.Necromancer.Energy.Current}.");
            yield return 3.5f;
            Require(!runner && !bell.IsRaised && !bell.Patrol && manager.InfectedCount == 1,
                "The infected runner must become a normal zombie, with no delayed patrol or ghost alarm.");
            Debug.Log("PASS: one of 18 villagers runs with a warning; real paid LMB infection cancels the alarm and converts normally without a patrol.");

            SceneManager.LoadSceneAsync(DemoLevelSetup.ScenePath);
            yield return 0.4f;
            manager = Object.FindFirstObjectByType<GameManager>();
            bell = Object.FindFirstObjectByType<AlarmBell>();
            Prepare(manager, bell);
            runner = bell.Runner;
            runner.GetComponent<UnitCombat>().TakeDamage(1000);
            Require(bell.IsPrevented && !bell.IsRaised, "A lethal hit before reaching the bell must immediately prevent alarm.");
            yield return 3f;
            Require(!bell.Patrol && !bell.IsRaised, "A dead messenger cannot create delayed reinforcements.");
            Debug.Log("PASS: real messenger death prevents the alarm permanently for the attempt.");

            SceneManager.LoadSceneAsync(DemoLevelSetup.ScenePath);
            yield return 0.4f;
            manager = Object.FindFirstObjectByType<GameManager>();
            bell = Object.FindFirstObjectByType<AlarmBell>();
            Prepare(manager, bell);
            runner = bell.Runner;
            Vector3 position = runner.transform.position;
            Time.timeScale = 0;
            yield return 0.2f;
            Require(runner.transform.position == position && !bell.TryRaise(runner) && !bell.IsRaised,
                "Pause must freeze running and prevent ringing.");
            Time.timeScale = 1;
            double deadline = EditorApplication.timeSinceStartup + 7;
            while (!bell.IsRaised && EditorApplication.timeSinceStartup < deadline) yield return 0.1f;
            Require(bell.IsRaised && !bell.IsPrevented && bell.Patrol && bell.Patrol.childCount == 3,
                "An uninterrupted actual run must reach the authored bell and summon three guards.");
            yield return 0.05f;
            Require(manager.GetComponent<GameNotifications>().CurrentKey == "alarm",
                "The real bell must replace its messenger warning with one patrol-arrival notice.");
            Require(!bell.TryRaise(runner), "A raised alarm must reject repeat ringing.");
            yield return 0.3f;
            Require(bell.Patrol.childCount == 3, "Repeated updates must not summon duplicate patrols.");
            foreach (var guard in bell.Patrol.GetComponentsInChildren<Guard>())
                Require(guard.isActiveAndEnabled && guard.Combat.IsAlive && guard.GetComponent<GuardRemains>(),
                    "Alarm guards must be ordinary combatants with the defender death/raising lifecycle.");
            runner.GetComponent<UnitCombat>().TakeDamage(1000);
            Require(bell.IsRaised && !bell.IsPrevented && bell.Patrol.childCount == 3,
                "Stopping a messenger after ringing cannot undo the summoned patrol.");
            manager.Necromancer.transform.position = new Vector3(-3, 0, 0);
            deadline = EditorApplication.timeSinceStartup + 10;
            while (!manager.IsDefeated && EditorApplication.timeSinceStartup < deadline) yield return 0.1f;
            Require(manager.IsDefeated && !manager.FinalPhase,
                "The additional patrol must actually attack and defeat an unprotected necromancer before the farm.");
            Require(manager.TryRestartLevel(), "Retry must work after defeat by alarm reinforcements.");
            yield return 0.4f;
            var fresh = Object.FindFirstObjectByType<GameManager>();
            var freshBell = Object.FindFirstObjectByType<AlarmBell>();
            Require(fresh && !fresh.IsFinished && freshBell && !freshBell.IsRaised && !freshBell.IsPrevented && !freshBell.Patrol
                && Object.FindObjectsByType<Guard>(FindObjectsSortMode.None).Length == 2
                && Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None).Length == 18,
                "Retry must reset the alarm, restore its messenger and remove the three extra guards.");
            Debug.Log("PASS: pause, actual bell arrival, exactly three armed reinforcements, no duplicate/retroactive cancellation, real patrol combat and clean defeat/retry.");
        }

        private static void Prepare(GameManager manager, AlarmBell bell)
        {
            manager.Necromancer.enabled = false;
            CrossbowChecks.DisableSceneArchers();
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None))
                if (human.gameObject != bell.Runner.gameObject) human.gameObject.SetActive(false);
        }

        private static void Capture(Camera source, Vector3 center)
        {
            var camera = new GameObject("AlarmCheckCamera").AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographicSize = 5;
                camera.transform.position = center + new Vector3(0, 12, -8);
                camera.transform.LookAt(center);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/alarm-runner.png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(texture);
                Object.Destroy(camera.gameObject);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
