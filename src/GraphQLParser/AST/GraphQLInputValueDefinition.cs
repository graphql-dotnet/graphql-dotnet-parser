﻿using System.Collections.Generic;

namespace GraphQLParser.AST
{
    public class GraphQLInputValueDefinition : GraphQLTypeDefinition
    {
        public GraphQLValue DefaultValue { get; set; }

        public IEnumerable<GraphQLDirective> Directives { get; set; }

        public override ASTNodeKind Kind => ASTNodeKind.InputValueDefinition;

        public GraphQLName Name { get; set; }
        public GraphQLType Type { get; set; }
    }
}