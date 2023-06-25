using System.Diagnostics;

namespace GraphQLParser.AST;

/// <summary>
/// AST node for <see cref="ASTNodeKind.EnumTypeDefinition"/>.
/// </summary>
[DebuggerDisplay("GraphQLEnumTypeDefinition: {Name}")]
public class GraphQLEnumTypeDefinition : GraphQLTypeDefinition, IHasDirectivesNode
{
    /// <summary>Initializes a new instance.</summary>
    [Obsolete("This constructor will be removed in v9.")]
    public GraphQLEnumTypeDefinition()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="GraphQLEnumTypeDefinition"/>.
    /// </summary>
    public GraphQLEnumTypeDefinition(GraphQLName name)
        : base(name)
    {
    }

    /// <inheritdoc/>
    public override ASTNodeKind Kind => ASTNodeKind.EnumTypeDefinition;

    /// <inheritdoc/>
    public GraphQLDirectives? Directives { get; set; }

    /// <summary>
    /// Nested <see cref="GraphQLEnumValuesDefinition"/> AST node with enum values.
    /// </summary>
    public GraphQLEnumValuesDefinition? Values { get; set; }
}

internal sealed class GraphQLEnumTypeDefinitionWithLocation : GraphQLEnumTypeDefinition
{
    private GraphQLLocation _location;

    public override GraphQLLocation Location
    {
        get => _location;
        set => _location = value;
    }
}

internal sealed class GraphQLEnumTypeDefinitionWithComment : GraphQLEnumTypeDefinition
{
    private List<GraphQLComment>? _comments;

    public override List<GraphQLComment>? Comments
    {
        get => _comments;
        set => _comments = value;
    }
}

internal sealed class GraphQLEnumTypeDefinitionFull : GraphQLEnumTypeDefinition
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
