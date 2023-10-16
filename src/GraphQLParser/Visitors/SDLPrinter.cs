using System.Diagnostics.CodeAnalysis;
using GraphQLParser.AST;

namespace GraphQLParser.Visitors;

/// <summary>
/// Prints AST into the provided <see cref="TextWriter"/> as a SDL document.
/// </summary>
/// <typeparam name="TContext">Type of the context object passed into all VisitXXX methods.</typeparam>
public class SDLPrinter<TContext> : ASTVisitor<TContext>
    where TContext : IPrintContext
{
    /// <summary>
    /// Creates visitor with default options.
    /// </summary>
    public SDLPrinter()
        : this(new SDLPrinterOptions())
    {
    }

    /// <summary>
    /// Creates visitor with the specified options.
    /// </summary>
    /// <param name="options">Visitor options.</param>
    public SDLPrinter(SDLPrinterOptions options)
    {
        Options = options;
    }

    /// <summary>
    /// Options used by visitor.
    /// </summary>
    public SDLPrinterOptions Options { get; }

    /// <inheritdoc/>
    protected override async ValueTask VisitDocumentAsync(GraphQLDocument document, TContext context)
    {
        await VisitAsync(document.Comments, context).ConfigureAwait(false); // Comments always null

        if (document.Definitions.Count > 0) // Definitions always > 0
        {
            for (int i = 0; i < document.Definitions.Count; ++i)
                await VisitAsync(document.Definitions[i], context).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitCommentAsync(GraphQLComment comment, TContext context)
    {
        if (Options.PrintComments)
        {
            await context.WriteAsync("#").ConfigureAwait(false);
            await context.WriteAsync(comment.Value).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override ValueTask VisitDescriptionAsync(GraphQLDescription description, TContext context)
    {
        bool ShouldBeMultilineBlockString()
        {
            bool newLineDetected = false;
            var span = description.Value.Span;
            for (int i = 0; i < span.Length; ++i)
            {
                char code = span[i];

                if (code < 0x0020 && code != 0x0009 && code != 0x000A && code != 0x000D)
                    return false;

                if (code == 0x000A)
                    newLineDetected = true;
            }

            // Note that string value without escape symbols and newline symbols
            // MAY BE represented as a single-line BlockString, for example,
            // """SOME TEXT""", but it was decided to use more brief "SOME TEXT"
            // for such cases. WriteMultilineBlockString method ALWAYS builds
            // BlockString printing """ on new line.
            return newLineDetected;
        }

        async ValueTask WriteMultilineBlockString()
        {
            await context.WriteAsync("\"\"\"").ConfigureAwait(false);
            await context.WriteLineAsync().ConfigureAwait(false); // internal NewLine in BlockString

            bool needStartNewLine = true;
            int length = description.Value.Span.Length;
            // http://spec.graphql.org/October2021/#BlockStringCharacter
            for (int i = 0; i < length; ++i)
            {
                if (needStartNewLine)
                {
                    await WriteIndentAsync(context).ConfigureAwait(false);
                    needStartNewLine = false;
                }

                char code = description.Value.Span[i];
                switch (code)
                {
                    case '\r':
                        break;

                    case '\n':
                        // do not use PrintContextExtensions.WriteLineAsync here to avoid NewLinePrinted check
                        await context.WriteAsync(Environment.NewLine).ConfigureAwait(false);
                        needStartNewLine = true;
                        break;

                    case '"':
                        if (i < length - 2 && description.Value.Span[i + 1] == '"' && description.Value.Span[i + 2] == '"')
                        {
                            await context.WriteAsync("\\\"").ConfigureAwait(false);
                        }
                        else
                        {
                            await context.WriteAsync(description.Value.Slice(i, 1)/*code*/).ConfigureAwait(false); //TODO: change
                        }
                        break;

                    default:
                        await context.WriteAsync(description.Value.Slice(i, 1)/*code*/).ConfigureAwait(false); //TODO: change
                        break;
                }
            }

            await context.WriteLineAsync().ConfigureAwait(false); // internal NewLine in BlockString
            await WriteIndentAsync(context).ConfigureAwait(false);
            await context.WriteAsync("\"\"\"").ConfigureAwait(false);
        }

        ValueTask WriteString() => WriteEncodedStringAsync(context, description.Value);

        if (!Options.PrintDescriptions)
            return default;

        // http://spec.graphql.org/October2021/#StringValue
        return ShouldBeMultilineBlockString()
            ? WriteMultilineBlockString()
            : WriteString();
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitNameAsync(GraphQLName name, TContext context)
    {
        await VisitAsync(name.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap(name.Value), context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitFragmentNameAsync(GraphQLFragmentName fragmentName, TContext context)
    {
        await VisitAsync(fragmentName.Comments, context).ConfigureAwait(false);
        await VisitAsync(fragmentName.Name, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitFragmentDefinitionAsync(GraphQLFragmentDefinition fragmentDefinition, TContext context)
    {
        await VisitAsync(fragmentDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("fragment "), context).ConfigureAwait(false);
        await VisitAsync(fragmentDefinition.FragmentName, context).ConfigureAwait(false);
        await VisitAsync(fragmentDefinition.TypeCondition, context).ConfigureAwait(false);
        await VisitAsync(fragmentDefinition.Directives, context).ConfigureAwait(false);
        await VisitAsync(fragmentDefinition.SelectionSet, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitFragmentSpreadAsync(GraphQLFragmentSpread fragmentSpread, TContext context)
    {
        await VisitAsync(fragmentSpread.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("..."), context).ConfigureAwait(false);
        await VisitAsync(fragmentSpread.FragmentName, context).ConfigureAwait(false);
        await VisitAsync(fragmentSpread.Directives, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitInlineFragmentAsync(GraphQLInlineFragment inlineFragment, TContext context)
    {
        await VisitAsync(inlineFragment.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("..."), context).ConfigureAwait(false);
        await VisitAsync(inlineFragment.TypeCondition, context).ConfigureAwait(false);
        await VisitAsync(inlineFragment.Directives, context).ConfigureAwait(false);
        await VisitAsync(inlineFragment.SelectionSet, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitTypeConditionAsync(GraphQLTypeCondition typeCondition, TContext context)
    {
        await VisitAsync(typeCondition.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap(HasPrintableComments(typeCondition) ? "on " : " on "), context).ConfigureAwait(false);
        await VisitAsync(typeCondition.Type, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitImplementsInterfacesAsync(GraphQLImplementsInterfaces implementsInterfaces, TContext context)
    {
        await VisitAsync(implementsInterfaces.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap(HasPrintableComments(implementsInterfaces) ? "implements " : " implements "), context).ConfigureAwait(false);

        for (int i = 0; i < implementsInterfaces.Items.Count; ++i)
        {
            await VisitAsync(implementsInterfaces.Items[i], context).ConfigureAwait(false);
            if (i < implementsInterfaces.Items.Count - 1)
                await context.WriteAsync(HasPrintableComments(implementsInterfaces[i + 1]) ? " &" : " & ").ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitSelectionSetAsync(GraphQLSelectionSet selectionSet, TContext context)
    {
        await VisitAsync(selectionSet.Comments, context).ConfigureAwait(false);

        bool hasParent = TryPeekParent(context, out var parent);
        if (!HasPrintableComments(selectionSet) && hasParent && (parent is GraphQLOperationDefinition op && op.Name is not null || parent is not GraphQLOperationDefinition))
        {
            await context.WriteAsync(" {").ConfigureAwait(false);
        }
        else
        {
            await VisitAsync(LiteralNode.Wrap("{"), context).ConfigureAwait(false);
        }
        await context.WriteLineAsync().ConfigureAwait(false); // internal NewLine in SelectionSet

        foreach (var selection in selectionSet.Selections)
        {
            await VisitAsync(selection, context).ConfigureAwait(false);
            await context.WriteLineAsync().ConfigureAwait(false);
        }

        await context.WriteLineAsync().ConfigureAwait(false);
        await WriteIndentAsync(context).ConfigureAwait(false);
        await context.WriteAsync("}").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitAliasAsync(GraphQLAlias alias, TContext context)
    {
        await VisitAsync(alias.Comments, context).ConfigureAwait(false);
        await VisitAsync(alias.Name, context).ConfigureAwait(false);
        await context.WriteAsync(":").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitFieldAsync(GraphQLField field, TContext context)
    {
        await VisitAsync(field.Comments, context).ConfigureAwait(false);
        await VisitAsync(field.Alias, context).ConfigureAwait(false);

        if (field.Alias != null && (field.Name.Comments == null || !Options.PrintComments))
            await context.WriteAsync(" ").ConfigureAwait(false);

        await VisitAsync(field.Name, context).ConfigureAwait(false);
        await VisitAsync(field.Arguments, context).ConfigureAwait(false);
        await VisitAsync(field.Directives, context).ConfigureAwait(false);
        await VisitAsync(field.SelectionSet, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitOperationDefinitionAsync(GraphQLOperationDefinition operationDefinition, TContext context)
    {
        await VisitAsync(operationDefinition.Comments, context).ConfigureAwait(false);
        if (operationDefinition.Name is not null)
        {
            await VisitAsync(LiteralNode.Wrap(GetOperationType(operationDefinition.Operation)), context).ConfigureAwait(false);
            await context.WriteAsync(" ").ConfigureAwait(false);
            await VisitAsync(operationDefinition.Name, context).ConfigureAwait(false);
        }
        await VisitAsync(operationDefinition.Variables, context).ConfigureAwait(false);
        await VisitAsync(operationDefinition.Directives, context).ConfigureAwait(false);
        await VisitAsync(operationDefinition.SelectionSet, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitDirectiveDefinitionAsync(GraphQLDirectiveDefinition directiveDefinition, TContext context)
    {
        await VisitAsync(directiveDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(directiveDefinition.Description, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("directive @"), context).ConfigureAwait(false);
        await VisitAsync(directiveDefinition.Name, context).ConfigureAwait(false);
        await VisitAsync(directiveDefinition.Arguments, context).ConfigureAwait(false);
        if (directiveDefinition.Repeatable)
            await context.WriteAsync(" repeatable").ConfigureAwait(false);
        await VisitAsync(directiveDefinition.Locations, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitDirectiveLocationsAsync(GraphQLDirectiveLocations directiveLocations, TContext context)
    {
        await VisitAsync(directiveLocations.Comments, context).ConfigureAwait(false);
        if (Options.EachDirectiveLocationOnNewLine)
        {
            await context.WriteAsync(" on").ConfigureAwait(false);
            if (directiveLocations.Items?.Count > 0) // should always be true but may be negligently uninitialized
            {
                await context.WriteLineAsync().ConfigureAwait(false);
                for (int i = 0; i < directiveLocations.Items.Count; ++i)
                {
                    await VisitAsync(LiteralNode.Wrap("| "), context).ConfigureAwait(false);
                    await context.WriteAsync(GetDirectiveLocation(directiveLocations.Items[i])).ConfigureAwait(false);
                    if (i < directiveLocations.Items.Count - 1)
                        await context.WriteLineAsync().ConfigureAwait(false);
                }
            }
        }
        else
        {
            await context.WriteAsync(" on").ConfigureAwait(false);
            if (directiveLocations.Items?.Count > 0) // should always be true but may be negligently uninitialized
            {
                await context.WriteAsync(" ").ConfigureAwait(false);
                for (int i = 0; i < directiveLocations.Items.Count; ++i)
                {
                    await context.WriteAsync(GetDirectiveLocation(directiveLocations.Items[i])).ConfigureAwait(false);
                    if (i < directiveLocations.Items.Count - 1)
                        await context.WriteAsync(" | ").ConfigureAwait(false);
                }
            }
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitVariableDefinitionAsync(GraphQLVariableDefinition variableDefinition, TContext context)
    {
        await VisitAsync(variableDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(variableDefinition.Variable, context).ConfigureAwait(false);
        await context.WriteAsync(": ").ConfigureAwait(false);
        await VisitAsync(variableDefinition.Type, context).ConfigureAwait(false);
        if (variableDefinition.DefaultValue != null)
        {
            await context.WriteAsync(" = ").ConfigureAwait(false);
            await VisitAsync(variableDefinition.DefaultValue, context).ConfigureAwait(false);
        }
        await VisitAsync(variableDefinition.Directives, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitVariablesDefinitionAsync(GraphQLVariablesDefinition variablesDefinition, TContext context)
    {
        await VisitAsync(variablesDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("("), context).ConfigureAwait(false);

        for (int i = 0; i < variablesDefinition.Items.Count; ++i)
        {
            await VisitAsync(variablesDefinition.Items[i], context).ConfigureAwait(false);
            if (i < variablesDefinition.Items.Count - 1)
                await context.WriteAsync(", ").ConfigureAwait(false);
        }

        await context.WriteAsync(")").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitVariableAsync(GraphQLVariable variable, TContext context)
    {
        await VisitAsync(variable.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("$"), context).ConfigureAwait(false);
        await VisitAsync(variable.Name, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitBooleanValueAsync(GraphQLBooleanValue booleanValue, TContext context)
    {
        await VisitAsync(booleanValue.Comments, context).ConfigureAwait(false);
        await context.WriteAsync(booleanValue.Value).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitScalarTypeDefinitionAsync(GraphQLScalarTypeDefinition scalarTypeDefinition, TContext context)
    {
        await VisitAsync(scalarTypeDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(scalarTypeDefinition.Description, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("scalar "), context).ConfigureAwait(false);
        await VisitAsync(scalarTypeDefinition.Name, context).ConfigureAwait(false);
        await VisitAsync(scalarTypeDefinition.Directives, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitSchemaExtensionAsync(GraphQLSchemaExtension schemaExtension, TContext context)
    {
        await VisitAsync(schemaExtension.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("extend schema"), context).ConfigureAwait(false);
        await VisitAsync(schemaExtension.Directives, context).ConfigureAwait(false);

        //TODO: https://github.com/graphql/graphql-spec/issues/921
        if (schemaExtension.OperationTypes?.Count > 0)
        {
            await context.WriteAsync(/*HasPrintableComments(schemaExtension) ? "{" :*/" {").ConfigureAwait(false); // always false
            await context.WriteLineAsync().ConfigureAwait(false);

            for (int i = 0; i < schemaExtension.OperationTypes.Count; ++i)
            {
                await VisitAsync(schemaExtension.OperationTypes[i], context).ConfigureAwait(false);
                await context.WriteLineAsync().ConfigureAwait(false);
            }

            await context.WriteAsync("}").ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitScalarTypeExtensionAsync(GraphQLScalarTypeExtension scalarTypeExtension, TContext context)
    {
        await VisitAsync(scalarTypeExtension.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("extend scalar "), context).ConfigureAwait(false);
        await VisitAsync(scalarTypeExtension.Name, context).ConfigureAwait(false);
        await VisitAsync(scalarTypeExtension.Directives, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitEnumTypeDefinitionAsync(GraphQLEnumTypeDefinition enumTypeDefinition, TContext context)
    {
        await VisitAsync(enumTypeDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(enumTypeDefinition.Description, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("enum "), context).ConfigureAwait(false);
        await VisitAsync(enumTypeDefinition.Name, context).ConfigureAwait(false);
        await VisitAsync(enumTypeDefinition.Directives, context).ConfigureAwait(false);
        await VisitAsync(enumTypeDefinition.Values, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitEnumTypeExtensionAsync(GraphQLEnumTypeExtension enumTypeExtension, TContext context)
    {
        await VisitAsync(enumTypeExtension.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("extend enum "), context).ConfigureAwait(false);
        await VisitAsync(enumTypeExtension.Name, context).ConfigureAwait(false);
        await VisitAsync(enumTypeExtension.Directives, context).ConfigureAwait(false);
        await VisitAsync(enumTypeExtension.Values, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitEnumValueDefinitionAsync(GraphQLEnumValueDefinition enumValueDefinition, TContext context)
    {
        await VisitAsync(enumValueDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(enumValueDefinition.Description, context).ConfigureAwait(false);
        await VisitAsync(enumValueDefinition.Name, context).ConfigureAwait(false);
        await VisitAsync(enumValueDefinition.Directives, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitEnumValuesDefinitionAsync(GraphQLEnumValuesDefinition enumValuesDefinition, TContext context)
    {
        await VisitAsync(enumValuesDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap(HasPrintableComments(enumValuesDefinition) || !TryPeekParent(context, out _) ? "{" : " {"), context).ConfigureAwait(false);
        await context.WriteLineAsync().ConfigureAwait(false);

        if (enumValuesDefinition.Items?.Count > 0) // should always be true but may be negligently uninitialized
        {
            foreach (var enumValueDefinition in enumValuesDefinition.Items)
            {
                await VisitAsync(enumValueDefinition, context).ConfigureAwait(false);
                await context.WriteLineAsync().ConfigureAwait(false);
            }
        }
        await context.WriteAsync("}").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitInputObjectTypeDefinitionAsync(GraphQLInputObjectTypeDefinition inputObjectTypeDefinition, TContext context)
    {
        await VisitAsync(inputObjectTypeDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(inputObjectTypeDefinition.Description, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("input "), context).ConfigureAwait(false);
        await VisitAsync(inputObjectTypeDefinition.Name, context).ConfigureAwait(false);
        await VisitAsync(inputObjectTypeDefinition.Directives, context).ConfigureAwait(false);
        await VisitAsync(inputObjectTypeDefinition.Fields, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitInputObjectTypeExtensionAsync(GraphQLInputObjectTypeExtension inputObjectTypeExtension, TContext context)
    {
        await VisitAsync(inputObjectTypeExtension.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("extend input "), context).ConfigureAwait(false);
        await VisitAsync(inputObjectTypeExtension.Name, context).ConfigureAwait(false);
        await VisitAsync(inputObjectTypeExtension.Directives, context).ConfigureAwait(false);
        await VisitAsync(inputObjectTypeExtension.Fields, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitInputValueDefinitionAsync(GraphQLInputValueDefinition inputValueDefinition, TContext context)
    {
        await VisitAsync(inputValueDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(inputValueDefinition.Description, context).ConfigureAwait(false);
        await VisitAsync(inputValueDefinition.Name, context).ConfigureAwait(false);
        await context.WriteAsync(": ").ConfigureAwait(false);
        await VisitAsync(inputValueDefinition.Type, context).ConfigureAwait(false);
        if (inputValueDefinition.DefaultValue != null)
        {
            await context.WriteAsync(" = ").ConfigureAwait(false);
            await VisitAsync(inputValueDefinition.DefaultValue, context).ConfigureAwait(false);
        }
        await VisitAsync(inputValueDefinition.Directives, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitInputFieldsDefinitionAsync(GraphQLInputFieldsDefinition inputFieldsDefinition, TContext context)
    {
        await VisitAsync(inputFieldsDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap(HasPrintableComments(inputFieldsDefinition) || !TryPeekParent(context, out _) ? "{" : " {"), context).ConfigureAwait(false);
        await context.WriteLineAsync().ConfigureAwait(false);

        if (inputFieldsDefinition.Items?.Count > 0) // should always be true but may be negligently uninitialized
        {
            foreach (var inputFieldDefinition in inputFieldsDefinition.Items)
            {
                await VisitAsync(inputFieldDefinition, context).ConfigureAwait(false);
                await context.WriteLineAsync().ConfigureAwait(false);
            }
        }

        await context.WriteAsync("}").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitObjectTypeDefinitionAsync(GraphQLObjectTypeDefinition objectTypeDefinition, TContext context)
    {
        await VisitAsync(objectTypeDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(objectTypeDefinition.Description, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("type "), context).ConfigureAwait(false);
        await VisitAsync(objectTypeDefinition.Name, context).ConfigureAwait(false);
        await VisitAsync(objectTypeDefinition.Interfaces, context).ConfigureAwait(false);
        await VisitAsync(objectTypeDefinition.Directives, context).ConfigureAwait(false);
        await VisitAsync(objectTypeDefinition.Fields, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitObjectTypeExtensionAsync(GraphQLObjectTypeExtension objectTypeExtension, TContext context)
    {
        await VisitAsync(objectTypeExtension.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("extend type "), context).ConfigureAwait(false);
        await VisitAsync(objectTypeExtension.Name, context).ConfigureAwait(false);
        await VisitAsync(objectTypeExtension.Interfaces, context).ConfigureAwait(false);
        await VisitAsync(objectTypeExtension.Directives, context).ConfigureAwait(false);
        await VisitAsync(objectTypeExtension.Fields, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitInterfaceTypeDefinitionAsync(GraphQLInterfaceTypeDefinition interfaceTypeDefinition, TContext context)
    {
        await VisitAsync(interfaceTypeDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(interfaceTypeDefinition.Description, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("interface "), context).ConfigureAwait(false);
        await VisitAsync(interfaceTypeDefinition.Name, context).ConfigureAwait(false);
        await VisitAsync(interfaceTypeDefinition.Interfaces, context).ConfigureAwait(false);
        await VisitAsync(interfaceTypeDefinition.Directives, context).ConfigureAwait(false);
        await VisitAsync(interfaceTypeDefinition.Fields, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitInterfaceTypeExtensionAsync(GraphQLInterfaceTypeExtension interfaceTypeExtension, TContext context)
    {
        await VisitAsync(interfaceTypeExtension.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("extend interface "), context).ConfigureAwait(false);
        await VisitAsync(interfaceTypeExtension.Name, context).ConfigureAwait(false);
        await VisitAsync(interfaceTypeExtension.Interfaces, context).ConfigureAwait(false);
        await VisitAsync(interfaceTypeExtension.Directives, context).ConfigureAwait(false);
        await VisitAsync(interfaceTypeExtension.Fields, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitFieldDefinitionAsync(GraphQLFieldDefinition fieldDefinition, TContext context)
    {
        await VisitAsync(fieldDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(fieldDefinition.Description, context).ConfigureAwait(false);
        await VisitAsync(fieldDefinition.Name, context).ConfigureAwait(false);
        await VisitAsync(fieldDefinition.Arguments, context).ConfigureAwait(false);
        await context.WriteAsync(": ").ConfigureAwait(false);
        await VisitAsync(fieldDefinition.Type, context).ConfigureAwait(false);
        await VisitAsync(fieldDefinition.Directives, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitFieldsDefinitionAsync(GraphQLFieldsDefinition fieldsDefinition, TContext context)
    {
        await VisitAsync(fieldsDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap(HasPrintableComments(fieldsDefinition) || !TryPeekParent(context, out _) ? "{" : " {"), context).ConfigureAwait(false);
        await context.WriteLineAsync().ConfigureAwait(false);

        if (fieldsDefinition.Items?.Count > 0) // should always be true but may be negligently uninitialized
        {
            foreach (var fieldDefinition in fieldsDefinition.Items)
            {
                await VisitAsync(fieldDefinition, context).ConfigureAwait(false);
                await context.WriteLineAsync().ConfigureAwait(false);
            }
        }

        await context.WriteAsync("}").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitSchemaDefinitionAsync(GraphQLSchemaDefinition schemaDefinition, TContext context)
    {
        await VisitAsync(schemaDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(schemaDefinition.Description, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("schema"), context).ConfigureAwait(false);
        await VisitAsync(schemaDefinition.Directives, context).ConfigureAwait(false);
        await context.WriteAsync(/*HasPrintableComments(schemaDefinition) ? "{" : */" {").ConfigureAwait(false); // always false
        await context.WriteLineAsync().ConfigureAwait(false);

        if (schemaDefinition.OperationTypes.Count > 0)
        {
            for (int i = 0; i < schemaDefinition.OperationTypes.Count; ++i)
            {
                await VisitAsync(schemaDefinition.OperationTypes[i], context).ConfigureAwait(false);
                await context.WriteLineAsync().ConfigureAwait(false);
            }
        }

        await context.WriteAsync("}").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitRootOperationTypeDefinitionAsync(GraphQLRootOperationTypeDefinition rootOperationTypeDefinition, TContext context)
    {
        await VisitAsync(rootOperationTypeDefinition.Comments, context).ConfigureAwait(false);
        await context.WriteAsync(GetOperationType(rootOperationTypeDefinition.Operation)).ConfigureAwait(false);
        await context.WriteAsync(": ").ConfigureAwait(false);
        await VisitAsync(rootOperationTypeDefinition.Type, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitUnionTypeDefinitionAsync(GraphQLUnionTypeDefinition unionTypeDefinition, TContext context)
    {
        await VisitAsync(unionTypeDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(unionTypeDefinition.Description, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("union "), context).ConfigureAwait(false);
        await VisitAsync(unionTypeDefinition.Name, context).ConfigureAwait(false);
        await VisitAsync(unionTypeDefinition.Directives, context).ConfigureAwait(false);
        await VisitAsync(unionTypeDefinition.Types, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitUnionTypeExtensionAsync(GraphQLUnionTypeExtension unionTypeExtension, TContext context)
    {
        await VisitAsync(unionTypeExtension.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("extend union "), context).ConfigureAwait(false);
        await VisitAsync(unionTypeExtension.Name, context).ConfigureAwait(false);
        await VisitAsync(unionTypeExtension.Directives, context).ConfigureAwait(false);
        await VisitAsync(unionTypeExtension.Types, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitUnionMemberTypesAsync(GraphQLUnionMemberTypes unionMemberTypes, TContext context)
    {
        await VisitAsync(unionMemberTypes.Comments, context).ConfigureAwait(false);

        if (unionMemberTypes.Comments == null || !Options.PrintComments)
            await context.WriteAsync(" ").ConfigureAwait(false);

        if (Options.EachUnionMemberOnNewLine)
        {
            await VisitAsync(LiteralNode.Wrap("="), context).ConfigureAwait(false);
            await context.WriteLineAsync().ConfigureAwait(false);

            for (int i = 0; i < unionMemberTypes.Items.Count; ++i)
            {
                await VisitAsync(LiteralNode.Wrap("| "), context).ConfigureAwait(false);
                await VisitAsync(unionMemberTypes.Items[i], context).ConfigureAwait(false);
                if (i < unionMemberTypes.Items.Count - 1)
                    await context.WriteLineAsync().ConfigureAwait(false);
            }
        }
        else
        {
            await VisitAsync(LiteralNode.Wrap("= "), context).ConfigureAwait(false);

            for (int i = 0; i < unionMemberTypes.Items.Count; ++i)
            {
                await VisitAsync(unionMemberTypes.Items[i], context).ConfigureAwait(false);
                if (i < unionMemberTypes.Items.Count - 1)
                    await context.WriteAsync(" | ").ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitDirectiveAsync(GraphQLDirective directive, TContext context)
    {
        await VisitAsync(directive.Comments, context).ConfigureAwait(false);
        await context.WriteAsync(TryPeekParent(context, out _) ? " @" : "@").ConfigureAwait(false);
        await VisitAsync(directive.Name, context).ConfigureAwait(false);
        await VisitAsync(directive.Arguments, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitDirectivesAsync(GraphQLDirectives directives, TContext context)
    {
        await VisitAsync(directives.Comments, context).ConfigureAwait(false); // Comment always null - see ParserContext.ParseDirectives

        foreach (var directive in directives.Items)
            await VisitAsync(directive, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitArgumentAsync(GraphQLArgument argument, TContext context)
    {
        await VisitAsync(argument.Comments, context).ConfigureAwait(false);
        await VisitAsync(argument.Name, context).ConfigureAwait(false);
        await context.WriteAsync(": ").ConfigureAwait(false);
        await VisitAsync(argument.Value, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitArgumentsDefinitionAsync(GraphQLArgumentsDefinition argumentsDefinition, TContext context)
    {
        await VisitAsync(argumentsDefinition.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("("), context).ConfigureAwait(false);
        for (int i = 0; i < argumentsDefinition.Items.Count; ++i)
        {
            await VisitAsync(argumentsDefinition.Items[i], context).ConfigureAwait(false);
            if (i < argumentsDefinition.Items.Count - 1)
                await context.WriteAsync(",").ConfigureAwait(false);
        }
        await context.WriteAsync(")").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitArgumentsAsync(GraphQLArguments arguments, TContext context)
    {
        await VisitAsync(arguments.Comments, context).ConfigureAwait(false);
        await VisitAsync(LiteralNode.Wrap("("), context).ConfigureAwait(false);
        for (int i = 0; i < arguments.Items.Count; ++i)
        {
            await VisitAsync(arguments.Items[i], context).ConfigureAwait(false);
            if (i < arguments.Items.Count - 1)
                await context.WriteAsync(", ").ConfigureAwait(false);
        }
        await context.WriteAsync(")").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitNonNullTypeAsync(GraphQLNonNullType nonNullType, TContext context)
    {
        await VisitAsync(nonNullType.Comments, context).ConfigureAwait(false);
        await VisitAsync(nonNullType.Type, context).ConfigureAwait(false);
        await context.WriteAsync("!").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitListTypeAsync(GraphQLListType listType, TContext context)
    {
        await VisitAsync(listType.Comments, context).ConfigureAwait(false);
        await context.WriteAsync("[").ConfigureAwait(false);
        await VisitAsync(listType.Type, context).ConfigureAwait(false);
        await context.WriteAsync("]").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitListValueAsync(GraphQLListValue listValue, TContext context)
    {
        await VisitAsync(listValue.Comments, context).ConfigureAwait(false);
        await context.WriteAsync("[").ConfigureAwait(false);
        if (listValue.Values?.Count > 0)
        {
            for (int i = 0; i < listValue.Values.Count; ++i)
            {
                await VisitAsync(listValue.Values[i], context).ConfigureAwait(false);
                if (i < listValue.Values.Count - 1)
                    await context.WriteAsync(", ").ConfigureAwait(false);
            }
        }
        await context.WriteAsync("]").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitNullValueAsync(GraphQLNullValue nullValue, TContext context)
    {
        await VisitAsync(nullValue.Comments, context).ConfigureAwait(false);
        await context.WriteAsync("null").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitStringValueAsync(GraphQLStringValue stringValue, TContext context)
    {
        await VisitAsync(stringValue.Comments, context).ConfigureAwait(false);
        await WriteEncodedStringAsync(context, stringValue.Value);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitIntValueAsync(GraphQLIntValue intValue, TContext context)
    {
        await VisitAsync(intValue.Comments, context).ConfigureAwait(false);
        await context.WriteAsync(intValue.Value).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitFloatValueAsync(GraphQLFloatValue floatValue, TContext context)
    {
        await VisitAsync(floatValue.Comments, context).ConfigureAwait(false);
        await context.WriteAsync(floatValue.Value).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitEnumValueAsync(GraphQLEnumValue enumValue, TContext context)
    {
        await VisitAsync(enumValue.Comments, context).ConfigureAwait(false);
        await VisitAsync(enumValue.Name, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitObjectValueAsync(GraphQLObjectValue objectValue, TContext context)
    {
        await VisitAsync(objectValue.Comments, context).ConfigureAwait(false);
        await context.WriteAsync("{").ConfigureAwait(false);
        if (objectValue.Fields?.Count > 0)
        {
            for (int i = 0; i < objectValue.Fields.Count; ++i)
            {
                await VisitAsync(objectValue.Fields[i], context).ConfigureAwait(false);
                if (i < objectValue.Fields.Count - 1)
                    await context.WriteAsync(", ").ConfigureAwait(false);
            }
        }
        await context.WriteAsync("}").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitObjectFieldAsync(GraphQLObjectField objectField, TContext context)
    {
        await VisitAsync(objectField.Comments, context).ConfigureAwait(false);
        await VisitAsync(objectField.Name, context).ConfigureAwait(false);
        await context.WriteAsync(": ").ConfigureAwait(false);
        await VisitAsync(objectField.Value, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async ValueTask VisitNamedTypeAsync(GraphQLNamedType namedType, TContext context)
    {
        await VisitAsync(namedType.Comments, context).ConfigureAwait(false);
        await VisitAsync(namedType.Name, context).ConfigureAwait(false);
    }

    /// <summary>
    /// Places vertical indentation between top level definitions.
    /// </summary>
    protected virtual async ValueTask MakeVerticalIndentationBetweenTopLevelDefinitions(ASTNode node, TContext context)
    {
        static bool IsTopLevelNode(ASTNode? node) =>
           node is GraphQLSchemaDefinition ||
           node is GraphQLExecutableDefinition ||
           node is GraphQLTypeExtension ||
           node is GraphQLSchemaExtension ||
           node is GraphQLTypeDefinition { IsChildDefinition: false };

        if (IsTopLevelNode(context.LastVisitedNode) && IsTopLevelNode(node))
            await context.WriteDoubleLineAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async ValueTask VisitAsync(ASTNode? node, TContext context)
    {
        if (node == null)
            return;

        await MakeVerticalIndentationBetweenTopLevelDefinitions(node, context).ConfigureAwait(false);

        int prevLevel = context.IndentLevel;

        if (context.Parents.Count > 0)
        {
            if (
                node is GraphQLFieldDefinition ||
                node is GraphQLEnumValueDefinition ||
                node is GraphQLRootOperationTypeDefinition ||
                node is GraphQLDirectiveLocations && Options.EachDirectiveLocationOnNewLine ||
                node is GraphQLUnionMemberTypes && Options.EachUnionMemberOnNewLine ||
                node is GraphQLArgumentsDefinition && HasPrintableComments(node)
              )
                context.IndentLevel = 1; // fixed indentation of 1
            else if (
                node is GraphQLField ||
                node is GraphQLFragmentSpread ||
                node is GraphQLInlineFragment ||
                node is GraphQLArgument ||
                node is GraphQLInputValueDefinition
                )
                ++context.IndentLevel; // nested indentation
        }

        context.Parents.Push(node);

        // ensure NewLine before printing comment if previous node did not print NewLine
        if (node is GraphQLComment && Options.PrintComments && !context.IndentPrinted)
            await context.WriteLineAsync().ConfigureAwait(false);

        // ensure NewLine after already printed comment before printing new node
        if (context.LastVisitedNode is GraphQLComment && Options.PrintComments && !context.IndentPrinted)
            await context.WriteLineAsync().ConfigureAwait(false);

        // ensure NewLine after already printed description before printing new node
        if (context.LastVisitedNode is GraphQLDescription && !context.IndentPrinted)
            await context.WriteLineAsync().ConfigureAwait(false);

        // ensure NewLine before printing description if previous node did not print NewLine
        if (node is GraphQLDescription && !context.IndentPrinted)
            await context.WriteLineAsync().ConfigureAwait(false);

        if ((context.LastVisitedNode is GraphQLFragmentSpread || context.LastVisitedNode is GraphQLSelectionSet) && !context.IndentPrinted)
            await context.WriteLineAsync().ConfigureAwait(false);

        // ensure NewLine before printing argument if previous node did not print NewLine
        // https://github.com/graphql-dotnet/parser/issues/330
        _ = TryPeekParent(context, out var parent);
        if (!context.IndentPrinted && node is GraphQLInputValueDefinition && parent is GraphQLArgumentsDefinition arguments)
        {
            switch (Options.ArgumentsPrintMode)
            {
                case SDLPrinterArgumentsMode.ForceNewLine:
                    await context.WriteLineAsync().ConfigureAwait(false);
                    break;

                case SDLPrinterArgumentsMode.PreferNewLine:
                    foreach (var arg in arguments.Items)
                    {
                        if (HasPrintableComments(arg) || HasPrintableDescription(arg))
                        {
                            await context.WriteLineAsync().ConfigureAwait(false);
                            break;
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        // ensure proper indentation on the current line before printing new node
        if (context.NewLinePrinted)
            await WriteIndentAsync(context).ConfigureAwait(false);
        // otherwise ensure single whitespace indentation for all arguments in list except the first one
        else if (parent is GraphQLArgumentsDefinition argsDef && node is GraphQLInputValueDefinition input && argsDef.Items.IndexOf(input) != 0)
            await context.WriteAsync(" ").ConfigureAwait(false);

        if (node is LiteralNode literalNode) // base.VisitAsync will throw on unknown node
            await context.WriteAsync(literalNode.Literal).ConfigureAwait(false);
        else
            await base.VisitAsync(node, context).ConfigureAwait(false);

        context.LastVisitedNode = node;
        context.Parents.Pop();
        context.IndentLevel = prevLevel;
    }

    private static string GetOperationType(OperationType type) => type switch
    {
        OperationType.Query => "query",
        OperationType.Mutation => "mutation",
        OperationType.Subscription => "subscription",

        _ => throw new NotSupportedException($"Unknown operation {type}"),
    };

    private static string GetDirectiveLocation(DirectiveLocation location) => location switch
    {
        // http://spec.graphql.org/October2021/#ExecutableDirectiveLocation
        DirectiveLocation.Query => "QUERY",
        DirectiveLocation.Mutation => "MUTATION",
        DirectiveLocation.Subscription => "SUBSCRIPTION",
        DirectiveLocation.Field => "FIELD",
        DirectiveLocation.FragmentDefinition => "FRAGMENT_DEFINITION",
        DirectiveLocation.FragmentSpread => "FRAGMENT_SPREAD",
        DirectiveLocation.InlineFragment => "INLINE_FRAGMENT",
        DirectiveLocation.VariableDefinition => "VARIABLE_DEFINITION",

        // http://spec.graphql.org/October2021/#TypeSystemDirectiveLocation
        DirectiveLocation.Schema => "SCHEMA",
        DirectiveLocation.Scalar => "SCALAR",
        DirectiveLocation.Object => "OBJECT",
        DirectiveLocation.FieldDefinition => "FIELD_DEFINITION",
        DirectiveLocation.ArgumentDefinition => "ARGUMENT_DEFINITION",
        DirectiveLocation.Interface => "INTERFACE",
        DirectiveLocation.Union => "UNION",
        DirectiveLocation.Enum => "ENUM",
        DirectiveLocation.EnumValue => "ENUM_VALUE",
        DirectiveLocation.InputObject => "INPUT_OBJECT",
        DirectiveLocation.InputFieldDefinition => "INPUT_FIELD_DEFINITION",

        _ => throw new NotSupportedException($"Unknown directive location {location}"),
    };

    private async ValueTask WriteIndentAsync(TContext context)
    {
        for (int i = 0; i < context.IndentLevel; ++i)
        {
            for (int j = 0; j < Options.IndentSize; ++j)
                await context.WriteAsync(" ").ConfigureAwait(false);
        }

        context.IndentPrinted = true;
    }

    private bool HasPrintableComments(ASTNode node) => node.Comments?.Count > 0 && Options.PrintComments;

    private bool HasPrintableDescription(IHasDescriptionNode node) => node.Description != null && Options.PrintDescriptions;

    // Returns parent if called inside VisitXXX i.e. after context.Parents.Push(node);
    // Returns grand-parent if called inside VisitAsync i.e. before context.Parents.Push(node);
    private static bool TryPeekParent(TContext context, [NotNullWhen(true)] out ASTNode? node)
    {
        node = null;

        using var e = context.Parents.GetEnumerator();
        for (int i = 0; i < 2; ++i)
            if (!e.MoveNext())
                return false;

        node = e.Current;
        return true;
    }

    private static readonly string[] _hex32 = new[]
    {
        "\\u0000", "\\u0001", "\\u0002", "\\u0003", "\\u0004", "\\u0005", "\\u0006", "\\u0007", "\\u0008", "\\u0009", "\\u000A", "\\u000B", "\\u000C", "\\u000D", "\\u000E", "\\u000F",
        "\\u0010", "\\u0011", "\\u0012", "\\u0013", "\\u0014", "\\u0015", "\\u0016", "\\u0017", "\\u0018", "\\u0019", "\\u001A", "\\u001B", "\\u001C", "\\u001D", "\\u001E", "\\u001F"
    };

    // http://spec.graphql.org/October2021/#StringCharacter
    private static async ValueTask WriteEncodedStringAsync(TContext context, ROM value)
    {
        await context.WriteAsync("\"").ConfigureAwait(false);

        int startIndexOfNotEncodedString = 0;
        for (int i = 0; i < value.Span.Length; ++i)
        {
            char code = value.Span[i];
            if (code < ' ')
            {
                if (startIndexOfNotEncodedString != i)
                    await context.WriteAsync(value.Slice(startIndexOfNotEncodedString, i - startIndexOfNotEncodedString)).ConfigureAwait(false);
                startIndexOfNotEncodedString = i + 1;

                if (code == '\b')
                    await context.WriteAsync("\\b").ConfigureAwait(false);
                else if (code == '\f')
                    await context.WriteAsync("\\f").ConfigureAwait(false);
                else if (code == '\n')
                    await context.WriteAsync("\\n").ConfigureAwait(false);
                else if (code == '\r')
                    await context.WriteAsync("\\r").ConfigureAwait(false);
                else if (code == '\t')
                    await context.WriteAsync("\\t").ConfigureAwait(false);
                else
                    await context.WriteAsync(_hex32[code]).ConfigureAwait(false);
            }
            else if (code == '\\')
            {
                if (startIndexOfNotEncodedString != i)
                    await context.WriteAsync(value.Slice(startIndexOfNotEncodedString, i - startIndexOfNotEncodedString)).ConfigureAwait(false);
                startIndexOfNotEncodedString = i + 1;

                await context.WriteAsync("\\\\").ConfigureAwait(false);
            }
            else if (code == '"')
            {
                if (startIndexOfNotEncodedString != i)
                    await context.WriteAsync(value.Slice(startIndexOfNotEncodedString, i - startIndexOfNotEncodedString)).ConfigureAwait(false);
                startIndexOfNotEncodedString = i + 1;

                await context.WriteAsync("\\\"").ConfigureAwait(false);
            }
        }

        if (startIndexOfNotEncodedString != value.Span.Length)
            await context.WriteAsync(value.Slice(startIndexOfNotEncodedString, value.Span.Length - startIndexOfNotEncodedString)).ConfigureAwait(false);
        await context.WriteAsync("\"").ConfigureAwait(false);
    }
}

/// <inheritdoc/>
public class SDLPrinter : SDLPrinter<SDLPrinter.DefaultPrintContext>
{
    /// <summary>
    /// Default implementation for <see cref="IPrintContext"/>.
    /// </summary>
    public class DefaultPrintContext : IPrintContext
    {
        /// <summary>
        /// Creates an instance with the specified <see cref="TextWriter"/> and indent size.
        /// </summary>
        public DefaultPrintContext(TextWriter writer)
        {
            Writer = writer;
        }

        /// <inheritdoc/>
        public TextWriter Writer { get; }

        /// <inheritdoc/>
        public Stack<ASTNode> Parents { get; set; } = new();

        /// <inheritdoc/>
        public CancellationToken CancellationToken { get; set; }

        /// <inheritdoc/>
        public int IndentLevel { get; set; }

        /// <inheritdoc/>
        [Obsolete("Use LastVisitedNode instead")]
        public bool LastDefinitionPrinted { get; set; }

        /// <inheritdoc/>
        public bool NewLinePrinted { get; set; } = true;

        /// <inheritdoc/>
        public bool IndentPrinted { get; set; }

        /// <inheritdoc/>
        public ASTNode? LastVisitedNode { get; set; }
    }

    /// <inheritdoc/>
    public SDLPrinter()
        : base()
    {
    }

    /// <inheritdoc/>
    public SDLPrinter(SDLPrinterOptions options)
        : base(options)
    {
    }

    /// <inheritdoc cref="SDLPrinter{TContext}"/>
    public virtual ValueTask PrintAsync(ASTNode node, TextWriter writer, CancellationToken cancellationToken = default)
    {
        var context = new DefaultPrintContext(writer)
        {
            CancellationToken = cancellationToken,
        };
        return VisitAsync(node, context);
    }
}

/// <summary>
/// Options for <see cref="SDLPrinter{TContext}"/>.
/// </summary>
public class SDLPrinterOptions
{
    /// <summary>
    /// Print comments into the output.
    /// By default <see langword="false"/>.
    /// </summary>
    public bool PrintComments { get; set; }

    /// <summary>
    /// Print descriptions into the output.
    /// By default <see langword="true"/>.
    /// </summary>
    public bool PrintDescriptions { get; set; } = true;

    /// <summary>
    /// Whether to print each directive location on its own line.
    /// By default <see langword="false"/>.
    /// </summary>
    public bool EachDirectiveLocationOnNewLine { get; set; }

    /// <summary>
    /// Whether to print each union member on its own line.
    /// By default <see langword="false"/>.
    /// </summary>
    public bool EachUnionMemberOnNewLine { get; set; }

    /// <summary>
    /// How to print each argument definition.
    /// By default <see cref="SDLPrinterArgumentsMode.PreferNewLine"/>.
    /// </summary>
    public SDLPrinterArgumentsMode ArgumentsPrintMode { get; set; } = SDLPrinterArgumentsMode.PreferNewLine;

    /// <summary>
    /// The size of the horizontal indentation in spaces.
    /// By default 2.
    /// </summary>
    public int IndentSize { get; set; } = 2;
}

/// <summary>
/// Pseudo AST node to allow calls to <see cref="SDLPrinter{TContext}.VisitAsync(ASTNode?, TContext)"/>
/// for indentation purposes. Any literal printed first after optional comment or description nodes in
/// any VisitXXX method should be wrapped into <see cref="LiteralNode"/> for proper indentation.
/// </summary>
internal class LiteralNode : ASTNode
{
    [ThreadStatic]
    internal static LiteralNode? _shared;

    private LiteralNode()
    {
    }

    public static LiteralNode Wrap(ROM literal)
    {
        _shared ??= new LiteralNode();
        _shared.Literal = literal;
        return _shared;
    }

    public ROM Literal { get; set; }

    public override ASTNodeKind Kind => (ASTNodeKind)(-1);
}
