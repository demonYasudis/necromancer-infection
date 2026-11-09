using UnityEngine;
using UnityEngine.SceneManagement;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";
        public const string FirstLevelPath = "Assets/Scenes/Tutorial1.unity";

        [SerializeField] private Texture2D backdrop;
        private readonly MenuView view = new MenuView();
        public bool IsLoading { get; private set; }

        private void Awake() => Time.timeScale = 1;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) view.CloseSettings();
        }

        private void OnGUI() => view.Draw(backdrop, this, null);

        public bool TryStartGame()
        {
            if (IsLoading || !Application.CanStreamedLevelBeLoaded(FirstLevelPath)) return false;
            view.CloseSettings();
            IsLoading = true;
            SceneManager.LoadSceneAsync(FirstLevelPath, LoadSceneMode.Single);
            return true;
        }

        internal void Quit()
        {
            GameSettings.Save();
            Application.Quit();
        }
    }
}
