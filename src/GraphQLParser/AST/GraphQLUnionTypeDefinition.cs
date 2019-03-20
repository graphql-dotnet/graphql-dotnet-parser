﻿using System.Collections.Generic;

namespace GraphQLParser.AST
{
    public class GraphQLUnionTypeDefinition : GraphQLTypeDefinition
    {
        public IEnumerable<GraphQLDirective> Directives { get; set; }

        public override ASTNodeKind Kind => ASTNodeKind.UnionTypeDefinition;

        public GraphQLName Name { get; set; }
        public IEnumerable<GraphQLNamedType> Types { get; set; }
    }
}