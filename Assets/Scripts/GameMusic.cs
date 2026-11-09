using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>The soundtrack stays on its own volume channel across menus and attempts.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class GameMusic : MonoBehaviour
    {
        private AudioSource source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartSoundtrack()
        {
            var root = new GameObject("Soundtrack");
            DontDestroyOnLoad(root);
            root.AddComponent<GameMusic>();
        }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.priority = 48;
            source.clip = PrototypeSounds.CreateSoundtrack();
            source.volume = GameSettings.Music;
            source.Play();
        }

        private void Update() => source.volume = GameSettings.Music;
        private void OnDestroy() { if (source && source.clip) Destroy(source.clip); }
    }
}
