using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class HUDChecks
    {
        public static IEnumerator Checks()
        {
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080),
                new Vector2(1024, 768), new Vector2(800, 600), new Vector2(640, 360), new Vector2(480, 800), new Vector2(320, 568) })
            {
                var layout = new HUDLayout(size.x, size.y);
                var rectangles = new[] { layout.Status, layout.Horde, layout.Objective, layout.Abilities, layout.Warning };
                var viewport = new Rect(0, 0, layout.Width, layout.Height);
                for (int i = 0; i < rectangles.Length; i++)
                {
                    Require(viewport.Contains(rectangles[i].min) && viewport.Contains(rectangles[i].max), "All HUD panels must fit " + size);
                    for (int j = i + 1; j < rectangles.Length; j++)
                        Require(!rectangles[i].Overlaps(rectangles[j]), "HUD panels must not overlap at " + size);
                    Vector2 screen = new Vector2(rectangles[i].center.x * layout.Scale, size.y - rectangles[i].center.y * layout.Scale);
                    Require(layout.Contains(layout.FromScreen(screen, size.y), true), "Pointer mapping must match panel drawing at " + size);
                }
                Require(layout.PlayBottom - layout.PlayTop >= 68, "Layout must leave room for the ability tooltip at " + size);
            }
            Debug.Log("PASS: HUD layouts and pointer mapping at 16:9, ultrawide, 4:3, small windows and portrait; panels fit without overlap.");
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (!manager.IsFinished) manager.Necromancer.Combat.TakeDamage(1000);
            Require(manager.TryRestartLevel(), "HUD checks require a fresh attempt.");
            yield return 0.35f;
            manager = Object.FindFirstObjectByType<GameManager>();
            var hud = manager.GetComponent<GameHUD>();
            var leader = manager.Necromancer;
            leader.enabled = false;
            CrossbowChecks.DisableSceneArchers();
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) human.gameObject.SetActive(false);
            var settings = new SerializedObject(leader.Energy);
            settings.FindProperty("regenerationPerSecond").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Require(hud && hud.IsVisible && hud.DisplayedHealth == 100 && hud.DisplayedEnergy == 100,
                "Fresh HUD must start at full values without filling from zero.");
            leader.Combat.TakeDamage(35);
            leader.Energy.TrySpend(40);
            Require(hud.Health == 65 && hud.Energy == 60 && hud.DisplayedHealth == 100 && hud.DisplayedEnergy == 100,
                "Actual values update immediately while visual bars retain their starting values.");
            yield return 0.08f;
            Require(hud.DisplayedHealth > 65 && hud.DisplayedHealth < 100 && hud.DisplayedEnergy > 60 && hud.DisplayedEnergy < 100,
                "Both bars must smoothly interpolate toward the actual values.");
            yield return 0.9f;
            Require(Mathf.Abs(hud.DisplayedHealth - 65) < 0.1f && Mathf.Abs(hud.DisplayedEnergy - 60) < 0.1f,
                "Smooth bars must converge promptly.");
            leader.Energy.Restore(30);
            yield return 0.08f;
            Require(hud.DisplayedEnergy > 60 && hud.DisplayedEnergy < 90 && hud.Energy == 90, "Restoration must animate upward too.");
            var feedback = manager.GetComponent<GameFeedback>();
            var units = new Zombie[3];
            string[] names = { "Zombie", "RaisedGuard", "Skeleton" };
            for (int i = 0; i < 3; i++)
            {
                units[i] = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Zombie>("Assets/Prefabs/" + names[i] + ".prefab"),
                    leader.transform.position + Vector3.right * (i + 1), Quaternion.identity);
                units[i].Initialize(leader); units[i].enabled = false;
            }
            yield return 0.2f;
            Require(feedback.ZombieCount == 1 && feedback.HeavyCount == 1 && feedback.SkeletonCount == 1 && feedback.HordeSize == 4,
                "Horde composition must distinguish ordinary, heavy and skeletal undead and count the leader once.");
            units[2].Combat.TakeDamage(1000);
            units[0].gameObject.SetActive(false);
            yield return 0.2f;
            Require(feedback.ZombieCount == 0 && feedback.SkeletonCount == 0 && feedback.HeavyCount == 1 && feedback.HordeSize == 2,
                "Dead or inactive members must leave their correct category.");
            units[0].gameObject.SetActive(true);
            yield return 0.2f;
            var infection = Object.FindFirstObjectByType<InfectionAbility>();
            var protection = Object.FindFirstObjectByType<ProtectionAbility>();
            var current = hud.Layout;
            foreach (var panel in new[] { current.Status, current.Horde, current.Objective, current.Abilities })
            {
                Vector2 point = new Vector2(panel.center.x * current.Scale, Screen.height - panel.center.y * current.Scale);
                float before = leader.Energy.Current;
                Require(AbilityPointer.IsBlocked(point) && !infection.TryInfectAtScreenPosition(point)
                    && !protection.TryCastAtScreenPosition(point) && leader.Energy.Current == before,
                    "Every panel must block both abilities without spending energy.");
            }
            Vector2 free = new Vector2(12, Screen.height / 2f);
            Require(!AbilityPointer.IsBlocked(free), "HUD must leave the play area clickable.");
            leader.Combat.TakeDamage(40);
            leader.Energy.TrySpend(leader.Energy.Current);
            yield return 0.9f;
            if (!Application.isBatchMode)
            using (var view = new GameViewResolution())
            {
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(640, 360), new Vector2Int(320, 568) })
                {
                    view.Set(size.x, size.y);
                    yield return 0.35f;
                    Require(Screen.width == size.x && Screen.height == size.y,
                        $"Game View must use the requested QA resolution {size}; actual {Screen.width}x{Screen.height}.");
                    ScreenCapture.CaptureScreenshot("Logs/hud-" + size.x + "x" + size.y + ".png");
                    yield return 0.35f;
                    Require(File.Exists("Logs/hud-" + size.x + "x" + size.y + ".png"), "Rendered HUD screenshot must be saved.");
                }
            }
            Time.timeScale = 0;
            float health = leader.CurrentHealth;
            Require(hud.IsVisible && !infection.TryInfectAtScreenPosition(free) && !protection.TryCastAtScreenPosition(free),
                "Pause keeps HUD readable while gameplay abilities stay blocked.");
            yield return 0.1f;
            Require(leader.CurrentHealth == health && hud.Energy == 0, "Presentation must not change game resources while paused.");
            Time.timeScale = 1;
            leader.transform.position = manager.Farm.ArrivalPoint.position;
            leader.enabled = true;
            Require(manager.Farm.TryBeginFinalPhase(leader) && manager.FinalPhase && hud.IsVisible,
                "The unified objective must support the real final assault.");
            Time.timeScale = 0;
            if (!Application.isBatchMode) ScreenCapture.CaptureScreenshot("Logs/hud-final-assault.png");
            yield return 0.35f;
            Time.timeScale = 1;
            leader.Combat.TakeDamage(1000);
            Require(manager.IsDefeated && !hud.IsVisible, "End screen must replace the gameplay HUD.");
            Require(manager.TryRestartLevel(), "HUD checks must support retry.");
            yield return 0.35f;
            var fresh = Object.FindFirstObjectByType<GameManager>().GetComponent<GameHUD>();
            Require(fresh && fresh != hud && fresh.IsVisible && fresh.DisplayedHealth == 100 && fresh.DisplayedEnergy == 100,
                "Retry must reset smooth values and remove the previous HUD instance.");
            Debug.Log("PASS: unified HUD health/energy smoothing, live horde composition, panel click blocking, "
                + (Application.isBatchMode ? "layout coverage" : "four rendered resolutions") + ", pause, final assault and clean retry.");
        }

        // Scope custom Game View sizes to this check and restore the user's selection afterwards.
        internal sealed class GameViewResolution : IDisposable
        {
            private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            private readonly EditorWindow window;
            private readonly object group;
            private readonly PropertyInfo selected;
            private readonly int previous;
            private int added;
            public GameViewResolution()
            {
                var assembly = typeof(UnityEditor.Editor).Assembly;
                window = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
                selected = window.GetType().GetProperty("selectedSizeIndex", Flags);
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
                var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
                group = sizesType.GetMethod("GetGroup").Invoke(singleton, new[] { Enum.Parse(groupType, "Standalone") });
                int builtins = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
                int restore = (int)selected.GetValue(window);
                for (int i = total - 1; i >= builtins; i--)
                {
                    var size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    if ((string)size.GetType().GetProperty("baseText", Flags).GetValue(size) != "HUD QA") continue;
                    if (restore >= i) restore = Mathf.Max(0, restore - 1);
                    group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { i });
                }
                previous = restore;
                selected.SetValue(window, previous);
            }
            public void Set(int width, int height)
            {
                var assembly = typeof(UnityEditor.Editor).Assembly;
                var type = assembly.GetType("UnityEditor.GameViewSize");
                var kind = assembly.GetType("UnityEditor.GameViewSizeType");
                var size = Activator.CreateInstance(type, new[] { Enum.Parse(kind, "FixedResolution"), (object)width, height, "HUD QA" });
                int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size }); added++;
                selected.SetValue(window, index); window.Focus(); window.Repaint();
                EditorApplication.QueuePlayerLoopUpdate();
            }
            public void Dispose()
            {
                selected.SetValue(window, previous);
                while (added-- > 0)
                {
                    int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
                    group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { count - 1 });
                }
                window.Repaint();
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
