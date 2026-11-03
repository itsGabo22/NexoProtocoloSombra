namespace Nexo.AI.Infected
{
    /// <summary>
    /// Contrato de un estado de la FSM táctica del infectado.
    /// Tick() se invoca desde la corrutina de SmartInfectedBrain cada 0.15s (no por frame).
    /// </summary>
    public interface IInfectedState
    {
        void Enter();
        void Tick();
        void Exit();
    }
}
