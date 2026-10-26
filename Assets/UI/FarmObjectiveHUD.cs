using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class FarmObjectiveHUD : MonoBehaviour
    {
        private GameManager manager;
        private GUIStyle title;
        private GUIStyle detail;
        private GUIStyle hint;

        private void Awake() => manager = GetComponent<GameManager>();

        public bool ContainsScreenPosition(Vector2 position)
        {
            if (!manager || !manager.Farm || manager.IsFinished) return false;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            if (scale <= 0) return false;
            float left = (Screen.width / scale - 360) * 0.5f;
            float height = manager.FinalPhase ? 120 : manager.GetComponent<JourneyRoute>() ? 104 : 66;
            return new Rect(left, 18, 360, height).Contains(new Vector2(position.x, Screen.height - position.y) / scale);
        }

        private void OnGUI()
        {
            if (!manager || !manager.Farm || manager.IsFinished) return;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            if (scale <= 0) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                detail = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                hint = new GUIStyle(detail) { fontSize = 12, wordWrap = true };
            }
            Matrix4x4 originalMatrix = GUI.matrix;
            Color originalColor = GUI.color;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                float left = (Screen.width / scale - 360) * 0.5f;
                GUI.color = new Color(0.055f, 0.05f, 0.07f, 0.94f);
                var assault = manager.Farm.GetComponent<FarmAssault>();
                var journey = manager.GetComponent<JourneyRoute>();
                bool captured = assault && assault.IsCaptured;
                GUI.DrawTexture(new Rect(left, 18, 360, manager.FinalPhase ? 120 : journey ? 104 : 66), Texture2D.whiteTexture);
                GUI.color = manager.FinalPhase ? new Color(0.83f, 0.65f, 1) : new Color(0.86f, 0.86f, 0.8f);
                GUI.Label(new Rect(left + 8, 25, 344, 28), captured ? "ФЕРМА ЗАХВАЧЕНА"
                    : manager.FinalPhase ? "ФЕРМА · ФИНАЛЬНАЯ ФАЗА" : journey ? journey.StageTitle : "ЦЕЛЬ: ДОБРАТЬСЯ ДО ФЕРМЫ", title);
                GUI.Label(new Rect(left + 8, 54, 344, 24), manager.FinalPhase && assault
                    ? $"Осквернение: {assault.CorruptionSeconds:0.0} / {assault.CorruptionDuration:0.#} сек"
                    : journey ? $"Участок {journey.Stage + 1}/3 · Путь {journey.DistanceTravelled:0}/{journey.Length:0} м" : "Собирайте орду по дороге", detail);
                if (journey && !manager.FinalPhase)
                    GUI.Label(new Rect(left + 12, 80, 336, 34), journey.StageHint, hint);
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
