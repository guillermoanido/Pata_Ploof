using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FallingWizard.Localization
{
    public static class TextBook
    {
        public const string ResourcePath = "Language/Text";

        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".csv";

        const char Separator = ',';
        const char Quote = '"';
        const char Comment = '#';
        const char Return = '\r';
        const char NewLine = '\n';
        const char Bom = '\uFEFF';

        const int KeyColumn = 0;

        static readonly Dictionary<string, string[]> Rows = new Dictionary<string, string[]>();

        static readonly int LanguageCount = Enum.GetValues(typeof(Language)).Length;

        public static bool Loaded { get; private set; }

        public static IEnumerable<string> Keys => Rows.Keys;

        public static void Load()
        {
            Rows.Clear();
            Loaded = true;

            var sheet = Resources.Load<TextAsset>(ResourcePath);

            if (sheet == null)
            {
                Debug.LogWarning($"There is no text file at {AssetPath}, so every line in the " +
                                 "game falls back to whatever is typed into the scene or the " +
                                 "spell asset, and no translation is possible.");
                return;
            }

            Fill(Parse(sheet.text));
        }

        public static bool TryFind(string key, Language language, out string text)
        {
            text = null;

            if (string.IsNullOrEmpty(key))
                return false;

            if (!Loaded)
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
                columns[i] = (Language)(-1);

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

        static void Add(string[] cells, Language[] columns, int row)
        {
            if (cells.Length == 0)
                return;

            string key = cells[KeyColumn].Trim();

            if (key.Length == 0 || key[0] == Comment)
                return;

            if (Rows.ContainsKey(key))
                Debug.LogWarning($"{AssetPath} gives the key '{key}' twice, the second time on " +
                                 $"row {row}. The lower one wins and the one above it is never " +
                                 "shown.");

            var line = new string[LanguageCount];

            for (int i = KeyColumn + 1; i < cells.Length && i < columns.Length; i++)
            {
                int column = (int)columns[i];

                if (column >= 0 && column < line.Length)
                    line[column] = cells[i];
            }

            Rows[key] = line;
        }

        static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (quoted)
                {
                    if (c != Quote)
                    {
                        if (c != Return)
                            cell.Append(c);
                    }
                    else if (i + 1 < text.Length && text[i + 1] == Quote)
                    {
                        cell.Append(Quote);
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }

                    continue;
                }

                switch (c)
                {
                    case Quote:
                        quoted = true;
                        break;

                    case Separator:
                        cells.Add(Take(cell));
                        break;

                    case Return:
                    case Bom:
                        break;

                    case NewLine:
                        cells.Add(Take(cell));
                        rows.Add(cells.ToArray());
                        cells.Clear();
                        break;

                    default:
                        cell.Append(c);
                        break;
                }
            }

            if (cell.Length > 0 || cells.Count > 0)
            {
                cells.Add(Take(cell));
                rows.Add(cells.ToArray());
            }

            return rows;
        }

        static string Take(StringBuilder cell)
        {
            string text = cell.ToString();
            cell.Clear();
            return text;
        }
    }
}
