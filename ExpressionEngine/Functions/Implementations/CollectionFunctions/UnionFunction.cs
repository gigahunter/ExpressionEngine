using ExpressionEngine.Functions.Base;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExpressionEngine.Functions.Implementations.CollectionFunctions
{
    public class UnionFunction : IFunction
    {
        public ValueTask<ValueContainer> ExecuteFunction(params ValueContainer[] parameters)
        {
            return parameters[0].Type() switch
            {
                ValueType.Array => ValueTask.FromResult(UnionList(parameters)),
                ValueType.Object => ValueTask.FromResult(UnionDict(parameters)),
                _ => throw new ExpressionEngineException(
                    $"Can only union Array and Object, not {parameters[0].Type()}.")
            };
        }

        private ValueContainer UnionDict(IReadOnlyList<ValueContainer> parameters)
        {
            var first = parameters[0].GetValue<Dictionary<string, ValueContainer>>();

            return new ValueContainer(parameters.Skip(1).Aggregate(first, ToDictionary));
        }

        private Dictionary<string, ValueContainer> ToDictionary(Dictionary<string, ValueContainer> first,
            ValueContainer valueContainer)
        {
            var second = valueContainer.GetValue<Dictionary<string, ValueContainer>>();

            foreach (var kv in second.ToList())
            {
                first[kv.Key] = kv.Value;
            }

            return first;
        }

        private ValueContainer UnionList(IReadOnlyList<ValueContainer> parameters)
        {
            var first = parameters[0].GetValue<IEnumerable<ValueContainer>>();

            var intersection = parameters.Skip(1)
                .Select(valueContainer => valueContainer.GetValue<IEnumerable<ValueContainer>>())
                .Aggregate(first, (current, collection) => current.Union(collection));

            return new ValueContainer(intersection);
        }
    }
}