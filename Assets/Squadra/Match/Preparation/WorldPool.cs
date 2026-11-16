using System;
using System.Collections.Generic;

namespace Squadra.Preparation
{
    /// <summary>
    /// What one type of world can look like: the stable IDs of its surface material variants and the names
    /// in its dictionary. The two are independent; any name can go with any surface. Both lists are kept
    /// sorted and without repeats, so the order the content was found in never changes a deal.
    /// </summary>
    public sealed class WorldPool
    {
        /// <summary>A pool with nothing in it, for a type no world uses.</summary>
        public static WorldPool Empty => new WorldPool(Array.Empty<string>(), Array.Empty<string>());

        public WorldPool(IEnumerable<string> variantIds, IEnumerable<string> names)
        {
            VariantIds = Clean(variantIds);
            Names = Clean(names);
        }

        /// <summary>Surface variant IDs, sorted ordinally.</summary>
        public IReadOnlyList<string> VariantIds { get; }

        /// <summary>World names, sorted ordinally.</summary>
        public IReadOnlyList<string> Names { get; }

        public bool HasVariant(string variantId)
        {
            foreach (string id in VariantIds)
                if (string.Equals(id, variantId, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// The names of a name dictionary file: one per line, with surrounding spaces and blank lines
        /// left out.
        /// </summary>
        public static IEnumerable<string> ReadNames(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;

            foreach (string line in text.Split('\n'))
            {
                string name = line.Trim();
                if (name.Length > 0) yield return name;
            }
        }

        static IReadOnlyList<string> Clean(IEnumerable<string> values)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            if (values != null)
            {
                foreach (string value in values)
                {
                    string trimmed = value?.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) set.Add(trimmed);
                }
            }

            return new List<string>(set);
        }
    }
}
