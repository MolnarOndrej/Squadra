namespace Squadra.Missions
{
    /// <summary>
    /// One world of a mission. Its position is in map coordinates, X to the right and Y up, and has
    /// nothing to do with pixels. <see cref="Owner"/> is a player's ID, or null for a neutral world.
    /// A node made without a type or size is a planet of a planet's default size.
    /// </summary>
    public sealed class MissionNode
    {
        public string Id { get; set; }
        public MissionNodeType Type { get; set; }

        /// <summary>
        /// The body's radius in game world units: a radius, not a diameter or a multiplier. The
        /// distance scale does not change it.
        /// </summary>
        public double Size { get; set; } = MissionRules.DefaultSize(MissionNodeType.Planet);

        public double X { get; set; }
        public double Y { get; set; }
        public string Owner { get; set; }
        public int Darts { get; set; }

        public MissionNode Clone()
        {
            return (MissionNode)MemberwiseClone();
        }
    }
}
