using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FallingWizard.Localization
{
    public static class TextBook
    {
        const string ResourcePath = "Language/Text";

        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".csv";

        const char CommentMarker = '#';

        const int KeyColumn = 0;

        const Language NoLanguage = (Language)(-1);

        static readonly Dictionary<string, string[]> Rows = new Dictionary<string, string[]>();

        static readonly int LanguageCount = Enum.GetValues(typeof(Language)).Length;

        static bool isLoaded;

        public static void Load()
        {
            Rows.Clear();
            isLoaded = true;

            var sheet = Resources.Load<TextAsset>(ResourcePath);

            if (sheet == null)
            {
                Debug.LogWarning($"There is no text file at {AssetPath}, so every line in the " +
                                 "game falls back to whatever is typed into the scene or the " +
                                 "spell asset, and no translation is possible.");
                return;
            }

            Fill(new SheetParser().Parse(sheet.text));
        }

        public static bool TryFind(string key, Language language, out string text)
        {
            text = null;

            if (string.IsNullOrEmpty(key))
                return false;

            if (!isLoaded)
                Load();

            if (!Rows.TryGetValue(key, out string[] row))
                return false;

            int column = (int)language;

            if (column < 0 || column >= row.Length)
                return false;

            text = row[column];
            return !string.IsNullOrEmpty(text);
        }

        static void Fill(List<string[]> rows)
        {
            if (rows.Count == 0)
            {
                Debug.LogWarning($"{AssetPath} is empty. The first row has to be the column " +
                                 "headings: key,english,spanish");
                return;
            }

            Language[] columns = ReadHeader(rows[0]);

            for (int i = 1; i < rows.Count; i++)
                Add(rows[i], columns, i);
        }

        static Language[] ReadHeader(string[] header)
        {
            var columns = new Language[header.Length];

            for (int i = 0; i < columns.Length; i++)
                columns[i] = NoLanguage;

            for (int i = KeyColumn + 1; i < header.Length; i++)
            {
                string heading = header[i].Trim();

                if (heading.Length == 0)
                    continue;

                if (Enum.TryParse(heading, true, out Language language))
                    columns[i] = language;
                else
                    Debug.LogWarning($"{AssetPath} has a column headed '{heading}', which is not " +
                                     "a language the game knows, so it is ignored. Add it to the " +
                                     "Language list in Loc.cs to be able to switch to it.");
            }

            return columns;
        }

        static void Add(string[] cells, Language[] columns, int rowNumber)
        {
            if (cells.Length == 0)
                return;

            string key = cells[KeyColumn].Trim();

            if (key.Length == 0 || key[0] == CommentMarker)
                return;

            if (Rows.ContainsKey(key))
                Debug.LogWarning($"{AssetPath} gives the key '{key}' twice, the second time on " +
                                 $"row {rowNumber}. The lower one wins and the one above it is never " +
                                 "shown.");

            Rows[key] = ArrangeByLanguage(cells, columns);
        }

        static string[] ArrangeByLanguage(string[] cells, Language[] columns)
        {
            var row = new string[LanguageCount];

            for (int i = KeyColumn + 1; i < cells.Length && i < columns.Length; i++)
            {
                int column = (int)columns[i];

                if (column >= 0 && column < row.Length)
                    row[column] = cells[i];
            }

            return row;
        }

        class SheetParser
        {
            const char Separator = ',';
            const char Quote = '"';
            const char CarriageReturn = '\r';
            const char NewLine = '\n';
            const char ByteOrderMark = '\uFEFF';

            readonly List<string[]> rows = new List<string[]>();
            readonly List<string> cells = new List<string>();
            readonly StringBuilder cell = new StringBuilder();

            bool isInsideQuotes;

            public List<string[]> Parse(string text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (!isInsideQuotes)
                    {
                        ReadOutsideQuotes(text[i]);
                    }
                    else if (IsDoubledQuote(text, i))
                    {
                        cell.Append(Quote);
                        i++;
                    }
                    else
                    {
                        ReadInsideQuotes(text[i]);
                    }
                }

                if (cell.Length > 0 || cells.Count > 0)
                    EndRow();

                return rows;
            }

            void ReadOutsideQuotes(char character)
            {
                switch (character)
                {
                    case Quote:
                        isInsideQuotes = true;
                        break;

                    case Separator:
                        EndCell();
                        break;

                    case CarriageReturn:
                    case ByteOrderMark:
                        break;

                    case NewLine:
                        EndRow();
                        break;

                    default:
                        cell.Append(character);
                        break;
                }
            }

            static bool IsDoubledQuote(string text, int index) =>
                text[index] == Quote && index + 1 < text.Length && text[index + 1] == Quote;

            void ReadInsideQuotes(char character)
            {
                if (character == Quote)
                    isInsideQuotes = false;
                else if (character != CarriageReturn)
                    cell.Append(character);
            }

            void EndCell()
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }

            void EndRow()
            {
                EndCell();
                rows.Add(cells.ToArray());
                cells.Clear();
            }
        }
    }
}
