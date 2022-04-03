using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit.Sdk;

namespace XunitHostFramework
{
    public class DeferredConstructorArguments
    {
        public static DeferredConstructorArguments Empty = new DeferredConstructorArguments(Array.Empty<ParameterInfo>(), Array.Empty<object>(), _ => throw new NotImplementedException());

        private readonly Func<IReadOnlyList<Tuple<int, ParameterInfo>>, string> _formatConstructorArgsMissingMessage;
        private readonly IReadOnlyList<ParameterInfo> _parameters;
        private readonly IReadOnlyList<object> _resolvedArguments;

        public DeferredConstructorArguments(IReadOnlyList<ParameterInfo> parameters, IReadOnlyList<object> resolvedArguments, Func<IReadOnlyList<Tuple<int, ParameterInfo>>, string> formatConstructorArgsMissingMessage)
        {
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            _resolvedArguments = resolvedArguments ?? throw new ArgumentNullException(nameof(resolvedArguments));
            _formatConstructorArgsMissingMessage = formatConstructorArgsMissingMessage ?? throw new ArgumentNullException(nameof(formatConstructorArgsMissingMessage));
        }

        public object[] Resolve(IServiceProvider provider, ExceptionAggregator aggregator)
        {
            var arguments = new object[_parameters.Count];
            var unusedArguments = new List<Tuple<int, ParameterInfo>>();

            for (var i = 0; i < arguments.Length; ++i)
            {
                if (TryResolveArgument(i, provider, aggregator, out var argumentValue))
                    arguments[i] = argumentValue;
                else
                    unusedArguments.Add(Tuple.Create(i, _parameters[i]));                    
            }

            if (unusedArguments.Count > 0)
                aggregator.Add(new TestClassException(_formatConstructorArgsMissingMessage(unusedArguments)));

            return arguments;
        }

        private bool TryResolveArgument(int index, IServiceProvider provider, ExceptionAggregator aggregator, out object argumentValue)
        {
            if (_resolvedArguments[index] != null)
            {
                argumentValue = _resolvedArguments[index];
                return true;
            }

            argumentValue = null;
            var parameter = _parameters[index];

            try
            {
                argumentValue = provider.GetService(parameter.ParameterType);
            }
            catch (Exception ex)
            {
                aggregator.Add(ex);
                return true;
            }

            if (argumentValue != null)
                return true;

            if (parameter.HasDefaultValue)
                argumentValue = parameter.DefaultValue;
            else if (parameter.IsOptional)
                argumentValue = GetDefaultValue(parameter.ParameterType);
            else if (parameter.GetCustomAttribute<ParamArrayAttribute>() != null)
                argumentValue = Array.CreateInstance(parameter.ParameterType, new int[1]);
            else
                return false;

            return true;
        }

        private static object GetDefaultValue(Type typeInfo)
        {
            return typeInfo.GetTypeInfo().IsValueType ? Activator.CreateInstance(typeInfo) : null;
        }
    }
}
