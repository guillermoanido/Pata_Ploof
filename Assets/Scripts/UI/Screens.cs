namespace FallingWizard.UI
{
    public static class Screens
    {
        public static bool IsModalOpen { get; private set; }

        public static void Claim() => IsModalOpen = true;

        public static void Release() => IsModalOpen = false;
    }
}
