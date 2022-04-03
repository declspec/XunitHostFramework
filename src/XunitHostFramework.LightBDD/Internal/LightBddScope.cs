using LightBDD.XUnit2;
using System;
using System.Reflection;

namespace XunitHostFramework.LightBDD.Internal
{
    /// <summary>
    /// Internal wrapper around LightBddScopeAttribute to un-protect
    /// two core 'internal' functions required to ensure LightBDD scenarios
    /// can function properly.
    /// </summary>
    /// <remarks>This is a hack, but the lightest possible touch into the LightBDD internals to get everything else to work</remarks>
    public class LightBddScope : IDisposable
    {
        private LightBddScopeAttribute _attribute;

        public LightBddScope()
        {
            _attribute = new LightBddScopeAttribute();

            var setup = _attribute.GetType().GetMethod("SetUp", BindingFlags.NonPublic | BindingFlags.Instance);
            CallMethod(_attribute, setup);
        }

        public void Dispose()
        {
            if (_attribute != null)
            {
                var attribute = _attribute;
                _attribute = null;

                var tearDown = attribute.GetType().GetMethod("TearDown", BindingFlags.NonPublic | BindingFlags.Instance);
                CallMethod(attribute, tearDown);
            }
        }

        private static void CallMethod(object instance, MethodInfo method)
        {
            // NOTE: Since 3.4.1, LightBdd added an IMessageSink parameter to the SetUp method
            //  this method exists to try and make the reflection a bit less brittle and support both permutations, using default values for all parameters.
            var args = Array.ConvertAll(method.GetParameters(), p => p.HasDefaultValue ? p.DefaultValue : GetDefaultValue(p.ParameterType));
            method.Invoke(instance, args.Length > 0 ? args : null);
        }

        private static object GetDefaultValue(Type typeInfo)
        {
            return typeInfo.IsValueType ? Activator.CreateInstance(typeInfo) : null;
        }
    }
}
