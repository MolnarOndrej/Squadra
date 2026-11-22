using System;
using NUnit.Framework;
using Squadra.Menus;

namespace Squadra.Tests
{
    /// <summary>
    /// Exercises where a keyboard move lands along a line of menu controls: which moves run along a
    /// column or a row, stepping and wrapping, the ends of a line that does not wrap, and stops that
    /// cannot take the focus.
    /// </summary>
    public class MenuStepTests
    {
        static readonly bool[] AllOpen = { true, true, true, true };

        // ------------------------------------------------------------ along or across

        [TestCase(MenuDirection.Up, -1)]
        [TestCase(MenuDirection.Down, 1)]
        [TestCase(MenuDirection.Left, 0)]
        [TestCase(MenuDirection.Right, 0)]
        public void Up_and_Down_run_along_a_column_and_Left_and_Right_across_it(MenuDirection direction, int step)
        {
            Assert.AreEqual(step, MenuStep.Along(MenuAxis.Column, direction));
        }

        [TestCase(MenuDirection.Left, -1)]
        [TestCase(MenuDirection.Right, 1)]
        [TestCase(MenuDirection.Up, 0)]
        [TestCase(MenuDirection.Down, 0)]
        public void Left_and_Right_run_along_a_row_and_Up_and_Down_across_it(MenuDirection direction, int step)
        {
            Assert.AreEqual(step, MenuStep.Along(MenuAxis.Row, direction));
        }

        // ------------------------------------------------------------ stepping

        [Test]
        public void A_step_moves_to_the_neighbouring_stop()
        {
            Assert.AreEqual(2, MenuStep.Next(AllOpen, 1, 1, false));
            Assert.AreEqual(0, MenuStep.Next(AllOpen, 1, -1, false));
        }

        [Test]
        public void A_line_that_wraps_carries_on_from_one_end_to_the_other()
        {
            Assert.AreEqual(0, MenuStep.Next(AllOpen, 3, 1, true));
            Assert.AreEqual(3, MenuStep.Next(AllOpen, 0, -1, true));
        }

        [Test]
        public void A_line_that_does_not_wrap_stops_at_its_ends()
        {
            Assert.AreEqual(-1, MenuStep.Next(AllOpen, 3, 1, false));
            Assert.AreEqual(-1, MenuStep.Next(AllOpen, 0, -1, false));
        }

        [Test]
        public void A_move_across_the_line_goes_nowhere()
        {
            Assert.AreEqual(-1, MenuStep.Next(AllOpen, 1, 0, true));
        }

        // ------------------------------------------------------------ stops that cannot take the focus

        [Test]
        public void Stops_that_cannot_take_the_focus_are_passed_over()
        {
            // Single Player, Sandbox, a disabled Multi Player, Settings, Quit.
            var open = new[] { true, true, false, true, true };

            Assert.AreEqual(1, MenuStep.Next(open, 0, 1, true));
            Assert.AreEqual(3, MenuStep.Next(open, 1, 1, true));
            Assert.AreEqual(1, MenuStep.Next(open, 3, -1, true));
        }

        [Test]
        public void Wrapping_also_passes_over_stops_that_cannot_take_the_focus()
        {
            // A disabled previous-page arrow, two missions, a disabled next-page arrow.
            var open = new[] { false, true, true, false };

            Assert.AreEqual(1, MenuStep.Next(open, 2, 1, true));
            Assert.AreEqual(2, MenuStep.Next(open, 1, -1, true));
        }

        [Test]
        public void Without_wrapping_closed_stops_at_the_end_block_the_move()
        {
            var open = new[] { true, true, false, false };

            Assert.AreEqual(-1, MenuStep.Next(open, 1, 1, false));
        }

        [Test]
        public void A_line_with_one_stop_that_can_take_the_focus_goes_nowhere()
        {
            var open = new[] { false, true, false };

            Assert.AreEqual(-1, MenuStep.Next(open, 1, 1, true));
            Assert.AreEqual(-1, MenuStep.Next(open, 1, -1, true));
            Assert.AreEqual(-1, MenuStep.Next(new[] { true }, 0, 1, true));
        }

        [Test]
        public void Two_stops_that_wrap_lead_to_each_other_both_ways()
        {
            // Cancel and Confirm in the dialog.
            var open = new[] { true, true };

            Assert.AreEqual(1, MenuStep.Next(open, 0, 1, true));
            Assert.AreEqual(1, MenuStep.Next(open, 0, -1, true));
        }

        [Test]
        public void A_place_outside_the_line_is_refused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MenuStep.Next(AllOpen, 4, 1, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => MenuStep.Next(AllOpen, -1, 1, true));
            Assert.Throws<ArgumentNullException>(() => MenuStep.Next(null, 0, 1, true));
        }
    }
}
