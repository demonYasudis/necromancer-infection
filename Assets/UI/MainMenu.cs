using UnityEngine;
using UnityEngine.SceneManagement;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";
        public const string FirstLevelPath = "Assets/Scenes/Tutorial1.unity";
        public const string SurvivalLevelPath = "Assets/Scenes/Survival01.unity";

        private static readonly string[] LevelPaths =
        {
            FirstLevelPath,
            "Assets/Scenes/Tutorial2.unity",
            "Assets/Scenes/Tutorial3.unity",
            "Assets/Scenes/Tutorial4.unity",
            SurvivalLevelPath
        };
        internal static int LevelCount => LevelPaths.Length;

        [SerializeField] private Texture2D backdrop;
        private readonly MenuView view = new MenuView();
        public bool IsLoading { get; private set; }

        private void Awake() => Time.timeScale = 1;

        private void Update()
        {
            if (!IsLoading && Input.GetKeyDown(KeyCode.Escape)) view.GoBack();
        }

        private void OnGUI() => view.Draw(backdrop, this, null);

        public bool TryStartGame() => TryStartLevel(0);

        internal bool CanStartLevel(int index) => !IsLoading && index >= 0 && index < LevelPaths.Length
            && Application.CanStreamedLevelBeLoaded(LevelPaths[index]);

        public bool TryStartLevel(int index)
        {
            if (!CanStartLevel(index)) return false;
            view.CloseSettings();
            IsLoading = true;
            SceneManager.LoadSceneAsync(LevelPaths[index], LoadSceneMode.Single);
            return true;
        }

        internal void Quit()
        {
            GameSettings.Save();
            Application.Quit();
        }
    }
}
