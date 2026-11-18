using System;
using System.Collections.Generic;
using Squadra.Preparation;
using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// What a mission can look like: per world type, the selectable surface materials, each under a stable
    /// ID, and the name dictionary, one name per line; and the space backdrops a mission is shown against.
    /// Only these materials are ever chosen: rings, glows, fleets and lines are neither surfaces nor
    /// backdrops. <c>Squadra &gt; Rebuild Mission Scene</c> fills the material lists from the Planets, Moons,
    /// Asteroids and Backdrops folders of <c>Visuals/Materials</c>, keeping the IDs it gave before; the name
    /// dictionaries are text files edited by hand. A mission's looks are chosen when it is prepared and
    /// found here again by ID, never by asset name or order.
    /// </summary>
    [CreateAssetMenu(menuName = "Squadra/World Looks", fileName = "WorldLooks")]
    public sealed class WorldLooks : ScriptableObject
    {
        [Serializable]
        sealed class Variant
        {
            [Tooltip("Stable ID that resolved assignments refer to. Never reuse or change it once shipped.")]
            public string id;

            public Material material;
        }

        [Header("Surfaces")]
        [SerializeField] Variant[] planets = Array.Empty<Variant>();
        [SerializeField] Variant[] moons = Array.Empty<Variant>();
        [SerializeField] Variant[] asteroids = Array.Empty<Variant>();

        [Header("Names")]
        [Tooltip("One planet name per line.")]
        [SerializeField] TextAsset planetNames;
        [Tooltip("One moon name per line.")]
        [SerializeField] TextAsset moonNames;
        [Tooltip("One asteroid name per line.")]
        [SerializeField] TextAsset asteroidNames;

        [Header("Space")]
        [Tooltip("Space backdrop materials, using the Squadra/Space shader. One is chosen per mission.")]
        [SerializeField] Variant[] backdrops = Array.Empty<Variant>();

        /// <summary>The pools mission preparation deals from.</summary>
        public WorldPools ToPools()
        {
            return new WorldPools(Pool(planets, planetNames), Pool(moons, moonNames), Pool(asteroids, asteroidNames),
                Usable(backdrops));
        }

        /// <summary>
        /// The surface material of one variant of a world type. False when this game has no such variant,
        /// which a prepared mission never asks for.
        /// </summary>
        public bool TryGetSurface(NodeKind kind, string variantId, out Material material)
        {
            return TryFind(VariantsOf(kind), variantId, out material);
        }

        /// <summary>
        /// The surface of one variant of a world type or, when the type has no such variant, its first
        /// surface: for a sandbox sample whose type was just changed. False when the type has no surface.
        /// </summary>
        public bool TryGetSurfaceOrFirst(NodeKind kind, string variantId, out Material material)
        {
            if (TryGetSurface(kind, variantId, out material)) return true;

            foreach (Variant variant in VariantsOf(kind))
            {
                if (variant == null || variant.material == null) continue;
                material = variant.material;
                return true;
            }

            return false;
        }

        /// <summary>The material of one space backdrop. False when this game has no such backdrop.</summary>
        public bool TryGetBackdrop(string backdropId, out Material material)
        {
            return TryFind(backdrops, backdropId, out material);
        }

        Variant[] VariantsOf(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Planet: return planets;
                case NodeKind.Moon: return moons;
                default: return asteroids;
            }
        }

        static bool TryFind(Variant[] variants, string id, out Material material)
        {
            foreach (Variant variant in variants)
            {
                if (variant != null && variant.material != null && string.Equals(variant.id, id, StringComparison.Ordinal))
                {
                    material = variant.material;
                    return true;
                }
            }

            material = null;
            return false;
        }

        static WorldPool Pool(Variant[] variants, TextAsset names)
        {
            return new WorldPool(Usable(variants), WorldPool.ReadNames(names != null ? names.text : null));
        }

        // A variant without a material is not offered: it could not be shown.
        static List<string> Usable(Variant[] variants)
        {
            var ids = new List<string>();
            foreach (Variant variant in variants)
            {
                if (variant != null && variant.material != null) ids.Add(variant.id);
            }

            return ids;
        }
    }
}
