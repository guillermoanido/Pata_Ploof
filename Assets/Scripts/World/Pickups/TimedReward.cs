using System;
using UnityEngine;

namespace FallingWizard.World
{
    public class TimedReward : MonoBehaviour
    {
        [Tooltip("Seconds from the moment the level starts. Until then the rewards can be taken. " +
                 "After that they fade and stay out of reach until the level is played again. " +
                 "The clock stops while the game is paused or a menu is open, but not for the " +
                 "Haste spell. The red 1:30 is a sprite, so if you change this, redraw it too.")]
        [Min(1f)] public float secondsAllowed = 90f;

        [Tooltip("Each pickup, with the faded copy that replaces it when time runs out. A pickup " +
                 "already taken shows no faded copy.")]
        public Reward[] rewards = new Reward[0];

        [Tooltip("The red timer, switched on when time runs out if any reward is still there. A " +
                 "child object holding your own art. The script only switches it on.")]
        public GameObject timeUpLook;

        [NonSerialized] bool isShowingTimeUp;

        bool IsTimeUp => Time.timeSinceLevelLoad >= secondsAllowed;

        void Update()
        {
            if (!isShowingTimeUp && IsTimeUp)
                ShowTimeUp();
        }

        void ShowTimeUp()
        {
            bool isAnyRewardLeft = false;

            foreach (Reward reward in rewards)
            {
                if (reward.pickup == null)
                    continue;

                reward.pickup.SetActive(false);
                SwitchOn(reward.fadedLook);
                isAnyRewardLeft = true;
            }

            if (isAnyRewardLeft)
                SwitchOn(timeUpLook);

            isShowingTimeUp = true;
        }

        static void SwitchOn(GameObject look)
        {
            if (look != null)
                look.SetActive(true);
        }

        [Serializable]
        public class Reward
        {
            [Tooltip("The pickup the wizard can grab while there is still time.")]
            public GameObject pickup;

            [Tooltip("The faded copy shown once time runs out. A child object holding your own " +
                     "art, with the colour already faded on its Sprite Renderer.")]
            public GameObject fadedLook;
        }
    }
}
