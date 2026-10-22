using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class DefeatScreen : MonoBehaviour
    {
        private GameManager manager;
        private GUIStyle titleStyle;
        private GUIStyle messageStyle;
        private GUIStyle buttonStyle;

        private void Awake() => manager = GetComponent<GameManager>();

        private void OnGUI()
        {
            if (!manager || !manager.IsDefeated) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.93f, 0.8f, 0.77f) }
                };
                messageStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.72f, 0.72f, 0.75f) }
                };
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            }
            Matrix4x4 originalMatrix = GUI.matrix;
            Color originalColor = GUI.color;
            int originalDepth = GUI.depth;
            bool originalEnabled = GUI.enabled;
            try
            {
                GUI.depth = -100;
                GUI.matrix = Matrix4x4.identity;
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.015f, 0.012f, 0.025f, 0.8f));
                float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
                if (scale <= 0) return;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                float left = (Screen.width / scale - 520) * 0.5f;
                float top = (Screen.height / scale - 240) * 0.5f;
                Fill(new Rect(left, top, 520, 240), new Color(0.075f, 0.065f, 0.09f));
                Fill(new Rect(left, top, 520, 3), new Color(0.56f, 0.3f, 0.38f));
                GUI.color = Color.white;
                GUI.Label(new Rect(left + 20, top + 28, 480, 52), "Некромант уничтожен", titleStyle);
                GUI.Label(new Rect(left + 20, top + 86, 480, 30), "Орда потеряла своего повелителя.", messageStyle);
                GUI.enabled = !manager.IsRestarting;
                if (GUI.Button(new Rect(left + 140, top + 143, 240, 52),
                    manager.IsRestarting ? "Загрузка…" : "Повторить", buttonStyle))
                    manager.TryRestartLevel();
            }
            finally
            {
                GUI.matrix = originalMatrix;
                GUI.color = originalColor;
                GUI.depth = originalDepth;
                GUI.enabled = originalEnabled;
            }
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
    }
}
