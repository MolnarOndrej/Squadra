using System.Collections.Generic;
using Squadra.Simulation;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// What the scene shows and plays when something happens in the match: a fleet leaving or
    /// arriving, a Dart produced, a world captured, a battle beginning, its casualties and its end.
    /// It listens to the match and tells the fleet layer, the effects, the sounds, the worlds and
    /// the camera; it changes nothing in the match. The end of the match is not handled here: that
    /// belongs to the campaign and the results screen, which the game controller looks after.
    /// </summary>
    sealed class MatchFeedback
    {
        readonly IReadOnlyList<WorldView> worlds;
        readonly FleetLayer fleets;
        readonly EffectsLayer effects;
        readonly SoundLayer sounds;
        readonly CameraRig rig;

        public MatchFeedback(IReadOnlyList<WorldView> worlds, FleetLayer fleets, EffectsLayer effects,
            SoundLayer sounds, CameraRig rig)
        {
            this.worlds = worlds;
            this.fleets = fleets;
            this.effects = effects;
            this.sounds = sounds;
            this.rig = rig;
        }

        public void Attach(Match match)
        {
            match.FleetLaunched += OnFleetLaunched;
            match.FleetArrived += OnFleetArrived;
            match.DartProduced += OnDartProduced;
            match.CaptureCompleted += OnCaptureCompleted;
            match.BattleStarted += OnBattleStarted;
            match.BattleCasualties += OnBattleCasualties;
            match.BattleEnded += OnBattleEnded;
        }

        void OnFleetLaunched(Fleet fleet)
        {
            fleets.BeginTransit(fleet);
            WorldView source = worlds[fleet.SourceId];
            effects.Shockwave(source.Position, Palette.Of(fleet.Faction), source.OrbitRadius * 0.85f,
                source.OrbitRadius * 1.3f, 0.45f, 1.6f);
            sounds.FleetDeparted(source.Position);
        }

        void OnDartProduced(Node node)
        {
            worlds[node.Id].Punch(0.25f);
        }

        void OnCaptureCompleted(Node node, Faction previousOwner)
        {
            WorldView world = worlds[node.Id];
            Color color = Palette.Of(node.Owner);
            world.Flash(1f);
            world.Punch(1f);
            effects.Shockwave(world.Position, color, world.BodyRadius, world.OrbitRadius * 2.4f, 0.9f, 2.6f);
            effects.Shockwave(world.Position, color, world.BodyRadius * 0.6f, world.OrbitRadius * 1.7f, 0.7f, 1.8f);
            fleets.Burst(world.Position, world.OrbitRadius, color, 28, 2.6f, 0.8f);
            effects.FloatText(world.Position + Vector2.up * (world.OrbitRadius + 0.3f), "CAPTURED",
                Palette.Bright(color, 0.3f), 3.2f, 0f, 1.5f);
            sounds.WorldCaptured(world.Position);
        }

        void OnFleetArrived(ArrivalReport report)
        {
            // The fleet's dots stay on screen: they join whoever they have reached.
            fleets.Arrive(report);

            WorldView world = worlds[report.NodeId];
            Color arriving = Palette.Of(report.Arriving);
            Vector2 above = world.Position + Vector2.up * (world.OrbitRadius + 0.3f);
            world.Punch(1f);

            switch (report.Outcome)
            {
                case ArrivalOutcome.BattleStarted:
                    return; // announced by the battle itself

                case ArrivalOutcome.BattleJoined:
                    // Reinforcements: the battle carries on with more Darts on one side.
                    effects.Shockwave(world.Position, arriving, world.OrbitRadius * 1.5f, world.OrbitRadius * 1.1f,
                        0.4f, 1.6f);
                    effects.FloatText(above, "+" + report.ArrivingDarts, Palette.Bright(arriving, 0.3f), 3.6f);
                    sounds.FleetArrived(world.Position);
                    return;
            }

            // Peaceful: a fleet joining its own side, or an unopposed landing.
            effects.Shockwave(world.Position, arriving, world.OrbitRadius * 1.25f, world.OrbitRadius * 0.95f,
                0.45f, 1.5f);
            bool landing = report.Outcome == ArrivalOutcome.Landed;
            if (landing) fleets.Burst(world.Position, world.OrbitRadius, arriving, 12, 1.2f);
            effects.FloatText(above, landing ? "LANDING" : "+" + report.ArrivingDarts,
                Palette.Bright(arriving, 0.3f), landing ? 2.6f : 3.6f);
            sounds.FleetArrived(world.Position);
        }

        // Nobody has lost a Dart yet. The fleet layer shows the fight from here on: shots, and an
        // explosion for the casualties the match reports.
        void OnBattleStarted(Battle battle)
        {
            WorldView world = worlds[battle.NodeId];
            effects.Shockwave(world.Position, Palette.Starlight, world.OrbitRadius * 0.8f, world.OrbitRadius * 1.6f,
                0.5f, 2.2f);
            // One line higher than an arrival's text, which may still be on screen.
            effects.FloatText(world.Position + Vector2.up * (world.OrbitRadius + 0.72f), "BATTLE",
                Palette.Bright(Palette.Accent, 0.2f), 2.6f, 0f, 1.2f);
            world.Flash(0.4f);
            rig.Shake(0.07f);
            sounds.BattleStarted(world.Position);
        }

        void OnBattleCasualties(Battle battle, int pairs)
        {
            fleets.BattleCasualties(battle, pairs);

            WorldView world = worlds[battle.NodeId];
            world.Punch(0.3f);
            world.Flash(0.12f);
            rig.Shake(0.015f);
        }

        void OnBattleEnded(BattleReport report)
        {
            fleets.BattleEnded(report);

            WorldView world = worlds[report.NodeId];
            Color winner = Palette.Of(report.Winner);
            Vector2 above = world.Position + Vector2.up * (world.OrbitRadius + 0.3f);
            effects.Shockwave(world.Position, winner, world.OrbitRadius * 0.9f, world.OrbitRadius * 1.7f, 0.7f, 2.4f);
            world.Flash(0.6f);
            world.Punch(1f);
            rig.Shake(Mathf.Min(0.16f, 0.05f + report.LossesPerSide * 0.003f));

            effects.FloatText(above + Vector2.up * 0.42f, "-" + report.LossesPerSide, Palette.Text, 4.2f);
            effects.FloatText(above, OutcomeTag(report.Outcome), Palette.Bright(winner, 0.3f), 2.4f, 0.15f, 1.3f);
        }

        static string OutcomeTag(BattleOutcome outcome)
        {
            switch (outcome)
            {
                case BattleOutcome.AttackWon: return "DEFENSES BROKEN";
                case BattleOutcome.Repelled: return "ATTACK REPELLED";
                case BattleOutcome.ControlRestored: return "CONTROL RESTORED";
                default: return "MUTUAL DESTRUCTION";
            }
        }
    }
}
