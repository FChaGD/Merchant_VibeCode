namespace Game.Core
{
    public interface IDamageFormula
    {
        float ComputeDamage(float attack, float defense);
    }
}
