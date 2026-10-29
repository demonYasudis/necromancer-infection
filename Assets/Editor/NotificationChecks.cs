using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class NotificationChecks
    {
        public static IEnumerator Checks()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (!manager.IsFinished) manager.Necromancer.Combat.TakeDamage(1000);
            Require(manager.TryRestartLevel(), "Notification checks require a fresh attempt.");
            yield return 0.35f;
            manager = Object.FindFirstObjectByType<GameManager>();
            var notices = manager.GetComponent<GameNotifications>();
            var bell = Object.FindFirstObjectByType<AlarmBell>();
            var leader = manager.Necromancer;
            leader.enabled = false;
            CrossbowChecks.DisableSceneArchers();
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None))
                if (human.gameObject != bell.Runner.gameObject) human.gameObject.SetActive(false);
            Require(bell.Runner.IsRunning, "The real scene must start its messenger.");
            Time.timeScale = 0;
            yield return 0.1f;
            Require(notices.CurrentKey == "runner" && notices.PendingCount == 1 && notices.CurrentCount == 1,
                "Repeated runner updates must produce one persistent, urgent notice.");
            var camera = manager.LevelCamera;
            Vector3 originalCamera = camera.transform.position;
            var list = new List<GameNotifications.Indicator>();
            camera.transform.position += camera.transform.right * 100;
            yield return 0.1f;
            notices.GetIndicators(camera, new HUDLayout(Screen.width, Screen.height), list);
            Require(list.Count == 1 && list[0].Text == "Посыльный" && list[0].Direction.x < 0,
                "An offscreen messenger must have an accurate left pointer.");
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(2560, 1080), new Vector2(1024, 768),
                new Vector2(640, 360), new Vector2(320, 568) })
            {
                var layout = new HUDLayout(size.x, size.y);
                foreach (var viewport in new[] { new Vector3(-1, 0.5f, 20), new Vector3(2, 0.5f, 20),
                    new Vector3(0.5f, -1, 20), new Vector3(0.5f, 2, 20), new Vector3(0.5f, 0.5f, -10) })
                {
                    Require(GameNotifications.TryPlaceIndicator(camera, camera.ViewportToWorldPoint(viewport), layout, out var indicator)
                        && !layout.Overlaps(indicator.Bounds, true) && indicator.Bounds.xMin >= 0
                        && indicator.Bounds.xMax <= layout.Width && indicator.Bounds.yMin >= layout.PlayTop - 0.01f
                        && indicator.Bounds.yMax <= layout.PlayBottom + 0.01f,
                        "Every pointer, including behind-camera threats, must fit clear of HUD at " + size);
                }
            }
            var capture = Capture("notifications-runner", 1280, 720);
            while (capture.MoveNext()) yield return capture.Current;
            capture = Capture("notifications-portrait", 320, 568);
            while (capture.MoveNext()) yield return capture.Current;
            camera.transform.position = bell.Runner.transform.position - camera.transform.forward * 20;
            notices.GetIndicators(camera, new HUDLayout(Screen.width, Screen.height), list);
            Require(list.Count == 0, "Seeing the messenger must remove its pointer.");
            Time.timeScale = 1;
            Require(bell.Runner.GetComponent<Civilian>().TryInfect(AssetDatabase.LoadAssetAtPath<Zombie>(InfectionSetup.PrefabPath), leader, 1),
                "Real infection must stop the messenger.");
            yield return 0.1f;
            notices.GetIndicators(camera, new HUDLayout(Screen.width, Screen.height), list);
            Require(notices.CurrentKey == "prevented" && list.Count == 0, "Preventing alarm replaces its notice and removes its pointer.");
            float remaining = notices.CurrentRemaining;
            Time.timeScale = 0;
            yield return 0.2f;
            Require(notices.CurrentRemaining == remaining, "Pause must freeze transient notice reading time.");
            Time.timeScale = 1;
            yield return 4.1f;
            Require(!notices.HasMessage, "Resolved alarm confirmation must expire rather than occupy HUD forever.");

            // Accelerated real waves arrive under a higher-priority live warning, then merge in its queue.
            var assault = manager.Farm.GetComponent<FarmAssault>();
            var settings = new SerializedObject(assault);
            var waves = settings.FindProperty("waves");
            for (int i = 0; i < waves.arraySize; i++) waves.GetArrayElementAtIndex(i).FindPropertyRelative("arrivalSeconds").floatValue = 0.2f + i * 0.2f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            assault.WaveArrived += _ => {
                foreach (var guard in assault.Reinforcements.GetComponentsInChildren<Guard>()) guard.enabled = false;
            };
            leader.enabled = true;
            leader.transform.position = manager.Farm.transform.position;
            manager.Farm.TryBeginFinalPhase(leader);
            // Keep the leader enabled for the real assault clock, but stop its route.
            leader.BeginRoute(manager.Farm.transform.position, new[] { manager.Farm.transform });
            camera.transform.position = originalCamera;
            yield return 0.1f;
            Require(assault.HasPendingWave && notices.CurrentKey == "wave" && notices.PendingCount == 2,
                "An imminent wave must take priority while assault start waits in the same panel.");
            Time.timeScale = 0;
            camera.transform.position += camera.transform.right * 100;
            notices.GetIndicators(camera, new HUDLayout(Screen.width, Screen.height), list);
            Require(list.Count > 0 && list[0].Text == "Новая волна", "Pending offscreen wave must indicate its arrival location.");
            capture = Capture("notifications-wave", 1280, 720);
            while (capture.MoveNext()) yield return capture.Current;
            camera.transform.position = originalCamera;
            Time.timeScale = 1;
            yield return 0.8f;
            Require(assault.WavesSpawned == 3 && notices.CurrentKey == "assault" && notices.PendingCount == 2,
                "Assault start must be readable; repeated arrivals share one queued notice.");
            Time.timeScale = 0;
            capture = Capture("notifications-assault", 1280, 720);
            while (capture.MoveNext()) yield return capture.Current;
            Time.timeScale = 1;
            yield return 4.1f;
            Require(notices.CurrentKey == "arrival" && notices.CurrentCount == 3,
                "All three real arrivals must merge and retain their full reading time while queued.");
            notices.GetIndicators(camera, new HUDLayout(Screen.width, Screen.height), list);
            // Move the camera away to guarantee that living reinforcements are outside the frame.
            camera.transform.position += camera.transform.right * 100;
            notices.GetIndicators(camera, new HUDLayout(Screen.width, Screen.height), list);
            Require(list.Count > 0, "Live offscreen wave guards must retain a direction marker after arrival.");
            foreach (var combat in assault.Reinforcements.GetComponentsInChildren<UnitCombat>()) combat.TakeDamage(1000);
            yield return 0.1f;
            notices.GetIndicators(camera, new HUDLayout(Screen.width, Screen.height), list);
            Require(list.Count == 0, "Eliminating the last wave defender must remove its threat marker.");
            var layoutNow = new HUDLayout(Screen.width, Screen.height);
            Vector2 banner = new Vector2(layoutNow.Warning.center.x * layoutNow.Scale,
                Screen.height - layoutNow.Warning.center.y * layoutNow.Scale);
            Require(manager.GetComponent<GameHUD>().ContainsScreenPosition(banner) && AbilityPointer.IsBlocked(banner),
                "Unified warning panel must block world clicks and suppress reward labels.");
            notices.enabled = false;
            Require(!notices.HasMessage && notices.PendingCount == 0, "Disabling clears presentation state.");
            notices.enabled = true;
            yield return 0.1f;
            Require(!notices.HasMessage, "Re-enabling must not replay past waves or assault start.");
            leader.Combat.TakeDamage(1000);
            Require(!notices.HasMessage && notices.PendingCount == 0, "Defeat must hide all warnings immediately.");
            Require(manager.TryRestartLevel(), "Notification retry must succeed.");
            yield return 0.35f;
            Require(!notices && Object.FindFirstObjectByType<GameManager>().GetComponent<GameNotifications>().CurrentKey != "arrival",
                "Retry must discard the previous notification queue and pointers.");
            Debug.Log("PASS: runner urgency/deduplication, all-edge/behind-camera pointers at five sizes, visible/resolved removal, infection, pause/expiry, real assault and three merged arrivals, live guard removal, HUD blocking, disable/re-enable, defeat and retry.");
        }

        private static IEnumerator Capture(string name, int width, int height)
        {
            if (Application.isBatchMode) yield break;
            using (var view = new HUDChecks.GameViewResolution())
            {
                view.Set(width, height);
                yield return 0.25f;
                ScreenCapture.CaptureScreenshot("Logs/" + name + ".png");
                yield return 0.25f;
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
