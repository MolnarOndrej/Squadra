namespace Squadra.Missions
{
    /// <summary>One player of a mission: a stable identity and the color it is drawn in.</summary>
    public sealed class MissionPlayer
    {
        public MissionPlayer()
        {
        }

        public MissionPlayer(string id, PlayerColor color)
        {
            Id = id;
            Color = color;
        }

        public string Id { get; set; }
        public PlayerColor Color { get; set; }
    }
}
