using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class PeasantLegChecks
    {
        public static void CaptureBeforeBatch() => Run(false);
        public static void RunBatch() => Run(true);
        public static void RunSmoothBatch() => Run(true, true);

        private static void Run(bool verify, bool smooth = false)
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/PeasantRigPreview.unity");
                var rig = Object.FindFirstObjectByType<PeasantSpriteRig>();
                if (smooth)
                {
                    foreach (float contact in new[] { 0f, .6f, 1f })
                    {
                        const float step = .001f;
                        Vector2 at = PeasantSpriteRig.Foot(contact, 1.35f);
                        Vector2 before = (at - PeasantSpriteRig.Foot(contact - step, 1.35f)) / step;
                        Vector2 after = (PeasantSpriteRig.Foot(contact + step, 1.35f) - at) / step;
                        if (Vector2.Distance(before, after) > .03f)
                            throw new InvalidOperationException("Foot velocity jumps at ground contact or loop seam: " + contact);
                    }
                }
                rig.SetIdlePose();
                var upper = new[] { rig.transform.Find("SpriteSkeleton/FarThigh"), rig.transform.Find("SpriteSkeleton/NearThigh") };
                var lower = new[] { rig.transform.Find("SpriteSkeleton/FarShin"), rig.transform.Find("SpriteSkeleton/NearShin") };
                var upperRest = new float[2];
                var lowerRest = new float[2];
                for (int side = 0; side < 2; side++)
                {
                    upperRest[side] = upper[side].localEulerAngles.z;
                    lowerRest[side] = lower[side].localEulerAngles.z;
                }
                string directory = smooth ? "Logs/peasant-gait-smooth" : "Logs/peasant-leg-" + (verify ? "after" : "before");
                Directory.CreateDirectory(directory);
                var report = new StringBuilder();
                foreach (float stride in new[] { .85f, 1.35f })
                {
                    rig.Stride = stride;
                    float upperMax = 0, lowerMax = 0, velocityChangeMax = 0;
                    foreach (bool mirror in new[] { false, true })
                    {
                        rig.MirrorX = mirror;
                        for (int frame = 0; frame < 240; frame++)
                        {
                            rig.SetPhase(frame / 240f);
                            for (int side = 0; side < 2; side++)
                            {
                                float thigh = Mathf.Abs(Mathf.DeltaAngle(upperRest[side], upper[side].localEulerAngles.z));
                                float shin = Mathf.Abs(Mathf.DeltaAngle(lowerRest[side], lower[side].localEulerAngles.z));
                                upperMax = Mathf.Max(upperMax, thigh);
                                lowerMax = Mathf.Max(lowerMax, shin);
                                if (verify && (thigh > 18.05f || shin > 18.05f))
                                    throw new InvalidOperationException($"Leg rotation exceeded art limits: stride={stride}, phase={frame / 240f}, thigh={thigh}, shin={shin}");
                                int upperIndex = side == 0 ? 9 : 7, lowerIndex = side == 0 ? 10 : 8;
                                Vector3 kneeA = Anchor(upper[side], rig.Pieces[upperIndex].ImageEnd);
                                Vector3 kneeB = Anchor(lower[side], rig.Pieces[lowerIndex].ImageStart);
                                if (Vector3.Distance(kneeA, kneeB) > .0001f)
                                    throw new InvalidOperationException("Thigh and shin detached at knee");
                                if (smooth)
                                {
                                    Vector3 knee = upper[side].parent.InverseTransformPoint(kneeA);
                                    Vector3 ankle = lower[side].parent.InverseTransformPoint(Anchor(lower[side], rig.Pieces[lowerIndex].ImageEnd));
                                    if (Mathf.Abs(ankle.x - rig.Pieces[lowerIndex].RestEnd.x) > .025f)
                                        throw new InvalidOperationException("Foot left its forward/back lane");
                                    if (knee.x < rig.Pieces[upperIndex].RestEnd.x - .015f)
                                        throw new InvalidOperationException("Knee bent outward instead of following the shared forward direction");
                                }
                                if (Mathf.Abs(upper[side].localScale.x - upper[side].localScale.y) > .0001f
                                    || Mathf.Abs(lower[side].localScale.x - lower[side].localScale.y) > .0001f)
                                    throw new InvalidOperationException("Leg artwork was stretched");
                            }
                            int interval = smooth ? 4 : 10;
                            if (smooth)
                            {
                                const float step = .0005f;
                                float phase = frame / 240f;
                                var center = PosePoints(rig, upper, lower);
                                rig.SetPhase(phase - step);
                                var before = PosePoints(rig, upper, lower);
                                rig.SetPhase(phase + step);
                                var after = PosePoints(rig, upper, lower);
                                rig.SetPhase(phase);
                                for (int point = 0; point < center.Length; point++)
                                {
                                    float velocityChange = Vector3.Distance((center[point] - before[point]) / step, (after[point] - center[point]) / step);
                                    velocityChangeMax = Mathf.Max(velocityChangeMax, velocityChange);
                                    if (velocityChange > .25f) throw new InvalidOperationException("Knee/ankle motion has a velocity discontinuity: " + phase + " change=" + velocityChange);
                                }
                            }
                            if (!mirror && frame % interval == 0)
                                PeasantRigSetup.Capture(Camera.main, directory + "/" + (stride > 1 ? "run-" : "walk-") + (frame / interval).ToString("D2") + ".png");
                        }
                    }
                    report.AppendLine($"stride={stride}: maximum thigh rotation={upperMax:F3}, shin rotation={lowerMax:F3}");
                    if (smooth) report.AppendLine($"maximum sampled knee/ankle velocity change={velocityChangeMax:F4}");
                }
                File.WriteAllText(directory + "/report.txt", report.ToString());
                Debug.Log((verify ? "PASS: " : "BASELINE: ") + "480 phases per facing: connected knees, uniform leg scale. " + report);
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static Vector3 Anchor(Transform part, Vector2 normalized)
        {
            Vector2 size = part.GetComponent<SpriteRenderer>().sprite.bounds.size;
            return part.TransformPoint(Vector2.Scale(normalized - Vector2.one * .5f, size));
        }

        private static Vector3[] PosePoints(PeasantSpriteRig rig, Transform[] upper, Transform[] lower) => new[] {
            Anchor(upper[0], rig.Pieces[9].ImageEnd), Anchor(lower[0], rig.Pieces[10].ImageEnd),
            Anchor(upper[1], rig.Pieces[7].ImageEnd), Anchor(lower[1], rig.Pieces[8].ImageEnd) };
    }
}
