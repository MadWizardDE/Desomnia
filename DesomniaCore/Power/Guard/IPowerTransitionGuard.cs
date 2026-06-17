namespace MadWizard.Desomnia.Power.Guard
{
    public interface IPowerTransitionGuard
    {
        Task BeforeTransition(PowerTransition transition);
    }

    public enum PowerTransition
    {
        Suspend,
        Hibernate,
        Shutdown,
        Reboot
    }
}
