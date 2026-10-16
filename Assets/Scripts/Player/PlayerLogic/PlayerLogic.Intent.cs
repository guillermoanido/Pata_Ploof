using UnityEngine;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
        public struct Intent
        {
            public Vector2 Move;
            public bool WasJumpPressed;
            public bool IsJumpHeld;
            public bool IsWalking;
            public bool IsLookingDown;

            public float Lean => Move.y;

            public Command Command => new Command
            {
                Steer = Move.x,
                IsJumpHeld = IsJumpHeld,
                IsWalking = IsWalking,
            };
        }

        public struct Command
        {
            public float Steer;
            public bool IsJumpHeld;
            public bool IsWalking;
        }
    }
}
