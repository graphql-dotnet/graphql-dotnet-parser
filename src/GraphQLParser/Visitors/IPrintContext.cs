using GraphQLParser.AST;

namespace GraphQLParser.Visitors;

/// <summary>
/// Context used by <see cref="SDLPrinter{TContext}"/> and <see cref="StructurePrinter{TContext}"/>.
/// </summary>
public interface IPrintContext : IASTVisitorContext
{
    /// <summary>
    /// A text writer to print document.
    /// </summary>
    TextWriter Writer { get; }

    /// <summary>
    /// Stack of AST nodes to track the current visitor position.
    /// </summary>
    Stack<ASTNode> Parents { get; }

    /// <summary>
    /// Tracks the current indent level.
    /// </summary>
    int IndentLevel { get; set; }

    /// <summary>
    /// Indicates whether last GraphQL AST definition node (executable definition,
    /// type system definition or type system extension) from printed document was
    /// actually printed. This property is required to properly print vertical
    /// indents between definitions.
    /// </summary>
    [Obsolete("Use LastVisitedNode instead")]
    bool LastDefinitionPrinted { get; set; }

    /// <summary>
    /// Indicates whether last printed character was NewLine. This property is
    /// required to properly print indentations.
    /// </summary>
    bool NewLinePrinted { get; set; }

    /// <summary>
    /// Indicates whether last printed characters were for horizontal indentation.
    /// It is assumed that the indentation is always printed after NewLine.
    /// </summary>
    bool IndentPrinted { get; set; }

    /// <summary>
    /// The last node visited by a printer.
    /// </summary>
    ASTNode? LastVisitedNode { get; set; }
}
