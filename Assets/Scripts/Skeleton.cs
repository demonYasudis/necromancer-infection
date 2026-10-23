using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Marks purchased grave reinforcements, which do not grant infection rewards.</summary>
    [RequireComponent(typeof(Zombie))]
    public sealed class Skeleton : MonoBehaviour { }
}
