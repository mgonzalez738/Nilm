namespace EnergyMetersService.Domain.Entities
{
    public abstract class Entity
    {
        public string Id { get; protected set; } = string.Empty;
    }
}