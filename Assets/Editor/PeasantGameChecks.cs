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
    public static class PeasantGameChecks
    {
        private const string Key = "PeasantGameChecks";
        private static IEnumerator routine;
        private static double next, deadline;
        private static string error;
        static PeasantGameChecks() => EditorApplication.update += Tick;

        [MenuItem("Necromancer Infection/Peasant Rig/Run Gameplay Checks")]
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(JourneySetup.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Watch(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = message; }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (routine == null)
                {
                    routine = Checks();
                    deadline = EditorApplication.timeSinceStartup + 90;
                    Application.logMessageReceived += Watch;
                }
                Require(error == null, error);
                Require(EditorApplication.timeSinceStartup < deadline, "Peasant gameplay checks timed out");
                if (EditorApplication.timeSinceStartup < next) return;
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
                else
                {
                    SessionState.SetBool(Key, false);
                    Application.logMessageReceived -= Watch;
                    Debug.Log("PASS: top-down peasant on 93 Journey and 18 Demo villagers, live walking/fleeing, facing, pause, recoil, paid infection/tint/bend, single conversion, original colliders and clean retry.");
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                SessionState.SetBool(Key, false);
                Application.logMessageReceived -= Watch;
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static IEnumerator Checks()
        {
            yield return .4;
            var manager = Object.FindFirstObjectByType<GameManager>();
            var people = Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None);
            Require(people.Length == 93, "Journey must retain 93 residents");
            foreach (var human in people) CheckVisual(human);
            Directory.CreateDirectory("Previews");
            PeasantRigSetup.Capture(manager.LevelCamera, "Previews/peasant-in-journey.png");
            manager.Necromancer.enabled = false;
            var prefab = AssetDatabase.LoadAssetAtPath<Civilian>(CivilianSetup.PrefabPath);
            var subject = Object.Instantiate(prefab, manager.Necromancer.transform.position + Vector3.back * 3, Quaternion.identity);
            yield return .3;
            var rig = subject.GetComponentInChildren<PeasantSpriteRig>();
            Require(subject.State == Civilian.BehaviourState.Fleeing && rig.Stride > 1, "Real undead presence must trigger running animation");
            float phase = rig.Phase;
            Vector3 start = subject.transform.position;
            yield return .25;
            Require(rig.Phase != phase && subject.transform.position != start, "Real civilian navigation must drive gait");
            CaptureClose(manager.LevelCamera, subject.transform.position, "Previews/peasant-in-game-close.png");
            Time.timeScale = 0;
            phase = rig.Phase;
            var head = rig.transform.Find("SpriteSkeleton/Head");
            Vector3 headPosition = head.localPosition;
            yield return .2;
            Require(rig.Phase == phase && head.localPosition == headPosition, "Game pause must freeze visible sprite");
            Time.timeScale = 1;
            subject.enabled = false;
            subject.transform.position += Vector3.right * .1f;
            yield return .1;
            Require(!rig.MirrorX, "Screen-right movement must use the authored facing");
            subject.transform.position -= Vector3.right * .1f;
            yield return .1;
            Require(rig.MirrorX, "Screen-left movement must mirror the sprite");
            Require(subject.Combat.TakeDamage(1), "Hit setup");
            yield return .04;
            Require(rig.RecoilOffset.sqrMagnitude > 0, "Damage must react on the visible model");
            yield return .3;
            Require(rig.RecoilOffset.sqrMagnitude < .00001f, "Hit reaction must settle");
            subject.enabled = true;
            Physics.SyncTransforms();
            var collider = subject.GetComponent<CapsuleCollider>();
            Bounds bounds = collider.bounds;
            Vector3 root = subject.transform.position;
            var ability = Object.FindFirstObjectByType<InfectionAbility>();
            Vector2 screen = manager.LevelCamera.WorldToScreenPoint(root + Vector3.up * .8f);
            float energy = manager.Necromancer.Energy.Current;
            Require(ability.TryInfectAtScreenPosition(screen), "Real click must still select the peasant collider");
            Require(manager.Necromancer.Energy.Current == energy - 15, "Art replacement must retain infection cost");
            yield return .3;
            Require(rig.InfectionBend > 0 && subject.transform.position == root && collider.bounds == bounds,
                "Infection must bend the visible sprite without moving its root or collider");
            var block = new MaterialPropertyBlock();
            head.GetComponent<SpriteRenderer>().GetPropertyBlock(block);
            Require(block.GetColor("_Color").g > .9f, "Existing infection tint must reach sprite parts");
            CaptureClose(manager.LevelCamera, root, "Previews/peasant-in-game-infected.png");
            Time.timeScale = 0;
            float bend = rig.InfectionBend;
            yield return .2;
            Require(subject && rig.InfectionBend == bend, "Pause must freeze sprite infection");
            Time.timeScale = 1;
            yield return ability.InfectionDelay;
            Require(!subject && manager.InfectedCount == 1 && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 1,
                "The new visual must convert exactly once into the existing zombie");
            manager.Necromancer.Combat.TakeDamage(1000);
            Require(manager.TryRestartLevel(), "Retry must remain available");
            yield return .6;
            var fresh = Object.FindFirstObjectByType<GameManager>();
            people = Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None);
            Require(fresh && fresh != manager && people.Length == 93 && fresh.InfectedCount == 0, "Journey retry must reset residents and infection");
            foreach (var human in people) CheckVisual(human);
            SceneManager.LoadScene(DemoLevelSetup.ScenePath);
            yield return .5;
            people = Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None);
            Require(people.Length == 18, "Compact demo must retain 18 residents");
            foreach (var human in people) CheckVisual(human);
        }

        private static void CheckVisual(Civilian human)
        {
            var rigs = human.GetComponentsInChildren<PeasantSpriteRig>();
            Require(rigs.Length == 1 && !rigs[0].PreviewWalking && rigs[0].GetComponent<CivilianSpriteVisual>(), "Exactly one gameplay sprite rig per resident");
            Require(rigs[0].GetComponentsInChildren<SpriteRenderer>().Length == 12, "All body parts must be built");
            foreach (var sprite in rigs[0].GetComponentsInChildren<SpriteRenderer>())
                Require(AssetDatabase.GetAssetPath(sprite.sprite).StartsWith(PeasantTopDownSetup.ArtPath), "Use the approved top-down artwork");
            foreach (Transform child in human.transform)
                if ((child.name == "Head" || child.name == "Shirt" || child.name.StartsWith("Leg_"))
                    && child.TryGetComponent<MeshRenderer>(out var renderer))
                    Require(!renderer.enabled, "Old body geometry must be hidden: " + human.name + "/" + child.name);
            Require(human.GetComponent<CapsuleCollider>().height == 1.5f, "Keep the original gameplay hitbox");
        }

        private static void CaptureClose(Camera source, Vector3 center, string path)
        {
            var camera = new GameObject("PeasantCaptureCamera").AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            camera.transform.rotation = source.transform.rotation;
            camera.transform.position = center + Vector3.up * .65f - camera.transform.forward * 8;
            camera.orthographicSize = 1.7f;
            try { PeasantRigSetup.Capture(camera, path); }
            finally { Object.Destroy(camera.gameObject); }
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
