using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class FeedbackChecks
    {
        public static IEnumerator Checks(GameManager manager, Zombie prefab)
        {
            var feedback = manager.GetComponent<GameFeedback>();
            Require(feedback, "The authored scene must include feedback UI.");
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) human.gameObject.SetActive(false);
            var leader = manager.Necromancer;
            leader.enabled = false;
            leader.transform.position = Vector3.zero;
            yield return 0.2f;
            Require(feedback.HordeSize == 1, "Horde HUD must initially include the living necromancer.");
            var humanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CivilianSetup.PrefabPath);
            var subject = Object.Instantiate(humanPrefab, new Vector3(-10, 0, -8), Quaternion.identity).GetComponent<Civilian>();
            Require(subject.TryInfect(prefab, leader, 0.5f), "Infection fixture must accept infection.");
            var glow = subject.GetComponentInChildren<MagicMotes>();
            Require(glow && glow.GetComponentInChildren<Light>() && glow.GetComponent<ParticleSystem>().isPlaying,
                "Pending infection must immediately show a green light and playing particles.");
            subject.gameObject.SetActive(false);
            yield return 0.1f;
            Require(!glow && subject.State == Civilian.BehaviourState.Wandering,
                "Cancelling infection must remove glow and restore the civilian state.");
            var energySettings = new SerializedObject(leader.Energy);
            energySettings.FindProperty("regenerationPerSecond").floatValue = 0;
            energySettings.ApplyModifiedPropertiesWithoutUndo();
            leader.Energy.TrySpend(20);
            subject.gameObject.SetActive(true);
            Require(subject.TryInfect(prefab, leader, 0.1f), "Conversion fixture must accept infection.");
            yield return 0.25f;
            var zombie = Object.FindFirstObjectByType<Zombie>();
            zombie.enabled = false;
            Require(feedback.HordeSize == 2 && feedback.RewardLabelCount == 1 && leader.Energy.Current == 85,
                "Conversion must update the horde and show a +5 reward for the actual gain.");
            Require(Object.FindObjectsByType<MagicMotes>(FindObjectsSortMode.None).Length > 0,
                "Completed conversion must emit a burst independent of the removed civilian.");
            float gained = 0;
            Action<float, Vector3> onReward = (amount, position) => gained += amount;
            leader.Energy.Rewarded += onReward;
            leader.Energy.Restore(14);
            var victim = Object.Instantiate(humanPrefab, new Vector3(-14, 0, -8), Quaternion.identity).GetComponent<Civilian>();
            victim.Combat.TakeDamage(1000);
            Require(gained == 1 && leader.Energy.Current == 100,
                "A reward near capacity must report only the actual clamped gain.");
            var fullVictim = Object.Instantiate(humanPrefab, new Vector3(-14, 0, -8), Quaternion.identity).GetComponent<Civilian>();
            fullVictim.Combat.TakeDamage(1000);
            Require(gained == 1, "No gain at full capacity must not emit a misleading popup.");
            leader.Energy.Rewarded -= onReward;
            zombie.Combat.TakeDamage(1000);
            yield return 0.2f;
            Require(feedback.HordeSize == 1, "Dead zombies must leave the horde counter.");
            var area = Object.Instantiate(Object.FindFirstObjectByType<ProtectionAbility>().AreaPrefab,
                new Vector3(0, 0, -6), Quaternion.identity);
            var arc = area.transform.Find("ProtectionTimeArc").GetComponent<LineRenderer>();
            int initialPoints = arc.positionCount;
            yield return 0.3f;
            Require(area.RemainingFraction < 1 && arc.positionCount < initialPoints,
                "Protection must display a shrinking time arc while keeping its fixed boundary.");
            float remaining = area.RemainingFraction;
            Time.timeScale = 0;
            yield return 0.2f;
            Require(area.RemainingFraction == remaining && arc.positionCount < initialPoints,
                "Pause must freeze the protection's visual clock.");
            Time.timeScale = 1;
            yield return 1.2f;
            Require(feedback.RewardLabelCount == 0, "Reward labels must expire after their short lifetime.");
            var farm = manager.Farm;
            var assault = farm.GetComponent<FarmAssault>();
            var settings = new SerializedObject(assault);
            settings.FindProperty("corruptionDuration").floatValue = 0.15f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Vector3 originalCamera = manager.LevelCamera.transform.localPosition;
            leader.enabled = true;
            leader.BeginRoute(farm.ArrivalPoint.position, new[] { farm.ArrivalPoint });
            Require(farm.TryBeginFinalPhase(leader), "Start the capture feedback fixture.");
            foreach (var guard in assault.Reinforcements.GetComponentsInChildren<Guard>()) guard.enabled = false;
            yield return 0.25f;
            Require(manager.IsVictorious && feedback.IsShaking && Time.timeScale == 0,
                "Capture must start a short camera shake despite victory pausing game time.");
            yield return 0.6f;
            Require(!feedback.IsShaking && Vector3.Distance(originalCamera, manager.LevelCamera.transform.localPosition) < 0.0001f,
                "Unscaled shake must finish and restore the exact camera origin without drift.");
            Require(manager.TryRestartLevel(), "Restart after the feedback scenario.");
            yield return 0.3f;
            var fresh = Object.FindFirstObjectByType<GameManager>().GetComponent<GameFeedback>();
            Require(fresh && fresh.RewardLabelCount == 0 && !fresh.IsShaking && fresh.HordeSize == 1
                && Object.FindObjectsByType<MagicMotes>(FindObjectsSortMode.None).Length == 0,
                "Retry must clear labels, visual effects, shake and horde state.");
            Debug.Log("PASS: infection glow/cancellation/conversion burst; actual capped energy popups; horde changes; protection arc/pause; unscaled capture shake restores camera; retry clears feedback.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
