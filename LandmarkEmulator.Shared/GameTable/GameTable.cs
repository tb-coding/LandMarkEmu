using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace LandmarkEmulator.Shared.GameTable
{
    public class GameTable<T> where T : class, new()
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public IEnumerable<T> Entries => entries.Values.ToList();
        public int RecordCount => entries.Count;
        /// <summary>The highest id (the .txt files aren't sorted by id, so not the last row's).</summary>
        public ulong MaxId => entries.Count == 0 ? 0 : entries.Keys.Max();
        /// <summary>
        /// What did not fit the model when the table was read (older client builds have fewer columns, and blanks in numeric
        /// columns); null when everything fit. The rows are kept with the model's default in those fields.
        /// </summary>
        public string LoadWarning { get; private set; }

        private Dictionary<ulong, T> entries = new();

        /// <summary>
        /// The client's tables write numbers the invariant way ("2.8"). Parsed with the machine's culture, a server on a
        /// comma-decimal Windows (German, Turkish …) read 2.8 as 28 (the '.' taken as a thousands separator): a profile's
        /// camera height and capsule radius, a crafting station's repair cost per point, all ten times or more too big.
        /// </summary>
        private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;
        private static readonly Dictionary<FieldInfo, GameTableFieldArrayAttribute> attributeCache;
        private bool _hasId = true;

        /// <summary>
        /// Initiailise a new <see cref="GameTable{T}"/> container for a data type.
        /// </summary>
        static GameTable()
        {
            attributeCache = new Dictionary<FieldInfo, GameTableFieldArrayAttribute>();
            foreach (FieldInfo modelField in typeof(T).GetFields())
            {
                GameTableFieldArrayAttribute attribute = modelField.GetCustomAttribute<GameTableFieldArrayAttribute>();
                attributeCache.Add(modelField, attribute);
            }
        }

        /// <summary>
        /// Initiailise a new <see cref="GameTable{T}"/> container for a data type, with a given file path and whether it has IDs.
        /// </summary>
        public GameTable(string path, bool hasId)
        {
            _hasId = hasId;

            using (StreamReader reader = File.OpenText(path))
            {
                ReadEntries(reader, Path.GetFileName(path));
            }
        }

        /// <summary>
        /// Reads the entries from the file provided as a <see cref="StreamReader"/>.
        /// </summary>
        private void ReadEntries(StreamReader reader, string tableName)
        {
            PropertyInfo[] typeFields = typeof(T).GetProperties();

            // Per field, how many rows had nothing for it, a blank number or a value that would not parse.
            var missing = new Dictionary<string, int>();
            var blank = new Dictionary<string, int>();
            var bad = new Dictionary<string, int>();
            string[] header = Array.Empty<string>();
            int columns = 0;

            uint recordCount = 0;
            string line = String.Empty;
            while ((line = reader.ReadLine()) != null)
            {
                // Skip header
                if (recordCount == 0)
                {
                    header = Regex.Split(line, @"\^");
                    // Each line ends in the separator: the last piece is not a column.
                    columns = header[^1].Length == 0 ? header.Length - 1 : header.Length;
                    recordCount++;
                    continue;
                }

                if (String.IsNullOrWhiteSpace(line))
                    continue;

                // Initiailise new entry for GameTable
                var entry = new T();

                // Create strings of each
                var pieces = Regex.Split(line, @"\^");
                for (int i = 0; i < typeFields.Length; i++)
                {
                    PropertyInfo modelField = typeFields[i];
                    TypeCode typeCode = Type.GetTypeCode(modelField.PropertyType);

                    // A field this build's table has no column for keeps the model's default.
                    if (i >= pieces.Length || (i >= columns && pieces[i].Length == 0))
                    {
                        Count(missing, modelField.Name);
                        continue;
                    }

                    if (pieces[i].Length == 0 && typeCode != TypeCode.String)
                    {
                        Count(blank, modelField.Name);
                        continue;
                    }

                    try
                    {
                        switch (typeCode)
                        {
                            case TypeCode.Boolean:
                                modelField.SetValue(entry, Convert.ToBoolean(Byte.Parse(pieces[i], Invariant)));
                                break;
                            case TypeCode.Byte:
                                modelField.SetValue(entry, Byte.Parse(pieces[i], Invariant));
                                break;
                            case TypeCode.SByte:
                                modelField.SetValue(entry, SByte.Parse(pieces[i], Invariant));
                                break;
                            case TypeCode.Int16:
                                modelField.SetValue(entry, Int16.Parse(pieces[i], Invariant));
                                break;
                            case TypeCode.Int32:
                                modelField.SetValue(entry, Int32.Parse(pieces[i], Invariant));
                                break;
                            case TypeCode.Int64:
                                modelField.SetValue(entry, Int64.Parse(pieces[i], Invariant));
                                break;
                            case TypeCode.UInt16:
                                modelField.SetValue(entry, UInt16.Parse(pieces[i], Invariant));
                                break;
                            case TypeCode.UInt32:
                                modelField.SetValue(entry, UInt32.Parse(pieces[i], Invariant));
                                break;
                            case TypeCode.UInt64:
                                modelField.SetValue(entry, UInt64.Parse(pieces[i], Invariant));
                                break;
                            case TypeCode.Single:
                                modelField.SetValue(entry, Single.Parse(pieces[i], System.Globalization.NumberStyles.Float, Invariant));
                                break;
                            case TypeCode.Double:
                                modelField.SetValue(entry, Double.Parse(pieces[i], System.Globalization.NumberStyles.Float, Invariant));
                                break;
                            case TypeCode.String:
                            default:
                                modelField.SetValue(entry, pieces[i]);
                                break;
                        }
                    }
                    catch (Exception ex) when (ex is FormatException or OverflowException)
                    {
                        Count(bad, modelField.Name);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"{modelField.Name} errored with value {pieces[i]}: {ex.Message}");
                        throw new ArgumentException($"{modelField.Name}: bad value '{pieces[i]}'.", ex);
                    }
                    
                }

                var id = (uint)(_hasId ? typeFields[0].GetValue(entry) : recordCount);
                entries.TryAdd(id, entry);

                recordCount++;
            }

            LoadWarning = Describe(tableName, typeFields, header, columns, missing, blank, bad);
            if (LoadWarning != null)
                log.Warn(LoadWarning);
        }

        private static void Count(Dictionary<string, int> counts, string field)
        {
            counts[field] = counts.GetValueOrDefault(field) + 1;
        }

        /// <summary>
        /// One line for the whole table, or null when it fit. Columns named differently from the model are only listed then:
        /// the names alone say nothing (the rows are read by position), but next to gaps they show where the two part ways.
        /// </summary>
        private static string Describe(string tableName, PropertyInfo[] typeFields, string[] header, int columns,
            Dictionary<string, int> missing, Dictionary<string, int> blank, Dictionary<string, int> bad)
        {
            if (missing.Count == 0 && blank.Count == 0 && bad.Count == 0)
                return null;

            static string Join(Dictionary<string, int> counts) => String.Join(", ", counts.Select(c => $"{c.Key} ({c.Value})"));
            static string Plain(string name) => new string(name.Where(Char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

            var parts = new List<string>();
            if (missing.Count > 0)
                parts.Add($"no column for: {Join(missing)}");
            if (blank.Count > 0)
                parts.Add($"blank numbers: {Join(blank)}");
            if (bad.Count > 0)
                parts.Add($"unreadable values: {Join(bad)}");

            string[] renamed = typeFields.Take(columns).Select((f, i) => (Field: f.Name, Column: header[i]))
                .Where(c => Plain(c.Field) != Plain(c.Column)).Select(c => $"{c.Field} <- {c.Column}").ToArray();
            if (renamed.Length > 0)
                parts.Add($"named differently: {String.Join(", ", renamed)}");

            return $"{tableName}: read with the model's defaults (rows affected in brackets); {String.Join("; ", parts)}.";
        }

        /// <summary>
        /// Returns a <see cref="{T}"/> with a given ID. Null if ID does not exist.
        /// </summary>
        public T GetEntry(ulong id)
        {
            return entries.TryGetValue(id, out T value) ? value : null;
        }
    }
}
