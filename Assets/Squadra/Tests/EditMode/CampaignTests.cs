using NUnit.Framework;
using Squadra.Campaign;

namespace Squadra.Tests
{
    /// <summary>
    /// Exercises the campaign boundary the mission map and the results screen use: victories and
    /// defeats in; mission states, pending unlock reveals, page contents and what the results screen
    /// offers next out.
    /// </summary>
    public class CampaignTests
    {
        /// <summary>The shipped catalog: Mission 1 is playable, Missions 2-5 have no scenario yet.</summary>
        static MissionSlot[] Shipped() => Catalog(true, false, false, false, false);

        /// <summary>Seven missions that all have a scenario.</summary>
        static MissionSlot[] SevenReady() => Catalog(true, true, true, true, true, true, true);

        static MissionSlot[] Catalog(params bool[] ready)
        {
            var missions = new MissionSlot[ready.Length];
            for (int i = 0; i < ready.Length; i++)
                missions[i] = new MissionSlot("mission-" + (i + 1), ready[i]);
            return missions;
        }

        static MissionState[] States(MissionSlot[] missions, CampaignProgress progress)
        {
            var states = new MissionState[missions.Length];
            for (int i = 0; i < missions.Length; i++)
                states[i] = CampaignRules.StateOf(missions, progress, i);
            return states;
        }

        // ------------------------------------------------------------ mission states

        [Test]
        public void Fresh_campaign_offers_only_Mission_1_and_previews_the_rest()
        {
            MissionSlot[] missions = Shipped();
            var progress = new CampaignProgress();

            CollectionAssert.AreEqual(
                new[]
                {
                    MissionState.Available, MissionState.Preview, MissionState.Preview, MissionState.Preview,
                    MissionState.Preview,
                },
                States(missions, progress));
            Assert.IsTrue(CampaignRules.CanLaunch(missions, progress, 0));
            Assert.IsFalse(CampaignRules.CanLaunch(missions, progress, 1));
            Assert.AreEqual(-1, CampaignRules.FirstPendingReveal(missions, progress));
        }

        [Test]
        public void Winning_Mission_1_marks_it_completed_and_keeps_it_playable()
        {
            MissionSlot[] missions = Shipped();
            var progress = new CampaignProgress();

            Assert.IsTrue(progress.Complete("mission-1"));

            Assert.AreEqual(MissionState.Completed, CampaignRules.StateOf(missions, progress, 0));
            Assert.IsTrue(CampaignRules.CanLaunch(missions, progress, 0));
        }

        [Test]
        public void A_victory_does_not_open_a_mission_that_has_no_scenario()
        {
            MissionSlot[] missions = Shipped();
            var progress = new CampaignProgress();

            progress.Complete("mission-1");

            Assert.AreEqual(MissionState.Preview, CampaignRules.StateOf(missions, progress, 1));
            Assert.IsFalse(CampaignRules.CanLaunch(missions, progress, 1));
            Assert.AreEqual(-1, CampaignRules.FirstPendingReveal(missions, progress));
        }

        [Test]
        public void Ready_missions_stay_locked_until_the_mission_before_them_is_won()
        {
            MissionSlot[] missions = SevenReady();
            var progress = new CampaignProgress();

            Assert.AreEqual(MissionState.Available, CampaignRules.StateOf(missions, progress, 0));
            for (int i = 1; i < missions.Length; i++)
            {
                Assert.AreEqual(MissionState.Locked, CampaignRules.StateOf(missions, progress, i), "Mission " + (i + 1));
                Assert.IsFalse(CampaignRules.CanLaunch(missions, progress, i));
            }
        }

        [Test]
        public void Missions_unlock_one_at_a_time_in_catalog_order()
        {
            MissionSlot[] missions = SevenReady();
            var progress = new CampaignProgress();

            for (int won = 0; won < missions.Length; won++)
            {
                Assert.AreEqual(won, CampaignRules.NextMission(missions, progress));
                progress.Complete(missions[won].Id);

                for (int i = 0; i < missions.Length; i++)
                {
                    MissionState expected = i <= won ? MissionState.Completed
                        : i == won + 1 ? MissionState.Available
                        : MissionState.Locked;
                    Assert.AreEqual(expected, CampaignRules.StateOf(missions, progress, i),
                        "Mission " + (i + 1) + " after winning Mission " + (won + 1));
                }
            }

            Assert.AreEqual(-1, CampaignRules.NextMission(missions, progress));
        }

        [Test]
        public void A_mission_without_a_scenario_blocks_the_ready_missions_after_it()
        {
            MissionSlot[] missions = Catalog(true, false, true);
            var progress = new CampaignProgress();

            progress.Complete("mission-1");

            Assert.AreEqual(MissionState.Preview, CampaignRules.StateOf(missions, progress, 1));
            Assert.AreEqual(MissionState.Locked, CampaignRules.StateOf(missions, progress, 2));
        }

        [Test]
        public void Completion_follows_the_mission_id_when_the_catalog_is_reordered()
        {
            var progress = new CampaignProgress();
            progress.Complete("alpha");

            MissionSlot[] reordered = { new MissionSlot("beta", true), new MissionSlot("alpha", true) };

            Assert.AreEqual(MissionState.Available, CampaignRules.StateOf(reordered, progress, 0));
            Assert.AreEqual(MissionState.Completed, CampaignRules.StateOf(reordered, progress, 1));
        }

        // ------------------------------------------------------------ unlock reveals

        [Test]
        public void Winning_a_mission_leaves_one_reveal_pending_for_the_next_ready_mission()
        {
            MissionSlot[] missions = SevenReady();
            var progress = new CampaignProgress();

            progress.Complete("mission-1");

            Assert.AreEqual(1, CampaignRules.FirstPendingReveal(missions, progress));
            Assert.IsTrue(CampaignRules.NeedsReveal(missions, progress, 1));
            for (int i = 2; i < missions.Length; i++)
                Assert.IsFalse(CampaignRules.NeedsReveal(missions, progress, i), "Mission " + (i + 1));
        }

        [Test]
        public void A_reveal_stays_pending_until_the_map_has_shown_it()
        {
            MissionSlot[] missions = SevenReady();
            var progress = new CampaignProgress();
            progress.Complete("mission-1");

            // The player opens the map and leaves before the animation ends: nothing is acknowledged.
            Assert.AreEqual(1, CampaignRules.FirstPendingReveal(missions, progress));
            Assert.AreEqual(1, CampaignRules.FirstPendingReveal(missions, progress));

            progress.MarkRevealed("mission-2");

            Assert.AreEqual(-1, CampaignRules.FirstPendingReveal(missions, progress));
            Assert.AreEqual(MissionState.Available, CampaignRules.StateOf(missions, progress, 1));
        }

        [Test]
        public void Replaying_a_completed_mission_does_not_replay_an_acknowledged_unlock()
        {
            MissionSlot[] missions = SevenReady();
            var progress = new CampaignProgress();
            progress.Complete("mission-1");
            progress.MarkRevealed("mission-2");

            Assert.IsFalse(progress.Complete("mission-1"), "a second victory is not a new completion");

            Assert.AreEqual(-1, CampaignRules.FirstPendingReveal(missions, progress));
            Assert.AreEqual(1, progress.CompletedCount);
        }

        [Test]
        public void The_first_mission_never_needs_a_reveal()
        {
            MissionSlot[] missions = SevenReady();

            Assert.IsFalse(CampaignRules.NeedsReveal(missions, new CampaignProgress(), 0));
        }

        // ------------------------------------------------------------ mission results

        [Test]
        public void Winning_with_a_ready_next_mission_offers_it_and_focuses_it()
        {
            MissionSlot[] missions = SevenReady();
            var progress = new CampaignProgress();

            ResultsPlan plan = CampaignRules.ConcludeMission(missions, progress, "mission-1", true);

            Assert.AreEqual(ResultsHeading.Victory, plan.Heading);
            Assert.AreEqual(NextMissionState.Enabled, plan.NextMission);
            Assert.IsTrue(plan.ShowsNextMission);
            Assert.IsTrue(plan.CanStartNextMission);
            Assert.IsNull(plan.NextMissionReason);
            Assert.AreEqual(1, plan.NextMissionIndex);
            Assert.AreEqual(ResultsAction.NextMission, plan.InitialFocus);
            Assert.IsFalse(plan.Celebrates);
        }

        [Test]
        public void The_victory_is_recorded_before_the_next_mission_is_judged()
        {
            MissionSlot[] missions = SevenReady();
            var progress = new CampaignProgress();
            Assert.IsFalse(CampaignRules.CanLaunch(missions, progress, 1), "locked until Mission 1 is won");

            ResultsPlan plan = CampaignRules.ConcludeMission(missions, progress, "mission-1", true);

            Assert.IsTrue(progress.IsCompleted("mission-1"));
            Assert.IsTrue(plan.CanStartNextMission);
            Assert.IsTrue(CampaignRules.CanLaunch(missions, progress, plan.NextMissionIndex));
            Assert.AreEqual(1, CampaignRules.FirstPendingReveal(missions, progress),
                "the map still shows the unlock when the player returns to it");
        }

        [Test]
        public void Winning_Mission_1_of_the_shipped_catalog_is_a_plain_victory_with_the_next_mission_coming_soon()
        {
            MissionSlot[] missions = Shipped();
            var progress = new CampaignProgress();

            ResultsPlan plan = CampaignRules.ConcludeMission(missions, progress, "mission-1", true);

            Assert.AreEqual(ResultsHeading.Victory, plan.Heading);
            Assert.IsFalse(plan.Celebrates, "Missions 2-5 exist, so the campaign is not complete");
            Assert.AreEqual(NextMissionState.ComingSoon, plan.NextMission);
            Assert.IsTrue(plan.ShowsNextMission);
            Assert.IsFalse(plan.CanStartNextMission);
            Assert.AreEqual("Next mission coming soon", plan.NextMissionReason);
            Assert.AreEqual(ResultsAction.Restart, plan.InitialFocus);
        }

        [Test]
        public void Next_mission_never_skips_ahead_to_a_later_ready_mission()
        {
            MissionSlot[] missions = Catalog(true, false, true);
            var progress = new CampaignProgress();

            ResultsPlan plan = CampaignRules.ConcludeMission(missions, progress, "mission-1", true);

            Assert.AreEqual(NextMissionState.ComingSoon, plan.NextMission);
            Assert.AreEqual(1, plan.NextMissionIndex);
            Assert.IsFalse(plan.CanStartNextMission);
        }

        [Test]
        public void Losing_with_a_locked_next_mission_shows_it_disabled_and_grants_nothing()
        {
            MissionSlot[] missions = SevenReady();
            var progress = new CampaignProgress();

            ResultsPlan plan = CampaignRules.ConcludeMission(missions, progress, "mission-1", false);

            Assert.AreEqual(ResultsHeading.Defeat, plan.Heading);
            Assert.AreEqual(NextMissionState.WinRequired, plan.NextMission);
            Assert.IsTrue(plan.ShowsNextMission);
            Assert.IsFalse(plan.CanStartNextMission);
            Assert.AreEqual("Win this mission to continue", plan.NextMissionReason);
            Assert.AreEqual(ResultsAction.Restart, plan.InitialFocus);
            Assert.AreEqual(0, progress.CompletedCount);
            Assert.IsFalse(CampaignRules.CanLaunch(missions, progress, 1));
        }

        [Test]
        public void Losing_a_replay_keeps_next_mission_disabled_and_the_earlier_victory()
        {
            MissionSlot[] missions = SevenReady();
            var progress = new CampaignProgress();
            CampaignRules.ConcludeMission(missions, progress, "mission-1", true);
            progress.MarkRevealed("mission-2");

            // Mission 2 is open on the map, but this attempt at Mission 1 was lost.
            ResultsPlan plan = CampaignRules.ConcludeMission(missions, progress, "mission-1", false);

            Assert.AreEqual(ResultsHeading.Defeat, plan.Heading);
            Assert.AreEqual(NextMissionState.WinRequired, plan.NextMission);
            Assert.IsFalse(plan.CanStartNextMission);
            Assert.AreEqual("Win this mission to continue", plan.NextMissionReason);
            Assert.IsTrue(progress.IsCompleted("mission-1"), "a defeat does not revoke a completion");
            Assert.IsTrue(CampaignRules.CanLaunch(missions, progress, 1), "the map still offers Mission 2");
            Assert.AreEqual(-1, CampaignRules.FirstPendingReveal(missions, progress));
        }

        [Test]
        public void Losing_with_a_preview_as_next_mission_asks_for_a_win_first()
        {
            ResultsPlan plan = CampaignRules.ConcludeMission(Shipped(), new CampaignProgress(), "mission-1", false);

            Assert.AreEqual(NextMissionState.WinRequired, plan.NextMission);
            Assert.AreEqual("Win this mission to continue", plan.NextMissionReason);
        }

        [Test]
        public void Winning_the_last_catalog_mission_completes_the_campaign()
        {
            MissionSlot[] missions = SevenReady();
            var progress = new CampaignProgress();
            for (int i = 0; i < 6; i++) progress.Complete(missions[i].Id);

            ResultsPlan plan = CampaignRules.ConcludeMission(missions, progress, "mission-7", true);

            Assert.AreEqual(ResultsHeading.CampaignComplete, plan.Heading);
            Assert.IsTrue(plan.Celebrates);
            Assert.AreEqual(NextMissionState.Hidden, plan.NextMission);
            Assert.IsFalse(plan.ShowsNextMission);
            Assert.IsNull(plan.NextMissionReason);
            Assert.AreEqual(-1, plan.NextMissionIndex);
            Assert.AreEqual(ResultsAction.BackToMenu, plan.InitialFocus);
            Assert.AreEqual(7, progress.CompletedCount);
        }

        [Test]
        public void Losing_the_last_catalog_mission_is_a_plain_defeat_without_next_mission()
        {
            MissionSlot[] missions = SevenReady();
            var progress = new CampaignProgress();
            for (int i = 0; i < 6; i++) progress.Complete(missions[i].Id);

            ResultsPlan plan = CampaignRules.ConcludeMission(missions, progress, "mission-7", false);

            Assert.AreEqual(ResultsHeading.Defeat, plan.Heading);
            Assert.IsFalse(plan.Celebrates);
            Assert.IsFalse(plan.ShowsNextMission);
            Assert.IsNull(plan.NextMissionReason);
            Assert.AreEqual(ResultsAction.Restart, plan.InitialFocus);
            Assert.IsFalse(progress.IsCompleted("mission-7"));
        }

        [Test]
        public void The_last_mission_is_the_end_of_the_catalog_not_the_last_ready_mission()
        {
            // Mission 2 is the last one with a scenario, but Mission 3 is still to come.
            MissionSlot[] missions = Catalog(true, true, false);
            var progress = new CampaignProgress();
            progress.Complete("mission-1");

            ResultsPlan plan = CampaignRules.ConcludeMission(missions, progress, "mission-2", true);

            Assert.AreEqual(ResultsHeading.Victory, plan.Heading);
            Assert.AreEqual(NextMissionState.ComingSoon, plan.NextMission);
        }

        [Test]
        public void A_single_mission_catalog_treats_its_mission_as_the_last()
        {
            MissionSlot[] missions = Catalog(true);

            ResultsPlan won = CampaignRules.ConcludeMission(missions, new CampaignProgress(), "mission-1", true);
            Assert.AreEqual(ResultsHeading.CampaignComplete, won.Heading);
            Assert.IsFalse(won.ShowsNextMission);
            Assert.AreEqual(ResultsAction.BackToMenu, won.InitialFocus);

            ResultsPlan lost = CampaignRules.ConcludeMission(missions, new CampaignProgress(), "mission-1", false);
            Assert.AreEqual(ResultsHeading.Defeat, lost.Heading);
            Assert.IsFalse(lost.ShowsNextMission);
            Assert.AreEqual(ResultsAction.Restart, lost.InitialFocus);
        }

        [Test]
        public void A_match_outside_the_campaign_gets_a_plain_result_without_next_mission()
        {
            MissionSlot[] missions = Catalog(true);
            var progress = new CampaignProgress();

            // A scene opened on its own has no active mission; an id the catalog does not know is the same.
            foreach (string missionId in new[] { null, "", "elsewhere" })
            {
                ResultsPlan won = CampaignRules.ConcludeMission(missions, progress, missionId, true);
                Assert.AreEqual(ResultsHeading.Victory, won.Heading, "no campaign completion for '" + missionId + "'");
                Assert.IsFalse(won.ShowsNextMission);
                Assert.AreEqual(ResultsAction.Restart, won.InitialFocus);

                ResultsPlan lost = CampaignRules.ConcludeMission(missions, progress, missionId, false);
                Assert.AreEqual(ResultsHeading.Defeat, lost.Heading);
                Assert.IsFalse(lost.ShowsNextMission);
                Assert.AreEqual(ResultsAction.Restart, lost.InitialFocus);
            }

            Assert.IsFalse(progress.IsCompleted("mission-1"));
        }

        [Test]
        public void Restarting_and_replaying_never_takes_progress_away()
        {
            MissionSlot[] missions = SevenReady();
            var progress = new CampaignProgress();
            CampaignRules.ConcludeMission(missions, progress, "mission-1", true);
            CampaignRules.ConcludeMission(missions, progress, "mission-2", true);

            // Back to Mission 1: lose it, then win it again.
            CampaignRules.ConcludeMission(missions, progress, "mission-1", false);
            ResultsPlan again = CampaignRules.ConcludeMission(missions, progress, "mission-1", true);

            Assert.AreEqual(2, progress.CompletedCount);
            Assert.AreEqual(MissionState.Completed, CampaignRules.StateOf(missions, progress, 0));
            Assert.AreEqual(MissionState.Completed, CampaignRules.StateOf(missions, progress, 1));
            Assert.AreEqual(MissionState.Available, CampaignRules.StateOf(missions, progress, 2));
            Assert.IsTrue(again.CanStartNextMission);
            Assert.AreEqual(1, again.NextMissionIndex, "Next Mission follows the mission just played");
        }

        [Test]
        public void A_fresh_launch_starts_the_campaign_over()
        {
            MissionSlot[] missions = SevenReady();
            CampaignRules.ConcludeMission(missions, new CampaignProgress(), "mission-1", true);

            var relaunched = new CampaignProgress();

            Assert.AreEqual(0, relaunched.CompletedCount);
            Assert.IsFalse(CampaignRules.CanLaunch(missions, relaunched, 1));
        }

        // ------------------------------------------------------------ pages

        [Test]
        public void Five_missions_fit_on_one_default_page_without_arrows()
        {
            var pages = new MissionPages(5, MissionPages.DefaultPerPage);

            Assert.AreEqual(5, pages.PerPage);
            Assert.AreEqual(1, pages.PageCount);
            Assert.IsFalse(pages.HasSeveralPages);
            Assert.AreEqual(5, pages.CountOnPage(0));
            Assert.IsFalse(pages.HasPrevious(0));
            Assert.IsFalse(pages.HasNext(0));
        }

        [Test]
        public void Seven_missions_at_five_per_page_leave_three_empty_slots_on_the_second_page()
        {
            var pages = new MissionPages(7, 5);

            Assert.AreEqual(2, pages.PageCount);
            Assert.IsTrue(pages.HasSeveralPages);
            Assert.AreEqual(0, pages.FirstOnPage(0));
            Assert.AreEqual(5, pages.CountOnPage(0));
            Assert.AreEqual(5, pages.FirstOnPage(1));
            Assert.AreEqual(2, pages.CountOnPage(1));

            Assert.IsFalse(pages.HasPrevious(0));
            Assert.IsTrue(pages.HasNext(0));
            Assert.IsTrue(pages.HasPrevious(1));
            Assert.IsFalse(pages.HasNext(1));
        }

        [Test]
        public void Seven_missions_at_three_per_page_make_pages_of_three_three_and_one()
        {
            var pages = new MissionPages(7, 3);

            Assert.AreEqual(3, pages.PageCount);
            Assert.AreEqual(3, pages.CountOnPage(0));
            Assert.AreEqual(3, pages.CountOnPage(1));
            Assert.AreEqual(1, pages.CountOnPage(2));
            Assert.AreEqual(6, pages.FirstOnPage(2));
        }

        [Test]
        public void Seven_missions_at_seven_per_page_share_one_page()
        {
            var pages = new MissionPages(7, 7);

            Assert.AreEqual(1, pages.PageCount);
            Assert.IsFalse(pages.HasSeveralPages);
            Assert.AreEqual(7, pages.CountOnPage(0));
        }

        [Test]
        public void A_newly_unlocked_mission_is_found_on_its_page()
        {
            Assert.AreEqual(1, new MissionPages(7, 5).PageOf(5), "Mission 6 at five per page");
            Assert.AreEqual(0, new MissionPages(7, 5).PageOf(4), "Mission 5 at five per page");
            Assert.AreEqual(1, new MissionPages(7, 3).PageOf(3), "Mission 4 at three per page");
            Assert.AreEqual(2, new MissionPages(7, 3).PageOf(6), "Mission 7 at three per page");
        }

        [Test]
        public void Page_size_is_limited_to_one_through_ten()
        {
            Assert.AreEqual(1, new MissionPages(7, 0).PerPage);
            Assert.AreEqual(1, new MissionPages(7, -4).PerPage);
            Assert.AreEqual(10, new MissionPages(7, 11).PerPage);
            Assert.AreEqual(7, new MissionPages(7, 1).PageCount);
        }

        [Test]
        public void Page_requests_past_either_end_stay_on_the_end_page()
        {
            var pages = new MissionPages(7, 5);

            Assert.AreEqual(0, pages.ClampPage(-1));
            Assert.AreEqual(1, pages.ClampPage(2));
        }

        [Test]
        public void An_empty_catalog_still_has_one_empty_page()
        {
            var pages = new MissionPages(0, 5);

            Assert.AreEqual(1, pages.PageCount);
            Assert.AreEqual(0, pages.CountOnPage(0));
            Assert.IsFalse(pages.HasSeveralPages);
        }
    }
}
