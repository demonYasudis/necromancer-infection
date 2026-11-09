using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Distinguishes an automatically raised knight from a converted civilian.</summary>
    [RequireComponent(typeof(Zombie))]
    [DisallowMultipleComponent]
    public sealed class RaisedGuard : MonoBehaviour { }
}
