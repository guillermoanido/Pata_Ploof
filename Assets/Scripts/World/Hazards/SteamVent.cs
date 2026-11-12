using System;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class SteamVent : Hazard
    {
        const float ShortestCycle = 0.1f;

        const int BurnDamage = 1;

        const float BurnCooldown = 1f;

        const float SteamRampup = 40f;

        const float FullStrengthOnTheGround = 1f;

        static readonly Color SteamColour = new Color(1f, 0.8f, 0.6f, 0.9f);

        [Header("Timing")]
        [Tooltip("Seconds the pipe sits silent between blasts. It is safe for this long AND for " +
                 "the warning after it, so the gap the player gets is the two added together: at " +
                 "a run of 4 boxes a second, 2.6 seconds is about ten boxes of ground.")]
        [Min(0f)] public float quietSeconds = 2f;

        [Tooltip("Seconds it sputters before it blasts, with the Warning Look switched on. It is " +
                 "still safe to stand in during this. THIS IS THE WHOLE TELL: at 0 the vent goes " +
                 "from silent to scalding in a single frame, which is the one way to make it " +
                 "unfair. Half a second is about as short as a player can act on.")]
        [Min(0f)] public float warningSeconds = 0.6f;

        [Tooltip("Seconds the column of steam blasts for, with the Steam Look switched on. This " +
                 "is the only time it burns, and a wizard who stays in the column is burned again " +
                 "every Rearm Delay for as long as they stay. 0 makes a vent that never fires.")]
        [Min(0f)] public float burstSeconds = 1.2f;

        [Tooltip("Where in the cycle this one starts, as a fraction of the whole thing. A row of " +
                 "vents all left at 0 blast in unison, which is one obstacle; set them to 0, 0.33 " +
                 "and 0.66 and they blast in a stagger you have to run through, which is a " +
                 "corridor. It is a fraction rather than seconds so that retuning the timings " +
                 "later keeps the stagger.")]
        [Range(0f, 1f)] public float phase = 0f;

        [Header("Steam")]
        [Tooltip("Optional push while the steam blasts, in boxes per second, worked like a Wind " +
                 "zone: the speed it tries to carry the wizard at. Leave it at (0, 0) for steam " +
                 "that only burns. (0, 6) turns the vent into an updraft that lifts the wizard " +
                 "even from standing - set Damage to 0 for one meant to be ridden, and remember " +
                 "they drop again when the blast stops, and a fall of more than 3 boxes hurts.")]
        public Vector2 push = Vector2.zero;

        [Header("Look")]
        [Tooltip("A child object switched on while the vent sits silent, and off the rest of " +
                 "the time: the vent shut. Fill it with your own art or animation. Never put the " +
                 "vent itself or one of its parents here: switching that off would switch the " +
                 "whole vent off.")]
        public GameObject quietLook;

        [Tooltip("A child object switched on only while the vent sputters its warning. Its " +
                 "animation starts over each time it is switched on, so a clip as long as " +
                 "Warning Seconds opens the vent just as the blast arrives.")]
        public GameObject warningLook;

        [Tooltip("A child object switched on only while the steam blasts: the vent fully open. " +
                 "This is the only time it burns, and it burns anyone inside the trigger " +
                 "collider on this object, so keep that collider the size of this art.")]
        public GameObject steamLook;

        [NonSerialized] float clock;
        [NonSerialized] bool isShowingWarning;
        [NonSerialized] bool isShowingSteam;

        protected override bool IsDangerousNow => IsBursting;

        protected override bool IsContinuous => true;

        bool IsWarning => IsTimeBetween(quietSeconds, BurstStartsAt);

        bool IsBursting => IsTimeBetween(BurstStartsAt, BurstStartsAt + burstSeconds);

        float CycleLength => Mathf.Max(ShortestCycle, quietSeconds + warningSeconds + burstSeconds);

        float BurstStartsAt => quietSeconds + warningSeconds;

        float TimeIntoCycle => Mathf.Repeat(clock + phase * CycleLength, CycleLength);

        protected override void Reset()
        {
            base.Reset();

            damage = BurnDamage;
            rearmDelay = BurnCooldown;
            affectsRagdolled = true;
            affectsOnStaff = false;
        }

        void OnValidate()
        {
            if (WouldSwitchItselfOff(quietLook) || WouldSwitchItselfOff(warningLook) ||
                WouldSwitchItselfOff(steamLook))
                Debug.LogWarning($"{name}: a SteamVent Look is the vent itself or one of its " +
                                 "parents, so switching that look off would switch the whole " +
                                 "vent off with it. Make the art a child of the vent and drag " +
                                 "the child in instead.", this);
        }

        protected override void Awake()
        {
            base.Awake();
            SwitchLooks(IsWarning, IsBursting);
        }

        void Update()
        {
            clock += Haste.DeltaTime;
            ShowTheRightLook();
        }

        protected override void Affect(PlayerLogic wizard) => Disarm(rearmDelay / Haste.WorldScale);

        protected override void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime)
        {
            base.OnPlayerInside(wizard, fixedDeltaTime);

            if (IsBursting && push != Vector2.zero && CanAffect(wizard))
                wizard.Logic.Push(push * Haste.WorldScale, SteamRampup, FullStrengthOnTheGround);
        }

        bool IsTimeBetween(float start, float end)
        {
            float now = TimeIntoCycle;
            return now >= start && now < end;
        }

        void ShowTheRightLook()
        {
            bool warning = IsWarning;
            bool bursting = IsBursting;

            if (warning != isShowingWarning || bursting != isShowingSteam)
                SwitchLooks(warning, bursting);
        }

        void SwitchLooks(bool warning, bool bursting)
        {
            isShowingWarning = warning;
            isShowingSteam = bursting;

            ShowLook(quietLook, !warning && !bursting);
            ShowLook(warningLook, warning);
            ShowLook(steamLook, bursting);
        }

        void OnDrawGizmos()
        {
            var column = GetComponent<Collider2D>();

            if (column == null)
                return;

            Gizmos.color = SteamColour;
            Gizmos.DrawWireCube(column.bounds.center, column.bounds.size);
        }
    }
}
