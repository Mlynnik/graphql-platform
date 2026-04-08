using System.Diagnostics;
using HotChocolate.Execution.Profiling;
using HotChocolate.Text.Json;
using HotChocolate.Types;
using HotChocolate.Utilities;
using static HotChocolate.Execution.ErrorHelper;

namespace HotChocolate.Execution.Processing;

internal static partial class ValueCompletion
{
    private static void CompleteLeafValue(
        ValueCompletionContext context,
        Selection selection,
        ILeafType type,
        ResultElement resultValue,
        object runtimeValue)
    {
        var operationContext = context.OperationContext;
        var resolverContext = context.ResolverContext;

        try
        {
            var runtimeType = type.RuntimeType;

            if (!runtimeType.IsInstanceOfType(runtimeValue)
                && operationContext.Converter.TryConvert(runtimeType, runtimeValue, out var c))
            {
                runtimeValue = c;
            }

            if (operationContext.RequestContext.Features.TryGet<ExecutionProfileCollector>(out var collector)
                && collector is not null)
            {
                var serializationStartTimestamp = Stopwatch.GetTimestamp();

                try
                {
                    type.CoerceOutputValue(runtimeValue, resultValue);
                }
                finally
                {
                    collector.AddSerializationByType(
                        resultValue.Path,
                        type.Name,
                        Stopwatch.GetElapsedTime(serializationStartTimestamp).Ticks * 100);
                }
            }
            else
            {
                type.CoerceOutputValue(runtimeValue, resultValue);
            }

            return;
        }
        catch (LeafCoercionException ex)
        {
            var errorPath = resultValue.Path;
            var error = InvalidLeafValue(ex, selection, errorPath);
            operationContext.ReportError(error, resolverContext);
        }
        catch (Exception ex)
        {
            var errorPath = resultValue.Path;
            var error = UnexpectedLeafValueSerializationError(ex, selection, errorPath);
            operationContext.ReportError(error, resolverContext);
        }

        resultValue.SetNullValue();
    }
}
