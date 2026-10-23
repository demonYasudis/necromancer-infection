using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Distinguishes a paid raised defender from a converted civilian.</summary>
    [RequireComponent(typeof(Zombie))]
    [DisallowMultipleComponent]
    public sealed class RaisedGuard : MonoBehaviour { }
}
