using System;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Collider2D))]
    [DefaultExecutionOrder(RunAfterTheWizard)]
    public class PressurePlate : MonoBehaviour
    {
        const int RunAfterTheWizard = 1;

        const float ReachBelowTheFeet = 0.1f;

        [Header("Plate")]
        [Tooltip("Seconds the wizard has to stand on the plate before it throws them. Step off " +
                 "sooner and the count starts again from nothing next time. It counts while any " +
                 "part of the wizard is over this object's collider, so they are on it for the " +
                 "plate's width PLUS their own: walking across a 1-box plate takes most of a " +
                 "second, running across takes under half. Try it at a walk before settling on " +
                 "a number. The Haste spell slows the count along with the rest of the world. " +
                 "0 throws them the moment they step on.")]
        [Min(0f)] public float secondsToLaunch = 1f;

        [Tooltip("How high it throws the wizard, in boxes. Worked out from gravity, so 3 really " +
                 "is three boxes. CAREFUL ABOVE 3: the fall back down is measured from the top of " +
                 "the throw, and falls of more than 3 boxes cost hearts (the wizard's Safe Fall " +
                 "Distance), so above 3 the landing starts to hurt. So does a throw of 3 that " +
                 "comes down somewhere lower than the plate.")]
        [Min(0f)] public float launchHeight = 3f;

        [Tooltip("Sideways speed added to the throw, in boxes per second. Positive throws them " +
                 "right, negative left, 0 straight up. It is always the same way, whichever way " +
                 "they walked on. In the air their own steering takes over at once, so small " +
                 "numbers are a nudge rather than a throw - with Tumbles on there is no steering, " +
                 "and the same number carries them much further.")]
        public float sidewaysPush = 0f;

        [Tooltip("Send the wizard tumbling as well as up, like the slime does. The tumble adds " +
                 "its own shove the way they are facing, on top of the push above, and they " +
                 "cannot steer until they get up. Off by default: a plate that lifts you is a " +
                 "ride, a plate that knocks you down is a tax.")]
        public bool tumbles = false;

        [Tooltip("Seconds after a throw before it can start counting again. Until then it shows " +
                 "its idle look and takes no notice of the wizard. It mostly matters for low " +
                 "throws, which can land them back on the plate before this runs out.")]
        [Min(0f)] public float rearmSeconds = 0.5f;

        [Header("Look")]
        [Tooltip("Shown while nobody is stood on the plate, and while it re-arms after a throw. " +
                 "Make it a child object and fill it with your own art or animation - the plate " +
                 "only ever switches it on and off. Never the plate itself or one of its " +
                 "parents, or the plate switches itself off with it. Leave it empty for no " +
                 "idle look.")]
        public GameObject idleLook;

        [Tooltip("Shown while the wizard is stood on the plate and it is counting down to the " +
                 "throw. A child object again, switched on and off and nothing else. For a plate " +
                 "that sinks further as the count runs out, give this object an animation as " +
                 "long as Seconds To Launch.")]
        public GameObject pressedLook;

        [NonSerialized] Collider2D hitbox;
        [NonSerialized] float secondsStoodOn;
        [NonSerialized] float readyAt;
        [NonSerialized] bool isShowingPressed;

        bool IsRearming => Time.time < readyAt;

        void Reset()
        {
            var shape = GetComponent<Collider2D>();

            if (shape != null)
                shape.isTrigger = true;
        }

        void OnValidate()
        {
            if (WouldSwitchThePlateOff(idleLook) || WouldSwitchThePlateOff(pressedLook))
                Debug.LogWarning($"{name}: a PressurePlate look is the plate itself or one of its " +
                                 "parents, so switching that look off switches the whole plate " +
                                 "off with it. Put the art on a child object and use that " +
                                 "instead.", this);
        }

        void Awake()
        {
            hitbox = GetComponent<Collider2D>();
            SwitchTheLooks();
        }

        void FixedUpdate()
        {
            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (IsRearming || !IsStoodOnBy(wizard))
            {
                PopBackUp();
                return;
            }

            secondsStoodOn += Haste.FixedDeltaTime;

            if (secondsStoodOn >= secondsToLaunch)
                LaunchTheWizard(wizard.Logic);
            else
                ShowTheRightLook(true);
        }

        bool WouldSwitchThePlateOff(GameObject look) =>
            look != null && transform.IsChildOf(look.transform);

        bool IsStoodOnBy(PlayerCharacter wizard)
        {
            if (wizard == null || wizard.Hitbox == null || hitbox == null || !hitbox.enabled)
                return false;

            PlayerLogic logic = wizard.Logic;

            if (!logic.health.IsAlive || logic.State != PlayerState.Normal)
                return false;

            return logic.movement.IsGrounded && IsTouching(hitbox.bounds, wizard.Hitbox.bounds);
        }

        static bool IsTouching(Bounds plate, Bounds body) =>
            body.min.x <= plate.max.x && body.max.x >= plate.min.x &&
            body.max.y >= plate.min.y && body.min.y - plate.max.y <= ReachBelowTheFeet;

        void LaunchTheWizard(PlayerLogic wizard)
        {
            PopBackUp();
            readyAt = Time.time + rearmSeconds;

            if (tumbles)
                wizard.TryTrip();

            wizard.TryBounce(launchHeight, sidewaysPush, true);
        }

        void PopBackUp()
        {
            secondsStoodOn = 0f;
            ShowTheRightLook(false);
        }

        void ShowTheRightLook(bool pressed)
        {
            if (pressed == isShowingPressed)
                return;

            isShowingPressed = pressed;
            SwitchTheLooks();
        }

        void SwitchTheLooks()
        {
            if (idleLook != null)
                idleLook.SetActive(!isShowingPressed);

            if (pressedLook != null)
                pressedLook.SetActive(isShowingPressed);
        }
    }
}
