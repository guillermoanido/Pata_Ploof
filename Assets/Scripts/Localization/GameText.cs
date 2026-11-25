using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Localization
{
    [CreateAssetMenu(menuName = "Falling Wizard/Game Text", fileName = "Game Text")]
    public class GameText : ScriptableObject
    {
        public const string ResourcePath = "Language/Game Text";

        [Tooltip("Every line of words in the game, in English and Spanish. A label shows the line " +
                 "its Localized Text names by Key. Change the words here, or on the label itself: " +
                 "its Localized Text shows this same line and edits it here.")]
        public List<Line> lines = new List<Line>();

        public Line Find(string key) => lines.Find(line => line != null && line.key == key);

        [Serializable]
        public class Line
        {
            [Tooltip("The name a label asks for this line by, like 'menu.play'. Changing it " +
                     "breaks every label still asking for the old name.")]
            public string key = "";

            [TextArea(1, 8)] public string english = "";

            [TextArea(1, 8)] public string spanish = "";
        }
    }
}
