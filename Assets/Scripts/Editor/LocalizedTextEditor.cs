using FallingWizard.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FallingWizard.EditorTools
{
    [CustomEditor(typeof(LocalizedText))]
    public class LocalizedTextEditor : Editor
    {
        const float WordsHeight = 54f;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("key"));
            serializedObject.ApplyModifiedProperties();

            var label = (LocalizedText)target;
            GameText book = Resources.Load<GameText>(GameText.ResourcePath);

            if (book == null)
            {
                EditorGUILayout.HelpBox($"There is no Game Text at {TextBook.AssetPath}.", MessageType.Warning);
                return;
            }

            if (string.IsNullOrEmpty(label.key))
            {
                EditorGUILayout.HelpBox("No Key, so this label is left alone in every language.", MessageType.Info);
                return;
            }

            ShowLine(book, label);
        }

        static void ShowLine(GameText book, LocalizedText label)
        {
            GameText.Line line = book.Find(label.key);

            if (line == null)
            {
                OfferNewLine(book, label);
                return;
            }

            EditorGUI.BeginChangeCheck();
            string english = Words("English", line.english);
            string spanish = Words("Spanish", line.spanish);

            if (EditorGUI.EndChangeCheck())
                Save(book, line, english, spanish, label);

            if (GUILayout.Button("Show every line in Game Text"))
                Selection.activeObject = book;
        }

        static string Words(string language, string words)
        {
            EditorGUILayout.LabelField(language);
            return EditorGUILayout.TextArea(words, GUILayout.MinHeight(WordsHeight));
        }

        static void OfferNewLine(GameText book, LocalizedText label)
        {
            EditorGUILayout.HelpBox($"Game Text has no line keyed '{label.key}' yet, so the game " +
                                    "would show the key itself.", MessageType.Warning);

            if (!GUILayout.Button("Add this line to Game Text"))
                return;

            var text = label.GetComponent<TMP_Text>();
            Undo.RecordObject(book, "Add text line");
            book.lines.Add(new GameText.Line { key = label.key, english = text != null ? text.text : "" });
            EditorUtility.SetDirty(book);
        }

        static void Save(GameText book, GameText.Line line, string english, string spanish, LocalizedText label)
        {
            Undo.RecordObject(book, "Edit text");
            line.english = english;
            line.spanish = spanish;
            EditorUtility.SetDirty(book);

            var text = label.GetComponent<TMP_Text>();

            if (text == null)
                return;

            Undo.RecordObject(text, "Edit text");
            text.text = english;
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }
    }
}
