namespace GraphQLParser.AST
{
    /// <summary>
    /// AST node for <see cref="ASTNodeKind.EnumValuesDefinition"/>.
    /// </summary>
    public class GraphQLEnumValuesDefinition : ASTListNode<GraphQLEnumValueDefinition>
    {
        /// <inheritdoc/>
        public override ASTNodeKind Kind => ASTNodeKind.EnumValuesDefinition;
    }

    internal sealed class GraphQLEnumValuesDefinitionWithLocation : GraphQLEnumValuesDefinition
    {
        private GraphQLLocation _location;

        public override GraphQLLocation Location
        {
            get => _location;
            set => _location = value;
        }
    }

    internal sealed class GraphQLEnumValuesDefinitionWithComment : GraphQLEnumValuesDefinition
    {
        private GraphQLComment? _comment;

        public override GraphQLComment? Comment
        {
            get => _comment;
            set => _comment = value;
        }
    }

    internal sealed class GraphQLEnumValuesDefinitionFull : GraphQLEnumValuesDefinition
    {
        private GraphQLLocation _location;
        private GraphQLComment? _comment;

        public override GraphQLLocation Location
        {
            get => _location;
            set => _location = value;
        }

        public override GraphQLComment? Comment
        {
            get => _comment;
            set => _comment = value;
        }
    }
}
