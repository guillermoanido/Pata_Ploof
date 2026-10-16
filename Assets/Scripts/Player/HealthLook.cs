using UnityEngine;

namespace FallingWizard.Player
{
    public class HealthLook : MonoBehaviour
    {
        [Tooltip("The health this look stands for. Put one look per health level under the " +
                 "wizard's Visual, each with its own sprite and Animator. The wizard starts with " +
                 "as much health as the highest look, and only the look matching their health " +
                 "is switched on - when they have more health than any look, the highest one " +
                 "is shown.")]
        [Min(1)] public int health = 1;
    }
}
