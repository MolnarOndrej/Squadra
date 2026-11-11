using NUnit.Framework;
using Squadra.Campaign;

namespace Squadra.Tests
{
    /// <summary>
    /// Exercises the campaign boundary the mission map uses: victories in, mission states, pending
    /// unlock reveals and page contents out.
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
