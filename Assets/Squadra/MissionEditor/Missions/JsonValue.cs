using System.Collections.Generic;

namespace Squadra.Missions
{
    /// <summary>One value of a parsed JSON document. A number keeps the text it was written with.</summary>
    sealed class JsonValue
    {
        public JsonKind Kind;
        public string Text;
        public List<KeyValuePair<string, JsonValue>> Members;
        public List<JsonValue> Items;
    }
}
