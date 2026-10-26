using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GungeonEID
{
    /// <summary>
    /// Minimal JSON reader: objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt; and
    /// numbers double. Used instead of Unity's JsonUtility, which leaves nested arrays null for classes
    /// defined in plugin assemblies.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            int i = 0;
            var value = ReadValue(text, ref i);
            SkipWhitespace(text, ref i);
            if (i != text.Length) throw Error(text, i, "trailing characters");
            return value;
        }

        private static object ReadValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw Error(s, i, "unexpected end");
            switch (s[i])
            {
                case '{': return ReadObject(s, ref i);
                case '[': return ReadArray(s, ref i);
                case '"': return ReadString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ReadNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ReadObject(string s, ref int i)
        {
            var obj = new Dictionary<string, object>();
            i++; // {
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return obj; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw Error(s, i, "expected property name");
                string key = ReadString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw Error(s, i, "expected ':'");
                i++;
                obj[key] = ReadValue(s, ref i);
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return obj; }
                throw Error(s, i, "expected ',' or '}'");
            }
        }

        private static List<object> ReadArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // [
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ReadValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return list; }
                throw Error(s, i, "expected ',' or ']'");
            }
        }

        private static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw Error(s, i, "bad \\u escape");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                        i += 4;
                        break;
                    default: sb.Append(e); break; // \" \\ \/
                }
            }
            throw Error(s, i, "unterminated string");
        }

        private static double ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw Error(s, i, "unexpected character");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Error(s, i, "expected " + word);
            i += word.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static Exception Error(string s, int i, string what) => new FormatException($"JSON: {what} at offset {i}");

        // ---- helpers for reading parsed values

        public static string Str(Dictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out var v) ? v as string : null;

        public static int Int(Dictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out var v) && v is double d ? (int)d : 0;

        public static List<object> Arr(Dictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out var v) && v is List<object> l ? l : new List<object>();

        public static int[] Ints(Dictionary<string, object> o, string key)
        {
            var list = Arr(o, key);
            var result = new int[list.Count];
            for (int i = 0; i < list.Count; i++) result[i] = list[i] is double d ? (int)d : 0;
            return result;
        }
    }
}
