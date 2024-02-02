using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace SimpleResourceReplacer
{
    public static class ExtentionMethods
    {
        public static Y GetOrCreate<X,Y>(this IDictionary<X,Y> d, X key) where Y : new()
        {
            if (d.TryGetValue(key, out var v) && v != null)
                return v;
            return d[key] = new Y();
        }
        public static string Capitalize(this string s) => s.Remove(1).ToUpper() + s.Remove(0, 1).ToLower();
        public static string CapitalizeInvariant(this string s) => s.Remove(1).ToUpperInvariant() + s.Remove(0, 1).ToLowerInvariant();

        public static string GetTupleString(this object obj, FieldInfo field)
        {
            var n = field.GetCustomAttribute<TupleElementNamesAttribute>()?.TransformNames;
            var t = obj.GetType();
            var i = 1;
            var s = new StringBuilder();
            s.Append('[');
            while (true)
            {
                var f = t.GetField("Item" + i);
                if (f == null)
                    break;
                if (i != 1)
                    s.Append(",");
                if (n != null && i <= n.Count && !string.IsNullOrEmpty(n[i - 1]))
                    s.Append(n[i - 1]);
                else
                    s.Append(f.Name);
                s.Append('=');
                s.Append(f.GetValue(obj));
                i++;
            }
            s.Append(']');
            return s.ToString();
        }

        public static List<string[]> SplitMetas(this string[] lines)
        {
            var l = new List<string[]>();
            var curr = new List<string>();
            for (int i = 0; i < lines.Length; i++)
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    if (curr.Count > 0)
                        l.Add(curr.ToArray());
                    curr.Clear();
                }
                else
                    curr.Add(lines[i]);
            if (curr.Count > 0)
                l.Add(curr.ToArray());
            return l;
        }

        public static string ToPlaceString(this int num)
        {
            if (num % 10 == 1)
                return num + "st";
            if (num % 10 == 2)
                return num + "nd";
            if (num % 10 == 3)
                return num + "rd";
            return num + "th";
        }
    }
}