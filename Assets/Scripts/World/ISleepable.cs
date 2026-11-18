namespace FallingWizard.World
{
    public interface ISleepable
    {
        bool CanSleepNow { get; }

        bool TrySleep(float seconds);
    }
}
