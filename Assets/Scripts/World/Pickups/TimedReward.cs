using System;
using UnityEngine;

namespace FallingWizard.World
{
    public class TimedReward : MonoBehaviour
    {
        const int SecondsPerMinute = 60;
        const int SecondsPerTen = 10;

        [Tooltip("Seconds from the moment the level starts. Until then the rewards can be taken " +
                 "and the timer counts up. When it gets here the timer stops and turns red, and " +
                 "the rewards fade and stay out of reach until the level is played again. The " +
                 "clock stops while the game is paused or a menu is open, but not for the Haste " +
                 "spell.")]
        [Min(1f)] public float secondsAllowed = 60f;

        [Tooltip("Each pickup, with the faded copy that replaces it when time runs out. A pickup " +
                 "already taken shows no faded copy.")]
        public Reward[] rewards = new Reward[0];

        [Header("Timer")]
        [Tooltip("The timer above the rewards: a child object that stays on until every reward " +
                 "has been taken.")]
        public GameObject timer;

        [Tooltip("The digits shown while the timer counts up.")]
        public Clock runningClock = new Clock();

        [Tooltip("The digits shown once time is up, reading Seconds Allowed. Their colour is set " +
                 "on their own Sprite Renderers.")]
        public Clock stoppedClock = new Clock();

        [NonSerialized] bool hasStopped;

        void Update()
        {
            if (!hasStopped)
                RunTheClock();

            if (timer != null)
                timer.SetActive(IsAnyRewardLeft());
        }

        void RunTheClock()
        {
            if (Time.timeSinceLevelLoad < secondsAllowed)
                runningClock.ShowTime(Mathf.FloorToInt(Time.timeSinceLevelLoad));
            else
                StopTheClock();
        }

        void StopTheClock()
        {
            hasStopped = true;

            FadeTheRewards();

            runningClock.Switch(false);
            stoppedClock.Switch(true);
            stoppedClock.ShowTime(Mathf.CeilToInt(secondsAllowed));
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

        [Serializable]
        public class Reward
        {
            [Tooltip("The pickup the wizard can grab while there is still time.")]
            public GameObject pickup;

            [Tooltip("The faded copy shown once time runs out. A child object holding your own " +
                     "art, with the colour already faded on its Sprite Renderer.")]
            public GameObject fadedLook;
        }

        [Serializable]
        public class Clock
        {
            const int NothingShownYet = -1;

            [Tooltip("The child object holding this clock's digits.")]
            public GameObject face;

            [Tooltip("The minutes digit: one child object per number, starting at 0. The script " +
                     "only switches them on and off, so their art is yours to redraw.")]
            public GameObject[] minutes = new GameObject[0];

            [Tooltip("The tens of seconds digit: one child object per number, starting at 0.")]
            public GameObject[] tens = new GameObject[0];

            [Tooltip("The seconds digit: one child object per number, starting at 0.")]
            public GameObject[] ones = new GameObject[0];

            [NonSerialized] int shownSeconds = NothingShownYet;

            public void Switch(bool isOn)
            {
                if (face != null)
                    face.SetActive(isOn);
            }

            public void ShowTime(int seconds)
            {
                if (seconds == shownSeconds)
                    return;

                shownSeconds = seconds;

                int secondsPastTheMinute = seconds % SecondsPerMinute;

                ShowDigit(minutes, seconds / SecondsPerMinute);
                ShowDigit(tens, secondsPastTheMinute / SecondsPerTen);
                ShowDigit(ones, secondsPastTheMinute % SecondsPerTen);
            }

            static void ShowDigit(GameObject[] digits, int number)
            {
                for (int i = 0; i < digits.Length; i++)
                {
                    if (digits[i] != null)
                        digits[i].SetActive(i == number);
                }
            }
        }
    }
}
