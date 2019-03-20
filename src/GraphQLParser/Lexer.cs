﻿namespace GraphQLParser
{
    public class Lexer : ILexer
    {
        public Token Lex(ISource source) => Lex(source, 0);

        public Token Lex(ISource source, int start)
        {
            using (var context = new LexerContext(source, start))
            {
                return context.GetToken();
            }
        }
    }
}