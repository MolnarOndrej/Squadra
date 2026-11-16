namespace Squadra.Missions
{
    public enum IssueCode
    {
        // The file is not a version 1 mission document at all.
        MalformedJson,
        DuplicateProperty,
        UnknownField,
        MissingField,
        WrongType,
        UnsupportedSchemaVersion,
        UnrepresentableNumber,

        // The document has the right shape and a wrong value.
        InvalidId,
        DuplicateId,
        InvalidName,
        InvalidDistanceScale,
        TooManyPlayers,
        UnsupportedColor,
        DuplicateColor,
        UnsupportedNodeType,
        InvalidSize,
        InvalidCoordinate,
        InvalidDarts,
        UnknownOwner,
        DanglingEdge,
        SelfConnection,
        DuplicateEdge,

        // Warnings.
        DisconnectedGraph,
        CoincidentNodes,
        ZeroLengthRoute,
    }
}
