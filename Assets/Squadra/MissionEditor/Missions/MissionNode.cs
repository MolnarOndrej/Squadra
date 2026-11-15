namespace Squadra.Missions
{
    /// <summary>
    /// One world of a mission. Its position is in map coordinates, X to the right and Y up, and has
    /// nothing to do with pixels. <see cref="Owner"/> is a player's ID, or null for a neutral world.
    /// </summary>
    public sealed class MissionNode
    {
        public string Id { get; set; }
        public MissionNodeType Type { get; set; }
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
