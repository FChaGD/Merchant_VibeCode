namespace Game.Core
{
    public interface IManagedComponent
    {
        void RegisterSelf(IDependencyRegistrar registrar);
        void ResolveDependencies(IDependencyRegistrar registrar);
    }
}
