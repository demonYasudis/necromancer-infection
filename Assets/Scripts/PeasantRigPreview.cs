using UnityEngine;

namespace NecromancerInfection
{
    public sealed class PeasantRigPreview : MonoBehaviour
    {
        public PeasantSpriteRig Rig;
        private void OnGUI()
        {
            float scale = Mathf.Min(Screen.width / 1000f, Screen.height / 700f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(24, 24, 325, 650), GUI.skin.box);
            GUILayout.Label("PEASANT / ARTICULATED SPRITE");
            GUILayout.Label("One authored view · Unity prototype");
            GUILayout.Space(15);
            if (GUILayout.Button(Rig.Paused ? "Resume" : "Pause", GUILayout.Height(34))) Rig.Paused = !Rig.Paused;
            if (GUILayout.Button("Idle", GUILayout.Height(34))) Rig.Tempo = 0;
            if (GUILayout.Button("Walk", GUILayout.Height(34))) { Rig.Tempo = 0.85f; Rig.Stride = 0.65f; }
            if (GUILayout.Button("Run", GUILayout.Height(34))) { Rig.Tempo = 1.8f; Rig.Stride = 0.95f; }
            GUILayout.Label("Tempo");
            Rig.Tempo = GUILayout.HorizontalSlider(Rig.Tempo, 0, 2);
            GUILayout.Label("Stride");
            Rig.Stride = GUILayout.HorizontalSlider(Rig.Stride, 0.1f, 1);
            bool pouch = GUILayout.Toggle(Rig.ShowPouch, "Belt pouch");
            Rig.SetPouch(pouch);
            if (GUILayout.Button(Rig.ArmDetached ? "Restore arm" : "Detach arm", GUILayout.Height(34)))
            {
                if (Rig.ArmDetached) Rig.RestoreArm(); else Rig.DetachArm();
            }
            GUILayout.Label("Phase (scrub pauses animation)");
            float phase = GUILayout.HorizontalSlider(Rig.Phase, 0, 0.9999f);
            if (Mathf.Abs(phase - Rig.Phase) > 0.0001f) { Rig.Paused = true; Rig.SetPhase(phase); }
            GUILayout.Space(18);
            GUILayout.Label("Cut from one complete human drawing\nAlternating feet · two-bone IK\nSeparate equipment · detachable arm\n\nOne authored facing direction.\nAnimation remains a prototype.");
            GUILayout.EndArea();
        }
    }
}
