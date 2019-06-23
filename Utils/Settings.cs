using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Micro.Utils {
    public abstract class Settings {
        public static readonly string StartupPath = Path.GetFullPath(
                System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "").Replace('/', '\\') + @"\..");
        public Dictionary<string, string> Data { get; protected set; } = new Dictionary<string, string>();
        public Dictionary<int, string> Other { get; protected set; } = new Dictionary<int, string>();
        public string FilePath { get; }
        public readonly Encoding encoding;
        public bool Existent
            => File.Exists(FilePath);

        public string this[string k] {
            get => Data.ContainsKey(k) ? Data[k] : null;
            set => Data[k] = value;
        }
        public Settings(string fileName, Encoding enc = null) {
            FilePath = Path.Combine(StartupPath, fileName);
            encoding = enc ?? Encoding.Unicode;
            if (Existent)
                Load();
            LoadDefaults(false);
            Save();
        }

        public void Clear() {
            Data.Clear();
            Other.Clear();
        }
        public abstract void LoadDefaults(bool overwrite);
        public void Load() {
            Clear();
            if (!File.Exists(FilePath))
                throw new FileNotFoundException($"Unable to find \"{FilePath}\".");
            var lines = File.ReadAllLines(FilePath, encoding);
            for (int i = 0; i < lines.Length; i++) {
                var line = lines[i];
                Match match;
                if (string.IsNullOrWhiteSpace(line) || !(match = Regex.Match(line, @"^(?![^\r\n=]*#)([^\r\n]+?)=([^\r\n]*)$")).Success) {
                    if (i < lines.Length - 1)
                        Other[i] = line;
                    continue;
                }
                var key = match.Groups[1].Value;
                var value = match.Groups[2].Value;
                Data[key] = value;
            }
        }
        public void Save() {
            string txt = "";
            int length = Data.Count + Other.Count;
            var data = IterateDictionary(Data).ToArray();
            for (int i = 0, d = 0; i < length; i++)
                txt += (Other.ContainsKey(i) ? Other[i] : (data[d].Key + "=" + data[d++].Value)) + "\r\n";
            File.WriteAllText(FilePath, txt, encoding);
        }

        public static IEnumerable<KeyValuePair<K, V>> IterateDictionary<K, V>(Dictionary<K, V> d) {
            foreach (var kv in d)
                yield return kv;
        }
    }
}