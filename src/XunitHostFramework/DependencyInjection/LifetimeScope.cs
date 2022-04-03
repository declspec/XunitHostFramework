using System;

namespace XunitHostFramework.DependencyInjection
{
    public class LifetimeScope
    {
        public string Name { get; private set; }
        public LifetimeScope Parent { get; private set; }
        public IServiceProvider ServiceProvider { get; private set; }

        internal void Initialise(string name, LifetimeScope parent, IServiceProvider serviceProvider)
        {
            Name = name;
            Parent = parent;
            ServiceProvider = serviceProvider;
        }
    }
}
