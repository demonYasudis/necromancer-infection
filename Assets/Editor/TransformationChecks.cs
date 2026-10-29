using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class TransformationChecks
    {
        public static IEnumerator Checks()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (!manager.IsFinished) manager.Necromancer.Combat.TakeDamage(1000);
            Require(manager.TryRestartLevel(), "Transformation checks require a fresh attempt.");
            yield return 0.35f;
            manager = Object.FindFirstObjectByType<GameManager>();
            var leader = manager.Necromancer;
            leader.enabled = false;
            leader.transform.position = new Vector3(0, 0, -2);
            CrossbowChecks.DisableSceneArchers();
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var civilian in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) civilian.gameObject.SetActive(false);
            var settings = new SerializedObject(leader.Energy);
            settings.FindProperty("regenerationPerSecond").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var ability = Object.FindFirstObjectByType<InfectionAbility>();
            var prefab = AssetDatabase.LoadAssetAtPath<Zombie>(InfectionSetup.PrefabPath);
            var human = Human(new Vector3(-1, 0, 0));
            var neighbour = Human(new Vector3(1.2f, 0, 0));
            yield return 0.1f;
            var head = human.transform.Find("Head");
            Vector3 rest = head.localPosition;
            Quaternion restRotation = head.localRotation;
            Vector3 neighbourHead = neighbour.transform.Find("Head").localPosition;
            Vector3 root = human.transform.position;
            var collider = human.GetComponent<Collider>();
            Physics.SyncTransforms();
            Bounds originalBounds = collider.bounds;
            Vector2 screen = manager.LevelCamera.WorldToScreenPoint(root + Vector3.up * 0.8f);
            Require(ability.TryInfectAtScreenPosition(screen) && leader.Energy.Current == 85,
                "A real paid infection must start the bending animation at the existing price.");
            var motion = human.GetComponent<CombatMotion>();
            Require(motion.CurrentTransformation == CombatMotion.Transformation.Infection
                && !ability.TryInfectAtScreenPosition(screen), "The animation must not allow a second infection.");
            yield return ability.InfectionDelay * 0.45f;
            Physics.SyncTransforms();
            Require(head.localPosition.y < rest.y && head.localRotation != restRotation
                && human.transform.position == root && collider.bounds == originalBounds,
                "The infected model must bend while its navigation root and click collider stay fixed.");
            Require(neighbour.transform.Find("Head").localPosition == neighbourHead,
                "The transformation must not deform a neighbouring target.");
            Capture(manager.LevelCamera, root, "transformation-infected");
            Time.timeScale = 0;
            float progress = motion.TransformationProgress;
            Vector3 frozen = head.localPosition;
            yield return 0.2f;
            Require(motion.TransformationProgress == progress && head.localPosition == frozen
                && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 0,
                "Pause freezes bending and prevents conversion.");
            Time.timeScale = 1;
            yield return ability.InfectionDelay * 0.65f;
            var zombies = Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None);
            Require(!human && zombies.Length == 1 && manager.InfectedCount == 1 && leader.Energy.Current == 90,
                "One finished animation must create exactly one zombie and one +5 reward.");
            var zombie = zombies[0]; zombie.enabled = false;
            motion = zombie.GetComponent<CombatMotion>();
            Require(motion.CurrentTransformation == CombatMotion.Transformation.ZombieRise && motion.TransformationProgress < 1,
                "The new zombie must straighten from the infected pose.");
            progress = motion.TransformationProgress;
            motion.BeginRising();
            Require(motion.TransformationProgress == progress, "A repeated request during rising must not restart the animation.");
            Capture(manager.LevelCamera, zombie.transform.position, "transformation-zombie");
            yield return 0.6f;
            Require(motion.CurrentTransformation == CombatMotion.Transformation.None && manager.InfectedCount == 1
                && leader.Energy.Current == 90, "Finishing the visual rise must not duplicate a conversion or a reward.");

            var cancelled = Human(new Vector3(0, 0, 2));
            var cancelledHead = cancelled.transform.Find("Head");
            Vector3 cancelledRest = cancelledHead.localPosition;
            Require(cancelled.TryInfect(prefab, leader, 0.8f), "Cancellation setup must infect a civilian.");
            yield return 0.2f;
            cancelled.gameObject.SetActive(false);
            Require(cancelledHead.localPosition == cancelledRest
                && cancelled.GetComponent<CombatMotion>().CurrentTransformation == CombatMotion.Transformation.None,
                "Disabling infection must restore the model immediately.");
            cancelled.gameObject.SetActive(true);
            yield return 0.9f;
            Require(Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 1 && manager.InfectedCount == 1,
                "Cancelled animation cannot complete a ghost conversion after reactivation.");
            Require(cancelled.TryInfect(prefab, leader, 0.7f), "Death race setup must infect again.");
            cancelled.Combat.TakeDamage(30, zombie.Combat);
            yield return 0.1f;
            zombies = Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None);
            foreach (var unit in zombies) unit.enabled = false;
            Require(!cancelled && zombies.Length == 2 && manager.InfectedCount == 2 && leader.Energy.Current == 98,
                "A zombie kill during infection must give exactly one chain conversion and the existing +3/+5 rewards.");
            yield return 0.8f;
            Require(Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 2 && leader.Energy.Current == 98,
                "The cancelled infection timer must not create another zombie after the death animation.");

            leader.Energy.Restore(100);
            var defender = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath),
                new Vector3(-3, 0, 0), Quaternion.identity).GetComponent<Guard>();
            defender.enabled = false; defender.Combat.TakeDamage(1000);
            yield return 0.05f;
            var corpse = Object.FindFirstObjectByType<GuardCorpse>();
            Require(corpse && corpse.TryRaise(leader, 35) && !corpse.TryRaise(leader, 35),
                "A real corpse must create one heavy and reject reuse.");
            var heavy = Object.FindFirstObjectByType<RaisedGuard>();
            heavy.GetComponent<Zombie>().enabled = false;
            var heavyMotion = heavy.GetComponent<CombatMotion>();
            var helmet = heavy.transform.Find("Helmet");
            float standingHeight = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RaisedGuard.prefab")
                .transform.Find("Helmet").localPosition.y;
            Require(heavyMotion.CurrentTransformation == CombatMotion.Transformation.HeavyRise && helmet.localPosition.y < 0.6f
                && leader.Energy.Current == 65 && manager.InfectedCount == 2 && manager.GuardsKilled == 1,
                "The heavy starts on the ground without a conversion rebate or false infection statistic.");
            yield return 0.18f;
            Capture(manager.LevelCamera, heavy.transform.position, "transformation-heavy");
            Time.timeScale = 0;
            frozen = helmet.localPosition; progress = heavyMotion.TransformationProgress;
            yield return 0.2f;
            Require(helmet.localPosition == frozen && heavyMotion.TransformationProgress == progress, "Pause must freeze the heavy getting up.");
            Time.timeScale = 1;
            yield return 0.65f;
            Require(!corpse && heavyMotion.CurrentTransformation == CombatMotion.Transformation.None
                && Mathf.Abs(helmet.localPosition.y - standingHeight) < 0.001f && leader.Energy.Current == 65,
                "Heavy rise ends in the authored standing pose without any extra unit or reward.");
            Capture(manager.LevelCamera, heavy.transform.position, "transformation-heavy-standing");

            var grave = Object.FindFirstObjectByType<Grave>();
            Require(grave.TryRaise(leader) && !grave.TryRaise(leader), "Skeleton assembly must preserve single-use grave semantics.");
            var skeletons = Object.FindObjectsByType<Skeleton>(FindObjectsSortMode.None);
            Require(skeletons.Length == 3 && leader.Energy.Current == 35, "Assembly creates precisely three skeletons for 30 energy.");
            foreach (var skeleton in skeletons)
            {
                skeleton.GetComponent<Zombie>().enabled = false;
                var assembly = skeleton.GetComponent<CombatMotion>();
                Require(assembly.CurrentTransformation == CombatMotion.Transformation.SkeletonAssembly
                    && Mathf.Abs(skeleton.transform.Find("Skull").localPosition.x) <= 0.4f,
                    "Skeleton pieces assemble close to their own spawn point.");
            }
            yield return 0.15f;
            Capture(manager.LevelCamera, grave.transform.position, "transformation-skeletons");
            var first = skeletons[0].GetComponent<CombatMotion>();
            var skull = skeletons[0].transform.Find("Skull");
            Time.timeScale = 0;
            frozen = skull.localPosition; progress = first.TransformationProgress;
            yield return 0.2f;
            Require(skull.localPosition == frozen && first.TransformationProgress == progress, "Pause must freeze individual assembling bones.");
            Time.timeScale = 1;
            skeletons[0].GetComponent<UnitCombat>().TakeDamage(1000);
            yield return 0.7f;
            Require(Object.FindObjectsByType<Skeleton>(FindObjectsSortMode.None).Length == 2
                && leader.Energy.Current == 35 && manager.InfectedCount == 2 && manager.GuardsKilled == 1,
                "A skeleton killed during assembly must stay dead; no finish callback may respawn or reward it.");
            Vector3 skullRest = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Skeleton.prefab")
                .transform.Find("Skull").localPosition;
            for (int i = 1; i < skeletons.Length; i++)
                Require(skeletons[i].GetComponent<CombatMotion>().CurrentTransformation == CombatMotion.Transformation.None
                    && skeletons[i].transform.Find("Skull").localPosition == skullRest,
                    "Assembled survivors must restore their authored bone positions.");
            Capture(manager.LevelCamera, grave.transform.position, "transformation-skeletons-assembled");

            heavyMotion.BeginRising();
            heavy.gameObject.SetActive(false);
            heavy.gameObject.SetActive(true);
            Require(heavyMotion.CurrentTransformation == CombatMotion.Transformation.None
                && Mathf.Abs(helmet.localPosition.y - standingHeight) < 0.001f,
                "Disabling a rising model must cancel and restore the pose without replay after activation.");
            var pending = Human(new Vector3(0, 0, 2));
            Require(pending.TryInfect(prefab, leader, 2), "Retry setup needs an unfinished transformation.");
            leader.Combat.TakeDamage(1000);
            progress = pending.GetComponent<CombatMotion>().TransformationProgress;
            yield return 0.25f;
            Require(pending.GetComponent<CombatMotion>().TransformationProgress == progress, "Defeat freezes pending transformation visuals.");
            Require(manager.TryRestartLevel(), "Retry must work during unfinished transformation.");
            yield return 0.35f;
            var fresh = Object.FindFirstObjectByType<GameManager>();
            Require(fresh && fresh != manager && fresh.InfectedCount == 0 && fresh.GuardsKilled == 0
                && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 0,
                "Retry must remove all old emerging units, statistics and pending conversion timers.");
            foreach (var component in Object.FindObjectsByType<CombatMotion>(FindObjectsSortMode.None))
                Require(component.CurrentTransformation == CombatMotion.Transformation.None, "Fresh attempt must not inherit a transformation pose.");
            Debug.Log("PASS: paid infection bend/rise, chain conversion race, heavy getting up, three skeleton assemblies, pause, cancellation, death, exact rewards/statistics, unchanged roots/colliders, neighbours and clean retry.");
        }

        private static Civilian Human(Vector3 position)
        {
            var human = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CivilianSetup.PrefabPath), position,
                Quaternion.identity).GetComponent<Civilian>();
            var settings = new SerializedObject(human);
            settings.FindProperty("movementArea").rectValue = new Rect(position.x, position.z, 0, 0);
            settings.ApplyModifiedPropertiesWithoutUndo();
            return human;
        }

        private static void Capture(Camera source, Vector3 center, string name)
        {
            var camera = new GameObject("TransformationCheckCamera").AddComponent<Camera>();
            camera.CopyFrom(source); camera.enabled = false;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographicSize = 2.7f;
                camera.transform.position = center + (name.StartsWith("transformation-heavy")
                    ? new Vector3(-4, 7, 7) : new Vector3(4, 7, 7));
                camera.transform.LookAt(center + Vector3.up * 0.65f);
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
