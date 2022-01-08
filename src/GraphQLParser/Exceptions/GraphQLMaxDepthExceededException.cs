namespace GraphQLParser.Exceptions;

/// <summary>
/// An exception representing a 'maximum depth exceeded' parser error.
/// </summary>
public class GraphQLMaxDepthExceededException : GraphQLParserException
{
    /// <summary>
    /// Initializes a new instance with the specified parameters.
    /// </summary>
    public GraphQLMaxDepthExceededException(ROM source, int location)
        : base("Maximum depth exceeded.", source, location)
    {
    }
}
