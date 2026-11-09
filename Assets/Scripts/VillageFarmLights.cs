using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>The farm windows change with the result of the attempt.</summary>
    public sealed class VillageFarmLights : MonoBehaviour
    {
        [SerializeField] private Renderer windows;
        private GameManager manager;

        private void Start() => manager = FindFirstObjectByType<GameManager>();

        private void LateUpdate()
        {
            if (!manager || !manager.IsVictorious || !windows) return;
            var corrupted = new MaterialPropertyBlock();
            corrupted.SetColor("_Color", new Color(.20f, .40f, .075f));
            corrupted.SetColor("_EmissionColor", new Color(.15f, .55f, .035f));
            windows.SetPropertyBlock(corrupted);
            enabled = false;
        }
    }
}
