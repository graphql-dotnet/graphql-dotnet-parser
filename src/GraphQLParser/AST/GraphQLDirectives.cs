namespace GraphQLParser.AST;

/// <summary>
/// AST node for <see cref="ASTNodeKind.Directives"/>.
/// </summary>
public class GraphQLDirectives : ASTListNode<GraphQLDirective>
{
    /// <summary>Initializes a new instance.</summary>
    [Obsolete("This constructor will be removed in v9.")]
    public GraphQLDirectives()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="GraphQLDirectives"/>.
    /// </summary>
    public GraphQLDirectives(List<GraphQLDirective> items)
        : base(items)
    {
    }

    /// <inheritdoc/>
    public override ASTNodeKind Kind => ASTNodeKind.Directives;
}

internal sealed class GraphQLDirectivesWithLocation : GraphQLDirectives
{
    private GraphQLLocation _location;

    public override GraphQLLocation Location
    {
        get => _location;
        set => _location = value;
    }
}
