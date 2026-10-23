using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class GraveyardChecks
    {
        public static IEnumerator Checks()
        {
            SceneManager.LoadSceneAsync(DemoLevelSetup.ScenePath);
            yield return 0.4f;
            var manager = Object.FindFirstObjectByType<GameManager>();
            var grave = Object.FindFirstObjectByType<Grave>();
            Require(grave && Object.FindObjectsByType<Grave>(FindObjectsSortMode.None).Length == 1,
                "The scene must have exactly one optional grave.");
            CrossbowChecks.DisableSceneArchers();
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) human.gameObject.SetActive(false);
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            var leader = manager.Necromancer;
            var before = leader.transform.position;
            yield return 0.4f;
            Require(!grave.IsConsumed && leader.transform.position != before,
                "Ignoring the optional grave must leave the autonomous route running.");
            leader.enabled = false;
            leader.transform.position = grave.transform.position + new Vector3(4, 0, 0);
            var settings = new SerializedObject(leader.Energy);
            settings.FindProperty("regenerationPerSecond").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            leader.Energy.TrySpend(leader.Energy.Current - 29);
            var ability = Object.FindFirstObjectByType<InfectionAbility>();
            Physics.SyncTransforms();
            Vector2 screen = manager.LevelCamera.WorldToScreenPoint(grave.transform.position + Vector3.up * 0.6f);
            Require(!ability.TryInfectAtScreenPosition(screen) && leader.Energy.Current == 29 && !grave.IsConsumed,
                "Insufficient energy must not consume the grave or spend energy.");
            leader.Energy.Restore(1);
            Time.timeScale = 0;
            Require(!ability.TryInfectAtScreenPosition(screen) && !grave.TryRaise(leader), "Pause must block raising.");
            Time.timeScale = 1;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var ray = manager.LevelCamera.ScreenPointToRay(screen);
            blocker.transform.position = ray.GetPoint(3);
            Physics.SyncTransforms();
            Require(!ability.TryInfectAtScreenPosition(screen) && leader.Energy.Current == 30 && !grave.IsConsumed,
                "A wall in front of the grave must block LMB without spending.");
            Object.Destroy(blocker);
            yield return 0.1f;
            Physics.SyncTransforms();
            Require(ability.TryInfectAtScreenPosition(screen) && grave.IsConsumed && leader.Energy.Current == 0,
                "Real camera-ray LMB at exactly 30 energy must raise the skeletons without a reward rebate.");
            Require(!grave.TryRaise(leader) && !ability.TryInfectAtScreenPosition(screen), "The grave is single-use.");
            var skeletons = Object.FindObjectsByType<Skeleton>(FindObjectsSortMode.None);
            Require(skeletons.Length == 3 && manager.InfectedCount == 0 && manager.GuardsKilled == 0,
                "Three purchased skeletons must not count as infected humans or guard kills.");
            foreach (var skeleton in skeletons)
            {
                var unit = skeleton.GetComponent<Zombie>();
                Require(unit.Leader == leader && unit.Combat.CurrentHealth == 25 && unit.Combat.Damage == 5,
                    "Weak skeletons must join the actual horde.");
            }
            Capture(manager.LevelCamera, grave.transform.position);
            yield return 3f;
            Require(manager.GetComponent<GameFeedback>().HordeSize == 4, "HUD must count skeletons and their leader.");
            foreach (var skeleton in skeletons)
                Require(Vector3.Distance(skeleton.transform.position, leader.transform.position) < 3,
                    "Skeletons must autonomously follow their necromancer.");
            var fighter = skeletons[0].GetComponent<Zombie>();
            var defender = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath),
                fighter.transform.position + Vector3.right * 0.6f, Quaternion.identity).GetComponent<Guard>();
            settings = new SerializedObject(defender.Combat);
            settings.FindProperty("damage").intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            yield return 1.3f;
            Require(defender.Combat.CurrentHealth < defender.Combat.MaxHealth, "Skeletons must actually attack defenders.");
            Debug.Log("PASS: optional route, 29/30 energy, pause, wall, real LMB, single-use grave, no false rewards/statistics, horde HUD, following and combat.");
            leader.Combat.TakeDamage(1000);
            Require(manager.IsDefeated && manager.TryRestartLevel(), "Defeat/retry must work with grave reinforcements.");
            yield return 0.5f;
            grave = Object.FindFirstObjectByType<Grave>();
            Require(grave && !grave.IsConsumed && Object.FindObjectsByType<Skeleton>(FindObjectsSortMode.None).Length == 0,
                "Retry must restore the grave and remove the old skeletons.");
            Debug.Log("PASS: retry resets the grave and removes summoned skeletons.");
        }

        private static void Capture(Camera source, Vector3 center)
        {
            var camera = new GameObject("GraveyardCheckCamera").AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographicSize = 4;
                camera.transform.position = center + new Vector3(0, 12, -8);
                camera.transform.LookAt(center);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/graveyard.png", texture.EncodeToPNG());
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
