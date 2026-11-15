namespace Squadra.MissionEditing
{
    public enum EditorTool
    {
        /// <summary>Clicking selects, dragging a node moves it.</summary>
        Select,

        /// <summary>The next click on the canvas places a node.</summary>
        AddNode,

        /// <summary>The next two nodes clicked are connected.</summary>
        AddConnection,
    }
}
