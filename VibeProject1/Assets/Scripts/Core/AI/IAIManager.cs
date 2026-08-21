namespace Game.Core
{
    public interface IAIManager
    {
        void Register(IAIControllable unit);
        void Unregister(IAIControllable unit);
    }
}
