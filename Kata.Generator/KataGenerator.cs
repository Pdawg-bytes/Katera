using Kata.Generator.Lowering;
using Kata.Generator.Parsing;
using Kata.Generator.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace Kata.Generator;

[Generator]
public sealed class KataGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        var layouts = ctx.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Kata.BitLayoutAttribute",
                static (node, _) => node is StructDeclarationSyntax,
                static (ctx, _)  => AttributeParser.ParseLayout(ctx));

        ctx.RegisterSourceOutput(layouts, (spc, result) =>
        {
            if (result.Diagnostic is not null)
            {
                spc.ReportDiagnostic(result.Diagnostic);
                return;
            }

            var layout = result.Model!;
            AttributeParser.ParseFields(layout, spc);
            AttributeParser.ResolveOffsets(layout);
            LayoutValidator.Validate(layout, spc);

            var plan = LayoutLowerer.Lower(layout);

            var debug = new StringBuilder();
            debug.AppendLine("/*");
            debug.AppendLine($"OwnedKind: {plan.OwnedKind}");
            debug.AppendLine($"SizeBytes: {plan.SizeBytes}");
            debug.AppendLine($"Endianness: {plan.Endianness}");
            debug.AppendLine($"Numeric: {plan.Numeric}");
            foreach (var f in plan.Fields)
            {
                debug.AppendLine($"- {f.Name}: off={f.Offset}, len={f.Length}, type={f.Type}");
            }
            debug.AppendLine("*/");

            spc.AddSource($"{plan.Symbol.Name}.plan.g.cs", debug.ToString());
        });
    }
}