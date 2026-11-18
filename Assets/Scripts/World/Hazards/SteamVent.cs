using System;
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

        const float PuffSpeed = 4f;

        const float PuffTravel = 1f;

        const float PuffsPerSquareBox = 20f;

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

        [Tooltip("The puffs that show the steam, the same idea as the Streaks on a Wind zone: a " +
                 "particle system on a child of this vent. Its look is yours - sprite, colour, " +
                 "size, fade - but when the level starts the vent aims it out of its top, sets " +
                 "its speed and fills the trigger collider with it, and it only plays while the " +
                 "steam blasts. So the puffs always show exactly where the steam burns: make the " +
                 "collider taller for a longer blast and the puffs follow. Leave it empty for " +
                 "no puffs.")]
        public ParticleSystem steam;

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
                 "collider on this object - the column the Steam puffs fill - so size that " +
                 "collider to how far the blast should reach.")]
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
            AimTheSteam();
            SwitchLooks(IsWarning, IsBursting);
        }

        void Update()
        {
            if (IsAsleep)
                HoldQuiet();
            else
                clock += Time.deltaTime;

            ShowTheRightLook();
        }

        protected override void Affect(PlayerLogic wizard) => Disarm(rearmDelay);

        protected override void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime)
        {
            base.OnPlayerInside(wizard, fixedDeltaTime);

            if (IsBursting && push != Vector2.zero && CanAffect(wizard))
                wizard.Logic.Push(push, SteamRampup, FullStrengthOnTheGround);
        }

        void AimTheSteam()
        {
            var column = GetComponent<BoxCollider2D>();

            if (steam == null || column == null)
                return;

            Vector2 way = transform.up;

            ParticleSystem.MainModule main = steam.main;
            main.startLifetime = PuffTravel / PuffSpeed;

            ParticleSystem.VelocityOverLifetimeModule velocity = steam.velocityOverLifetime;
            velocity.x = way.x * PuffSpeed;
            velocity.y = way.y * PuffSpeed;
            velocity.z = 0f;

            FillTheColumn(column);
        }

        void FillTheColumn(BoxCollider2D column)
        {
            ParticleSystem.ShapeModule shape = steam.shape;
            shape.position = column.offset;
            shape.scale = new Vector3(column.size.x, column.size.y, 0f);

            ParticleSystem.EmissionModule emission = steam.emission;
            emission.rateOverTime = column.size.x * column.size.y * PuffsPerSquareBox;
        }

        bool IsTimeBetween(float start, float end)
        {
            float now = TimeIntoCycle;
            return now >= start && now < end;
        }

        void HoldQuiet() => clock = CycleLength * (1f - phase);

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
            ShowSteam(bursting);
        }

        void ShowSteam(bool bursting)
        {
            if (steam == null)
                return;

            if (!bursting)
            {
                steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }

            steam.Play();
            steam.Emit(CountPuffsInAFullColumn());
        }

        int CountPuffsInAFullColumn() =>
            Mathf.RoundToInt(steam.emission.rateOverTime.constant * steam.main.startLifetime.constant);

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
