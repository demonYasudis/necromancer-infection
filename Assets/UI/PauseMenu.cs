using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class PauseMenu : MonoBehaviour
    {
        private GameManager manager;
        private readonly MenuView view = new MenuView();

        private void Awake() => manager = GetComponent<GameManager>();

        private void Update()
        {
            if (!manager || manager.IsFinished || manager.IsRestarting || !Input.GetKeyDown(KeyCode.Escape)) return;
            if (manager.IsPaused)
            {
                if (!view.CloseSettings()) manager.TryResumeGame();
            }
            else manager.TryPauseGame();
        }

        private void OnGUI()
        {
            if (manager && manager.IsPaused && !manager.IsFinished) view.Draw(null, null, manager);
        }

        private void OnDisable()
        {
            view.CloseSettings();
            if (manager) manager.TryResumeGame();
        }
    }
}
