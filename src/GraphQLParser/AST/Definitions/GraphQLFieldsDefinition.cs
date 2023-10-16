namespace GraphQLParser.AST;

/// <summary>
/// AST node for <see cref="ASTNodeKind.FieldsDefinition"/>.
/// </summary>
public class GraphQLFieldsDefinition : ASTListNode<GraphQLFieldDefinition>
{
    /// <inheritdoc/>
    public override ASTNodeKind Kind => ASTNodeKind.FieldsDefinition;
}

internal sealed class GraphQLFieldsDefinitionWithLocation : GraphQLFieldsDefinition
{
    private GraphQLLocation _location;

    public override GraphQLLocation Location
    {
        get => _location;
        set => _location = value;
    }
}

internal sealed class GraphQLFieldsDefinitionWithComment : GraphQLFieldsDefinition
{
    private List<GraphQLComment>? _comments;

    public override List<GraphQLComment>? Comments
    {
        get => _comments;
        set => _comments = value;
    }
}

internal sealed class GraphQLFieldsDefinitionFull : GraphQLFieldsDefinition
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
