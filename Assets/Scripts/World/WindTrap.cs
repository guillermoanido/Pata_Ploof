using System;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class WindTrap : WindZone2D
    {
        const float ShortestCycle = 0.1f;

        const float KickGrace = 0.2f;

        const float NotArmed = -1f;

        const float Half = 0.5f;

        const float StrongPush = 6f;

        const float RiskyTumbleScale = 0.5f;

        const float TurbineRampup = 60f;
        const float TurbineGroundScale = 0.6f;

        static readonly Vector2 TurbinePush = new Vector2(12f, 0f);

        [Header("Turbine")]
        [Tooltip("Seconds it sits shut between blasts. This is the window the player has to get " +
                 "across, so read it next to how long the crossing is: at a run of 4 boxes a " +
                 "second, 2.5 seconds is ten boxes of ground. The last part of it is spent " +
                 "opening, so the genuinely safe stretch is this minus Shutter Travel.")]
        [Min(0f)] public float shutSeconds = 2.5f;

        [Tooltip("Seconds it blows at full strength, with the Blowing Look switched on. Short is " +
                 "kinder than weak: a brief hard gale is something you time, a long soft one is " +
                 "something you wait out.")]
        [Min(0f)] public float openSeconds = 1.25f;

        [Tooltip("Seconds it spends winding up before it blows, with the Warning Look switched " +
                 "on the whole time. It is taken out of the shut time rather than added on - so " +
                 "Open Seconds really is how long it blows - and the push eases away over the " +
                 "same time once a blast ends. THIS IS THE WHOLE WARNING. Set it to 0 and the " +
                 "trap fires with no tell at all, which is the one way to make this thing " +
                 "unfair. Half a second is about as short as a player can act on.")]
        [Min(0f)] public float shutterTravel = 0.6f;

        [Tooltip("Where in the cycle this one starts, as a fraction of the whole thing. A row " +
                 "of turbines all left at 0 fire in unison, which is one obstacle; set them to " +
                 "0, 0.33 and 0.66 and they fire in a stagger you have to run through, which is " +
                 "a corridor. It is a fraction rather than seconds so that retuning the timings " +
                 "later keeps the stagger.")]
        [Range(0f, 1f)] public float phase = 0f;

        [Tooltip("Let the Haste spell slow this turbine down along with everything else. Leave " +
                 "it on: the spell exists to buy you time to get through something, and a vent " +
                 "that kept its normal rhythm while the whole world waded would be the one " +
                 "thing in the level the spell does not help with. It also keeps the total " +
                 "shove per blast the same - gentler for longer, not weaker.")]
        public bool hasteSlowsCycle = true;

        [Header("Blast")]
        [Tooltip("The punch, in boxes per second, landed once on the step the trap reaches full " +
                 "strength. The push holds the wizard at a speed for as long as they are in " +
                 "it; this is added to whatever they are already doing and then let go of, " +
                 "which is what makes this a launcher rather than a fan: (0,14) throws them 3.3 " +
                 "boxes up against a jump of 1.5, and (7,0) fires them sideways at nearly twice " +
                 "a run. It turns with the object, like the push. It only catches someone who " +
                 "was already inside when it fired - walk into a running turbine and you get " +
                 "the gale instead.")]
        public Vector2 kick = new Vector2(7f, 0f);

        [Tooltip("Seconds the wizard cannot steer for after the punch, so the launch is the " +
                 "arc it looked like rather than something they immediately drive out of. Keep " +
                 "it short - a sixth of a second reads as being hit, half a second reads as the " +
                 "controller having come unplugged. Note that sideways wind is also suspended " +
                 "for this long, by design: the punch owns those few frames and the gale takes " +
                 "over afterwards.")]
        [Min(0f)] public float kickLockout = 0.15f;

        [Tooltip("Send them tumbling as well as flying. Off by default, because a turbine you " +
                 "can steer out of is a ride and a turbine that knocks you down is a tax. Turn " +
                 "it on for one placed over a drop you meant them to fall down.")]
        public bool tumbles = false;

        [Tooltip("How much of the push reaches a wizard who is ALREADY tumbling, as a fraction. " +
                 "This is not a taste setting. Wind on a ragdoll is added straight to their " +
                 "speed every step with nothing pushing back - there is no running to clamp it " +
                 "and no drag to bleed it off - so at 1 a strong sideways trap accelerates a " +
                 "tumbling wizard forever and posts them off the edge of the level, and the " +
                 "Bubble spell multiplies that by three and a half. A quarter still carries " +
                 "them through the blast without the trap owning them.")]
        [Range(0f, 1f)] public float tumbleScale = 0.25f;

        [Header("Look")]
        [Tooltip("A child object switched on only while the trap is winding up - the Shutter " +
                 "Travel seconds just before it blows - and off the rest of the time. This is " +
                 "the one thing the player learns to read, so fill it with art or an animation " +
                 "nothing else in the level uses. Never put the trap itself here, or it " +
                 "switches itself off. Leave it empty and there is no warning to see.")]
        public GameObject warningLook;

        [Tooltip("A child object switched on only while the trap blows at full strength, and " +
                 "off the rest of the time. Fill it with your own art or animation and size it " +
                 "to the trigger collider, because that collider is where the wind reaches. It " +
                 "goes off the moment the blast ends, while the push eases away over Shutter " +
                 "Travel. Leave it empty and nothing shows.")]
        public GameObject blowingLook;

        [NonSerialized] float clock;
        [NonSerialized] float armedUntil = NotArmed;
        [NonSerialized] bool wasOpen;

        [NonSerialized] bool isShowingWarning;
        [NonSerialized] bool isShowingBlowing;

        float CycleLength =>
            Mathf.Max(ShortestCycle, Mathf.Max(0f, shutSeconds) + Mathf.Max(0f, openSeconds));

        float TimeIntoCycle => Mathf.Repeat(clock + phase * CycleLength, CycleLength);

        bool IsOpen => TimeIntoCycle >= Mathf.Max(0f, shutSeconds);

        bool IsWindingUp => !IsOpen && WindUp > 0f;

        bool HasStrongPush => push.sqrMagnitude > StrongPush * StrongPush;

        float Travel => Mathf.Clamp(shutterTravel, 0f, Mathf.Max(0f, shutSeconds) * Half);

        float Openness
        {
            get
            {
                float shut = Mathf.Max(0f, shutSeconds);

                if (shut <= 0f)
                    return 1f;

                float now = TimeIntoCycle;

                if (now >= shut)
                    return 1f;

                float travel = Travel;

                if (travel <= 0f)
                    return 0f;

                if (now < travel)
                    return 1f - now / travel;

                if (now >= shut - travel)
                    return (now - (shut - travel)) / travel;

                return 0f;
            }
        }

        float WindUp
        {
            get
            {
                float shut = Mathf.Max(0f, shutSeconds);

                if (shut <= 0f)
                    return 1f;

                float now = TimeIntoCycle;

                if (now >= shut)
                    return 1f;

                float travel = Travel;

                if (travel <= 0f || now < shut - travel)
                    return 0f;

                return (now - (shut - travel)) / travel;
            }
        }

        protected override void Reset()
        {
            base.Reset();

            push = TurbinePush;
            rampup = TurbineRampup;
            groundScale = TurbineGroundScale;

            affectsOnStaff = false;

            affectsRagdolled = true;
        }

        void OnValidate() => Validate();

        protected override void Awake()
        {
            base.Awake();

            Validate();
            SwitchLooks(IsWindingUp, IsOpen);
        }

        void Update()
        {
            AdvanceTheCycle();
            ShowTheRightLook();
        }

        protected override void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime)
        {
            if (!CanAffect(wizard))
                return;

            float openness = Openness;

            if (openness <= 0f)
                return;

            PlayerLogic logic = wizard.Logic;
            bool tumbling = logic.State == PlayerState.Ragdoll;

            float strength = openness * (tumbling ? tumbleScale : 1f);

            logic.Push(WorldPush * strength * Haste.WorldScale, rampup, groundScale);

            if (openness < 1f || tumbling || clock > armedUntil)
                return;

            armedUntil = NotArmed;
            Blast(logic);
        }

        void Validate()
        {
            shutSeconds = Mathf.Max(0f, shutSeconds);
            openSeconds = Mathf.Max(0f, openSeconds);
            shutterTravel = Mathf.Max(0f, shutterTravel);

            WarnAboutSettingsLeftAtZero();
            WarnAboutTheShutterTravel();
            WarnAboutTumblingWizards();
        }

        void WarnAboutSettingsLeftAtZero()
        {
            if (shutSeconds <= 0f && openSeconds <= 0f)
                Debug.LogWarning("WindTrap has a shut time and an open time of 0, so it has no " +
                                 "cycle at all and simply blows forever. Give it some of each, " +
                                 "or use a plain Wind zone instead - that is what one is.", this);

            if (push.sqrMagnitude < Epsilon && kick.sqrMagnitude < Epsilon)
                Debug.LogWarning("WindTrap has a push and a kick of 0, so it opens and closes " +
                                 "and does nothing to anyone standing in it. Put a number in " +
                                 "Kick to throw the wizard, or in Push to hold them.", this);
        }

        void WarnAboutTheShutterTravel()
        {
            if (shutSeconds > 0f && shutterTravel <= 0f)
                Debug.LogWarning("WindTrap has a Shutter Travel of 0, so it snaps from shut to " +
                                 "full blast in a single frame and the Warning Look never " +
                                 "shows. Half a second is about the shortest tell that is " +
                                 "still fair.", this);

            if (shutterTravel > shutSeconds * Half && shutSeconds > 0f)
                Debug.LogWarning("WindTrap's Shutter Travel is more than half its shut time, so " +
                                 "it is always winding up or easing off and never properly " +
                                 "shut. It is clamped at runtime, but the numbers in the " +
                                 "inspector will not be the ones you get.", this);
        }

        void WarnAboutTumblingWizards()
        {
            if (affectsRagdolled && tumbleScale > RiskyTumbleScale && HasStrongPush)
                Debug.LogWarning("WindTrap pushes harder than 6 boxes a second and lets more " +
                                 "than half of it reach a tumbling wizard. Wind on a ragdoll " +
                                 "has nothing pushing back, so it keeps accumulating - a trip " +
                                 "inside this one is a wizard leaving the level sideways. Drop " +
                                 "Tumble Scale.", this);
        }

        void AdvanceTheCycle()
        {
            clock += hasteSlowsCycle ? Haste.DeltaTime : Time.deltaTime;

            bool isOpenNow = IsOpen;

            if (isOpenNow && !wasOpen)
                armedUntil = clock + KickGrace;

            wasOpen = isOpenNow;
        }

        void ShowTheRightLook()
        {
            bool warning = IsWindingUp;
            bool blowing = IsOpen;

            if (warning != isShowingWarning || blowing != isShowingBlowing)
                SwitchLooks(warning, blowing);
        }

        void SwitchLooks(bool warning, bool blowing)
        {
            isShowingWarning = warning;
            isShowingBlowing = blowing;

            if (warningLook != null)
                warningLook.SetActive(warning);

            if (blowingLook != null)
                blowingLook.SetActive(blowing);
        }

        void Blast(PlayerLogic wizard)
        {
            if (tumbles)
                wizard.TryTrip();

            wizard.Shove(TurnWithThisObject(kick), kickLockout);
        }
    }
}
