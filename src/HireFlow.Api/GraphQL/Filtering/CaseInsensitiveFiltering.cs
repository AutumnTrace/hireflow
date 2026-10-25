using System.Linq.Expressions;
using System.Reflection;
using HotChocolate.CostAnalysis.Types;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Filters.Expressions;
using HotChocolate.Language;
using HotChocolate.Types;

namespace HireFlow.Api.GraphQL.Filtering;

/// <summary>
/// Adds <c>icontains</c> and <c>istartsWith</c> to every string filter. The built-in
/// <c>contains</c>/<c>startsWith</c> translate to case-sensitive SQL on both SQLite and PostgreSQL,
/// so "ana" never matched "Ana Popescu". Both sides are lower-cased, which EF Core translates to
/// <c>lower(column)</c> on either provider. SQLite's <c>lower()</c> only folds ASCII letters.
/// </summary>
public sealed class HireFlowFilterConvention : FilterConvention
{
    public const int ContainsIgnoreCase = 1025;
    public const int StartsWithIgnoreCase = 1026;

    protected override void Configure(IFilterConventionDescriptor descriptor)
    {
        descriptor.AddDefaults();

        descriptor.Operation(ContainsIgnoreCase).Name("icontains")
            .Description("Case-insensitive substring match.");
        descriptor.Operation(StartsWithIgnoreCase).Name("istartsWith")
            .Description("Case-insensitive prefix match.");

        descriptor.Configure<StringOperationFilterInputType>(x =>
        {
            // Priced like the built-in string operations: a lower() scan is at least as costly.
            x.Operation(ContainsIgnoreCase).Type<StringType>().Directive(new CostDirective(20));
            x.Operation(StartsWithIgnoreCase).Type<StringType>().Directive(new CostDirective(20));
        });

        descriptor.AddProviderExtension(new QueryableFilterProviderExtension(p => p
            .AddFieldHandler(c => new StringContainsIgnoreCaseHandler(c.InputParser))
            .AddFieldHandler(c => new StringStartsWithIgnoreCaseHandler(c.InputParser))));
    }
}

public abstract class StringIgnoreCaseHandler(InputParser inputParser, string method)
    : QueryableStringOperationHandler(inputParser)
{
    private static readonly MethodInfo ToLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private readonly MethodInfo _compare = typeof(string).GetMethod(method, [typeof(string)])!;

    public override Expression HandleOperation(
        QueryableFilterContext context,
        IFilterOperationField field,
        IValueNode value,
        object? parsedValue)
    {
        if (parsedValue is not string term)
        {
            // A null term matches everything, like an omitted filter.
            return Expression.Constant(true);
        }

        var property = context.GetInstance();
        var lowered = Expression.Call(property, ToLower);
        var match = Expression.Call(lowered, _compare, Expression.Constant(term.ToLowerInvariant()));

        // Null columns never match instead of throwing in in-memory providers.
        return Expression.AndAlso(Expression.NotEqual(property, Expression.Constant(null, typeof(string))), match);
    }
}

public sealed class StringContainsIgnoreCaseHandler(InputParser inputParser)
    : StringIgnoreCaseHandler(inputParser, nameof(string.Contains))
{
    protected override int Operation => HireFlowFilterConvention.ContainsIgnoreCase;
}

public sealed class StringStartsWithIgnoreCaseHandler(InputParser inputParser)
    : StringIgnoreCaseHandler(inputParser, nameof(string.StartsWith))
{
    protected override int Operation => HireFlowFilterConvention.StartsWithIgnoreCase;
}
