using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class FarmObjectiveHUD : MonoBehaviour
    {
        private GameManager manager;
        private GUIStyle title;
        private GUIStyle detail;

        private void Awake() => manager = GetComponent<GameManager>();

        private void OnGUI()
        {
            if (!manager || !manager.Farm || manager.IsFinished) return;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            if (scale <= 0) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                detail = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            }
            Matrix4x4 originalMatrix = GUI.matrix;
            Color originalColor = GUI.color;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                float left = (Screen.width / scale - 360) * 0.5f;
                GUI.color = new Color(0.055f, 0.05f, 0.07f, 0.94f);
                var assault = manager.Farm.GetComponent<FarmAssault>();
                bool captured = assault && assault.IsCaptured;
                GUI.DrawTexture(new Rect(left, 18, 360, manager.FinalPhase ? 120 : 66), Texture2D.whiteTexture);
                GUI.color = manager.FinalPhase ? new Color(0.83f, 0.65f, 1) : new Color(0.86f, 0.86f, 0.8f);
                GUI.Label(new Rect(left + 8, 25, 344, 28), captured ? "ФЕРМА ЗАХВАЧЕНА"
                    : manager.FinalPhase ? "ФЕРМА · ФИНАЛЬНАЯ ФАЗА" : "ЦЕЛЬ: ДОБРАТЬСЯ ДО ФЕРМЫ", title);
                GUI.Label(new Rect(left + 8, 54, 344, 24), manager.FinalPhase && assault
                    ? $"Осквернение: {assault.CorruptionSeconds:0.0} / {assault.CorruptionDuration:0.#} сек"
                    : "Собирайте орду по дороге", detail);
                if (manager.FinalPhase && assault)
                {
                    GUI.color = new Color(0.2f, 0.16f, 0.25f);
                    GUI.DrawTexture(new Rect(left + 18, 86, 324, 14), Texture2D.whiteTexture);
                    GUI.color = new Color(0.68f, 0.36f, 0.92f);
                    GUI.DrawTexture(new Rect(left + 18, 86, 324 * assault.Progress, 14), Texture2D.whiteTexture);
                    GUI.color = new Color(0.83f, 0.74f, 0.9f);
                    GUI.Label(new Rect(left + 8, 105, 344, 24), captured ? "Осквернение завершено" : "Защищайте некроманта рядом с фермой", detail);
                }
            }
            finally
            {
                GUI.matrix = originalMatrix;
                GUI.color = originalColor;
            }
        }
    }
}
