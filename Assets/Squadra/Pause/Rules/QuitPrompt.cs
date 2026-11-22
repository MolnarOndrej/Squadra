namespace Squadra.Pause
{
    /// <summary>
    /// The text of a quit confirmation: where the player is going and what leaving costs. Only an
    /// unfinished mission warns about lost mission progress; a result that is already on screen is
    /// never described as being thrown away. Leaving a sandbox run always throws the run away, and its
    /// Quit to Menu leads to the main menu rather than the mission map.
    /// </summary>
    public readonly struct QuitPrompt
    {
        public const string MenuTitle = "QUIT TO MENU?";
        public const string DesktopTitle = "QUIT TO DESKTOP?";
        public const string MenuDestination = "You will return to the mission map.";
        public const string DesktopDestination = "The game will close.";
        public const string ProgressLost = "Current mission progress will be lost.";
        public const string VictoryKept = "Your victory is already recorded.";
        public const string SandboxMenuDestination = "You will return to the main menu.";
        public const string SandboxDiscarded = "This sandbox run will be discarded.";

        /// <summary>Campaign progress is never saved, so closing the game always ends the campaign.</summary>
        public const string SessionOnly = "Campaign progress is kept only while the game is running.";

        public readonly string Title;
        public readonly string Message;

        QuitPrompt(string title, string message)
        {
            Title = title;
            Message = message;
        }

        public static QuitPrompt For(QuitDestination destination, MissionStanding standing)
        {
            bool unfinished = standing == MissionStanding.Unfinished;
            if (destination == QuitDestination.Desktop)
            {
                string closing = unfinished ? DesktopDestination + " " + ProgressLost : DesktopDestination;
                return new QuitPrompt(DesktopTitle, closing + " " + SessionOnly);
            }

            string message = MenuDestination;
            if (unfinished) message += " " + ProgressLost;
            else if (standing == MissionStanding.Won) message += " " + VictoryKept;
            return new QuitPrompt(MenuTitle, message);
        }

        /// <summary>The confirmation for leaving a sandbox run, which is never kept.</summary>
        public static QuitPrompt ForSandbox(QuitDestination destination)
        {
            if (destination == QuitDestination.Desktop)
                return new QuitPrompt(DesktopTitle, DesktopDestination + " " + SandboxDiscarded + " " + SessionOnly);

            return new QuitPrompt(MenuTitle, SandboxMenuDestination + " " + SandboxDiscarded);
        }
    }
}
