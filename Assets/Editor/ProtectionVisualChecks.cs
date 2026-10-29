using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class ProtectionVisualChecks
    {
        public static IEnumerator Checks()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (!manager.IsFinished) manager.Necromancer.Combat.TakeDamage(1000);
            Require(manager.TryRestartLevel(), "Protection visuals require a fresh attempt.");
            yield return 0.35f;
            manager = Object.FindFirstObjectByType<GameManager>();
            var leader = manager.Necromancer;
            leader.enabled = false;
            var center = new Vector3(-8, 0, 0);
            leader.transform.position = center + Vector3.forward;
            CrossbowChecks.DisableSceneArchers();
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) human.gameObject.SetActive(false);
            var ability = Object.FindFirstObjectByType<ProtectionAbility>();
            Object.FindFirstObjectByType<AbilityPreview>().enabled = false;
            leader.Energy.Restore(100);
            Physics.SyncTransforms();
            Require(ability.TryCastAtScreenPosition(manager.LevelCamera.WorldToScreenPoint(center)), "Real RMB must cast protection.");
            var area = Object.FindFirstObjectByType<ProtectionArea>();
            Require(area.RevealProgress < 1 && ProtectionArea.IncomingDamageMultiplier(leader.Combat) == 0.5f,
                "Protection must work at full radius while runes are still opening.");
            var zombie = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Zombie>(InfectionSetup.PrefabPath), center, Quaternion.identity);
            zombie.Initialize(leader); zombie.enabled = false;
            var skeleton = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GraveyardSetup.SkeletonPath), center + Vector3.left, Quaternion.identity);
            skeleton.GetComponent<Zombie>().enabled = false;
            var heavy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RaisedGuardSetup.UndeadPath), center + Vector3.right, Quaternion.identity);
            heavy.GetComponent<Zombie>().enabled = false;
            var humanUnit = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CivilianSetup.PrefabPath), center + Vector3.back, Quaternion.identity);
            humanUnit.GetComponent<Civilian>().enabled = false;
            yield return 0.1f;
            var marker = zombie.GetComponent<ProtectionMarker>();
            Require(marker.IsVisible && leader.GetComponent<ProtectionMarker>().IsVisible
                && skeleton.GetComponent<ProtectionMarker>().IsVisible && heavy.GetComponent<ProtectionMarker>().IsVisible
                && !humanUnit.GetComponent<ProtectionMarker>().IsVisible, "Only actually protected units show shields, including both raised types.");
            Capture(manager.LevelCamera, center, "protection-opening");
            zombie.transform.position = center + Vector3.right * (area.Radius + 0.02f);
            yield return 0.05f;
            Require(!marker.IsVisible && ProtectionArea.IncomingDamageMultiplier(zombie.Combat) == 1, "Exit removes shield and reduction together.");
            zombie.transform.position = center + Vector3.right * area.Radius;
            yield return 0.05f;
            Require(marker.IsVisible, "Exact boundary remains protected.");
            zombie.transform.position = center;
            area.enabled = false;
            yield return 0.05f;
            Require(!marker.IsVisible && !area.GetComponent<LineRenderer>().enabled
                && !area.GetComponent<MeshRenderer>().enabled, "Disabling area hides its effects and shield.");
            area.enabled = true;
            yield return 0.15f;
            Require(area.RevealProgress == 1 && marker.IsVisible, "Reactivation uses original lifetime without replaying opening.");
            Time.timeScale = 0;
            float remaining = area.RemainingFraction;
            var rune = area.transform.Find("ProtectionRune_0");
            Vector3 scale = rune.localScale;
            var arc = area.transform.Find("ProtectionTimeArc").GetComponent<LineRenderer>();
            int count = arc.positionCount;
            yield return 0.3f;
            Require(area.RemainingFraction == remaining && rune.localScale == scale && arc.positionCount == count && marker.IsVisible,
                "Pause freezes runes, countdown, lifetime and protection.");
            Time.timeScale = 1;
            Capture(manager.LevelCamera, center, "protection-active");
            yield return area.Duration * area.RemainingFraction - 0.5f;
            Require(area.VisualStrength < 0.65f && area.VisualStrength > 0 && arc.positionCount < count
                && marker.IsVisible, "Final second fades and countdown shrinks while protection remains effective.");
            Capture(manager.LevelCamera, center, "protection-expiring");
            var overlap = Object.Instantiate(ability.AreaPrefab, center, Quaternion.identity);
            yield return 0.65f;
            Require(!area && marker.IsVisible && ProtectionArea.IncomingDamageMultiplier(zombie.Combat) == 0.5f,
                "A second area keeps a single shield and non-stacking protection after first expiry.");
            Object.Destroy(overlap.gameObject);
            yield return 0.05f;
            Require(!marker.IsVisible && ProtectionArea.IncomingDamageMultiplier(zombie.Combat) == 1, "Last area removal clears the shield.");
            leader.Combat.TakeDamage(1000);
            Require(manager.TryRestartLevel(), "Protection check retry must succeed.");
            yield return 0.35f;
            Require(!zombie && !heavy && !skeleton && !humanUnit
                && Object.FindObjectsByType<ProtectionArea>(FindObjectsSortMode.None).Length == 0,
                "Retry removes all previous areas and unit shields.");
            Debug.Log("PASS: protection opening, full-radius damage, all undead shields, living exclusion, exit/boundary, disable, pause, fading countdown, overlap, expiry and clean retry.");
        }

        private static void Capture(Camera source, Vector3 center, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = new GameObject("ProtectionCheckCamera").AddComponent<Camera>();
            camera.CopyFrom(source); camera.enabled = false;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographicSize = 4.5f;
                camera.transform.rotation = source.transform.rotation;
                camera.transform.position = center + Vector3.up * 0.4f - source.transform.forward * 12;
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                File.WriteAllBytes("Logs/" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; target.Release();
                Object.Destroy(target); Object.Destroy(texture); Object.Destroy(camera.gameObject);
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
