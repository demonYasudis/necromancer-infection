using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    [InitializeOnLoad]
    public static class PeasantRigPlayChecks
    {
        private const string Key = "PeasantRigPlayChecks";
        private static IEnumerator checks;
        private static double next, deadline;
        static PeasantRigPlayChecks() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PeasantRigPreview.unity");
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (checks == null) { checks = Checks(); deadline = EditorApplication.timeSinceStartup + 45; }
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Peasant Play checks timed out");
                if (EditorApplication.timeSinceStartup < next) return;
                if (checks.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(checks.Current);
                else
                {
                    SessionState.SetBool(Key, false);
                    Debug.Log("PASS: peasant Play mode animation, pause/resume, idle, distance-driven gait, teleport, equipment, detach/restore and animation frame export.");
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception error)
            {
                SessionState.SetBool(Key, false);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static IEnumerator Checks()
        {
            yield return 0.3;
            var rig = Object.FindFirstObjectByType<PeasantSpriteRig>();
            Require(rig && rig.GetComponentsInChildren<SpriteRenderer>().Length == 12, "Runtime rig must build exactly once");
            float phase = rig.Phase;
            yield return 0.25;
            Require(rig.Phase != phase, "Walk must advance");
            rig.DetachArm();
            rig.Paused = true;
            phase = rig.Phase;
            var loose = rig.transform.Find("SpriteSkeleton/DetachedArm");
            Vector3 frozen = loose.localPosition;
            yield return 0.2;
            Require(rig.Phase == phase && loose.localPosition == frozen, "Local pause must freeze gait and falling arm");
            rig.Paused = false;
            Time.timeScale = 0;
            yield return 0.2;
            Require(rig.Phase == phase && loose.localPosition == frozen, "Game pause must freeze gait and falling arm");
            Time.timeScale = 1;
            yield return 0.25;
            Require(rig.Phase != phase && loose.localPosition != frozen, "Resume must advance both");
            rig.RestoreArm();
            rig.Tempo = 0;
            yield return 0.3;
            var foot = rig.transform.Find("SpriteSkeleton/FarShin");
            frozen = foot.localPosition;
            phase = rig.Phase;
            yield return 0.2;
            Require(rig.Phase == phase && foot.localPosition == frozen, "Idle must settle and stop");
            rig.PreviewWalking = false;
            rig.transform.position += Vector3.right * 0.1f;
            yield return 0.15;
            Require(rig.Phase != phase, "Actual movement must drive gait");
            phase = rig.Phase;
            rig.transform.position += Vector3.right * 10;
            yield return 0.15;
            Require(rig.Phase == phase, "Teleport must not count as steps");
            rig.transform.position = Vector3.zero;
            rig.Paused = true;
            rig.SetPouch(false);
            Require(!rig.PouchSocket.GetComponent<SpriteRenderer>().enabled, "Remove pouch");
            rig.SetPouch(true);
            rig.DetachArm();
            Require(rig.ArmDetached, "Detach arm");
            rig.RestoreArm();
            Require(!rig.ArmDetached && rig.GetComponentsInChildren<SpriteRenderer>().Length == 12, "Restore without duplicates");
            Directory.CreateDirectory("Logs/peasant-frames");
            for (int i = 0; i < 24; i++)
            {
                rig.SetPhase(i / 24f);
                PeasantRigSetup.Capture(Camera.main, "Logs/peasant-frames/walk-" + i.ToString("D2") + ".png");
            }
            rig.DetachArm();
            rig.Paused = false;
            yield return 0.5;
            rig.Paused = true;
            PeasantRigSetup.Capture(Camera.main, "Logs/peasant-unity-detached.png");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new Exception(message); }
    }
}
