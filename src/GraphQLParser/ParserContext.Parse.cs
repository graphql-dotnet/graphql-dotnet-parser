using System;
using System.Collections.Generic;
using System.Diagnostics;
using GraphQLParser.AST;
using GraphQLParser.Exceptions;

namespace GraphQLParser;

// WARNING: mutable struct, pass it by reference to those methods that will change it
internal partial struct ParserContext
{
    // http://spec.graphql.org/October2021/#Document
    public GraphQLDocument ParseDocument()
    {
        int start = _currentToken.Start;
        var definitions = ParseDefinitionsIfNotEOF();

        SetCurrentComment(null); // push current (last) comment into _unattachedComments

        _document.Location = new GraphQLLocation
        (
            start,
            // Formally, to denote the end of the document, it is better to use _prevToken.End,
            // since _prevToken represents some real meaningful token; _currentToken here is always EOF.
            // EOF is a technical token with length = 0, _prevToken.End and _currentToken.End have the same value here.
            _prevToken.End // equals to _currentToken.End (EOF)
        );
        _document.Definitions = definitions;
        _document.UnattachedComments = _unattachedComments;

        Debug.Assert(_currentDepth == 1, "Depth has not returned to 1 after parsing document");

        return _document;
    }

    // http://spec.graphql.org/October2021/#TypeCondition
    private GraphQLTypeCondition? ParseTypeCondition(bool optional)
    {
        if (optional && _currentToken.Value != "on")
            return null;

        IncreaseDepth();

        int start = _currentToken.Start;

        var condition = NodeHelper.CreateGraphQLTypeCondition(_ignoreOptions);

        condition.Comment = GetComment();
        ExpectKeyword("on");
        condition.Type = ParseNamedType();
        condition.Location = GetLocation(start);

        DecreaseDepth();
        return condition;
    }

    // http://spec.graphql.org/October2021/#Argument
    private GraphQLArgument ParseArgument()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var arg = NodeHelper.CreateGraphQLArgument(_ignoreOptions);

        arg.Comment = GetComment();
        arg.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#Argument");
        Expect(TokenKind.COLON);
        arg.Value = ParseValueLiteral(false);
        arg.Location = GetLocation(start);

        DecreaseDepth();
        return arg;
    }

    // http://spec.graphql.org/October2021/#Arguments
    private GraphQLArguments ParseArguments()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var args = NodeHelper.CreateGraphQLArguments(_ignoreOptions);

        args.Comment = GetComment();
        args.Items = OneOrMore(TokenKind.PAREN_L, (ref ParserContext context) => context.ParseArgument(), TokenKind.PAREN_R);
        args.Location = GetLocation(start);

        DecreaseDepth();
        return args;
    }

    // http://spec.graphql.org/October2021/#ArgumentsDefinition
    private GraphQLArgumentsDefinition ParseArgumentsDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var argsDef = NodeHelper.CreateGraphQLArgumentsDefinition(_ignoreOptions);

        argsDef.Comment = GetComment();
        argsDef.Items = OneOrMore(TokenKind.PAREN_L, (ref ParserContext context) => context.ParseInputValueDef(), TokenKind.PAREN_R);
        argsDef.Location = GetLocation(start);

        DecreaseDepth();
        return argsDef;
    }

    // http://spec.graphql.org/October2021/#InputFieldsDefinition
    private GraphQLInputFieldsDefinition ParseInputFieldsDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var inputFieldsDef = NodeHelper.CreateGraphQLInputFieldsDefinition(_ignoreOptions);

        inputFieldsDef.Comment = GetComment();
        inputFieldsDef.Items = OneOrMore(TokenKind.BRACE_L, (ref ParserContext context) => context.ParseInputValueDef(), TokenKind.BRACE_R);
        inputFieldsDef.Location = GetLocation(start);

        DecreaseDepth();
        return inputFieldsDef;
    }

    // http://spec.graphql.org/October2021/#FieldsDefinition
    private GraphQLFieldsDefinition ParseFieldsDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var fieldsDef = NodeHelper.CreateGraphQLFieldsDefinition(_ignoreOptions);

        fieldsDef.Comment = GetComment();
        fieldsDef.Items = OneOrMore(TokenKind.BRACE_L, (ref ParserContext context) => context.ParseFieldDefinition(), TokenKind.BRACE_R);
        fieldsDef.Location = GetLocation(start);

        DecreaseDepth();
        return fieldsDef;
    }

    // http://spec.graphql.org/October2021/#EnumValuesDefinition
    private GraphQLEnumValuesDefinition ParseEnumValuesDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var enumValuesDef = NodeHelper.CreateGraphQLEnumValuesDefinition(_ignoreOptions);

        enumValuesDef.Comment = GetComment();
        enumValuesDef.Items = OneOrMore(TokenKind.BRACE_L, (ref ParserContext context) => context.ParseEnumValueDefinition(), TokenKind.BRACE_R);
        enumValuesDef.Location = GetLocation(start);

        DecreaseDepth();
        return enumValuesDef;
    }

    // http://spec.graphql.org/October2021/#VariableDefinitions
    private GraphQLVariablesDefinition ParseVariablesDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var variablesDef = NodeHelper.CreateGraphQLVariablesDefinition(_ignoreOptions);

        variablesDef.Comment = GetComment();
        variablesDef.Items = OneOrMore(TokenKind.PAREN_L, (ref ParserContext context) => context.ParseVariableDefinition(), TokenKind.PAREN_R);
        variablesDef.Location = GetLocation(start);

        DecreaseDepth();
        return variablesDef;
    }

    // http://spec.graphql.org/October2021/#BooleanValue
    // There is no true/false value check, see calling method.
    private GraphQLBooleanValue ParseBooleanValue()
    {
        IncreaseDepth();

        var token = _currentToken;

        var val = NodeHelper.CreateGraphQLBooleanValue(_ignoreOptions);

        val.Comment = GetComment();
        Advance();
        val.Value = token.Value;
        val.Location = GetLocation(token.Start);

        DecreaseDepth();
        return val;
    }

    private ASTNode ParseDefinition()
    {
        if (Peek(TokenKind.BRACE_L))
            return ParseOperationDefinition();

        if (Peek(TokenKind.NAME))
            return ParseNamedDefinition();

        if (Peek(TokenKind.STRING))
            return ParseNamedDefinitionWithDescription();

        return Throw_Unexpected_Token();
    }

    private ASTNode Throw_Unexpected_Token(string? description = null)
    {
        throw new GraphQLSyntaxErrorException($"Unexpected {_currentToken}{description}", _source, _currentToken.Start);
    }

    private List<ASTNode> ParseDefinitionsIfNotEOF()
    {
        var result = new List<ASTNode>();

        if (_currentToken.Kind != TokenKind.EOF)
        {
            do
            {
                result.Add(ParseDefinition());
            }
            while (!Skip(TokenKind.EOF));
        }

        return result;
    }

    // http://spec.graphql.org/October2021/#Comment
    private void ParseComment()
    {
        // skip comments
        if (_ignoreOptions.HasFlag(IgnoreOptions.Comments))
        {
            while (Peek(TokenKind.COMMENT))
            {
                Advance(fromParseComment: true);
            }
            return;
        }

        if (!Peek(TokenKind.COMMENT))
        {
            return;
        }

        IncreaseDepth();

        var text = new List<ROM>();
        int start = _currentToken.Start;
        int end;

        do
        {
            text.Add(_currentToken.Value);
            end = _currentToken.End;
            Advance(fromParseComment: true);
        }
        while (_currentToken.Kind == TokenKind.COMMENT);

        var comment = NodeHelper.CreateGraphQLComment(_ignoreOptions);

        comment.Location = new GraphQLLocation(start, end);

        if (text.Count == 1)
        {
            comment.Text = text[0];
        }
        else if (text.Count > 1)
        {
            var (owner, result) = text.Concat();
            comment.Text = result;
            (_document.RentedMemoryTracker ??= new List<(System.Buffers.IMemoryOwner<char>, ASTNode)>()).Add((owner, comment));
        }

        SetCurrentComment(comment);
        DecreaseDepth();
    }

    private void SetCurrentComment(GraphQLComment? comment)
    {
        if (_currentComment != null)
            (_unattachedComments ??= new List<GraphQLComment>()).Add(_currentComment);

        _currentComment = comment;
    }

    private GraphQLComment? GetComment()
    {
        var ret = _currentComment;
        _currentComment = null;
        return ret;
    }

    // http://spec.graphql.org/October2021/#Directive
    private GraphQLDirective ParseDirective()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var dir = NodeHelper.CreateGraphQLDirective(_ignoreOptions);

        dir.Comment = GetComment();
        Expect(TokenKind.AT);
        dir.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#Directive");
        dir.Arguments = Peek(TokenKind.PAREN_L) ? ParseArguments() : null;
        dir.Location = GetLocation(start);

        DecreaseDepth();
        return dir;
    }

    // http://spec.graphql.org/October2021/#DirectiveDefinition
    private GraphQLDirectiveDefinition ParseDirectiveDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLDirectiveDefinition(_ignoreOptions);

        def.Description = Peek(TokenKind.STRING) ? ParseDescription() : null;
        def.Comment = GetComment();
        ExpectKeyword("directive");
        Expect(TokenKind.AT);
        def.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#DirectiveDefinition");
        def.Arguments = Peek(TokenKind.PAREN_L) ? ParseArgumentsDefinition() : null;
        def.Repeatable = Peek(TokenKind.NAME) && ParseRepeatable();
        ExpectKeyword("on");
        def.Locations = ParseDirectiveLocations();
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    private bool ParseRepeatable()
    {
        if (_currentToken.Value == "on")
            return false;

        if (_currentToken.Value == "repeatable")
        {
            Advance();
            return true;
        }

        Throw_Unexpected_Token("; did you miss 'repeatable'?");
        return false; // for compiler
    }

    private GraphQLDirectiveLocations ParseDirectiveLocations()
    {
        IncreaseDepth();
        var comment = GetComment();

        int start = _currentToken.Start;

        var directiveLocations = NodeHelper.CreateGraphQLDirectiveLocations(_ignoreOptions);

        var items = new List<DirectiveLocation>();

        // Directive locations may be defined with an optional leading | character
        // to aid formatting when representing a longer list of possible locations
        _ = Skip(TokenKind.PIPE);

        do
        {
            items.Add(ParseDirectiveLocation());
        }
        while (Skip(TokenKind.PIPE));

        directiveLocations.Items = items;
        directiveLocations.Comment = comment;
        directiveLocations.Location = GetLocation(start);

        DecreaseDepth();
        return directiveLocations;
    }

    // http://spec.graphql.org/October2021/#Directives
    private GraphQLDirectives ParseDirectives()
    {
        IncreaseDepth();
        // Directives go one after another without any "list prefix", so it is impossible
        // to distinguish the comment of the first directive from the comment to the entire
        // list of directives. Therefore, a comment for the directive itself is used.
        //var comment = GetComment();

        int start = _currentToken.Start;

        var directives = NodeHelper.CreateGraphQLDirectives(_ignoreOptions);

        // OneOrMore does not work here because there are no open and close tokens
        var items = new List<GraphQLDirective> { ParseDirective() };

        while (Peek(TokenKind.AT))
            items.Add(ParseDirective());

        directives.Items = items;
        //directives.Comment = comment;
        directives.Location = GetLocation(start);

        DecreaseDepth();
        return directives;
    }

    // http://spec.graphql.org/October2021/#EnumTypeDefinition
    private GraphQLEnumTypeDefinition ParseEnumTypeDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLEnumTypeDefinition(_ignoreOptions);

        def.Description = Peek(TokenKind.STRING) ? ParseDescription() : null;
        def.Comment = GetComment();
        ExpectKeyword("enum");
        def.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#EnumTypeDefinition");
        def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        def.Values = Peek(TokenKind.BRACE_L) ? ParseEnumValuesDefinition() : null;
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#EnumTypeExtension
    // Note that due to the spec type extensions have no descriptions.
    private GraphQLEnumTypeExtension ParseEnumTypeExtension(int start, GraphQLComment? comment)
    {
        IncreaseDepth();

        ExpectKeyword("enum");

        var extension = NodeHelper.CreateGraphQLEnumTypeExtension(_ignoreOptions);

        extension.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#EnumTypeExtension");
        extension.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        extension.Values = Peek(TokenKind.BRACE_L) ? ParseEnumValuesDefinition() : null;
        extension.Comment = comment;
        extension.Location = GetLocation(start);

        if (extension.Directives == null && extension.Values == null)
            return (GraphQLEnumTypeExtension)Throw_Unexpected_Token("; for more information see http://spec.graphql.org/October2021/#EnumTypeExtension");

        DecreaseDepth();
        return extension;
    }

    // http://spec.graphql.org/October2021/#EnumValue
    private GraphQLEnumValue ParseEnumValue()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var val = NodeHelper.CreateGraphQLEnumValue(_ignoreOptions);

        val.Comment = GetComment();
        val.Name = ParseEnumName();
        val.Location = GetLocation(start);

        DecreaseDepth();
        return val;
    }

    // Like ParseFragmentName but without special term in grammar.
    // TODO: https://github.com/graphql/graphql-spec/issues/919
    private GraphQLName ParseEnumName()
    {
        if (_currentToken.Value == "true" || _currentToken.Value == "false" || _currentToken.Value == "null")
        {
            Throw_Unexpected_Token("; enum values are represented as unquoted names but not 'true' or 'false' or 'null'.");
        }

        return ParseName("; for more information see http://spec.graphql.org/October2021/#EnumValue");
    }

    // http://spec.graphql.org/October2021/#EnumValueDefinition
    private GraphQLEnumValueDefinition ParseEnumValueDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLEnumValueDefinition(_ignoreOptions);

        def.Description = Peek(TokenKind.STRING) ? ParseDescription() : null;
        def.Comment = GetComment();
        def.EnumValue = ParseEnumValue();
        def.Name = def.EnumValue.Name; // ATTENTION: should set Name property (inherited from GraphQLTypeDefinition)
        def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#FieldDefinition
    private GraphQLFieldDefinition ParseFieldDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLFieldDefinition(_ignoreOptions);

        def.Description = Peek(TokenKind.STRING) ? ParseDescription() : null;
        def.Comment = GetComment();
        def.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#FieldDefinition");
        def.Arguments = Peek(TokenKind.PAREN_L) ? ParseArgumentsDefinition() : null;
        Expect(TokenKind.COLON);
        def.Type = ParseType();
        def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#Field
    // http://spec.graphql.org/October2021/#Alias
    private GraphQLField ParseField()
    {
        IncreaseDepth();

        // start of alias (if exists) equals start of field
        int start = _currentToken.Start;

        var nameOrAliasComment = GetComment();
        var nameOrAlias = ParseName("; for more information see http://spec.graphql.org/October2021/#Field");

        GraphQLName name;
        GraphQLName? alias;

        GraphQLComment? nameComment;
        GraphQLComment? aliasComment;

        GraphQLLocation aliasLocation = default;

        if (Skip(TokenKind.COLON)) // alias exists
        {
            aliasLocation = GetLocation(start);

            nameComment = GetComment();
            aliasComment = nameOrAliasComment;

            name = ParseName("; for more information see http://spec.graphql.org/October2021/#Field");
            alias = nameOrAlias;
        }
        else // no alias
        {
            aliasComment = null;
            nameComment = nameOrAliasComment;

            alias = null;
            name = nameOrAlias;
        }

        var field = NodeHelper.CreateGraphQLField(_ignoreOptions);

        if (alias != null)
        {
            var aliasNode = NodeHelper.CreateGraphQLAlias(_ignoreOptions);

            aliasNode.Comment = aliasComment;
            aliasNode.Name = alias;
            aliasNode.Location = aliasLocation;

            field.Alias = aliasNode;
        }
        field.Comment = nameComment;
        field.Name = name;
        field.Arguments = Peek(TokenKind.PAREN_L) ? ParseArguments() : null;
        field.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        field.SelectionSet = Peek(TokenKind.BRACE_L) ? ParseSelectionSet() : null;
        field.Location = GetLocation(start);

        DecreaseDepth();
        return field;
    }

    // http://spec.graphql.org/October2021/#FloatValue
    private GraphQLFloatValue ParseFloatValue(/*bool isConstant*/)
    {
        IncreaseDepth();

        var token = _currentToken;

        var val = NodeHelper.CreateGraphQLFloatValue(_ignoreOptions);

        val.Comment = GetComment();
        Advance();
        val.Value = token.Value;
        val.Location = GetLocation(token.Start);

        DecreaseDepth();
        return val;
    }

    private ASTNode ParseFragment()
    {
        int start = _currentToken.Start;
        var comment = GetComment();
        Expect(TokenKind.SPREAD);

        return Peek(TokenKind.NAME) && _currentToken.Value != "on"
            ? ParseFragmentSpread(start, comment)
            : ParseInlineFragment(start, comment);
    }

    // http://spec.graphql.org/October2021/#FragmentSpread
    private GraphQLFragmentSpread ParseFragmentSpread(int start, GraphQLComment? comment)
    {
        IncreaseDepth();

        var spread = NodeHelper.CreateGraphQLFragmentSpread(_ignoreOptions);

        spread.Name = ParseFragmentName();
        spread.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        spread.Comment = comment;
        spread.Location = GetLocation(start);

        DecreaseDepth();
        return spread;
    }

    // http://spec.graphql.org/October2021/#InlineFragment
    private GraphQLInlineFragment ParseInlineFragment(int start, GraphQLComment? comment)
    {
        IncreaseDepth();

        var frag = NodeHelper.CreateGraphQLInlineFragment(_ignoreOptions);

        frag.TypeCondition = ParseTypeCondition(optional: true);
        frag.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        frag.SelectionSet = ParseSelectionSet();
        frag.Comment = comment;
        frag.Location = GetLocation(start);

        DecreaseDepth();
        return frag;
    }

    // http://spec.graphql.org/October2021/#FragmentDefinition
    private GraphQLFragmentDefinition ParseFragmentDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLFragmentDefinition(_ignoreOptions);

        def.Comment = GetComment();
        ExpectKeyword("fragment");
        def.Name = ParseFragmentName();
        def.TypeCondition = ParseTypeCondition(optional: false)!; // never returns null
        def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        def.SelectionSet = ParseSelectionSet();
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#FragmentName
    private GraphQLName ParseFragmentName()
    {
        if (_currentToken.Value == "on")
        {
            Throw_Unexpected_Token("; fragment name can not be 'on'.");
        }

        return ParseName("; for more information see http://spec.graphql.org/October2021/#FragmentName");
    }

    // http://spec.graphql.org/October2021/#ImplementsInterfaces
    private GraphQLImplementsInterfaces ParseImplementsInterfaces()
    {
        IncreaseDepth();
        var comment = GetComment();

        int start = _currentToken.Start;

        ExpectKeyword("implements");

        var implementsInterfaces = NodeHelper.CreateGraphQLImplementsInterfaces(_ignoreOptions);

        List<GraphQLNamedType> types = new();

        // Objects that implement interfaces may be defined with an optional leading & character
        // to aid formatting when representing a longer list of implemented interfaces
        _ = Skip(TokenKind.AMPERSAND);

        do
        {
            types.Add(ParseNamedType());
        }
        while (Skip(TokenKind.AMPERSAND));

        implementsInterfaces.Items = types;
        implementsInterfaces.Comment = comment;
        implementsInterfaces.Location = GetLocation(start);

        DecreaseDepth();
        return implementsInterfaces;
    }

    // http://spec.graphql.org/October2021/#InputObjectTypeDefinition
    private GraphQLInputObjectTypeDefinition ParseInputObjectTypeDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLInputObjectTypeDefinition(_ignoreOptions);

        def.Description = Peek(TokenKind.STRING) ? ParseDescription() : null;
        def.Comment = GetComment();
        ExpectKeyword("input");
        def.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#InputObjectTypeDefinition");
        def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        def.Fields = Peek(TokenKind.BRACE_L) ? ParseInputFieldsDefinition() : null;
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#InputObjectTypeExtension
    // Note that due to the spec type extensions have no descriptions.
    private GraphQLInputObjectTypeExtension ParseInputObjectTypeExtension(int start, GraphQLComment? comment)
    {
        IncreaseDepth();

        var extension = NodeHelper.CreateGraphQLInputObjectTypeExtension(_ignoreOptions);

        extension.Comment = comment;
        ExpectKeyword("input");
        extension.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#InputObjectTypeExtension");
        extension.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        extension.Fields = Peek(TokenKind.BRACE_L) ? ParseInputFieldsDefinition() : null;
        extension.Location = GetLocation(start);

        if (extension.Directives == null && extension.Fields == null)
            return (GraphQLInputObjectTypeExtension)Throw_Unexpected_Token("; for more information see http://spec.graphql.org/October2021/#InputObjectTypeExtension");

        DecreaseDepth();
        return extension;
    }

    // http://spec.graphql.org/October2021/#InputValueDefinition
    private GraphQLInputValueDefinition ParseInputValueDef()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLInputValueDefinition(_ignoreOptions);

        def.Description = Peek(TokenKind.STRING) ? ParseDescription() : null;
        def.Comment = GetComment();
        def.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#InputValueDefinition");
        Expect(TokenKind.COLON);
        def.Type = ParseType();
        def.DefaultValue = Skip(TokenKind.EQUALS) ? ParseValueLiteral(true) : null;
        def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#IntValue
    private GraphQLIntValue ParseIntValue(/*bool isConstant*/)
    {
        IncreaseDepth();

        var token = _currentToken;

        var val = NodeHelper.CreateGraphQLIntValue(_ignoreOptions);

        val.Comment = GetComment();
        Advance();
        val.Value = token.Value;
        val.Location = GetLocation(token.Start);

        DecreaseDepth();
        return val;
    }

    // http://spec.graphql.org/October2021/#InterfaceTypeDefinition
    private GraphQLInterfaceTypeDefinition ParseInterfaceTypeDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLInterfaceTypeDefinition(_ignoreOptions);

        def.Description = Peek(TokenKind.STRING) ? ParseDescription() : null;
        def.Comment = GetComment();
        ExpectKeyword("interface");
        def.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#InterfaceTypeDefinition");
        def.Interfaces = _currentToken.Value == "implements" ? ParseImplementsInterfaces() : null;
        def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        def.Fields = Peek(TokenKind.BRACE_L) ? ParseFieldsDefinition() : null;
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#InterfaceTypeExtension
    // Note that due to the spec type extensions have no descriptions.
    private GraphQLInterfaceTypeExtension ParseInterfaceTypeExtension(int start, GraphQLComment? comment)
    {
        IncreaseDepth();

        var extension = NodeHelper.CreateGraphQLInterfaceTypeExtension(_ignoreOptions);

        extension.Comment = comment;
        ExpectKeyword("interface");
        extension.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#InterfaceTypeExtension");
        extension.Interfaces = _currentToken.Value == "implements" ? ParseImplementsInterfaces() : null;
        extension.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        extension.Fields = Peek(TokenKind.BRACE_L) ? ParseFieldsDefinition() : null;
        extension.Location = GetLocation(start);

        if (extension.Directives == null && extension.Fields == null && extension.Interfaces == null)
            return (GraphQLInterfaceTypeExtension)Throw_Unexpected_Token("; for more information see http://spec.graphql.org/October2021/#InterfaceTypeExtension");

        DecreaseDepth();
        return extension;
    }

    // http://spec.graphql.org/October2021/#ListValue
    private GraphQLValue ParseListValue(bool isConstant)
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        // the compiler caches these delegates in the generated code
        ParseCallback<GraphQLValue> constant = (ref ParserContext context) => context.ParseValueLiteral(true);
        ParseCallback<GraphQLValue> value = (ref ParserContext context) => context.ParseValueLiteral(false);

        var val = NodeHelper.CreateGraphQLListValue(_ignoreOptions);

        val.Comment = GetComment();
        val.Values = ZeroOrMore(TokenKind.BRACKET_L, isConstant ? constant : value, TokenKind.BRACKET_R);
        val.Value = _source.Slice(start, _currentToken.End - start - 1);
        val.Location = GetLocation(start);

        DecreaseDepth();
        return val;
    }

    // http://spec.graphql.org/October2021/#Name
    private GraphQLName ParseName(string description)
    {
        IncreaseDepth();

        int start = _currentToken.Start;
        var value = _currentToken.Value;

        var name = NodeHelper.CreateGraphQLName(_ignoreOptions);

        name.Comment = GetComment();
        Expect(TokenKind.NAME, description);
        name.Value = value;
        name.Location = GetLocation(start);

        DecreaseDepth();
        return name;
    }

    private ASTNode ParseNamedDefinition()
    {
        return ExpectOneOf(TopLevelKeywordOneOf, advance: false) switch
        {
            "query" => ParseOperationDefinition(),
            "mutation" => ParseOperationDefinition(),
            "subscription" => ParseOperationDefinition(),
            "fragment" => ParseFragmentDefinition(),
            "schema" => ParseSchemaDefinition(),
            "scalar" => ParseScalarTypeDefinition(),
            "type" => ParseObjectTypeDefinition(),
            "interface" => ParseInterfaceTypeDefinition(),
            "union" => ParseUnionTypeDefinition(),
            "enum" => ParseEnumTypeDefinition(),
            "input" => ParseInputObjectTypeDefinition(),
            "extend" => ParseTypeExtension(),
            "directive" => ParseDirectiveDefinition(),

            _ => throw new NotSupportedException("Compiler never gets here since ExpectOneOf throws.")
        };
    }

    // TODO: 1. May be changed to use ExpectOneOf, or
    // TODO: 2. https://github.com/graphql/graphql-spec/pull/892 which allow to remove this method
    private ASTNode ParseNamedDefinitionWithDescription()
    {
        // look-ahead to next token (_currentToken remains unchanged)
        var token = Lexer.Lex(_source, _currentToken.End);
        // skip comments
        while (token.Kind != TokenKind.EOF && token.Kind == TokenKind.COMMENT)
        {
            token = Lexer.Lex(_source, token.End);
        }

        // verify this is a NAME
        if (token.Kind == TokenKind.NAME)
        {
            // retrieve the value
            var value = token.Value;

            if (value == "schema")
                return ParseSchemaDefinition();

            if (value == "scalar")
                return ParseScalarTypeDefinition();

            if (value == "type")
                return ParseObjectTypeDefinition();

            if (value == "interface")
                return ParseInterfaceTypeDefinition();

            if (value == "union")
                return ParseUnionTypeDefinition();

            if (value == "enum")
                return ParseEnumTypeDefinition();

            if (value == "input")
                return ParseInputObjectTypeDefinition();

            if (value == "directive")
                return ParseDirectiveDefinition();
        }

        return Throw_Unexpected_Token();
    }

    // http://spec.graphql.org/October2021/#NamedType
    private GraphQLNamedType ParseNamedType()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var named = NodeHelper.CreateGraphQLNamedType(_ignoreOptions);

        named.Comment = GetComment();
        named.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#NamedType");
        named.Location = GetLocation(start);

        DecreaseDepth();
        return named;
    }

    private GraphQLValue ParseNameValue(/*bool isConstant*/)
    {
        var token = _currentToken;

        if (token.Value == "true" || token.Value == "false")
        {
            return ParseBooleanValue();
        }
        else if (!token.Value.IsEmpty)
        {
            return token.Value == "null"
                ? ParseNullValue()
                : ParseEnumValue();
        }

        return (GraphQLValue)Throw_Unexpected_Token();
    }

    // http://spec.graphql.org/October2021/#ObjectValue
    private GraphQLValue ParseObjectValue(bool isConstant)
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        // the compiler caches these delegates in the generated code
        ParseCallback<GraphQLObjectField> constant = (ref ParserContext context) => context.ParseObjectField(true);
        ParseCallback<GraphQLObjectField> value = (ref ParserContext context) => context.ParseObjectField(false);

        var val = NodeHelper.CreateGraphQLObjectValue(_ignoreOptions);

        val.Comment = GetComment();
        val.Fields = ZeroOrMore(TokenKind.BRACE_L, isConstant ? constant : value, TokenKind.BRACE_R);
        val.Location = GetLocation(start);

        DecreaseDepth();
        return val;
    }

    // http://spec.graphql.org/October2021/#NullValue
    private GraphQLValue ParseNullValue()
    {
        IncreaseDepth();

        var token = _currentToken;

        var val = NodeHelper.CreateGraphQLNullValue(_ignoreOptions);

        val.Comment = GetComment();
        val.Value = token.Value;
        Advance();
        val.Location = GetLocation(token.Start);

        DecreaseDepth();
        return val;
    }

    // http://spec.graphql.org/October2021/#ObjectField
    private GraphQLObjectField ParseObjectField(bool isConstant)
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var field = NodeHelper.CreateGraphQLObjectField(_ignoreOptions);

        field.Comment = GetComment();
        field.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#ObjectField");
        Expect(TokenKind.COLON);
        field.Value = ParseValueLiteral(isConstant);
        field.Location = GetLocation(start);

        DecreaseDepth();
        return field;
    }

    // http://spec.graphql.org/October2021/#ObjectTypeDefinition
    private GraphQLObjectTypeDefinition ParseObjectTypeDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLObjectTypeDefinition(_ignoreOptions);

        def.Description = Peek(TokenKind.STRING) ? ParseDescription() : null;
        def.Comment = GetComment();
        ExpectKeyword("type");
        def.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#ObjectTypeDefinition");
        def.Interfaces = _currentToken.Value == "implements" ? ParseImplementsInterfaces() : null;
        def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        def.Fields = Peek(TokenKind.BRACE_L) ? ParseFieldsDefinition() : null;
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#ObjectTypeExtension
    // Note that due to the spec type extensions have no descriptions.
    private GraphQLObjectTypeExtension ParseObjectTypeExtension(int start, GraphQLComment? comment)
    {
        IncreaseDepth();

        var extension = NodeHelper.CreateGraphQLObjectTypeExtension(_ignoreOptions);

        extension.Comment = comment;
        ExpectKeyword("type");
        extension.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#ObjectTypeExtension");
        extension.Interfaces = _currentToken.Value == "implements" ? ParseImplementsInterfaces() : null;
        extension.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        extension.Fields = Peek(TokenKind.BRACE_L) ? ParseFieldsDefinition() : null;
        extension.Location = GetLocation(start);

        if (extension.Directives == null && extension.Fields == null && extension.Interfaces == null)
            return (GraphQLObjectTypeExtension)Throw_Unexpected_Token("; for more information see http://spec.graphql.org/October2021/#ObjectTypeExtension");

        DecreaseDepth();
        return extension;
    }

    // http://spec.graphql.org/October2021/#OperationDefinition
    private ASTNode ParseOperationDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLOperationDefinition(_ignoreOptions);

        def.Comment = GetComment();

        if (Peek(TokenKind.BRACE_L))
        {
            def.Operation = OperationType.Query;
            def.SelectionSet = ParseSelectionSet();
            def.Location = GetLocation(start);
        }
        else
        {
            def.Operation = ParseOperationType();
            def.Name = Peek(TokenKind.NAME) ? ParseName("; for more information see http://spec.graphql.org/October2021/#OperationDefinition") : null; // Peek(TokenKind.NAME) because of anonymous query
            def.Variables = Peek(TokenKind.PAREN_L) ? ParseVariablesDefinition() : null;
            def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
            def.SelectionSet = ParseSelectionSet();
            def.Location = GetLocation(start);
        }

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#OperationType
    private OperationType ParseOperationType()
    {
        return ExpectOneOf(OperationTypeOneOf) switch
        {
            "query" => OperationType.Query,
            "mutation" => OperationType.Mutation,
            "subscription" => OperationType.Subscription,

            _ => throw new NotSupportedException("Compiler never gets here since ExpectOneOf throws.")
        };
    }

    // http://spec.graphql.org/June2018/#DirectiveLocation
    private DirectiveLocation ParseDirectiveLocation()
    {
        return ExpectOneOf(DirectiveLocationOneOf) switch
        {
            // http://spec.graphql.org/June2018/#ExecutableDirectiveLocation
            "QUERY" => DirectiveLocation.Query,
            "MUTATION" => DirectiveLocation.Mutation,
            "SUBSCRIPTION" => DirectiveLocation.Subscription,
            "FIELD" => DirectiveLocation.Field,
            "FRAGMENT_DEFINITION" => DirectiveLocation.FragmentDefinition,
            "FRAGMENT_SPREAD" => DirectiveLocation.FragmentSpread,
            "INLINE_FRAGMENT" => DirectiveLocation.InlineFragment,
            "VARIABLE_DEFINITION" => DirectiveLocation.VariableDefinition,

            // http://spec.graphql.org/June2018/#TypeSystemDirectiveLocation
            "SCHEMA" => DirectiveLocation.Schema,
            "SCALAR" => DirectiveLocation.Scalar,
            "OBJECT" => DirectiveLocation.Object,
            "FIELD_DEFINITION" => DirectiveLocation.FieldDefinition,
            "ARGUMENT_DEFINITION" => DirectiveLocation.ArgumentDefinition,
            "INTERFACE" => DirectiveLocation.Interface,
            "UNION" => DirectiveLocation.Union,
            "ENUM" => DirectiveLocation.Enum,
            "ENUM_VALUE" => DirectiveLocation.EnumValue,
            "INPUT_OBJECT" => DirectiveLocation.InputObject,
            "INPUT_FIELD_DEFINITION" => DirectiveLocation.InputFieldDefinition,

            _ => throw new NotSupportedException("Compiler never gets here since ExpectOneOf throws.")
        };
    }

    // http://spec.graphql.org/October2021/#RootOperationTypeDefinition
    private GraphQLRootOperationTypeDefinition ParseRootOperationTypeDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLOperationTypeDefinition(_ignoreOptions);

        def.Comment = GetComment();
        def.Operation = ParseOperationType();
        Expect(TokenKind.COLON);
        def.Type = ParseNamedType();
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#ScalarTypeDefinition
    private GraphQLScalarTypeDefinition ParseScalarTypeDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLScalarTypeDefinition(_ignoreOptions);

        def.Description = Peek(TokenKind.STRING) ? ParseDescription() : null;
        def.Comment = GetComment();
        ExpectKeyword("scalar");
        def.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#ScalarTypeDefinition");
        def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#ScalarTypeExtension
    // Note that due to the spec type extensions have no descriptions.
    private GraphQLScalarTypeExtension ParseScalarTypeExtension(int start, GraphQLComment? comment)
    {
        IncreaseDepth();

        var extension = NodeHelper.CreateGraphQLScalarTypeExtension(_ignoreOptions);

        extension.Comment = comment;
        ExpectKeyword("scalar");
        extension.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#ScalarTypeExtension");
        extension.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        extension.Location = GetLocation(start);

        if (extension.Directives == null)
            return (GraphQLScalarTypeExtension)Throw_Unexpected_Token("; for more information see http://spec.graphql.org/October2021/#ScalarTypeExtension");

        DecreaseDepth();
        return extension;
    }

    // http://spec.graphql.org/October2021/#SchemaDefinition
    private GraphQLSchemaDefinition ParseSchemaDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLSchemaDefinition(_ignoreOptions);

        def.Description = Peek(TokenKind.STRING) ? ParseDescription() : null;
        def.Comment = GetComment();
        ExpectKeyword("schema");
        def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        def.OperationTypes = OneOrMore(TokenKind.BRACE_L, (ref ParserContext context) => context.ParseRootOperationTypeDefinition(), TokenKind.BRACE_R);
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#Selection
    private ASTNode ParseSelection()
    {
        return Peek(TokenKind.SPREAD) ?
            ParseFragment() :
            ParseField();
    }

    // http://spec.graphql.org/October2021/#SelectionSet
    private GraphQLSelectionSet ParseSelectionSet()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var selection = NodeHelper.CreateGraphQLSelectionSet(_ignoreOptions);

        selection.Comment = GetComment();
        selection.Selections = OneOrMore(TokenKind.BRACE_L, (ref ParserContext context) => context.ParseSelection(), TokenKind.BRACE_R);
        selection.Location = GetLocation(start);

        DecreaseDepth();
        return selection;
    }

    // http://spec.graphql.org/October2021/#StringValue
    private GraphQLStringValue ParseStringValue(/*bool isConstant*/)
    {
        IncreaseDepth();

        var token = _currentToken;

        var val = NodeHelper.CreateGraphQLStringValue(_ignoreOptions);

        val.Comment = GetComment();
        Advance();
        val.Value = token.Value;
        val.Location = GetLocation(token.Start);

        DecreaseDepth();
        return val;
    }

    // http://spec.graphql.org/October2021/#Description
    private GraphQLDescription ParseDescription()
    {
        IncreaseDepth();

        var token = _currentToken;
        Advance();

        var descr = NodeHelper.CreateGraphQLDescription(_ignoreOptions);

        descr.Value = token.Value;
        descr.Location = GetLocation(token.Start);

        DecreaseDepth();
        return descr;
    }

    // http://spec.graphql.org/October2021/#Type
    private GraphQLType ParseType()
    {
        IncreaseDepth();

        GraphQLType type;
        int start = _currentToken.Start;
        if (Peek(TokenKind.BRACKET_L))
        {
            var listType = NodeHelper.CreateGraphQLListType(_ignoreOptions);

            listType.Comment = GetComment();

            Advance(); // skip BRACKET_L

            listType.Type = ParseType();

            Expect(TokenKind.BRACKET_R);

            listType.Location = GetLocation(start);
            type = listType;
        }
        else
        {
            type = ParseNamedType();
        }

        if (!Skip(TokenKind.BANG))
        {
            DecreaseDepth();
            return type;
        }

        var nonNull = NodeHelper.CreateGraphQLNonNullType(_ignoreOptions); //TODO: deal with depth

        nonNull.Type = type;
        // move comment from wrapped type to wrapping type
        nonNull.Comment = type.Comment;
        type.Comment = null;
        nonNull.Location = GetLocation(start);

        DecreaseDepth();
        return nonNull;
    }

    // http://spec.graphql.org/October2021/#TypeExtension
    private GraphQLTypeExtension ParseTypeExtension()
    {
        int start = _currentToken.Start;
        var comment = GetComment();

        ExpectKeyword("extend");

        return ExpectOneOf(TypeExtensionOneOf, advance: false) switch
        {
            "scalar" => ParseScalarTypeExtension(start, comment),
            "type" => ParseObjectTypeExtension(start, comment),
            "interface" => ParseInterfaceTypeExtension(start, comment),
            "union" => ParseUnionTypeExtension(start, comment),
            "enum" => ParseEnumTypeExtension(start, comment),
            "input" => ParseInputObjectTypeExtension(start, comment),

            _ => throw new NotSupportedException("Compiler never gets here since ExpectOneOf throws.")
        };
    }

    // http://spec.graphql.org/October2021/#UnionMemberTypes
    private GraphQLUnionMemberTypes ParseUnionMemberTypes()
    {
        IncreaseDepth();
        var comment = GetComment();

        int start = _currentToken.Start;

        Expect(TokenKind.EQUALS);

        var unionMemberTypes = NodeHelper.CreateGraphQLUnionMemberTypes(_ignoreOptions);

        List<GraphQLNamedType> types = new();

        // Union members may be defined with an optional leading | character
        // to aid formatting when representing a longer list of possible types
        _ = Skip(TokenKind.PIPE);

        do
        {
            types.Add(ParseNamedType());
        }
        while (Skip(TokenKind.PIPE));

        unionMemberTypes.Items = types;
        unionMemberTypes.Comment = comment;
        unionMemberTypes.Location = GetLocation(start);

        DecreaseDepth();
        return unionMemberTypes;
    }

    // http://spec.graphql.org/October2021/#UnionTypeDefinition
    private GraphQLUnionTypeDefinition ParseUnionTypeDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLUnionTypeDefinition(_ignoreOptions);

        def.Description = Peek(TokenKind.STRING) ? ParseDescription() : null;
        def.Comment = GetComment();
        ExpectKeyword("union");
        def.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#UnionTypeDefinition");
        def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        def.Types = Peek(TokenKind.EQUALS) ? ParseUnionMemberTypes() : null;
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }

    // http://spec.graphql.org/October2021/#UnionTypeExtension
    // Note that due to the spec type extensions have no descriptions.
    private GraphQLUnionTypeExtension ParseUnionTypeExtension(int start, GraphQLComment? comment)
    {
        IncreaseDepth();

        var extension = NodeHelper.CreateGraphQLUnionTypeExtension(_ignoreOptions);

        extension.Comment = comment;
        ExpectKeyword("union");
        extension.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#UnionTypeExtension");
        extension.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        extension.Types = Peek(TokenKind.EQUALS) ? ParseUnionMemberTypes() : null;
        extension.Location = GetLocation(start);

        if (extension.Directives == null && extension.Types == null)
            return (GraphQLUnionTypeExtension)Throw_Unexpected_Token("; for more information see http://spec.graphql.org/October2021/#UnionTypeExtension");

        DecreaseDepth();
        return extension;
    }

    private GraphQLValue ParseValueLiteral(bool isConstant)
    {
        return _currentToken.Kind switch
        {
            TokenKind.BRACKET_L => ParseListValue(isConstant),
            TokenKind.BRACE_L => ParseObjectValue(isConstant),
            TokenKind.INT => ParseIntValue(/*isConstant*/),
            TokenKind.FLOAT => ParseFloatValue(/*isConstant*/),
            TokenKind.STRING => ParseStringValue(/*isConstant*/),
            TokenKind.NAME => ParseNameValue(/*isConstant*/),
            TokenKind.DOLLAR when !isConstant => ParseVariable(),
            _ => (GraphQLValue)Throw_Unexpected_Token()
        };
    }

    // http://spec.graphql.org/October2021/#Variable
    private GraphQLVariable ParseVariable()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var variable = NodeHelper.CreateGraphQLVariable(_ignoreOptions);

        variable.Comment = GetComment();
        Expect(TokenKind.DOLLAR);
        variable.Name = ParseName("; for more information see http://spec.graphql.org/October2021/#Variable");
        variable.Location = GetLocation(start);

        DecreaseDepth();
        return variable;
    }

    // http://spec.graphql.org/October2021/#VariableDefinition
    private GraphQLVariableDefinition ParseVariableDefinition()
    {
        IncreaseDepth();

        int start = _currentToken.Start;

        var def = NodeHelper.CreateGraphQLVariableDefinition(_ignoreOptions);

        def.Comment = GetComment();
        def.Variable = ParseVariable();
        Expect(TokenKind.COLON);
        def.Type = ParseType();
        def.DefaultValue = Skip(TokenKind.EQUALS) ? ParseValueLiteral(true) : null;
        def.Directives = Peek(TokenKind.AT) ? ParseDirectives() : null;
        def.Location = GetLocation(start);

        DecreaseDepth();
        return def;
    }
}
