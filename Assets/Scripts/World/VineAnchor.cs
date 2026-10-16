using System;
using System.Collections.Generic;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class VineAnchor : MonoBehaviour
    {
        const float Epsilon = 0.0001f;

        static readonly Color LineColour = new Color(0.42f, 0.75f, 0.38f);
        static readonly Color ReachColour = new Color(0.42f, 0.75f, 0.38f, 0.25f);

        static readonly List<VineAnchor> Hanging = new List<VineAnchor>();

        [Header("Vine")]
        [Tooltip("Where the vine is tied, relative to this object. The wizard swings from here, " +
                 "so put it at the ceiling and not at the loose end. This only moves the swing " +
                 "and the top of the rope - place your knot art there yourself.")]
        public Vector2 knotOffset = Vector2.zero;

        [Tooltip("How far the vine hangs, in boxes. A mage is one box tall.")]
        [Min(0.5f)] public float length = 5f;

        [Tooltip("How far it will lean either side of straight down, in degrees. Lower makes a " +
                 "stiff vine that barely swings.")]
        [Range(0f, 89f)] public float maxSwing = 70f;

        [Tooltip("How close the wizard has to be for the Glow Look to show and for the spell to " +
                 "reach it, in boxes. Measured to the nearest point ON the vine, not to the knot, " +
                 "so a long vine can be caught anywhere along its length.")]
        [Min(0.5f)] public float grabRange = 2.5f;

        [Header("Look")]
        [Tooltip("A child object switched on only while the wizard is close enough to grab this " +
                 "vine, and off the rest of the time. THIS is the tell - if a player never learns " +
                 "what it means, no vine ever gets used. Fill it with your own glow art or " +
                 "animation. Never put the vine itself here, or it switches itself off. Leave it " +
                 "empty and nothing shows.")]
        public GameObject glowLook;

        [Header("The Rope")]
        [Tooltip("The vine itself, as a child with your rope art. It is hidden until the wizard " +
                 "calls it down, then stretched from the knot and turned to follow the swing - " +
                 "that is the vine unrolling. Its width is left exactly as you made it.")]
        public SpriteRenderer rope;

        [Tooltip("Seconds the vine takes to unroll once called.")]
        [Min(0f)] public float unrollTime = 0.25f;

        [Tooltip("Once called down it stays down for the rest of the run. Off makes it roll back " +
                 "up the moment it is let go, so every swing has to be called again.")]
        public bool staysDown = true;

        [NonSerialized] float unrolled;
        [NonSerialized] bool called;
        [NonSerialized] bool showingGlow;

        [NonSerialized] float ropeThickness = 1f;

        public static IReadOnlyList<VineAnchor> All => Hanging;

        public Vector2 Knot => (Vector2)transform.position + knotOffset;

        public Vector2 Tail => Knot + Vector2.down * length;

        public bool IsDown => called;

        void OnEnable() => Hanging.Add(this);

        void OnDisable() => Hanging.Remove(this);

        void Awake()
        {
            ShowGlowLook(false);

            if (rope == null)
                return;

            rope.enabled = false;
            ropeThickness = rope.transform.localScale.x;
        }

        void Update()
        {
            ShowGlowLookWhenNear();
            Unroll();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = LineColour;
            Gizmos.DrawLine(Knot, Tail);

            float sweep = maxSwing * Mathf.Deg2Rad;

            Gizmos.DrawLine(Knot, Knot + new Vector2(Mathf.Sin(-sweep), -Mathf.Cos(sweep)) * length);
            Gizmos.DrawLine(Knot, Knot + new Vector2(Mathf.Sin(sweep), -Mathf.Cos(sweep)) * length);

            Gizmos.color = ReachColour;
            Gizmos.DrawWireSphere(Tail, grabRange);
            Gizmos.DrawWireSphere(Knot, grabRange);
        }

        public float DistanceTo(Vector2 point)
        {
            Vector2 down = Tail - Knot;
            float run = down.sqrMagnitude;

            if (run <= Epsilon)
                return Vector2.Distance(point, Knot);

            float along = Mathf.Clamp01(Vector2.Dot(point - Knot, down) / run);

            return Vector2.Distance(point, Knot + down * along);
        }

        public bool IsWithinReach(Vector2 point) => DistanceTo(point) <= grabRange;

        public void CallDown()
        {
            called = true;

            if (unrollTime <= 0f)
                unrolled = 1f;
        }

        public void RollUp()
        {
            if (staysDown)
                return;

            called = false;
            unrolled = 0f;
        }

        public static VineAnchor Nearest(Vector2 point)
        {
            VineAnchor closest = null;
            float best = float.MaxValue;

            foreach (VineAnchor vine in Hanging)
            {
                float gap = vine.DistanceTo(point);

                if (gap > vine.grabRange || gap >= best)
                    continue;

                best = gap;
                closest = vine;
            }

            return closest;
        }

        float Swing => BeingRidden ? PlayerCharacter.Instance.Logic.vine.Lean : 0f;

        bool WizardIsNear
        {
            get
            {
                PlayerCharacter wizard = PlayerCharacter.Instance;

                return wizard != null &&
                       wizard.Logic.health.IsAlive &&
                       IsWithinReach(wizard.Logic.movement.Position);
            }
        }

        bool BeingRidden
        {
            get
            {
                PlayerCharacter wizard = PlayerCharacter.Instance;

                return wizard != null &&
                       wizard.Logic.IsOnVine &&
                       (wizard.Logic.vine.Anchor - Knot).sqrMagnitude < Epsilon;
            }
        }

        void ShowGlowLookWhenNear()
        {
            bool near = WizardIsNear;

            if (near != showingGlow)
                ShowGlowLook(near);
        }

        void ShowGlowLook(bool near)
        {
            showingGlow = near;

            if (glowLook != null)
                glowLook.SetActive(near);
        }

        void Unroll()
        {
            if (rope == null || rope.sprite == null)
                return;

            if (called && !staysDown && !BeingRidden)
                called = false;

            float want = called ? 1f : 0f;

            unrolled = unrollTime <= 0f
                ? want
                : Mathf.MoveTowards(unrolled, want, Time.deltaTime / unrollTime);

            rope.enabled = unrolled > Epsilon;

            if (!rope.enabled)
                return;

            Stretch(unrolled, Swing);
        }

        void Stretch(float howFar, float lean)
        {
            Vector2 unit = rope.sprite.bounds.size;

            if (unit.x <= Epsilon || unit.y <= Epsilon)
                return;

            float hangs = length * howFar;
            float tilt = lean * Mathf.Deg2Rad;

            var along = new Vector2(Mathf.Sin(tilt), -Mathf.Cos(tilt));

            rope.transform.position = Knot + along * (hangs * 0.5f);
            rope.transform.rotation = Quaternion.Euler(0f, 0f, lean);

            rope.transform.localScale =
                new Vector3(ropeThickness, Mathf.Max(Epsilon, hangs) / unit.y, 1f);
        }
    }
}
