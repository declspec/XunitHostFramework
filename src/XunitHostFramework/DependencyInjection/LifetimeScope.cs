using System;

namespace XunitHostFramework.DependencyInjection
{
    internal class LifetimeScope
    {
        public string Name { get; private set; }
        public LifetimeScope Parent { get; private set; }
        public IServiceProvider ServiceProvider { get; private set; }

        public void Initialise(string name, LifetimeScope parent, IServiceProvider serviceProvider)
        {
            Name = name;
            Parent = parent;
            ServiceProvider = serviceProvider;
        }
    }
}
