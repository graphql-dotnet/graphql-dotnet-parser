namespace GraphQLParser.AST;

/// <summary>
/// AST node for <see cref="ASTNodeKind.ObjectField"/>.
/// </summary>
public class GraphQLObjectField : ASTNode, INamedNode
{
    /// <summary>
    /// Creates a new instance of <see cref="GraphQLObjectField"/>.
    /// </summary>
    [Obsolete("This constructor will be removed in v9.")]
    public GraphQLObjectField()
    {
        Name = null!;
        Value = null!;
    }

    /// <summary>
    /// Creates a new instance of <see cref="GraphQLObjectField"/>.
    /// </summary>
    public GraphQLObjectField(GraphQLName name, GraphQLValue value)
    {
        Name = name;
        Value = value;
    }

    /// <inheritdoc/>
    public override ASTNodeKind Kind => ASTNodeKind.ObjectField;

    /// <inheritdoc/>
    public GraphQLName Name { get; set; } = null!;

    /// <summary>
    /// Value of the field represented as a nested AST node.
    /// </summary>
    public GraphQLValue Value { get; set; } = null!;
}

internal sealed class GraphQLObjectFieldWithLocation : GraphQLObjectField
{
    private GraphQLLocation _location;

    public override GraphQLLocation Location
    {
        get => _location;
        set => _location = value;
    }
}

internal sealed class GraphQLObjectFieldWithComment : GraphQLObjectField
{
    private List<GraphQLComment>? _comments;

    public override List<GraphQLComment>? Comments
    {
        get => _comments;
        set => _comments = value;
    }
}

internal sealed class GraphQLObjectFieldFull : GraphQLObjectField
{
    private GraphQLLocation _location;
    private List<GraphQLComment>? _comments;

    public override GraphQLLocation Location
    {
        get => _location;
        set => _location = value;
    }

    public override List<GraphQLComment>? Comments
    {
        get => _comments;
        set => _comments = value;
    }
}
