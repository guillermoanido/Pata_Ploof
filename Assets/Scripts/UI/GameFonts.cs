using TMPro;
using UnityEngine;

namespace FallingWizard.UI
{
    [CreateAssetMenu(menuName = "Falling Wizard/Game Fonts", fileName = "Game Fonts")]
    public class GameFonts : ScriptableObject
    {
        public const string ResourcePath = "Game Fonts";

        [Tooltip("The game's font: every text uses it while Pixel Font is ticked in Settings, " +
                 "which is how the game starts. Build your menus with it.")]
        public TMP_FontAsset pixelFont;

        [Tooltip("The plain, easy to read font every text switches to when a player unticks " +
                 "Pixel Font in Settings. Sizes are matched so capital letters stand as tall " +
                 "in either font, and nothing changes in your prefabs.")]
        public TMP_FontAsset plainFont;
    }
}
