using System;
using UnityEngine;

namespace FallingWizard.World
{
    public class TimedReward : MonoBehaviour
    {
        const int SecondsPerMinute = 60;
        const int SecondsPerTen = 10;
        const int NothingShownYet = -1;

        [Tooltip("Seconds from the moment the level starts. Until then the rewards can be taken " +
                 "and the timer counts down. After that they fade and stay out of reach until " +
                 "the level is played again, and the timer goes back to showing this limit. The " +
                 "clock stops while the game is paused or a menu is open, but not for the Haste " +
                 "spell.")]
        [Min(1f)] public float secondsAllowed = 90f;

        [Tooltip("Each pickup, with the faded copy that replaces it when time runs out. A pickup " +
                 "already taken shows no faded copy.")]
        public Reward[] rewards = new Reward[0];

        [Header("Timer")]
        [Tooltip("The timer above the rewards: a child object that stays on until every reward " +
                 "has been taken.")]
        public GameObject timer;

        [Tooltip("The minutes digit: one child object per number, starting at 0. The script only " +
                 "switches them on and off, so their art is yours to redraw.")]
        public GameObject[] minuteDigits = new GameObject[0];

        [Tooltip("The tens of seconds digit: one child object per number, starting at 0.")]
        public GameObject[] tenSecondDigits = new GameObject[0];

        [Tooltip("The seconds digit: one child object per number, starting at 0.")]
        public GameObject[] secondDigits = new GameObject[0];

        [NonSerialized] bool hasFaded;
        [NonSerialized] int shownSeconds = NothingShownYet;

        float SecondsLeft => secondsAllowed - Time.timeSinceLevelLoad;

        int SecondsToShow => Mathf.CeilToInt(hasFaded ? secondsAllowed : SecondsLeft);

        void Update()
        {
            if (!hasFaded && SecondsLeft <= 0f)
                FadeTheRewards();

            ShowTheTimer();
        }

        void FadeTheRewards()
        {
            foreach (Reward reward in rewards)
            {
                if (reward.pickup == null)
                    continue;

                reward.pickup.SetActive(false);

                if (reward.fadedLook != null)
                    reward.fadedLook.SetActive(true);
            }

            hasFaded = true;
        }

        void ShowTheTimer()
        {
            if (timer != null)
                timer.SetActive(IsAnyRewardLeft());

            int seconds = SecondsToShow;

            if (seconds == shownSeconds)
                return;

            shownSeconds = seconds;

            int secondsPastTheMinute = seconds % SecondsPerMinute;

            ShowDigit(minuteDigits, seconds / SecondsPerMinute);
            ShowDigit(tenSecondDigits, secondsPastTheMinute / SecondsPerTen);
            ShowDigit(secondDigits, secondsPastTheMinute % SecondsPerTen);
        }

        bool IsAnyRewardLeft()
        {
            foreach (Reward reward in rewards)
            {
                if (reward.pickup != null)
                    return true;
            }

            return false;
        }

        static void ShowDigit(GameObject[] digits, int number)
        {
            for (int i = 0; i < digits.Length; i++)
            {
                if (digits[i] != null)
                    digits[i].SetActive(i == number);
            }
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
