using NUnit.Framework;
using Squadra.Pause;

namespace Squadra.Tests
{
    /// <summary>
    /// Exercises the pause boundary the mission scene uses: Escape presses and menu actions in; the
    /// screen showing, whether the mission is paused and whether a quit goes ahead out. Also covers the
    /// quit confirmation text and how pausing and background muting combine with the volume settings.
    /// </summary>
    public class PauseTests
    {
        static EscapeStep Escape(PauseFlow flow, bool usedByControl = false, bool dialogOpen = false)
        {
            return flow.Escape(usedByControl, dialogOpen);
        }

        // ------------------------------------------------------------ opening and resuming

        [Test]
        public void A_mission_starts_unpaused()
        {
            var flow = new PauseFlow();

            Assert.AreEqual(PauseScreen.Closed, flow.Screen);
            Assert.IsFalse(flow.IsOpen);
            Assert.IsFalse(flow.Leaving);
        }

        [Test]
        public void The_root_menu_lists_Resume_Settings_Quit_to_Menu_and_Quit_to_Desktop_in_that_order()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    PauseAction.Resume, PauseAction.Settings, PauseAction.QuitToMenu, PauseAction.QuitToDesktop,
                },
                PauseFlow.Actions);
            Assert.AreEqual(PauseAction.Resume, PauseFlow.InitialFocus);
        }

        [Test]
        public void Escape_opens_the_pause_menu_on_its_root_screen()
        {
            var flow = new PauseFlow();

            Assert.AreEqual(EscapeStep.Open, Escape(flow));

            Assert.AreEqual(PauseScreen.Menu, flow.Screen);
            Assert.IsTrue(flow.IsOpen);
        }

        [Test]
        public void One_Escape_press_opens_the_menu_and_only_the_next_one_resumes()
        {
            var flow = new PauseFlow();

            Assert.AreEqual(EscapeStep.Open, Escape(flow));
            Assert.IsTrue(flow.IsOpen, "the opening press must not also close the menu");

            Assert.AreEqual(EscapeStep.Resume, Escape(flow));
            Assert.IsFalse(flow.IsOpen);
        }

        [Test]
        public void Resume_closes_the_menu_and_can_be_followed_by_another_pause()
        {
            var flow = new PauseFlow();
            flow.Open();

            Assert.IsTrue(flow.Resume());
            Assert.IsFalse(flow.IsOpen);
            Assert.IsFalse(flow.Resume(), "there is nothing left to resume");

            Assert.IsTrue(flow.Open());
            Assert.AreEqual(PauseScreen.Menu, flow.Screen);
        }

        [Test]
        public void Opening_an_open_menu_changes_nothing()
        {
            var flow = new PauseFlow();
            flow.Open();
            flow.OpenSettings();

            Assert.IsFalse(flow.Open());
            Assert.AreEqual(PauseScreen.Settings, flow.Screen);
        }

        // ------------------------------------------------------------ settings

        [Test]
        public void Settings_opens_from_the_root_menu_and_keeps_the_mission_paused()
        {
            var flow = new PauseFlow();
            flow.Open();

            Assert.IsTrue(flow.OpenSettings());

            Assert.AreEqual(PauseScreen.Settings, flow.Screen);
            Assert.IsTrue(flow.IsOpen);
        }

        [Test]
        public void Settings_cannot_be_opened_while_the_menu_is_closed()
        {
            var flow = new PauseFlow();

            Assert.IsFalse(flow.OpenSettings());
            Assert.AreEqual(PauseScreen.Closed, flow.Screen);
        }

        [Test]
        public void Back_from_Settings_returns_to_the_root_menu_without_resuming()
        {
            var flow = new PauseFlow();
            flow.Open();
            flow.OpenSettings();

            Assert.IsTrue(flow.LeaveSettings());

            Assert.AreEqual(PauseScreen.Menu, flow.Screen);
            Assert.IsTrue(flow.IsOpen);
        }

        [Test]
        public void Escape_in_Settings_returns_to_the_root_menu_and_a_later_Escape_resumes()
        {
            var flow = new PauseFlow();
            flow.Open();
            flow.OpenSettings();

            Assert.AreEqual(EscapeStep.LeaveSettings, Escape(flow));
            Assert.AreEqual(PauseScreen.Menu, flow.Screen);

            Assert.AreEqual(EscapeStep.Resume, Escape(flow));
            Assert.IsFalse(flow.IsOpen);
        }

        [Test]
        public void Resume_is_not_available_from_Settings()
        {
            var flow = new PauseFlow();
            flow.Open();
            flow.OpenSettings();

            Assert.IsFalse(flow.Resume());
            Assert.AreEqual(PauseScreen.Settings, flow.Screen);
        }

        // ------------------------------------------------------------ what Escape reaches first

        [Test]
        public void An_open_dropdown_takes_Escape_without_leaving_Settings()
        {
            var flow = new PauseFlow();
            flow.Open();
            flow.OpenSettings();

            Assert.AreEqual(EscapeStep.None, Escape(flow, usedByControl: true));
            Assert.AreEqual(PauseScreen.Settings, flow.Screen);

            Assert.AreEqual(EscapeStep.LeaveSettings, Escape(flow));
        }

        [Test]
        public void Escape_cancels_a_display_confirmation_and_stays_in_Settings()
        {
            var flow = new PauseFlow();
            flow.Open();
            flow.OpenSettings();

            Assert.AreEqual(EscapeStep.CancelDialog, Escape(flow, dialogOpen: true));
            Assert.AreEqual(PauseScreen.Settings, flow.Screen);

            Assert.AreEqual(EscapeStep.LeaveSettings, Escape(flow));
            Assert.AreEqual(PauseScreen.Menu, flow.Screen);
        }

        [Test]
        public void Escape_cancels_a_quit_dialog_and_leaves_the_mission_paused_on_the_root_menu()
        {
            var flow = new PauseFlow();
            flow.Open();

            Assert.AreEqual(EscapeStep.CancelDialog, Escape(flow, dialogOpen: true));

            Assert.AreEqual(PauseScreen.Menu, flow.Screen);
            Assert.IsTrue(flow.IsOpen);
            Assert.IsFalse(flow.Leaving);
        }

        // ------------------------------------------------------------ quitting

        [Test]
        public void A_confirmed_quit_goes_ahead_once([Values] QuitDestination destination)
        {
            var flow = new PauseFlow();
            flow.Open();

            Assert.IsTrue(flow.ConfirmQuit(destination));
            Assert.IsTrue(flow.Leaving);
            Assert.AreEqual(destination, flow.Destination);

            Assert.IsFalse(flow.ConfirmQuit(destination), "a repeated Confirm must not navigate again");
            Assert.IsFalse(flow.ConfirmQuit(QuitDestination.Menu));
            Assert.IsFalse(flow.ConfirmQuit(QuitDestination.Desktop));
            Assert.AreEqual(destination, flow.Destination);
        }

        [Test]
        public void A_quit_cannot_be_confirmed_while_the_menu_is_closed()
        {
            var flow = new PauseFlow();

            Assert.IsFalse(flow.ConfirmQuit(QuitDestination.Menu));
            Assert.IsFalse(flow.Leaving);
        }

        [Test]
        public void Nothing_is_accepted_once_the_mission_is_being_left()
        {
            var flow = new PauseFlow();
            flow.Open();
            flow.ConfirmQuit(QuitDestination.Menu);

            Assert.AreEqual(EscapeStep.None, Escape(flow));
            Assert.AreEqual(EscapeStep.None, Escape(flow, dialogOpen: true));
            Assert.IsFalse(flow.Resume());
            Assert.IsFalse(flow.OpenSettings());
            Assert.IsFalse(flow.Open());
            Assert.IsTrue(flow.IsOpen, "the mission stays paused until its scene is gone");
        }

        // ------------------------------------------------------------ quit confirmation text

        [Test]
        public void Quit_dialogs_name_their_destination([Values] MissionStanding standing)
        {
            QuitPrompt menu = QuitPrompt.For(QuitDestination.Menu, standing);
            QuitPrompt desktop = QuitPrompt.For(QuitDestination.Desktop, standing);

            Assert.AreEqual(QuitPrompt.MenuTitle, menu.Title);
            StringAssert.Contains(QuitPrompt.MenuDestination, menu.Message);
            Assert.AreEqual(QuitPrompt.DesktopTitle, desktop.Title);
            StringAssert.Contains(QuitPrompt.DesktopDestination, desktop.Message);
        }

        [Test]
        public void Leaving_an_unfinished_mission_warns_that_its_progress_is_lost([Values] QuitDestination destination)
        {
            QuitPrompt prompt = QuitPrompt.For(destination, MissionStanding.Unfinished);

            StringAssert.Contains(QuitPrompt.ProgressLost, prompt.Message);
        }

        [Test]
        public void Leaving_a_result_never_warns_about_lost_mission_progress(
            [Values] QuitDestination destination,
            [Values(MissionStanding.Won, MissionStanding.Lost)] MissionStanding standing)
        {
            QuitPrompt prompt = QuitPrompt.For(destination, standing);

            StringAssert.DoesNotContain(QuitPrompt.ProgressLost, prompt.Message);
        }

        [Test]
        public void Quitting_to_the_menu_after_a_victory_says_the_victory_is_kept()
        {
            StringAssert.Contains(QuitPrompt.VictoryKept,
                QuitPrompt.For(QuitDestination.Menu, MissionStanding.Won).Message);
            StringAssert.DoesNotContain(QuitPrompt.VictoryKept,
                QuitPrompt.For(QuitDestination.Menu, MissionStanding.Lost).Message);
            StringAssert.DoesNotContain(QuitPrompt.VictoryKept,
                QuitPrompt.For(QuitDestination.Menu, MissionStanding.Unfinished).Message);
        }

        [Test]
        public void Quitting_to_the_desktop_says_campaign_progress_lasts_only_for_the_session(
            [Values] MissionStanding standing)
        {
            StringAssert.Contains(QuitPrompt.SessionOnly,
                QuitPrompt.For(QuitDestination.Desktop, standing).Message);
            StringAssert.DoesNotContain(QuitPrompt.SessionOnly,
                QuitPrompt.For(QuitDestination.Menu, standing).Message);
        }

        // ------------------------------------------------------------ audio mix

        [Test]
        public void Output_follows_the_master_volume_while_the_game_is_in_front()
        {
            Assert.AreEqual(0.4f, AudioMix.Output(0.4f, muteInBackground: false, focused: true));
            Assert.AreEqual(0.4f, AudioMix.Output(0.4f, muteInBackground: true, focused: true));
            Assert.AreEqual(1f, AudioMix.Output(1f, muteInBackground: true, focused: true));
        }

        [Test]
        public void Output_is_silent_in_the_background_only_when_Mute_in_Background_is_on()
        {
            Assert.AreEqual(0f, AudioMix.Output(1f, muteInBackground: true, focused: false));
            Assert.AreEqual(0.7f, AudioMix.Output(0.7f, muteInBackground: false, focused: false));
        }

        [Test]
        public void Returning_to_the_game_restores_the_master_volume_as_it_is_now()
        {
            // The volume was changed to zero, and then to 0.25, while the mute was in effect.
            Assert.AreEqual(0f, AudioMix.Output(0f, muteInBackground: true, focused: true));
            Assert.AreEqual(0.25f, AudioMix.Output(0.25f, muteInBackground: true, focused: true));
        }

        [Test]
        public void Output_stays_between_silence_and_full_volume()
        {
            Assert.AreEqual(0f, AudioMix.Output(-1f, false, true));
            Assert.AreEqual(1f, AudioMix.Output(3f, false, true));
            Assert.AreEqual(0f, AudioMix.Output(float.NaN, false, true));
        }

        [Test]
        public void Pausing_halves_the_music_and_resuming_brings_it_back()
        {
            Assert.AreEqual(0.5f, AudioMix.PausedMusicLevel);
            Assert.AreEqual(0.5f, AudioMix.MusicFactor(paused: true));
            Assert.AreEqual(1f, AudioMix.MusicFactor(paused: false));
        }

        [Test]
        public void Repeated_pauses_do_not_lower_the_music_further()
        {
            float first = AudioMix.MusicFactor(true);
            AudioMix.MusicFactor(false);
            float second = AudioMix.MusicFactor(true);
            float third = AudioMix.MusicFactor(true);

            Assert.AreEqual(first, second);
            Assert.AreEqual(first, third);
        }
    }
}
