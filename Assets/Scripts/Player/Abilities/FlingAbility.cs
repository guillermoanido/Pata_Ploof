using System.Collections.Generic;
using FallingWizard.UI;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Fling", fileName = "Fling")]
    public class FlingAbility : Ability
    {
        const float StickDeadzone = 0.2f;

        [Header("Power")]
        [Tooltip("Launch speed at no charge, in boxes per second.")]
        [Min(0f)] public float minSpeed = 6f;

        [Tooltip("Launch speed at full charge. A standing jump leaves at about 9, for scale.")]
        [Min(0f)] public float maxSpeed = 14f;

        [Tooltip("Seconds of holding to wind up to full power.")]
        [Min(0.05f)] public float chargeTime = 0.7f;

        [Header("Angle")]
        [Tooltip("Flattest shot, in degrees above horizontal - stick pushed all the way down.")]
        [Range(0f, 90f)] public float minAngle = 15f;

        [Tooltip("Steepest shot - stick pushed all the way up.")]
        [Range(0f, 90f)] public float maxAngle = 80f;

        [Tooltip("Angle with the stick neutral, so a bare hold-and-release is still a sensible " +
                 "jump rather than a mistake.")]
        [Range(0f, 90f)] public float restAngle = 55f;

        [Header("Flight")]
        [Tooltip("Extra seconds of locked steering on top of the flight the arc predicted. A " +
                 "little makes the landing read as deliberate; a lot takes the recovery away.")]
        [Min(0f)] public float extraLock = 0.05f;

        [Header("The Look Ahead")]
        [Tooltip("What the flight is allowed to notice. Ground so it knows where you land, " +
                 "Hazard so the arrow can warn you what you are about to fly through. This is " +
                 "still simulated even though the arrow does not draw the path - it is what " +
                 "sets the landing and how long steering stays locked.")]
        public LayerMask seen = (1 << 6) | (1 << 8);

        [Tooltip("Seconds per simulated step. Smaller is smoother and more accurate.")]
        [Min(0.005f)] public float step = 0.02f;

        [Tooltip("Most steps to simulate, whatever else happens.")]
        [Range(8, 400)] public int steps = 200;

        [Tooltip("How far ahead to look, in boxes. The line stops here even if it never lands.")]
        [Min(1f)] public float lookAhead = 16f;

        [Header("Arrow")]
        [Tooltip("Leave empty and the arrow is drawn from two boxes, a shaft and a turned square " +
                 "for the head. Give it a real arrow sprite and that is drawn whole instead - " +
                 "draw it pointing RIGHT, because it is turned from there.")]
        public Sprite arrowArt;

        [Tooltip("How long the arrow is with no charge, in boxes.")]
        [Min(0.1f)] public float minLength = 1.1f;

        [Tooltip("How long it is at full charge. This is a read on power, not a promise about " +
                 "distance - a full throw carries much further than the arrow is long.")]
        [Min(0.1f)] public float maxLength = 3f;

        [Tooltip("How thick the shaft is, in boxes. Ignored when an arrow sprite is given.")]
        [Min(0.01f)] public float thickness = 0.12f;

        [Tooltip("How big the head is, in boxes, measured along the arrow.")]
        [Min(0.05f)] public float headSize = 0.45f;

        public Color safe = new Color(0.95f, 0.93f, 0.75f, 0.85f);

        [Tooltip("Drawn when the flight passes through something that will change where you end " +
                 "up - hazards here are things you fly through, not walls.")]
        public Color danger = new Color(0.95f, 0.35f, 0.30f, 0.9f);

        [Tooltip("Sorting order. Above the level, so the arrow is never drawn inside a wall.")]
        public int sortingOrder = 20;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override string WhyNot(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal ? null : $"you are {wizard.State}";

        public override void ModifyStats(PlayerLogic wizard, PlayerLogic.Modifiers stats)
        {
            if (wizard.spellbook.StateOf<Charge>(this).winding)
                stats.Rooted = true;
        }

        public override void OnHeld(PlayerLogic wizard, float heldSeconds, float fixedDeltaTime)
        {
            Charge charge = wizard.spellbook.StateOf<Charge>(this);

            if (wizard.State != PlayerState.Normal)
            {
                Drop(wizard);
                return;
            }

            if (!charge.winding)
            {
                charge.winding = true;
                charge.angle = restAngle;
                charge.facing = wizard.movement.Facing;
            }

            Aim(wizard, charge, fixedDeltaTime);
            Draw(wizard, charge);
        }

        public override void OnChargeLost(PlayerLogic wizard) => Drop(wizard);

        public override void OnReleased(PlayerLogic wizard, float heldSeconds)
        {
            Charge charge = wizard.spellbook.StateOf<Charge>(this);

            if (!charge.winding)
                return;

            charge.winding = false;
            charge.arrow?.Hide();

            if (wizard.State != PlayerState.Normal || !wizard.spellbook.Fire(this))
                return;

            Vector2 launch = Launch(wizard, charge);

            wizard.PredictArc(launch, Look(wizard), charge.path, out PlayerLogic.Movement.ArcEnd end);
            wizard.Fling(launch, end.Seconds + extraLock);

            charge.wound = 0f;
        }

        public override float ChargeFor(PlayerLogic wizard)
        {
            Charge charge = wizard.spellbook.StateOf<Charge>(this);
            return charge.winding ? Mathf.Clamp01(charge.wound) : -1f;
        }

        public override void OnRunReset(PlayerLogic wizard) => Drop(wizard);

        public override void OnUnequipped(PlayerLogic wizard)
        {
            Charge charge = wizard.spellbook.StateOf<Charge>(this);

            Drop(wizard);

            if (charge.arrow != null)
                Destroy(charge.arrow.gameObject);

            charge.arrow = null;
        }

        protected override void Validate()
        {
            maxSpeed = Mathf.Max(minSpeed, maxSpeed);
            maxLength = Mathf.Max(minLength, maxLength);
            maxAngle = Mathf.Max(minAngle, maxAngle);
            restAngle = Mathf.Clamp(restAngle, minAngle, maxAngle);

            if (tiers != null)
                foreach (Tier tier in tiers)
                    tier?.Validate();

            CheckTiers(tiers != null ? tiers.Length : 0);
        }

        void Aim(PlayerLogic wizard, Charge charge, float fixedDeltaTime)
        {
            Tier tier = Of(wizard);

            charge.wound = Mathf.Min(1f,
                charge.wound + fixedDeltaTime / Mathf.Max(0.05f, tier.chargeTime));

            Vector2 stick = wizard.Steering.Move;

            if (stick.sqrMagnitude < StickDeadzone * StickDeadzone)
                return;

            if (Mathf.Abs(stick.x) > StickDeadzone)
                charge.facing = stick.x < 0f ? -1 : 1;

            charge.angle = stick.y >= 0f
                ? Mathf.Lerp(restAngle, maxAngle, stick.y)
                : Mathf.Lerp(restAngle, minAngle, -stick.y);
        }

        Vector2 Launch(PlayerLogic wizard, Charge charge)
        {
            Tier tier = Of(wizard);

            float speed = Mathf.Lerp(minSpeed, tier.maxSpeed, Mathf.Clamp01(charge.wound));
            float radians = Mathf.Deg2Rad * Mathf.Clamp(charge.angle, 0f, 90f);

            return new Vector2(charge.facing * Mathf.Cos(radians), Mathf.Sin(radians)) * speed;
        }

        PlayerLogic.Movement.ArcSettings Look(PlayerLogic wizard) =>
            new PlayerLogic.Movement.ArcSettings
            {
                Layers = seen,
                Step = step,
                Steps = steps,
                Distance = lookAhead,
            };

        void Draw(PlayerLogic wizard, Charge charge)
        {
            if (charge.arrow == null)
                charge.arrow = FlingArrow.Make(arrowArt, thickness, headSize, safe, danger,
                    sortingOrder);

            Vector2 launch = Launch(wizard, charge);

            wizard.PredictArc(launch, Look(wizard), charge.path,
                out PlayerLogic.Movement.ArcEnd end);

            Vector2 from = charge.path.Count > 0
                ? charge.path[0]
                : (Vector2)wizard.Rig.position;

            float length = Mathf.Lerp(minLength, maxLength, Mathf.Clamp01(charge.wound));

            charge.arrow.Show(from, launch, length, end.Hazard, charge.wound);
        }

        void Drop(PlayerLogic wizard)
        {
            Charge charge = wizard.spellbook.StateOf<Charge>(this);

            charge.winding = false;
            charge.wound = 0f;
            charge.arrow?.Hide();
        }

        Tier Of(PlayerLogic wizard) =>
            TierFor(tiers, wizard.spellbook.RankOf(this)) ?? new Tier();

        [System.Serializable]
        public class Tier
        {
            [Min(0f)] public float maxSpeed = 14f;
            [Min(0.05f)] public float chargeTime = 0.7f;

            public void Validate()
            {
                maxSpeed = Mathf.Max(0f, maxSpeed);
                chargeTime = Mathf.Max(0.05f, chargeTime);
            }
        }

        public class Charge
        {
            public readonly List<Vector2> path = new List<Vector2>(256);

            public FlingArrow arrow;
            public bool winding;
            public float wound;
            public float angle = 55f;
            public int facing = 1;
        }
    }
}
