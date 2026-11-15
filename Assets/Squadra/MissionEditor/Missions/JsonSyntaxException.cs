using System;

namespace Squadra.Missions
{
    sealed class JsonSyntaxException : Exception
    {
        public JsonSyntaxException(IssueCode code, string message, int line, int column) : base(message)
        {
            Code = code;
            Line = line;
            Column = column;
        }

        public IssueCode Code { get; }
        public int Line { get; }
        public int Column { get; }
    }
}
