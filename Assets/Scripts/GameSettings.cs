using UnityEngine;

namespace NecromancerInfection
{
    internal static class GameSettings
    {
        private const string MusicKey = "settings.music";
        private const string EffectsKey = "settings.effects";
        private const string FullscreenKey = "settings.fullscreen";

        internal static float Music { get; private set; }
        internal static float Effects { get; private set; }
        internal static bool Fullscreen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            Music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, .65f));
            Effects = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey, 1));
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            if (PlayerPrefs.HasKey(FullscreenKey)) ApplyDisplayMode();
        }

        internal static void SetMusic(float volume)
        {
            Music = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MusicKey, Music);
        }

        internal static void SetEffects(float volume)
        {
            Effects = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(EffectsKey, Effects);
        }

        internal static void SetFullscreen(bool fullscreen)
        {
            Fullscreen = fullscreen;
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            ApplyDisplayMode();
        }

        private static void ApplyDisplayMode()
        {
            if (Application.isEditor || Screen.fullScreen == Fullscreen) return;
            if (Fullscreen)
            {
                var display = Screen.currentResolution;
                Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                var display = Screen.currentResolution;
                Screen.SetResolution(Mathf.Min(1280, display.width), Mathf.Min(720, display.height), FullScreenMode.Windowed);
            }
        }

        internal static void Save() => PlayerPrefs.Save();
    }
}
