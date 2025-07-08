using System;
using System.Collections.Generic;
using System.Linq;

namespace Novelist.DraftBuilder
{
    internal static class DuplicateDetector
    {
        /// <summary>Returns true if two paragraphs are ≥ 0.85 similar.</summary>
        public static bool AreSimilar(string a, string b)
        {
            a = Normalize(a);
            b = Normalize(b);
            if (a.Length == 0 || b.Length == 0) return false;

            var setA = new HashSet<string>(a.Split(' '));
            var setB = new HashSet<string>(b.Split(' '));

            int intersect = setA.Intersect(setB).Count();
            int union     = setA.Union(setB).Count();

            double jaccard = union == 0 ? 0 : (double)intersect / union;
            return jaccard >= 0.85;
        }

        private static string Normalize(string s)
            => string.Join(' ',
                   s.ToLowerInvariant()
                    .Replace("\n", " ")
                    .Replace("\r", "")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
