﻿namespace GraphQLParser.AST
{
    public class GraphQLObjectField : ASTNode
    {
        public override ASTNodeKind Kind => ASTNodeKind.ObjectField;

        public GraphQLName Name { get; set; }
        public GraphQLValue Value { get; set; }
    }
}