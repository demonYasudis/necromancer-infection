using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    [InitializeOnLoad]
    public static class ProgressionChecks
    {
        private const string Key = "NecromancerInfection.ProgressionChecks";
        private const string BatchKey = Key + ".Batch";
        private const string PreviewKey = Key + ".Preview";
        private static IEnumerator routine;
        private static double next, deadline;
        private static string error;
        static ProgressionChecks() => EditorApplication.update += Tick;

        [MenuItem("Necromancer Infection/Run Progression Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before running checks.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(BatchKey, true);
            Run();
        }

        public static void PreviewBatch()
        {
            SessionState.SetBool(PreviewKey, true);
            var view = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            view.Show();
            view.Focus();
            RunBatch();
        }

        private static void Watch(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = message; }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || EditorApplication.isCompiling) return;
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (routine == null)
                {
                    routine = Checks();
                    deadline = EditorApplication.timeSinceStartup + 120;
                    Application.logMessageReceived += Watch;
                }
                Require(error == null, error);
                Require(EditorApplication.timeSinceStartup < deadline, "Progression checks timed out");
                if (EditorApplication.timeSinceStartup < next) return;
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
                else Finish(true, SessionState.GetBool(PreviewKey, false)
                    ? "Progression Game View preview rendered."
                    : "Progression and onboarding checks passed on both levels.");
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(Key, false);
            SessionState.SetBool(PreviewKey, false);
            Application.logMessageReceived -= Watch;
            routine = null;
            error = null;
            next = 0;
            bool batch = SessionState.GetBool(BatchKey, false);
            SessionState.SetBool(BatchKey, false);
            if (success) Debug.Log("PASS: " + message);
            else Debug.LogError("FAIL: " + message);
            if (batch) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        public static IEnumerator Checks()
        {
            foreach (string scene in new[] { DemoLevelSetup.ScenePath, JourneySetup.ScenePath })
            {
                SceneManager.LoadSceneAsync(scene);
                yield return .6;
                var manager = Object.FindFirstObjectByType<GameManager>();
                var p = manager.GetComponent<SoulProgression>();
                var guide = manager.GetComponent<OnboardingGuide>();
                Require(p && p.Level == 1 && p.Experience == 0 && guide && guide.Step == 0, "Fresh attempt state");
                manager.Necromancer.enabled = false;
                foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.enabled = false;
                foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) human.enabled = false;
                var infection = Object.FindFirstObjectByType<InfectionAbility>();
                Civilian subject = null;
                foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None))
                {
                    human.enabled = true;
                    var screen = manager.LevelCamera.WorldToScreenPoint(human.transform.position + Vector3.up * .8f);
                    if (screen.z > 0 && infection.TryGetTarget(screen, out var target, out _, out _, out var reason)
                        && target == human.transform && reason == null && infection.TryInfectAtScreenPosition(screen))
                    { subject = human; subject.enabled = true; break; }
                    human.enabled = false;
                }
                Require(subject && guide.Step == 1, "Real paid infection advances onboarding");
                Time.timeScale = 0;
                yield return .15;
                Require(guide.Step == 1 && p.Experience == 0, "Pause freezes tutorial and delayed conversion");
                Time.timeScale = 1;
                yield return infection.InfectionDelay + .15;
                Require(p.Experience == 1 && manager.InfectedCount == 1, "One actual conversion grants one XP");
                foreach (var zombie in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None)) zombie.enabled = false;
                Time.timeScale = 8;
                yield return .85;
                Time.timeScale = 1;
                Require(guide.Step == 2, "Explanation completes in game time");
                var nearbyGuard = Object.FindFirstObjectByType<Guard>();
                nearbyGuard.transform.position = manager.Necromancer.transform.position + Vector3.forward * 12;
                Physics.SyncTransforms();
                yield return .15;
                Require(guide.IsVisible, "Nearby guard triggers protection hint");
                var shieldAbility = Object.FindFirstObjectByType<ProtectionAbility>();
                bool shieldCast = false;
                for (int y = 3; y <= 6 && !shieldCast; y++)
                    for (int x = 1; x <= 9 && !shieldCast; x++)
                        shieldCast = shieldAbility.TryCastAtScreenPosition(new Vector2(Screen.width * x / 10f, Screen.height * y / 10f));
                Require(shieldCast && guide.IsComplete && !guide.IsVisible, "Actual paid protection completes tutorial");
                foreach (string path in new[] { "Assets/Prefabs/Skeleton.prefab", "Assets/Prefabs/RaisedGuard.prefab" })
                {
                    var reinforcement = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),
                        manager.Necromancer.transform.position + Vector3.right * 50, Quaternion.identity).GetComponent<Zombie>();
                    reinforcement.Initialize(manager.Necromancer); reinforcement.enabled = false;
                }
                Require(p.Experience == 1, "Summoned skeletons and raised guards cannot farm experience");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Zombie.prefab");
                for (int i = 0; i < 3; i++)
                {
                    var zombie = Object.Instantiate(prefab, manager.Necromancer.transform.position + Vector3.right * (3 + i), Quaternion.identity).GetComponent<Zombie>();
                    zombie.Initialize(manager.Necromancer); zombie.Initialize(manager.Necromancer); zombie.enabled = false;
                }
                yield return .15;
                Require(p.IsChoosing && p.Level == 2 && p.Experience == 0 && Time.timeScale == 0, "Four unique conversions open a paused choice");
                Require(p.Offers.Count == 3 && p.Offers[0] != p.Offers[1] && p.Offers[0] != p.Offers[2] && p.Offers[1] != p.Offers[2], "Three distinct cards");
                Require(!guide.IsVisible && AbilityPointer.IsBlocked(new Vector2(Screen.width / 2, Screen.height / 2)), "Modal hides tutorial and blocks abilities");
                float elapsed = (float)manager.ElapsedSeconds;
                float frozenEnergy = manager.Necromancer.Energy.Current;
                yield return .2;
                Require(Mathf.Abs((float)manager.ElapsedSeconds - elapsed) < .001f, "Choice freezes attempt timer");
                Require(manager.Necromancer.Energy.Current == frozenEnergy, "Choice freezes energy regeneration");
                if (!Application.isBatchMode)
                {
                    Directory.CreateDirectory("Previews");
                    using (var view = new HUDChecks.GameViewResolution())
                    {
                        view.Set(1280, 720);
                        yield return .25;
                        ScreenCapture.CaptureScreenshot("Previews/level-up-cards.png");
                        yield return .25;
                        view.Set(320, 568);
                        yield return .25;
                        ScreenCapture.CaptureScreenshot("Previews/level-up-cards-portrait.png");
                        yield return .25;
                    }
                }
                Require(!p.TryChoose(-1) && !p.TryChoose(3), "Invalid choices do nothing");
                var selected = p.Offers[0];
                float capacity = manager.Necromancer.Energy.Maximum;
                float regen = manager.Necromancer.Energy.Regeneration;
                float infectionCost = infection.InfectionCost;
                var protection = Object.FindFirstObjectByType<ProtectionAbility>();
                float protectionCost = protection.EnergyCost;
                Require(p.TryChoose(0) && !p.TryChoose(0) && !p.IsChoosing && Time.timeScale == 1 && p.Rank(selected) == 1, "Exactly one upgrade and resume");
                Require(manager.Necromancer.Energy.Maximum == capacity + (selected == SoulProgression.Upgrade.EnergyCapacity ? 20 : 0), "Capacity effect");
                Require(manager.Necromancer.Energy.Regeneration == regen + (selected == SoulProgression.Upgrade.Regeneration ? .5f : 0), "Regeneration effect");
                Require(Mathf.Approximately(infection.InfectionCost, infectionCost * (selected == SoulProgression.Upgrade.InfectionCost ? .8f : 1)), "Infection cost effect");
                Require(Mathf.Approximately(protection.EnergyCost, protectionCost * (selected == SoulProgression.Upgrade.ProtectionCost ? .8f : 1)), "Protection cost effect");
                var enemy = Object.FindFirstObjectByType<Guard>();
                Require(enemy.Combat.TakeDamage(10000), "Real guard death");
                Require(p.Experience == 3, "Guard death gives three XP");
                p.AddExperience(21);
                Time.timeScale = 0;
                yield return .15;
                Require(!p.IsChoosing && p.Level == 2, "External pause cannot be stolen");
                Time.timeScale = .5f;
                yield return .15;
                Require(p.IsChoosing && p.Level == 3 && p.Experience == 17, "Overflow survives level threshold");
                p.enabled = false;
                Require(Time.timeScale == .5f && !p.IsChoosing, "Disabling releases owned pause");
                p.enabled = true;
                Time.timeScale = 1;
                yield return .15;
                Require(p.IsChoosing && p.Level == 3 && p.Experience == 17, "Re-enabling preserves unchosen level");
                Require(p.TryChoose(0), "Choose preserved offer");
                yield return .15;
                Require(p.IsChoosing && p.Level == 4 && p.Experience == 7, "Queued next level");
                manager.SendMessage("OnNecromancerDied", manager.Necromancer.Combat);
                yield return .15;
                Require(!p.IsChoosing && Time.timeScale == 0, "Defeat cancels choice without unpausing");
                Require(manager.TryRestartLevel(), "Restart from defeat");
                yield return .7;
                var fresh = Object.FindFirstObjectByType<GameManager>();
                p = fresh.GetComponent<SoulProgression>();
                Require(fresh != manager && p.Level == 1 && p.Experience == 0 && !p.IsChoosing && Time.timeScale == 1, "Restart clears choices and XP");
                Require(fresh.Necromancer.Energy.Maximum == capacity && fresh.Necromancer.Energy.Regeneration == regen, "Restart clears stats");
                Require(fresh.GetComponent<OnboardingGuide>().Step == 0, "Restart resets tutorial");
                fresh.GetComponent<OnboardingGuide>().Skip();
                Require(fresh.GetComponent<OnboardingGuide>().IsComplete, "Tutorial can be skipped");
                foreach (var size in new[] { new Vector2(320, 568), new Vector2(1280, 720), new Vector2(2560, 1080) })
                {
                    bool vertical = size.x < size.y;
                    float scale = vertical ? Mathf.Min(size.x / 540, size.y / 720) : Mathf.Min(size.x / 1140, size.y / 720);
                    float w = size.x / scale, h = size.y / scale;
                    Rect previous = default;
                    for (int i = 0; i < 3; i++)
                    {
                        Rect card = ProgressionScreen.CardBounds(i, w, h);
                        Require(card.xMin >= 0 && card.yMin >= 140 && card.xMax <= w && card.yMax <= h, "Cards fit screen");
                        Require(i == 0 || !previous.Overlaps(card), "Cards never overlap"); previous = card;
                    }
                }
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
