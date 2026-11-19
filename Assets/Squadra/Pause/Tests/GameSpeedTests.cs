using System;
using System.Collections.Generic;
using NUnit.Framework;
using Squadra.Pause;
using Squadra.Simulation;

namespace Squadra.Tests
{
    /// <summary>
    /// Exercises the game clock the Settings screens, the mission scene and the sandbox share: the
    /// saved game speed, a sandbox run's temporary speed and the pause in; the factor on elapsed time,
    /// the selected speed and the preference to save out. Also drives a match through the clock the way
    /// the mission scene does, to show that every speed plays the same game, only faster or slower.
    /// </summary>
    public class GameSpeedTests
    {
        static readonly GameSpeed[] AllSpeeds = { GameSpeed.Slow, GameSpeed.Normal, GameSpeed.Fast, GameSpeed.Turbo };

        // ------------------------------------------------------------ the choices

        [Test]
        public void The_choices_are_Slow_Normal_Fast_and_Turbo_in_that_order()
        {
            CollectionAssert.AreEqual(AllSpeeds, GameClock.Speeds);
            CollectionAssert.AreEqual(new[] { "Slow", "Normal", "Fast", "Turbo" },
                Array.ConvertAll(AllSpeeds, GameClock.NameOf));
        }

        [Test]
        public void Each_choice_has_its_multiplier()
        {
            Assert.AreEqual(0.75f, GameClock.Multiplier(GameSpeed.Slow));
            Assert.AreEqual(1f, GameClock.Multiplier(GameSpeed.Normal));
            Assert.AreEqual(1.5f, GameClock.Multiplier(GameSpeed.Fast));
            Assert.AreEqual(2f, GameClock.Multiplier(GameSpeed.Turbo));
        }

        [Test]
        public void A_new_profile_plays_at_Normal_speed()
        {
            var clock = new GameClock();
            clock.BeginRun();

            Assert.AreEqual(GameSpeed.Normal, GameClock.DefaultSpeed);
            Assert.AreEqual(GameSpeed.Normal, clock.Preference);
            Assert.AreEqual(1f, clock.Scale);
        }

        // ------------------------------------------------------------ saving and loading

        [Test]
        public void Each_choice_is_saved_as_its_own_stable_name()
        {
            CollectionAssert.AreEqual(new[] { "slow", "normal", "fast", "turbo" },
                Array.ConvertAll(AllSpeeds, GameClock.KeyOf));
        }

        [TestCaseSource(nameof(AllSpeeds))]
        public void A_saved_choice_is_loaded_unchanged_in_the_next_session(GameSpeed speed)
        {
            var session = new GameClock();
            session.Choose(speed);
            string saved = GameClock.KeyOf(session.Preference);

            var next = new GameClock(GameClock.FromKey(saved));
            next.BeginRun();

            Assert.AreEqual(speed, next.Preference);
            Assert.AreEqual(GameClock.Multiplier(speed), next.Scale);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("warp")]
        [TestCase("Turbo")]
        [TestCase("1.5")]
        [TestCase("3")]
        public void A_missing_or_unknown_saved_value_loads_as_Normal(string saved)
        {
            Assert.AreEqual(GameSpeed.Normal, GameClock.FromKey(saved));
        }

        [Test]
        public void A_speed_outside_the_choices_is_never_taken()
        {
            var clock = new GameClock((GameSpeed)17);
            Assert.AreEqual(GameSpeed.Normal, clock.Preference);

            clock.Choose(GameSpeed.Fast);
            clock.Choose((GameSpeed)(-1));
            Assert.AreEqual(GameSpeed.Fast, clock.Preference);

            clock.BeginRun();
            Assert.IsFalse(clock.SetOverride((GameSpeed)9));
            Assert.IsNull(clock.Override);
        }

        // ------------------------------------------------------------ menus and runs

        [TestCaseSource(nameof(AllSpeeds))]
        public void Menus_run_at_normal_speed_whatever_the_preference(GameSpeed speed)
        {
            var clock = new GameClock(speed);
            Assert.AreEqual(1f, clock.Scale, "before any mission");

            clock.BeginRun();
            clock.EndRun();
            Assert.AreEqual(1f, clock.Scale, "after leaving a mission");
            Assert.AreEqual(speed, clock.Preference);
        }

        [TestCaseSource(nameof(AllSpeeds))]
        public void A_run_plays_at_the_preference(GameSpeed speed)
        {
            var clock = new GameClock(speed);

            clock.BeginRun();

            Assert.AreEqual(speed, clock.Speed);
            Assert.AreEqual(GameClock.Multiplier(speed), clock.Scale);
        }

        [Test]
        public void A_choice_made_in_the_main_menu_is_the_speed_of_the_next_mission()
        {
            var clock = new GameClock();

            clock.Choose(GameSpeed.Turbo);
            Assert.AreEqual(1f, clock.Scale, "the menu itself is not sped up");

            clock.BeginRun();
            Assert.AreEqual(2f, clock.Scale);
        }

        [Test]
        public void Choosing_during_a_run_applies_at_once()
        {
            var clock = new GameClock();
            clock.BeginRun();

            clock.Choose(GameSpeed.Slow);

            Assert.AreEqual(0.75f, clock.Scale);
        }

        [Test]
        public void Restarts_and_mission_changes_neither_reset_nor_compound_the_speed()
        {
            var clock = new GameClock(GameSpeed.Fast);

            for (int i = 0; i < 5; i++)
            {
                clock.BeginRun();
                Assert.AreEqual(1.5f, clock.Scale);
                clock.SetPaused(true);
                clock.EndRun();
                Assert.AreEqual(1f, clock.Scale);
            }

            Assert.AreEqual(GameSpeed.Fast, clock.Preference);
            clock.BeginRun();
            Assert.IsFalse(clock.Paused, "a new run never starts paused");
            Assert.AreEqual(1.5f, clock.Scale);
        }

        // ------------------------------------------------------------ pause

        [TestCaseSource(nameof(AllSpeeds))]
        public void Pausing_stops_time_and_resuming_restores_the_speed(GameSpeed speed)
        {
            var clock = new GameClock(speed);
            clock.BeginRun();

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(clock.SetPaused(true));
                Assert.AreEqual(0f, clock.Scale);
                Assert.AreEqual(speed, clock.Speed, "the pause keeps the selected speed");
                Assert.IsTrue(clock.SetPaused(false));
                Assert.AreEqual(GameClock.Multiplier(speed), clock.Scale);
            }

            Assert.AreEqual(speed, clock.Preference);
        }

        [Test]
        public void Pausing_twice_or_resuming_twice_changes_nothing()
        {
            var clock = new GameClock(GameSpeed.Turbo);
            clock.BeginRun();

            Assert.IsFalse(clock.SetPaused(false));
            Assert.IsTrue(clock.SetPaused(true));
            Assert.IsFalse(clock.SetPaused(true));
            Assert.IsTrue(clock.SetPaused(false));
            Assert.AreEqual(2f, clock.Scale);
        }

        [Test]
        public void There_is_no_pause_outside_a_run()
        {
            var clock = new GameClock();

            Assert.IsFalse(clock.SetPaused(true));
            Assert.AreEqual(1f, clock.Scale);
        }

        [TestCaseSource(nameof(AllSpeeds))]
        public void A_choice_made_while_paused_waits_for_the_resume(GameSpeed before)
        {
            foreach (GameSpeed after in AllSpeeds)
            {
                var clock = new GameClock(before);
                clock.BeginRun();
                clock.SetPaused(true);

                clock.Choose(after);
                Assert.AreEqual(0f, clock.Scale, "still frozen");

                clock.SetPaused(false);
                Assert.AreEqual(GameClock.Multiplier(after), clock.Scale);
                Assert.AreEqual(after, clock.Preference);
            }
        }

        [Test]
        public void Leaving_a_paused_run_releases_the_pause()
        {
            var clock = new GameClock(GameSpeed.Slow);
            clock.BeginRun();
            clock.SetPaused(true);

            clock.EndRun();

            Assert.IsFalse(clock.Paused);
            Assert.AreEqual(1f, clock.Scale);
            Assert.AreEqual(GameSpeed.Slow, clock.Preference, "pausing never saves zero or loses the choice");
        }

        // ------------------------------------------------------------ the sandbox's speed

        [Test]
        public void A_sandbox_run_starts_at_the_preference_without_a_speed_of_its_own()
        {
            var clock = new GameClock(GameSpeed.Fast);

            clock.BeginRun();

            Assert.IsNull(clock.Override);
            Assert.AreEqual(GameSpeed.Fast, clock.Speed);
        }

        [Test]
        public void The_sandbox_speed_applies_to_the_run_but_is_never_the_preference_to_save()
        {
            var clock = new GameClock(GameSpeed.Normal);
            clock.BeginRun();

            Assert.IsTrue(clock.SetOverride(GameSpeed.Turbo));

            Assert.AreEqual(GameSpeed.Turbo, clock.Speed);
            Assert.AreEqual(2f, clock.Scale);
            Assert.AreEqual(GameSpeed.Normal, clock.Preference);
            Assert.AreEqual("normal", GameClock.KeyOf(clock.Preference),
                "saving another preference, such as a volume, saves the preference, not the sandbox speed");
        }

        [Test]
        public void The_pause_takes_precedence_over_the_sandbox_speed()
        {
            var clock = new GameClock();
            clock.BeginRun();
            clock.SetOverride(GameSpeed.Slow);

            clock.SetPaused(true);
            Assert.AreEqual(0f, clock.Scale);

            clock.SetPaused(false);
            Assert.AreEqual(0.75f, clock.Scale);
        }

        [Test]
        public void A_choice_in_Settings_replaces_the_sandbox_speed_even_when_it_is_the_saved_one()
        {
            var clock = new GameClock(GameSpeed.Fast);
            clock.BeginRun();
            clock.SetOverride(GameSpeed.Turbo);
            int changes = 0;
            clock.Changed += () => changes++;

            clock.Choose(GameSpeed.Fast);

            Assert.IsNull(clock.Override);
            Assert.AreEqual(GameSpeed.Fast, clock.Speed);
            Assert.AreEqual(1.5f, clock.Scale);
            Assert.AreEqual(1, changes, "the sandbox's selector is told, so it can show the change");
        }

        [Test]
        public void After_a_choice_in_Settings_a_sandbox_speed_is_temporary_again()
        {
            var clock = new GameClock();
            clock.BeginRun();
            clock.SetOverride(GameSpeed.Turbo);
            clock.Choose(GameSpeed.Slow);

            Assert.IsTrue(clock.SetOverride(GameSpeed.Fast));

            Assert.AreEqual(1.5f, clock.Scale);
            Assert.AreEqual(GameSpeed.Slow, clock.Preference);
        }

        [Test]
        public void Leaving_the_sandbox_discards_its_speed_and_the_next_run_starts_from_the_preference()
        {
            var clock = new GameClock(GameSpeed.Slow);
            clock.BeginRun();
            clock.SetOverride(GameSpeed.Turbo);

            clock.EndRun();
            Assert.IsNull(clock.Override);
            Assert.AreEqual(1f, clock.Scale, "the menu is not sped up");

            clock.BeginRun();
            Assert.AreEqual(0.75f, clock.Scale);
        }

        [Test]
        public void A_sandbox_speed_needs_a_run()
        {
            var clock = new GameClock();

            Assert.IsFalse(clock.SetOverride(GameSpeed.Turbo));
            Assert.IsNull(clock.Override);
            Assert.AreEqual(1f, clock.Scale);
        }

        [Test]
        public void Every_change_is_announced()
        {
            var clock = new GameClock();
            int changes = 0;
            clock.Changed += () => changes++;

            clock.BeginRun();
            clock.SetOverride(GameSpeed.Fast);
            clock.SetOverride(GameSpeed.Fast); // no change
            clock.SetPaused(true);
            clock.SetPaused(true); // no change
            clock.Choose(GameSpeed.Normal);
            clock.Choose(GameSpeed.Normal); // the player's choice is always announced
            clock.EndRun();

            Assert.AreEqual(6, changes);
        }

        // ------------------------------------------------------------ a match at every speed

        // Real seconds per frame. With these speeds every run below takes a whole number of frames.
        const double Frame = 1.0 / 40.0;
        const double GameSeconds = 12.3;

        sealed class Outcome
        {
            public double Elapsed;
            public int Produced;
            public int Arrivals;
            public int Captures;
            public int Casualties;
            public int Frames;
            public readonly List<string> Worlds = new List<string>();
            public int FleetsInTransit;
            public MatchStatus Status;
        }

        // Blue's planet, Red's planet and a neutral moon. At the start Blue sends its fleet to the moon
        // (arrives 1.5 s, captured 6.5 s, which then produces) and Red attacks Blue's planet (arrives
        // 4.5 s, battle against what Blue has built by then).
        static Match NewMatch()
        {
            var config = new MatchConfig { FleetSpeed = 2 };
            config.Nodes.Add(NodeConfig.Planet("Blue", new MapPoint(0, 0), Faction.Blue, 30));
            config.Nodes.Add(NodeConfig.Planet("Red", new MapPoint(9, 0), Faction.Red, 30));
            config.Nodes.Add(NodeConfig.Moon("Moon", new MapPoint(3, 0)));
            config.ConnectAll();
            var match = new Match(config);
            Assert.IsTrue(match.TryLaunch(Faction.Blue, 0, 2));
            Assert.IsTrue(match.TryLaunch(Faction.Red, 1, 0));
            return match;
        }

        // Plays GameSeconds of game time the way the mission scene does: every frame the match gets
        // the real frame time scaled by the clock.
        static Outcome Play(GameSpeed speed)
        {
            var clock = new GameClock(speed);
            clock.BeginRun();
            Match match = NewMatch();
            var outcome = new Outcome();
            match.DartProduced += _ => outcome.Produced++;
            match.FleetArrived += _ => outcome.Arrivals++;
            match.CaptureCompleted += (_, _) => outcome.Captures++;
            match.BattleCasualties += (_, count) => outcome.Casualties += count;

            outcome.Frames = (int)Math.Round(GameSeconds / GameClock.Multiplier(speed) / Frame);
            for (int i = 0; i < outcome.Frames; i++) match.Advance(Frame * clock.Scale);

            outcome.Elapsed = match.Elapsed;
            outcome.Status = match.Status;
            outcome.FleetsInTransit = match.Fleets.Count;
            foreach (Node node in match.Nodes)
                outcome.Worlds.Add(node.Name + " " + node.Owner + " " + node.Darts + " " + node.Captor);
            return outcome;
        }

        [TestCaseSource(nameof(AllSpeeds))]
        public void Every_speed_plays_the_same_match_in_its_own_real_time(GameSpeed speed)
        {
            Outcome normal = Play(GameSpeed.Normal);
            Outcome played = Play(speed);

            Assert.AreEqual(GameSeconds, played.Elapsed, 1e-9, "match time");
            Assert.AreEqual(normal.Elapsed, played.Elapsed, 1e-9);
            Assert.AreEqual(normal.Produced, played.Produced, "production");
            Assert.AreEqual(normal.Arrivals, played.Arrivals, "arrivals");
            Assert.AreEqual(normal.Captures, played.Captures, "captures");
            Assert.AreEqual(normal.Casualties, played.Casualties, "battle casualties");
            CollectionAssert.AreEqual(normal.Worlds, played.Worlds);
            Assert.AreEqual(normal.FleetsInTransit, played.FleetsInTransit);
            Assert.AreEqual(normal.Status, played.Status);
            Assert.AreEqual(normal.Frames / GameClock.Multiplier(speed), played.Frames, 1.0,
                "real time taken shrinks with the speed");
        }

        [Test]
        public void The_reference_match_has_production_arrivals_a_capture_and_a_battle()
        {
            Outcome normal = Play(GameSpeed.Normal);

            Assert.Greater(normal.Produced, 0);
            Assert.AreEqual(2, normal.Arrivals);
            Assert.Greater(normal.Captures, 0);
            Assert.Greater(normal.Casualties, 0);
        }

        [TestCaseSource(nameof(AllSpeeds))]
        public void A_paused_match_resumes_at_the_speed_chosen_meanwhile_without_catching_up(GameSpeed before)
        {
            foreach (GameSpeed after in AllSpeeds)
            {
                var clock = new GameClock(before);
                clock.BeginRun();
                Match match = NewMatch();

                for (int i = 0; i < 40; i++) match.Advance(Frame * clock.Scale);
                double pausedAt = match.Elapsed;
                int blueDarts = match.Nodes[0].Darts;

                clock.SetPaused(true);
                for (int i = 0; i < 200; i++) match.Advance(Frame * clock.Scale);
                clock.Choose(after);
                for (int i = 0; i < 200; i++) match.Advance(Frame * clock.Scale);

                Assert.AreEqual(pausedAt, match.Elapsed, "no time passes while paused");
                Assert.AreEqual(blueDarts, match.Nodes[0].Darts);

                clock.SetPaused(false);
                match.Advance(Frame * clock.Scale);

                Assert.AreEqual(pausedAt + Frame * GameClock.Multiplier(after), match.Elapsed, 1e-9,
                    before + " then " + after + ": one frame at the new speed, nothing made up");
            }
        }
    }
}
