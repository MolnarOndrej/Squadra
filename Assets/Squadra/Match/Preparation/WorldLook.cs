namespace Squadra.Preparation
{
    /// <summary>
    /// How one world is shown: the stable ID of its surface material variant and the name it goes by.
    /// Both are display data only; no rule ever reads them, and node IDs stay the world's identity.
    /// </summary>
    public sealed class WorldLook
    {
        public WorldLook(string variantId, string name)
        {
            VariantId = variantId ?? "";
            Name = name ?? "";
        }

        /// <summary>The surface material variant, by the stable ID its world pool gives it.</summary>
        public string VariantId { get; }

        /// <summary>The world's name, from its type's name dictionary.</summary>
        public string Name { get; }

        public override string ToString() => Name + " (" + VariantId + ")";
    }
}
