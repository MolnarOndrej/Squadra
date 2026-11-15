using System;
using System.Collections.Generic;
using NUnit.Framework;
using Squadra.MissionEditing;
using Squadra.Missions;

namespace Squadra.Tests
{
    /// <summary>
    /// Exercises the mission editor the way an author uses it, without a window: editing commands and
    /// toolbar actions in; the mission, its validation, the file store and the questions put to the
    /// author out. Files, file dialogs and the author's answers are stand-ins, so cancelled dialogs,
    /// failed writes and files changed behind the editor's back can be produced at will.
    /// </summary>
    public class MissionEditingTests
    {
        // ------------------------------------------------------------ stand-ins

        /// <summary>A disk in memory. Writes can be made to fail and files changed from outside.</summary>
        sealed class FakeFiles : IMissionFileStore
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly HashSet<string> Unreadable = new HashSet<string>(StringComparer.Ordinal);
            public string FailWritesWith;
            public Action BeforeNextRead;
            public int Writes;

            public MissionFileContent Read(string path)
            {
                Action hook = BeforeNextRead;
                BeforeNextRead = null;
                hook?.Invoke();

                if (Unreadable.Contains(path)) return MissionFileContent.Unreadable("Access was denied.");
                return Files.TryGetValue(path, out string text) ? MissionFileContent.Of(text) : MissionFileContent.Missing();
            }

            public bool Exists(string path)
            {
                return Files.ContainsKey(path);
            }

            public void WriteAtomically(string path, string text)
            {
                if (FailWritesWith != null) throw new MissionFileException(FailWritesWith);

                Files[path] = text;
                Writes++;
            }
        }

        /// <summary>The application around the editor: file dialogs that answer from a script, and an exit.</summary>
        sealed class FakeShell : IMissionEditorShell
        {
            public readonly Queue<string> OpenAnswers = new Queue<string>();
            public readonly Queue<SaveDestination> SaveAnswers = new Queue<SaveDestination>();
            public readonly List<string> SuggestedNames = new List<string>();
            public int OpenDialogs;
            public int SaveDialogs;
            public int Exits;

            public void ChooseFileToOpen(Action<string> chosen)
            {
                OpenDialogs++;
                Assert.IsTrue(OpenAnswers.Count > 0, "an Open dialog the test did not expect");
                chosen(OpenAnswers.Dequeue());
            }

            public void ChooseFileToSave(string suggestedFileName, Action<SaveDestination> chosen)
            {
                SaveDialogs++;
                SuggestedNames.Add(suggestedFileName);
                Assert.IsTrue(SaveAnswers.Count > 0, "a Save As dialog the test did not expect");
                chosen(SaveAnswers.Dequeue());
            }

            public void Exit()
            {
                Exits++;
            }
        }

        /// <summary>The author: answers every question from a script and remembers what was asked.</summary>
        sealed class FakePrompts : IMissionEditorPrompts
        {
            public readonly Queue<UnsavedChoice> Unsaved = new Queue<UnsavedChoice>();
            public readonly Queue<ExternalChangeChoice> External = new Queue<ExternalChangeChoice>();
            public readonly Queue<bool> DiscardInput = new Queue<bool>();
            public readonly Queue<bool> RemovePlayer = new Queue<bool>();
            public readonly Queue<bool> ReplaceFile = new Queue<bool>();

            public readonly List<LeaveReason> UnsavedAsked = new List<LeaveReason>();
            public readonly List<ExternalChange> ExternalAsked = new List<ExternalChange>();
            public readonly List<string> InvalidAsked = new List<string>();
            public readonly List<(string player, int nodes)> RemoveAsked = new List<(string, int)>();
            public readonly List<string> ReplaceAsked = new List<string>();
            public readonly List<(string title, string message)> Errors = new List<(string, string)>();

            /// <summary>When set, the next unsaved-changes question is held here instead of answered.</summary>
            public bool HoldUnsaved;
            public Action<UnsavedChoice> HeldUnsaved;

            public void AskUnsavedChanges(LeaveReason reason, string missionLabel, Action<UnsavedChoice> answer)
            {
                UnsavedAsked.Add(reason);
                if (HoldUnsaved)
                {
                    HeldUnsaved = answer;
                    return;
                }

                Assert.IsTrue(Unsaved.Count > 0, "an unsaved-changes question the test did not expect");
                answer(Unsaved.Dequeue());
            }

            public void AskExternalChange(string path, ExternalChange change, Action<ExternalChangeChoice> answer)
            {
                ExternalAsked.Add(change);
                Assert.IsTrue(External.Count > 0, "an external-change question the test did not expect");
                answer(External.Dequeue());
            }

            public void AskInvalidInput(string fields, Action<bool> discard)
            {
                InvalidAsked.Add(fields);
                Assert.IsTrue(DiscardInput.Count > 0, "an invalid-input question the test did not expect");
                discard(DiscardInput.Dequeue());
            }

            public void ConfirmRemovePlayer(string playerId, int ownedNodes, Action<bool> confirmed)
            {
                RemoveAsked.Add((playerId, ownedNodes));
                Assert.IsTrue(RemovePlayer.Count > 0, "a remove-player question the test did not expect");
                confirmed(RemovePlayer.Dequeue());
            }

            public void ConfirmReplaceFile(string path, Action<bool> confirmed)
            {
                ReplaceAsked.Add(path);
                Assert.IsTrue(ReplaceFile.Count > 0, "a replace-file question the test did not expect");
                confirmed(ReplaceFile.Dequeue());
            }

            public void ShowError(string title, string message, Action dismissed)
            {
                Errors.Add((title, message));
                dismissed();
            }
        }

        /// <summary>An editor with its stand-ins, and the author's gestures as one-line helpers.</summary>
        sealed class Editor
        {
            public readonly FakeFiles Files = new FakeFiles();
            public readonly FakeShell Shell = new FakeShell();
            public readonly FakePrompts Prompts = new FakePrompts();
            public readonly MissionEditorSession Session;
            public Func<MissionDefinition, string> Serialize = MissionJson.Write;
            public int Changes;
            int missions;

            public Editor()
            {
                Session = new MissionEditorSession(Files, Shell, Prompts, () => "mission-" + ++missions,
                    mission => Serialize(mission));
                Session.Changed += () => Changes++;
            }

            public MissionDocument Document => Session.Document;
            public MissionDefinition Mission => Session.Document.Mission;

            public string AddNode(double x, double y)
            {
                Session.ArmAddNode();
                Session.ClickCanvas(x, y);
                Assert.AreEqual(SelectionKind.Node, Session.Selection.Kind, "placing a node selects it");
                return Session.Selection.NodeId;
            }

            public void Connect(string a, string b)
            {
                Session.Select(Selection.None);
                Session.ArmAddConnection();
                Session.ClickNode(a);
                Session.ClickNode(b);
            }

            public void SaveAs(string path, bool confirmedInDialog = false)
            {
                Shell.SaveAnswers.Enqueue(new SaveDestination(path, confirmedInDialog));
                Session.SaveAs();
            }

            public void Open(string path)
            {
                Shell.OpenAnswers.Enqueue(path);
                Session.Open();
            }

            /// <summary>A saved mission with three nodes in a row, N1-N2-N3, connected in a chain.</summary>
            public static Editor WithSavedChain(string path = "chain.json")
            {
                var editor = new Editor();
                editor.AddNode(0, 0);
                editor.AddNode(3, 4);
                editor.AddNode(6, 8);
                editor.Connect("N1", "N2");
                editor.Connect("N2", "N3");
                editor.SaveAs(path);
                Assert.IsFalse(editor.Session.HasUnsavedChanges);
                return editor;
            }
        }

        static string[] NodeIds(MissionDefinition mission)
        {
            var ids = new List<string>();
            foreach (MissionNode node in mission.Nodes) ids.Add(node.Id);
            return ids.ToArray();
        }

        static string[] Routes(MissionDefinition mission)
        {
            var routes = new List<string>();
            foreach (MissionEdge edge in mission.Edges) routes.Add(edge.Source + ">" + edge.Target);
            return routes.ToArray();
        }

        // ------------------------------------------------------------ a new mission

        [Test]
        public void A_new_mission_is_empty_with_useful_defaults()
        {
            var editor = new Editor();
            MissionDefinition mission = editor.Mission;

            Assert.AreEqual("Untitled Mission", mission.Name);
            Assert.AreEqual(1d, mission.DistanceScale);
            Assert.AreEqual(0, mission.Nodes.Count);
            Assert.AreEqual(0, mission.Edges.Count);
            Assert.AreEqual(2, mission.Players.Count);
            Assert.AreEqual("player1", mission.Players[0].Id);
            Assert.AreEqual(PlayerColor.Blue, mission.Players[0].Color);
            Assert.AreEqual("player2", mission.Players[1].Id);
            Assert.AreEqual(PlayerColor.Red, mission.Players[1].Color);
            Assert.AreEqual(0, editor.Session.Validation.Issues.Count);
            Assert.AreEqual(SelectionKind.None, editor.Session.Selection.Kind);
            Assert.AreEqual(EditorTool.Select, editor.Session.Tool);
        }

        [Test]
        public void A_generated_mission_ID_is_valid_and_different_every_time()
        {
            string first = MissionDocument.GenerateMissionId();
            string second = MissionDocument.GenerateMissionId();

            Assert.IsTrue(MissionRules.IsValidId(first));
            Assert.AreNotEqual(first, second);
            Assert.IsTrue(MissionRules.IsValidId(MissionDocument.CreateNew(first).Mission.Id));
        }

        [Test]
        public void A_new_mission_has_nothing_to_save_until_it_is_edited()
        {
            var editor = new Editor();

            Assert.IsNull(editor.Session.FilePath);
            Assert.IsFalse(editor.Session.HasUnsavedChanges, "an untouched new mission is not unsaved work");

            // Looking around is not editing.
            editor.Session.ArmAddNode();
            editor.Session.CancelTool();
            editor.Session.ShowDistances = true;
            Assert.IsFalse(editor.Session.HasUnsavedChanges);

            // Nothing to lose, so nothing is asked: not for New, not for Open, not for closing.
            editor.Session.New();
            editor.Shell.OpenAnswers.Enqueue(null);
            editor.Session.Open();
            editor.Session.RequestQuit();
            Assert.AreEqual(0, editor.Prompts.UnsavedAsked.Count);
            Assert.AreEqual(1, editor.Shell.Exits);

            // The first change makes it work worth asking about.
            editor = new Editor();
            editor.Document.SetName("Draft");
            Assert.IsTrue(editor.Session.HasUnsavedChanges);
            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Cancel);
            editor.Session.RequestQuit();
            Assert.AreEqual(1, editor.Prompts.UnsavedAsked.Count);
            Assert.AreEqual(0, editor.Shell.Exits);
            Assert.AreEqual("Draft", editor.Mission.Name);
        }

        [Test]
        public void Save_without_a_file_asks_where_and_then_keeps_that_file()
        {
            var editor = new Editor();
            editor.AddNode(1, 2);
            editor.Shell.SaveAnswers.Enqueue(new SaveDestination("first.json", false));

            editor.Session.Save();

            Assert.AreEqual(1, editor.Shell.SaveDialogs);
            Assert.AreEqual("mission-1.json", editor.Shell.SuggestedNames[0], "the mission ID is offered as the file name");
            Assert.AreEqual("first.json", editor.Session.FilePath);
            Assert.IsFalse(editor.Session.HasUnsavedChanges);
            Assert.IsTrue(editor.Files.Files.ContainsKey("first.json"));

            editor.Document.SetName("Second draft");
            editor.Session.Save();

            Assert.AreEqual(1, editor.Shell.SaveDialogs, "a mission with a file is saved there without asking");
            Assert.AreEqual(2, editor.Files.Writes);
            Assert.AreEqual("Second draft", MissionJson.Read(editor.Files.Files["first.json"]).Mission.Name);
        }

        [Test]
        public void Mission_ID_name_and_distance_scale_can_be_edited()
        {
            var editor = new Editor();

            Assert.IsTrue(editor.Document.SetMissionId("outer-rim_01").Applied);
            Assert.IsTrue(editor.Document.SetName("Outer Rim").Applied);
            Assert.IsTrue(editor.Document.SetDistanceScale(0.25).Applied);

            Assert.AreEqual("outer-rim_01", editor.Mission.Id);
            Assert.AreEqual("Outer Rim", editor.Mission.Name);
            Assert.AreEqual(0.25, editor.Mission.DistanceScale);

            Assert.IsTrue(editor.Document.SetMissionId("not valid").Rejected);
            Assert.IsTrue(editor.Document.SetMissionId("").Rejected);
            Assert.IsTrue(editor.Document.SetDistanceScale(0).Rejected);
            Assert.IsTrue(editor.Document.SetDistanceScale(-1).Rejected);
            Assert.IsTrue(editor.Document.SetDistanceScale(double.NaN).Rejected);
            Assert.IsTrue(editor.Document.SetDistanceScale(double.PositiveInfinity).Rejected);
            Assert.AreEqual("outer-rim_01", editor.Mission.Id);
            Assert.AreEqual(0.25, editor.Mission.DistanceScale);
        }

        [Test]
        public void Renaming_the_mission_ID_does_not_rename_its_file()
        {
            Editor editor = Editor.WithSavedChain("chain.json");

            editor.Document.SetMissionId("renamed");
            editor.Session.Save();

            Assert.AreEqual("chain.json", editor.Session.FilePath);
            Assert.AreEqual(0, editor.Shell.SaveDialogs - 1, "only the first Save As asked for a file");
            Assert.AreEqual("renamed", MissionJson.Read(editor.Files.Files["chain.json"]).Mission.Id);
        }

        // ------------------------------------------------------------ nodes

        [Test]
        public void A_new_node_is_a_neutral_planet_without_Darts_where_it_was_placed()
        {
            var editor = new Editor();

            string id = editor.AddNode(-2.5, 7.25);

            MissionNode node = editor.Mission.FindNode(id);
            Assert.AreEqual("N1", node.Id);
            Assert.AreEqual(MissionNodeType.Planet, node.Type);
            Assert.IsNull(node.Owner);
            Assert.AreEqual(0, node.Darts);
            Assert.AreEqual(-2.5, node.X);
            Assert.AreEqual(7.25, node.Y);
        }

        [Test]
        public void Node_IDs_count_up_and_survivors_keep_theirs_when_a_node_is_deleted()
        {
            var editor = new Editor();
            editor.AddNode(0, 0);
            editor.AddNode(1, 0);
            editor.AddNode(2, 0);
            CollectionAssert.AreEqual(new[] { "N1", "N2", "N3" }, NodeIds(editor.Mission));

            editor.Session.Select(Selection.Node("N2"));
            editor.Session.DeleteSelected();
            CollectionAssert.AreEqual(new[] { "N1", "N3" }, NodeIds(editor.Mission), "nothing is renumbered");

            Assert.AreEqual("N4", editor.AddNode(3, 0));

            editor.Session.Select(Selection.Node("N4"));
            editor.Session.DeleteSelected();
            Assert.AreEqual("N5", editor.AddNode(4, 0), "a deleted ID is not handed out again while the mission is open");
            CollectionAssert.AreEqual(new[] { "N1", "N3", "N5" }, NodeIds(editor.Mission));
        }

        [Test]
        public void Moving_and_reconfiguring_a_node_never_changes_its_ID_or_its_routes()
        {
            Editor editor = Editor.WithSavedChain();

            editor.Document.MoveNode("N2", -9, 9);
            editor.Document.SetNodeType("N2", MissionNodeType.Moon);
            editor.Document.SetNodeOwner("N2", "player2");
            editor.Document.SetNodeDarts("N2", 40);

            CollectionAssert.AreEqual(new[] { "N1", "N2", "N3" }, NodeIds(editor.Mission));
            CollectionAssert.AreEqual(new[] { "N1>N2", "N2>N3" }, Routes(editor.Mission));
        }

        [Test]
        public void Nodes_added_to_a_reopened_mission_never_collide_with_its_IDs()
        {
            // Imported IDs that do not follow the N-number convention, and a gap in those that do.
            var mission = new MissionDefinition { Id = "imported", Name = "Imported" };
            foreach (string id in new[] { "alpha", "N7", "n9", "N007", "N2", "Nx", "N12b" })
                mission.Nodes.Add(new MissionNode { Id = id, X = mission.Nodes.Count, Y = 0 });
            var editor = new Editor();
            editor.Files.Files["imported.json"] = MissionJson.Write(mission);
            editor.Open("imported.json");

            CollectionAssert.AreEqual(new[] { "alpha", "N7", "n9", "N007", "N2", "Nx", "N12b" }, NodeIds(editor.Mission),
                "valid imported IDs are kept as they are");

            Assert.AreEqual("N8", editor.AddNode(0, 5), "one above the highest N-number in the file");
            Assert.AreEqual("N9", editor.AddNode(0, 6), "n9 is a different ID, so N9 is free");
            Assert.AreEqual("N10", editor.AddNode(0, 7));
            Assert.IsFalse(editor.Session.Validation.HasErrors);
        }

        [Test]
        public void A_node_can_be_dragged_or_given_exact_fractional_and_negative_coordinates()
        {
            var editor = new Editor();
            string id = editor.AddNode(0, 0);

            Assert.IsTrue(editor.Document.MoveNode(id, -12.375, 0.1 + 0.2).Applied);

            MissionNode node = editor.Mission.FindNode(id);
            Assert.AreEqual(-12.375, node.X);
            Assert.AreEqual(0.1 + 0.2, node.Y, "the coordinate is kept exactly, not as displayed");

            Assert.IsTrue(editor.Document.MoveNode(id, double.NaN, 0).Rejected);
            Assert.IsTrue(editor.Document.MoveNode(id, 0, double.NegativeInfinity).Rejected);
            Assert.IsTrue(editor.Document.MoveNode(id, 2e9, 0).Rejected);
            Assert.AreEqual(-12.375, node.X, "a refused move changes nothing");
        }

        [Test]
        public void Type_owner_and_Darts_of_a_node_can_be_changed()
        {
            var editor = new Editor();
            string id = editor.AddNode(0, 0);

            Assert.IsTrue(editor.Document.SetNodeType(id, MissionNodeType.Moon).Applied);
            Assert.IsTrue(editor.Document.SetNodeOwner(id, "player2").Applied);
            Assert.IsTrue(editor.Document.SetNodeDarts(id, int.MaxValue).Applied);

            MissionNode node = editor.Mission.FindNode(id);
            Assert.AreEqual(MissionNodeType.Moon, node.Type);
            Assert.AreEqual("player2", node.Owner);
            Assert.AreEqual(int.MaxValue, node.Darts);

            Assert.IsTrue(editor.Document.SetNodeOwner(id, "nobody").Rejected, "an owner must be a player");
            Assert.IsTrue(editor.Document.SetNodeOwner(id, "Player2").Rejected, "player IDs are case-sensitive");
            Assert.IsTrue(editor.Document.SetNodeDarts(id, -1).Rejected);
            Assert.AreEqual("player2", node.Owner);
            Assert.AreEqual(int.MaxValue, node.Darts);

            Assert.IsTrue(editor.Document.SetNodeOwner(id, null).Applied);
            Assert.IsNull(node.Owner, "no owner is neutral");
        }

        // ------------------------------------------------------------ routes

        [Test]
        public void Two_nodes_are_connected_by_picking_one_and_then_the_other()
        {
            var editor = new Editor();
            editor.AddNode(0, 0);
            editor.AddNode(5, 0);
            editor.Session.Select(Selection.None);

            editor.Session.ArmAddConnection();
            Assert.AreEqual(EditorTool.AddConnection, editor.Session.Tool);
            Assert.IsNull(editor.Session.ConnectionSource, "with nothing selected both ends are picked");
            editor.Session.ClickNode("N2");
            Assert.AreEqual("N2", editor.Session.ConnectionSource);
            Assert.AreEqual(0, editor.Mission.Edges.Count);

            editor.Session.ClickNode("N1");

            CollectionAssert.AreEqual(new[] { "N2>N1" }, Routes(editor.Mission));
            Assert.AreEqual(EditorTool.Select, editor.Session.Tool, "the tool is done after one connection");
            Assert.IsNull(editor.Session.ConnectionSource);
            Assert.AreEqual(Selection.Edge("N1", "N2"), editor.Session.Selection, "the new route is selected");
        }

        [Test]
        public void A_node_cannot_be_connected_to_itself()
        {
            var editor = new Editor();
            editor.AddNode(0, 0);
            editor.AddNode(5, 0);
            editor.Session.Select(Selection.None);
            editor.Session.ArmAddConnection();
            editor.Session.ClickNode("N1");

            editor.Session.ClickNode("N1");

            Assert.AreEqual(0, editor.Mission.Edges.Count);
            StringAssert.Contains("itself", editor.Session.Status);
            Assert.AreEqual("N1", editor.Session.ConnectionSource, "the connection is still waiting for a second node");

            editor.Session.ClickNode("N2");
            Assert.AreEqual(1, editor.Mission.Edges.Count);
            Assert.IsTrue(editor.Document.Connect("N2", "N2").Rejected);
        }

        [Test]
        public void Add_Connection_starts_from_the_node_that_is_already_selected()
        {
            var editor = new Editor();
            editor.AddNode(0, 0);
            editor.AddNode(5, 0);
            editor.AddNode(9, 0);
            editor.Session.ClickNode("N2");

            editor.Session.ArmAddConnection();

            Assert.AreEqual("N2", editor.Session.ConnectionSource, "the selected node is the first end");
            Assert.IsTrue(editor.Session.Selection.IsNode("N2"));
            Assert.AreEqual(0, editor.Mission.Edges.Count);

            editor.Session.ClickNode("N3");

            CollectionAssert.AreEqual(new[] { "N2>N3" }, Routes(editor.Mission), "one click finishes the route");
            Assert.AreEqual(EditorTool.Select, editor.Session.Tool);
            Assert.IsNull(editor.Session.ConnectionSource);

            // A selected route gives no starting node: both ends are picked, as with nothing selected.
            Assert.AreEqual(SelectionKind.Edge, editor.Session.Selection.Kind);
            editor.Session.ArmAddConnection();
            Assert.IsNull(editor.Session.ConnectionSource);
            editor.Session.ClickNode("N1");
            editor.Session.ClickNode("N2");
            Assert.AreEqual(2, editor.Mission.Edges.Count);

            // Pressing the button again, or Escape, drops the start that was taken from the selection.
            editor.Session.ClickNode("N1");
            editor.Session.ArmAddConnection();
            Assert.AreEqual("N1", editor.Session.ConnectionSource);
            editor.Session.ArmAddConnection();
            Assert.AreEqual(EditorTool.Select, editor.Session.Tool);
            Assert.IsNull(editor.Session.ConnectionSource);

            editor.Session.ArmAddConnection();
            Assert.IsTrue(editor.Session.CancelTool());
            Assert.IsNull(editor.Session.ConnectionSource);
            editor.Session.ClickNode("N3");
            Assert.AreEqual(2, editor.Mission.Edges.Count, "nothing is connected once the tool is put away");
        }

        [Test]
        public void A_connection_from_the_selected_node_still_refuses_itself_and_duplicates()
        {
            var editor = new Editor();
            editor.AddNode(0, 0);
            editor.AddNode(5, 0);
            editor.Connect("N1", "N2");
            editor.Session.ClickNode("N1");
            editor.Session.ArmAddConnection();

            editor.Session.ClickNode("N1");
            StringAssert.Contains("itself", editor.Session.Status);
            editor.Session.ClickNode("N2");
            StringAssert.Contains("already connected", editor.Session.Status);

            Assert.AreEqual(1, editor.Mission.Edges.Count);
            Assert.AreEqual("N1", editor.Session.ConnectionSource, "the connection is still waiting for a node it can take");
        }

        [Test]
        public void Two_nodes_cannot_be_connected_twice_in_either_order()
        {
            var editor = new Editor();
            editor.AddNode(0, 0);
            editor.AddNode(5, 0);
            editor.Connect("N1", "N2");
            int revision = editor.Document.Revision;

            editor.Connect("N1", "N2");
            StringAssert.Contains("already connected", editor.Session.Status);
            editor.Session.CancelTool();
            editor.Connect("N2", "N1");

            Assert.AreEqual(1, editor.Mission.Edges.Count);
            Assert.AreEqual(revision, editor.Document.Revision, "a refused connection is not an edit");
            Assert.IsTrue(editor.Document.Connect("N2", "N1").Rejected);
            Assert.IsTrue(editor.Document.Connect("N1", "N9").Rejected, "both ends must exist");
            Assert.IsFalse(editor.Session.Validation.HasErrors);
        }

        [Test]
        public void Deleting_a_route_removes_only_that_route()
        {
            Editor editor = Editor.WithSavedChain();

            editor.Session.ClickEdge("N2", "N1");
            Assert.AreEqual(Selection.Edge("N1", "N2"), editor.Session.Selection, "a route is the same from either end");
            Assert.IsTrue(editor.Session.DeleteSelected());

            CollectionAssert.AreEqual(new[] { "N2>N3" }, Routes(editor.Mission));
            CollectionAssert.AreEqual(new[] { "N1", "N2", "N3" }, NodeIds(editor.Mission));
            Assert.AreEqual(SelectionKind.None, editor.Session.Selection.Kind);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);
        }

        [Test]
        public void Deleting_a_node_removes_its_routes_with_it_as_one_edit()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Connect("N1", "N3");
            int revision = editor.Document.Revision;

            editor.Session.Select(Selection.Node("N2"));
            Assert.IsTrue(editor.Session.DeleteSelected());

            CollectionAssert.AreEqual(new[] { "N1", "N3" }, NodeIds(editor.Mission));
            CollectionAssert.AreEqual(new[] { "N1>N3" }, Routes(editor.Mission), "only routes that ended at N2 are gone");
            Assert.AreEqual(revision + 1, editor.Document.Revision);
            Assert.AreEqual(SelectionKind.None, editor.Session.Selection.Kind);
            Assert.IsFalse(editor.Session.Validation.HasErrors, "no route is left pointing at the deleted node");
        }

        [Test]
        public void Deleting_with_nothing_selected_changes_nothing()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Session.Select(Selection.None);

            Assert.IsFalse(editor.Session.DeleteSelected());

            Assert.AreEqual(3, editor.Mission.Nodes.Count);
            Assert.IsFalse(editor.Session.HasUnsavedChanges);
        }

        // ------------------------------------------------------------ tools and selection

        [Test]
        public void Add_Node_places_one_node_at_the_next_canvas_click_and_then_stands_down()
        {
            var editor = new Editor();

            editor.Session.ClickCanvas(1, 1);
            Assert.AreEqual(0, editor.Mission.Nodes.Count, "a click without the tool places nothing");

            editor.Session.ArmAddNode();
            Assert.AreEqual(EditorTool.AddNode, editor.Session.Tool);
            editor.Session.ClickCanvas(4, -4);
            Assert.AreEqual(1, editor.Mission.Nodes.Count);
            Assert.AreEqual(EditorTool.Select, editor.Session.Tool);

            editor.Session.ClickCanvas(8, 8);
            Assert.AreEqual(1, editor.Mission.Nodes.Count, "the tool placed its one node");
            Assert.AreEqual(SelectionKind.None, editor.Session.Selection.Kind, "a click on empty space selects nothing");
        }

        [Test]
        public void Escape_cancels_a_pending_tool_or_connection()
        {
            var editor = new Editor();
            editor.AddNode(0, 0);
            editor.AddNode(5, 0);

            Assert.IsFalse(editor.Session.CancelTool(), "nothing is pending");

            editor.Session.ArmAddNode();
            Assert.IsTrue(editor.Session.CancelTool());
            editor.Session.ClickCanvas(9, 9);
            Assert.AreEqual(2, editor.Mission.Nodes.Count, "the cancelled tool places nothing");

            editor.Session.ArmAddConnection();
            editor.Session.ClickNode("N1");
            Assert.IsTrue(editor.Session.CancelTool());
            Assert.IsNull(editor.Session.ConnectionSource);
            editor.Session.ClickNode("N2");
            Assert.AreEqual(0, editor.Mission.Edges.Count, "the cancelled connection is not completed");
            Assert.AreEqual(Selection.Node("N2"), editor.Session.Selection, "the click selects again");
        }

        [Test]
        public void Pressing_a_tool_button_again_disarms_it()
        {
            var editor = new Editor();

            editor.Session.ArmAddNode();
            editor.Session.ArmAddNode();
            Assert.AreEqual(EditorTool.Select, editor.Session.Tool);

            editor.Session.ArmAddConnection();
            editor.Session.ArmAddNode();
            Assert.AreEqual(EditorTool.AddNode, editor.Session.Tool, "arming one tool replaces the other");
        }

        [Test]
        public void One_object_is_selected_at_a_time()
        {
            Editor editor = Editor.WithSavedChain();

            editor.Session.ClickNode("N1");
            Assert.IsTrue(editor.Session.Selection.IsNode("N1"));

            editor.Session.ClickEdge("N2", "N3");
            Assert.AreEqual(SelectionKind.Edge, editor.Session.Selection.Kind);
            Assert.IsFalse(editor.Session.Selection.IsNode("N1"));

            editor.Session.Select(Selection.Node("N404"));
            Assert.AreEqual(SelectionKind.None, editor.Session.Selection.Kind, "there is no such node to select");
            Assert.IsFalse(editor.Session.HasUnsavedChanges, "selecting is not an edit");
        }

        // ------------------------------------------------------------ players

        [Test]
        public void Up_to_six_players_each_get_a_color_of_their_own()
        {
            var editor = new Editor();
            CollectionAssert.AreEqual(
                new[] { PlayerColor.Green, PlayerColor.Yellow, PlayerColor.Purple, PlayerColor.Orange },
                editor.Document.AvailableColors());

            for (int i = 3; i <= 6; i++)
            {
                Assert.IsTrue(editor.Document.CanAddPlayer);
                Assert.IsTrue(editor.Document.AddPlayer("p" + i, editor.Document.AvailableColors()[0]).Applied);
            }

            Assert.AreEqual(6, editor.Mission.Players.Count);
            var used = new HashSet<PlayerColor>();
            foreach (MissionPlayer player in editor.Mission.Players) Assert.IsTrue(used.Add(player.Color), "colors are distinct");
            Assert.AreEqual(0, editor.Document.AvailableColors().Count);

            Assert.IsFalse(editor.Document.CanAddPlayer, "Add Player is unavailable at six");
            Assert.IsTrue(editor.Document.AddPlayer("p7", PlayerColor.Blue).Rejected);
            Assert.AreEqual(6, editor.Mission.Players.Count);
            Assert.IsFalse(editor.Session.Validation.HasErrors);
        }

        [Test]
        public void A_player_needs_a_valid_ID_that_no_other_player_has()
        {
            var editor = new Editor();

            Assert.IsTrue(editor.Document.AddPlayer("", PlayerColor.Green).Rejected);
            Assert.IsTrue(editor.Document.AddPlayer("two words", PlayerColor.Green).Rejected);
            Assert.IsTrue(editor.Document.AddPlayer("player1", PlayerColor.Green).Rejected);
            Assert.IsTrue(editor.Document.AddPlayer("green", PlayerColor.Blue).Rejected, "blue is taken");
            Assert.AreEqual(2, editor.Mission.Players.Count);

            Assert.IsTrue(editor.Document.AddPlayer("Player1", PlayerColor.Green).Applied, "IDs are case-sensitive");
            editor.AddNode(0, 0);
            Assert.IsTrue(editor.Document.AddPlayer("N1", PlayerColor.Yellow).Applied, "a player may be named like a node");
            Assert.IsFalse(editor.Session.Validation.HasErrors);
        }

        [Test]
        public void Changing_a_players_color_recolors_all_its_nodes_at_once()
        {
            var editor = new Editor();
            editor.AddNode(0, 0);
            editor.AddNode(1, 0);
            editor.AddNode(2, 0);
            editor.Document.SetNodeOwner("N1", "player1");
            editor.Document.SetNodeOwner("N3", "player1");
            editor.Document.SetNodeOwner("N2", "player2");

            CollectionAssert.AreEqual(
                new[] { PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow, PlayerColor.Purple, PlayerColor.Orange },
                editor.Document.AvailableColors("player1"), "the unused colors, and the player's own");

            Assert.IsTrue(editor.Document.SetPlayerColor("player1", PlayerColor.Purple).Applied);

            // A node holds its owner, never a color, so every owned node follows the player.
            foreach (string id in new[] { "N1", "N3" })
                Assert.AreEqual(PlayerColor.Purple, editor.Mission.FindPlayer(editor.Mission.FindNode(id).Owner).Color);
            Assert.AreEqual(PlayerColor.Red, editor.Mission.FindPlayer(editor.Mission.FindNode("N2").Owner).Color);

            Assert.IsTrue(editor.Document.SetPlayerColor("player1", PlayerColor.Red).Rejected, "red belongs to player2");
            Assert.AreEqual(PlayerColor.Purple, editor.Mission.FindPlayer("player1").Color);
            CollectionAssert.Contains(editor.Document.AvailableColors(), PlayerColor.Blue, "blue is free again");
        }

        [Test]
        public void Removing_a_player_is_confirmed_first_and_cancelling_changes_nothing()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetNodeOwner("N1", "player1");
            editor.Document.SetNodeOwner("N2", "player1");
            editor.Document.SetNodeOwner("N3", "player2");
            editor.Session.Save();
            editor.Prompts.RemovePlayer.Enqueue(false);

            editor.Session.RequestRemovePlayer("player1");

            Assert.AreEqual(("player1", 2), editor.Prompts.RemoveAsked[0], "the question names the player and its node count");
            Assert.AreEqual(2, editor.Mission.Players.Count);
            Assert.AreEqual("player1", editor.Mission.FindNode("N1").Owner);
            Assert.IsFalse(editor.Session.HasUnsavedChanges);
        }

        [Test]
        public void A_removed_players_nodes_become_neutral_and_keep_everything_else()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetNodeOwner("N1", "player1");
            editor.Document.SetNodeOwner("N2", "player1");
            editor.Document.SetNodeOwner("N3", "player2");
            editor.Document.SetNodeType("N2", MissionNodeType.Moon);
            editor.Document.SetNodeDarts("N2", 25);
            editor.Prompts.RemovePlayer.Enqueue(true);

            editor.Session.RequestRemovePlayer("player1");

            Assert.AreEqual(1, editor.Mission.Players.Count);
            Assert.IsNull(editor.Mission.FindPlayer("player1"));
            Assert.IsNull(editor.Mission.FindNode("N1").Owner);
            MissionNode moon = editor.Mission.FindNode("N2");
            Assert.IsNull(moon.Owner);
            Assert.AreEqual(MissionNodeType.Moon, moon.Type);
            Assert.AreEqual(25, moon.Darts);
            Assert.AreEqual(3d, moon.X);
            Assert.AreEqual(4d, moon.Y);
            Assert.AreEqual("player2", editor.Mission.FindNode("N3").Owner, "other players keep their nodes");
            CollectionAssert.AreEqual(new[] { "N1>N2", "N2>N3" }, Routes(editor.Mission));
            Assert.IsFalse(editor.Session.Validation.HasErrors);

            CollectionAssert.Contains(editor.Document.AvailableColors(), PlayerColor.Blue, "the color can be used again");
            Assert.IsTrue(editor.Document.AddPlayer("newcomer", PlayerColor.Blue).Applied);
        }

        [Test]
        public void A_player_without_nodes_is_removed_without_a_question()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetNodeOwner("N1", "player2");
            editor.Session.Save();

            editor.Session.RequestRemovePlayer("player1");

            Assert.AreEqual(0, editor.Prompts.RemoveAsked.Count, "no node changes, so there is nothing to confirm");
            Assert.IsNull(editor.Mission.FindPlayer("player1"));
            Assert.AreEqual(1, editor.Mission.Players.Count);
            Assert.AreEqual("player2", editor.Mission.FindNode("N1").Owner, "other players and their nodes are untouched");
            Assert.IsTrue(editor.Session.HasUnsavedChanges, "removing a player is an edit");
            Assert.IsFalse(editor.Session.Busy);
            CollectionAssert.Contains(editor.Document.AvailableColors(), PlayerColor.Blue, "the color can be used again");

            // The player that does own a node is still asked about.
            editor.Prompts.RemovePlayer.Enqueue(false);
            editor.Session.RequestRemovePlayer("player2");
            Assert.AreEqual(("player2", 1), editor.Prompts.RemoveAsked[0]);
            Assert.AreEqual(1, editor.Mission.Players.Count);

            // Once its node is neutral there is nothing left to ask.
            editor.Document.SetNodeOwner("N1", null);
            editor.Session.RequestRemovePlayer("player2");
            Assert.AreEqual(1, editor.Prompts.RemoveAsked.Count);
            Assert.AreEqual(0, editor.Mission.Players.Count);

            editor.Session.RequestRemovePlayer("nobody");
            Assert.AreEqual(1, editor.Prompts.RemoveAsked.Count, "there is no such player to remove");
        }

        [Test]
        public void A_mission_may_have_no_players_at_all()
        {
            var editor = new Editor();
            editor.AddNode(0, 0);

            editor.Session.RequestRemovePlayer("player1");
            editor.Session.RequestRemovePlayer("player2");

            Assert.AreEqual(0, editor.Mission.Players.Count);
            Assert.IsFalse(editor.Session.Validation.HasErrors);
            editor.SaveAs("nobody.json");
            Assert.AreEqual(0, MissionJson.Read(editor.Files.Files["nobody.json"]).Mission.Players.Count);
        }

        [Test]
        public void Owners_are_offered_by_name_whatever_order_the_players_were_added_in()
        {
            var editor = new Editor();
            CollectionAssert.AreEqual(new[] { "player1", "player2" }, editor.Document.PlayerIdsByName());

            // Remove the first player and add it again: it is last in the mission, first in the list.
            editor.Session.RequestRemovePlayer("player1");
            editor.Document.AddPlayer("player1", PlayerColor.Blue);
            editor.Document.AddPlayer("Zed", PlayerColor.Green);
            editor.Document.AddPlayer("alpha", PlayerColor.Yellow);
            editor.Document.AddPlayer("Alpha", PlayerColor.Purple);

            CollectionAssert.AreEqual(new[] { "Alpha", "alpha", "player1", "player2", "Zed" },
                editor.Document.PlayerIdsByName());

            // The mission, and so the file, keeps the order the players were added in.
            var added = new List<string>();
            foreach (MissionPlayer player in editor.Mission.Players) added.Add(player.Id);
            CollectionAssert.AreEqual(new[] { "player2", "player1", "Zed", "alpha", "Alpha" }, added);
        }

        // ------------------------------------------------------------ distances

        [Test]
        public void A_routes_distance_follows_its_nodes_and_the_scale()
        {
            var editor = new Editor();
            editor.AddNode(0, 0);
            editor.AddNode(3, 4);
            editor.Connect("N1", "N2");
            MissionEdge route = editor.Mission.Edges[0];

            Assert.AreEqual(5d, editor.Mission.MapDistance(route));
            Assert.AreEqual(5d, editor.Mission.GameDistance(route));

            editor.Document.SetDistanceScale(2.5);
            Assert.AreEqual(12.5, editor.Mission.GameDistance(route));

            editor.Document.MoveNode("N2", 6, 8);
            Assert.AreEqual(25d, editor.Mission.GameDistance(route), "moving the far end");

            editor.Document.MoveNode("N1", 6, 0);
            Assert.AreEqual(20d, editor.Mission.GameDistance(route), "moving the near end");
            Assert.AreEqual("20", MissionFields.FormatDistance(editor.Mission.GameDistance(route)));
            Assert.AreEqual("0.333", MissionFields.FormatDistance(1d / 3d), "labels may round; the value does not");
        }

        [Test]
        public void Showing_distance_labels_is_a_view_setting_and_not_an_edit()
        {
            Editor editor = Editor.WithSavedChain();
            string before = MissionJson.Write(editor.Mission);
            int changes = editor.Changes;

            editor.Session.ShowDistances = true;

            Assert.IsTrue(editor.Session.ShowDistances);
            Assert.Greater(editor.Changes, changes, "the canvas is told to redraw");
            Assert.IsFalse(editor.Session.HasUnsavedChanges);
            Assert.AreEqual(before, MissionJson.Write(editor.Mission));
            Assert.AreEqual(5d, editor.Mission.GameDistance(editor.Mission.Edges[0]));
        }

        // ------------------------------------------------------------ unsaved changes

        [Test]
        public void Every_real_edit_makes_the_mission_unsaved_and_a_no_op_does_not()
        {
            Editor editor = Editor.WithSavedChain();
            MissionDocument document = editor.Document;

            // Assigning what is already there.
            Assert.AreEqual(EditOutcome.Unchanged, document.SetMissionId(editor.Mission.Id).Outcome);
            Assert.AreEqual(EditOutcome.Unchanged, document.SetName("Untitled Mission").Outcome);
            Assert.AreEqual(EditOutcome.Unchanged, document.SetDistanceScale(1).Outcome);
            Assert.AreEqual(EditOutcome.Unchanged, document.MoveNode("N2", 3, 4).Outcome);
            Assert.AreEqual(EditOutcome.Unchanged, document.SetNodeType("N2", MissionNodeType.Planet).Outcome);
            Assert.AreEqual(EditOutcome.Unchanged, document.SetNodeOwner("N2", null).Outcome);
            Assert.AreEqual(EditOutcome.Unchanged, document.SetNodeDarts("N2", 0).Outcome);
            Assert.AreEqual(EditOutcome.Unchanged, document.SetPlayerColor("player1", PlayerColor.Blue).Outcome);
            Assert.IsFalse(editor.Session.HasUnsavedChanges);

            var edits = new Action[]
            {
                () => document.SetName("Renamed"),
                () => document.SetDistanceScale(3),
                () => document.MoveNode("N2", 3, 4.5),
                () => document.SetNodeType("N2", MissionNodeType.Moon),
                () => document.SetNodeOwner("N2", "player1"),
                () => document.SetNodeDarts("N2", 1),
                () => document.SetPlayerColor("player1", PlayerColor.Green),
                () => document.AddPlayer("p3", PlayerColor.Blue),
                () => document.Disconnect("N1", "N2"),
                () => document.Connect("N1", "N3"),
                () => document.RemoveNode("N3"),
                () => document.AddNode(9, 9),
            };

            foreach (Action edit in edits)
            {
                edit();
                Assert.IsTrue(editor.Session.HasUnsavedChanges);
                editor.Session.Save();
                Assert.IsFalse(editor.Session.HasUnsavedChanges);
            }
        }

        [Test]
        public void New_with_unsaved_work_offers_Save_Discard_and_Cancel()
        {
            // Cancel keeps everything.
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetName("Edited");
            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Cancel);
            editor.Session.New();
            Assert.AreEqual(LeaveReason.NewMission, editor.Prompts.UnsavedAsked[0]);
            Assert.AreEqual("Edited", editor.Mission.Name);
            Assert.AreEqual("chain.json", editor.Session.FilePath);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);
            Assert.AreEqual(1, editor.Files.Writes);

            // Save writes the work, then starts the new mission.
            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Save);
            editor.Session.New();
            Assert.AreEqual("Edited", MissionJson.Read(editor.Files.Files["chain.json"]).Mission.Name);
            Assert.AreEqual("Untitled Mission", editor.Mission.Name);
            Assert.AreEqual(0, editor.Mission.Nodes.Count);
            Assert.IsNull(editor.Session.FilePath);
            Assert.AreNotEqual("mission-1", editor.Mission.Id, "a new mission gets an ID of its own");

            // Discard starts the new mission without writing.
            editor = Editor.WithSavedChain();
            editor.Document.SetName("Thrown away");
            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Discard);
            editor.Session.New();
            Assert.AreEqual("Untitled Mission", editor.Mission.Name);
            Assert.AreEqual("Untitled Mission", MissionJson.Read(editor.Files.Files["chain.json"]).Mission.Name);
            Assert.AreEqual(1, editor.Files.Writes);
        }

        [Test]
        public void New_without_unsaved_work_asks_nothing()
        {
            Editor editor = Editor.WithSavedChain();

            editor.Session.New();

            Assert.AreEqual(0, editor.Prompts.UnsavedAsked.Count);
            Assert.AreEqual(0, editor.Mission.Nodes.Count);
            Assert.IsNull(editor.Session.FilePath);
            Assert.IsFalse(editor.Session.HasUnsavedChanges, "an untouched new mission has nothing to save");
        }

        [Test]
        public void Quit_and_the_windows_close_button_protect_unsaved_work()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Session.RequestQuit();
            Assert.AreEqual(1, editor.Shell.Exits, "nothing unsaved: the editor just closes");
            Assert.AreEqual(0, editor.Prompts.UnsavedAsked.Count);

            editor = Editor.WithSavedChain();
            editor.Document.SetName("Edited");

            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Cancel);
            editor.Session.RequestQuit();
            Assert.AreEqual(LeaveReason.Quit, editor.Prompts.UnsavedAsked[0]);
            Assert.AreEqual(0, editor.Shell.Exits);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);

            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Save);
            editor.Session.RequestQuit();
            Assert.AreEqual(1, editor.Shell.Exits);
            Assert.AreEqual("Edited", MissionJson.Read(editor.Files.Files["chain.json"]).Mission.Name);

            editor = Editor.WithSavedChain();
            editor.Document.SetName("Thrown away");
            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Discard);
            editor.Session.RequestQuit();
            Assert.AreEqual(1, editor.Shell.Exits);
            Assert.AreEqual(1, editor.Files.Writes, "Discard writes nothing");
        }

        [Test]
        public void An_edited_mission_without_a_file_is_offered_for_saving_before_quitting()
        {
            var editor = new Editor();
            editor.AddNode(0, 0);
            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Save);
            editor.Shell.SaveAnswers.Enqueue(new SaveDestination("kept.json", false));

            editor.Session.RequestQuit();

            Assert.AreEqual(1, editor.Shell.Exits);
            Assert.IsTrue(editor.Files.Files.ContainsKey("kept.json"));
        }

        [Test]
        public void Choosing_Save_only_goes_on_when_the_save_succeeds()
        {
            // The Save As dialog is cancelled.
            var editor = new Editor();
            editor.AddNode(0, 0);
            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Save);
            editor.Shell.SaveAnswers.Enqueue(null);
            editor.Session.RequestQuit();
            Assert.AreEqual(0, editor.Shell.Exits, "a cancelled save cancels the quit");
            Assert.AreEqual(1, editor.Mission.Nodes.Count);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);

            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Save);
            editor.Shell.SaveAnswers.Enqueue(null);
            editor.Session.New();
            Assert.AreEqual(1, editor.Mission.Nodes.Count, "and cancels New");

            // The write fails.
            editor = Editor.WithSavedChain();
            editor.Document.SetName("Edited");
            editor.Files.FailWritesWith = "The disk is full.";
            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Save);
            editor.Shell.OpenAnswers.Enqueue("other.json");
            editor.Session.Open();
            Assert.AreEqual(0, editor.Shell.OpenDialogs, "a failed save cancels Open before it asks for a file");
            Assert.AreEqual("Edited", editor.Mission.Name);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);
            Assert.AreEqual(1, editor.Prompts.Errors.Count);

            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Save);
            editor.Session.RequestQuit();
            Assert.AreEqual(0, editor.Shell.Exits);
        }

        [Test]
        public void While_a_question_is_open_other_commands_wait()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetName("Edited");
            editor.Prompts.HoldUnsaved = true;

            editor.Session.RequestQuit();
            Assert.IsTrue(editor.Session.Busy);

            // The author keeps clicking while the question is up.
            editor.Session.RequestQuit();
            editor.Session.New();
            editor.Session.Save();
            editor.Session.ArmAddNode();
            editor.Session.ClickCanvas(50, 50);
            editor.Session.ClickNode("N1");
            editor.Session.DeleteSelected();

            Assert.AreEqual(1, editor.Prompts.UnsavedAsked.Count, "one question, not a pile of them");
            Assert.AreEqual(3, editor.Mission.Nodes.Count);
            Assert.AreEqual(EditorTool.Select, editor.Session.Tool);
            Assert.AreEqual(1, editor.Files.Writes);

            editor.Prompts.HeldUnsaved(UnsavedChoice.Cancel);
            Assert.IsFalse(editor.Session.Busy);
            Assert.AreEqual(0, editor.Shell.Exits);
        }

        // ------------------------------------------------------------ opening

        [Test]
        public void A_saved_mission_reopens_exactly_as_it_was()
        {
            var editor = new Editor();
            editor.Document.SetMissionId("round-trip");
            editor.Document.SetName("Round trip");
            editor.Document.SetDistanceScale(0.1 + 0.2);
            editor.Document.AddPlayer("third", PlayerColor.Orange);
            editor.AddNode(0.1 + 0.2, -1d / 3d);
            editor.AddNode(123456.789012345, 1e-7);
            editor.AddNode(-4, 2);
            editor.Document.SetNodeType("N2", MissionNodeType.Moon);
            editor.Document.SetNodeOwner("N1", "third");
            editor.Document.SetNodeOwner("N3", "player2");
            editor.Document.SetNodeDarts("N1", 30);
            editor.Document.SetNodeDarts("N3", 2147483647);
            editor.Connect("N3", "N1");
            editor.Connect("N1", "N2");
            editor.SaveAs("trip.json");
            string saved = editor.Files.Files["trip.json"];

            var other = new Editor();
            other.Files.Files["trip.json"] = saved;
            other.Open("trip.json");

            Assert.AreEqual("trip.json", other.Session.FilePath);
            Assert.IsFalse(other.Session.HasUnsavedChanges, "a freshly opened mission is clean");
            MissionDefinition mission = other.Mission;
            Assert.AreEqual("round-trip", mission.Id);
            Assert.AreEqual("Round trip", mission.Name);
            Assert.AreEqual(0.1 + 0.2, mission.DistanceScale);
            Assert.AreEqual("third", mission.Players[2].Id);
            Assert.AreEqual(PlayerColor.Orange, mission.Players[2].Color);
            Assert.AreEqual(0.1 + 0.2, mission.FindNode("N1").X);
            Assert.AreEqual(-1d / 3d, mission.FindNode("N1").Y);
            Assert.AreEqual(123456.789012345, mission.FindNode("N2").X);
            Assert.AreEqual(1e-7, mission.FindNode("N2").Y);
            Assert.AreEqual(MissionNodeType.Moon, mission.FindNode("N2").Type);
            Assert.AreEqual("third", mission.FindNode("N1").Owner);
            Assert.AreEqual(2147483647, mission.FindNode("N3").Darts);
            CollectionAssert.AreEqual(new[] { "N3>N1", "N1>N2" }, Routes(mission));

            // Saving the reopened mission, again and again, writes the same file.
            for (int i = 0; i < 3; i++)
            {
                other.Document.SetName("x");
                other.Document.SetName("Round trip");
                other.Session.Save();
                Assert.AreEqual(saved, other.Files.Files["trip.json"]);
            }
        }

        [Test]
        public void Cancelling_the_Open_dialog_keeps_the_mission_and_its_file()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Shell.OpenAnswers.Enqueue(null);

            editor.Session.Open();

            Assert.AreEqual(1, editor.Shell.OpenDialogs);
            Assert.AreEqual(3, editor.Mission.Nodes.Count);
            Assert.AreEqual("chain.json", editor.Session.FilePath);
            Assert.IsFalse(editor.Session.HasUnsavedChanges);
        }

        [Test]
        public void A_file_with_an_error_is_refused_and_the_current_work_is_untouched()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetName("Edited");
            editor.Session.Select(Selection.Node("N2"));
            string before = MissionJson.Write(editor.Mission);
            string good = editor.Files.Files["chain.json"];

            var bad = new Dictionary<string, string>
            {
                ["broken.json"] = "{ \"schemaVersion\": 1, ",
                ["twice.json"] = good.Replace("\"name\":", "\"id\": \"again\", \"name\":"),
                ["future.json"] = good.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"),
                ["extra.json"] = good.Replace("\"darts\": 0 }", "\"darts\": 0, \"production\": 2 }"),
                ["color.json"] = good.Replace("\"red\"", "\"teal\""),
                ["darts.json"] = good.Replace("\"darts\": 0", "\"darts\": 1.5"),
                ["dangling.json"] = good.Replace("\"target\": \"N3\"", "\"target\": \"N33\""),
                ["self.json"] = good.Replace("\"target\": \"N3\"", "\"target\": \"N2\""),
                ["reversed.json"] = good.Replace("{ \"source\": \"N2\", \"target\": \"N3\" }", "{ \"source\": \"N2\", \"target\": \"N1\" }"),
            };

            foreach (KeyValuePair<string, string> file in bad)
            {
                Assert.AreNotEqual(good, file.Value, file.Key + " should differ from the good file");
                editor.Files.Files[file.Key] = file.Value;
                editor.Prompts.Errors.Clear();
                editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Discard);

                editor.Open(file.Key);

                Assert.AreEqual(1, editor.Prompts.Errors.Count, file.Key + " should be refused with one message");
                StringAssert.Contains(file.Key, editor.Prompts.Errors[0].title);
                StringAssert.Contains("unchanged", editor.Prompts.Errors[0].message);
                Assert.AreEqual(before, MissionJson.Write(editor.Mission), "no part of " + file.Key + " may be loaded");
                Assert.AreEqual("chain.json", editor.Session.FilePath);
                Assert.IsTrue(editor.Session.HasUnsavedChanges, "Discard only permitted the open; the work is still unsaved");
                Assert.IsTrue(editor.Session.Selection.IsNode("N2"));
                Assert.IsFalse(editor.Session.Busy);
            }
        }

        [Test]
        public void A_refusal_names_the_field_and_says_what_is_wrong()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Files.Files["bad.json"] = editor.Files.Files["chain.json"]
                .Replace("\"owner\": null, \"darts\": 0 }", "\"owner\": \"ghost\", \"darts\": -3 }");

            editor.Open("bad.json");

            string message = editor.Prompts.Errors[0].message;
            StringAssert.Contains("nodes[0].darts", message);
            StringAssert.Contains("-3", message);
            StringAssert.Contains("nodes[0].owner", message);
            StringAssert.Contains("ghost", message);
        }

        [Test]
        public void A_missing_or_unreadable_file_is_reported_and_nothing_changes()
        {
            Editor editor = Editor.WithSavedChain();

            editor.Open("nowhere.json");
            Assert.AreEqual(1, editor.Prompts.Errors.Count);
            Assert.AreEqual(3, editor.Mission.Nodes.Count);

            editor.Files.Files["locked.json"] = editor.Files.Files["chain.json"];
            editor.Files.Unreadable.Add("locked.json");
            editor.Open("locked.json");
            Assert.AreEqual(2, editor.Prompts.Errors.Count);
            Assert.AreEqual("chain.json", editor.Session.FilePath);
        }

        [Test]
        public void A_file_with_warnings_opens_and_shows_them()
        {
            var mission = new MissionDefinition { Id = "loose", Name = "Loose ends" };
            mission.Nodes.Add(new MissionNode { Id = "A", X = 1, Y = 1 });
            mission.Nodes.Add(new MissionNode { Id = "B", X = 1, Y = 1 });
            mission.Nodes.Add(new MissionNode { Id = "C", X = 5, Y = 5 });
            mission.Edges.Add(new MissionEdge("A", "B"));
            var editor = new Editor();
            editor.Files.Files["loose.json"] = MissionJson.Write(mission);

            editor.Open("loose.json");

            Assert.AreEqual(0, editor.Prompts.Errors.Count);
            Assert.AreEqual("loose", editor.Mission.Id);
            MissionValidation validation = editor.Session.Validation;
            Assert.IsFalse(validation.HasErrors);
            Assert.IsTrue(validation.Has(IssueCode.DisconnectedGraph));
            Assert.IsTrue(validation.Has(IssueCode.CoincidentNodes));
            Assert.IsTrue(validation.Has(IssueCode.ZeroLengthRoute));
            StringAssert.Contains("3 warnings", editor.Session.Status);
        }

        [Test]
        public void An_unfinished_layout_can_still_be_saved()
        {
            var editor = new Editor();
            editor.AddNode(0, 0);
            editor.AddNode(0, 0);
            editor.AddNode(9, 9);
            editor.Connect("N1", "N2");
            Assert.AreEqual(3, editor.Session.Validation.WarningCount);

            editor.SaveAs("draft.json");

            Assert.AreEqual(0, editor.Prompts.Errors.Count, "warnings never block a save");
            Assert.IsFalse(editor.Session.HasUnsavedChanges);
            Assert.AreEqual(3, MissionJson.Read(editor.Files.Files["draft.json"]).Mission.Nodes.Count);
        }

        [Test]
        public void Opening_resets_the_tool_and_the_selection()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Session.ClickNode("N1");
            editor.Session.ArmAddConnection();
            editor.Session.ClickNode("N1");
            int replaced = 0;
            editor.Session.DocumentReplaced += () => replaced++;

            editor.Open("chain.json");

            Assert.AreEqual(1, replaced);
            Assert.AreEqual(EditorTool.Select, editor.Session.Tool);
            Assert.IsNull(editor.Session.ConnectionSource);
            Assert.AreEqual(SelectionKind.None, editor.Session.Selection.Kind);
        }

        // ------------------------------------------------------------ saving and failure

        [Test]
        public void Save_As_takes_the_new_file_only_once_it_is_written()
        {
            Editor editor = Editor.WithSavedChain("first.json");
            editor.Document.SetName("Second");

            // Cancelled.
            editor.Shell.SaveAnswers.Enqueue(null);
            editor.Session.SaveAs();
            Assert.AreEqual("first.json", editor.Session.FilePath);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);
            Assert.AreEqual("first.json", editor.Shell.SuggestedNames[1], "the current file name is offered");

            // Failed.
            editor.Files.FailWritesWith = "Access was denied.";
            editor.SaveAs("second.json");
            Assert.AreEqual("first.json", editor.Session.FilePath);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);
            Assert.IsFalse(editor.Files.Files.ContainsKey("second.json"));

            // Written.
            editor.Files.FailWritesWith = null;
            editor.SaveAs("second.json");
            Assert.AreEqual("second.json", editor.Session.FilePath);
            Assert.IsFalse(editor.Session.HasUnsavedChanges);
            Assert.AreEqual("Second", MissionJson.Read(editor.Files.Files["second.json"]).Mission.Name);
            Assert.AreEqual("Untitled Mission", MissionJson.Read(editor.Files.Files["first.json"]).Mission.Name,
                "the first file keeps what was last saved to it");

            editor.Document.SetName("Third");
            editor.Session.Save();
            Assert.AreEqual("Third", MissionJson.Read(editor.Files.Files["second.json"]).Mission.Name, "Save now goes to the new file");
        }

        [Test]
        public void Saving_over_another_file_needs_the_authors_consent()
        {
            Editor editor = Editor.WithSavedChain("mine.json");
            editor.Files.Files["theirs.json"] = "someone else's work";
            editor.Document.SetName("Mine");

            editor.Prompts.ReplaceFile.Enqueue(false);
            editor.SaveAs("theirs.json");
            Assert.AreEqual("theirs.json", editor.Prompts.ReplaceAsked[0]);
            Assert.AreEqual("someone else's work", editor.Files.Files["theirs.json"]);
            Assert.AreEqual("mine.json", editor.Session.FilePath);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);

            editor.Prompts.ReplaceFile.Enqueue(true);
            editor.SaveAs("theirs.json");
            Assert.AreEqual("Mine", MissionJson.Read(editor.Files.Files["theirs.json"]).Mission.Name);
            Assert.AreEqual("theirs.json", editor.Session.FilePath);

            // A file dialog that has already asked is not second-guessed.
            editor.Files.Files["third.json"] = "old";
            editor.Document.SetName("Again");
            editor.SaveAs("third.json", confirmedInDialog: true);
            Assert.AreEqual(2, editor.Prompts.ReplaceAsked.Count);
            Assert.AreEqual("Again", MissionJson.Read(editor.Files.Files["third.json"]).Mission.Name);
        }

        [Test]
        public void A_failed_write_leaves_the_old_file_whole_and_the_work_unsaved_and_a_retry_succeeds()
        {
            Editor editor = Editor.WithSavedChain();
            string old = editor.Files.Files["chain.json"];
            editor.Document.SetName("Edited");
            editor.Files.FailWritesWith = "There is not enough space on the disk.";

            editor.Session.Save();

            Assert.AreEqual(1, editor.Prompts.Errors.Count);
            StringAssert.Contains("chain.json", editor.Prompts.Errors[0].title);
            StringAssert.Contains("not enough space", editor.Prompts.Errors[0].message);
            Assert.AreEqual(old, editor.Files.Files["chain.json"]);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);
            Assert.AreEqual("Edited", editor.Mission.Name);
            Assert.IsFalse(editor.Session.Busy);

            editor.Files.FailWritesWith = null;
            editor.Session.Save();

            Assert.AreEqual("Edited", MissionJson.Read(editor.Files.Files["chain.json"]).Mission.Name);
            Assert.IsFalse(editor.Session.HasUnsavedChanges);
        }

        [Test]
        public void A_mission_that_cannot_be_serialized_is_not_written()
        {
            Editor editor = Editor.WithSavedChain();
            string old = editor.Files.Files["chain.json"];
            editor.Document.SetName("Edited");
            editor.Serialize = _ => throw new InvalidOperationException("out of memory");

            editor.Session.Save();

            Assert.AreEqual(1, editor.Prompts.Errors.Count);
            StringAssert.Contains("out of memory", editor.Prompts.Errors[0].message);
            Assert.AreEqual(old, editor.Files.Files["chain.json"]);
            Assert.AreEqual(1, editor.Files.Writes);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);

            editor.Session.SaveAs();
            Assert.AreEqual(1, editor.Shell.SaveDialogs, "nothing is asked for a save that cannot happen");

            editor.Serialize = MissionJson.Write;
            editor.Session.Save();
            Assert.IsFalse(editor.Session.HasUnsavedChanges);
        }

        // ------------------------------------------------------------ files changed by something else

        [Test]
        public void A_file_changed_by_another_program_is_never_overwritten_silently()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetName("Mine");
            editor.Files.Files["chain.json"] = "edited elsewhere";
            editor.Prompts.External.Enqueue(ExternalChangeChoice.Cancel);

            editor.Session.Save();

            Assert.AreEqual(ExternalChange.Modified, editor.Prompts.ExternalAsked[0]);
            Assert.AreEqual("edited elsewhere", editor.Files.Files["chain.json"]);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);
            Assert.AreEqual("Mine", editor.Mission.Name);

            // Asking again does not wear the protection down.
            editor.Prompts.External.Enqueue(ExternalChangeChoice.Cancel);
            editor.Session.Save();
            Assert.AreEqual(2, editor.Prompts.ExternalAsked.Count);
            Assert.AreEqual("edited elsewhere", editor.Files.Files["chain.json"]);
        }

        [Test]
        public void Overwrite_replaces_the_changed_file_and_later_saves_go_through()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetName("Mine");
            editor.Files.Files["chain.json"] = "edited elsewhere";
            editor.Prompts.External.Enqueue(ExternalChangeChoice.Overwrite);

            editor.Session.Save();

            Assert.AreEqual("Mine", MissionJson.Read(editor.Files.Files["chain.json"]).Mission.Name);
            Assert.IsFalse(editor.Session.HasUnsavedChanges);

            editor.Document.SetName("Mine again");
            editor.Session.Save();
            Assert.AreEqual(1, editor.Prompts.ExternalAsked.Count, "the file the editor wrote is the new baseline");
            Assert.AreEqual("Mine again", MissionJson.Read(editor.Files.Files["chain.json"]).Mission.Name);
        }

        [Test]
        public void Overwrite_covers_only_the_change_the_author_was_shown()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetName("Mine");
            editor.Files.Files["chain.json"] = "first outside edit";

            // Between the question and the write, the file changes once more.
            editor.Prompts.External.Enqueue(ExternalChangeChoice.Overwrite);
            editor.Prompts.External.Enqueue(ExternalChangeChoice.Cancel);
            editor.Files.BeforeNextRead = () =>
                editor.Files.BeforeNextRead = () => editor.Files.Files["chain.json"] = "second outside edit";

            editor.Session.Save();

            Assert.AreEqual(2, editor.Prompts.ExternalAsked.Count, "the second change is a new question");
            Assert.AreEqual("second outside edit", editor.Files.Files["chain.json"]);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);
        }

        [Test]
        public void Save_As_keeps_the_changed_file_and_moves_the_mission_to_another()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetName("Mine");
            editor.Files.Files["chain.json"] = "edited elsewhere";
            editor.Prompts.External.Enqueue(ExternalChangeChoice.SaveAs);
            editor.Shell.SaveAnswers.Enqueue(new SaveDestination("mine.json", false));

            editor.Session.Save();

            Assert.AreEqual("edited elsewhere", editor.Files.Files["chain.json"], "the other program's edit survives");
            Assert.AreEqual("Mine", MissionJson.Read(editor.Files.Files["mine.json"]).Mission.Name);
            Assert.AreEqual("mine.json", editor.Session.FilePath);
            Assert.IsFalse(editor.Session.HasUnsavedChanges);

            editor.Document.SetName("Mine again");
            editor.Session.Save();
            Assert.AreEqual(1, editor.Prompts.ExternalAsked.Count, "the new file has a baseline of its own");
            Assert.AreEqual("edited elsewhere", editor.Files.Files["chain.json"]);
        }

        [Test]
        public void A_deleted_file_is_noticed_before_it_is_written_again()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetName("Mine");
            editor.Files.Files.Remove("chain.json");

            editor.Prompts.External.Enqueue(ExternalChangeChoice.Cancel);
            editor.Session.Save();
            Assert.AreEqual(ExternalChange.Deleted, editor.Prompts.ExternalAsked[0]);
            Assert.IsFalse(editor.Files.Files.ContainsKey("chain.json"));
            Assert.IsTrue(editor.Session.HasUnsavedChanges);

            editor.Prompts.External.Enqueue(ExternalChangeChoice.Overwrite);
            editor.Session.Save();
            Assert.AreEqual("Mine", MissionJson.Read(editor.Files.Files["chain.json"]).Mission.Name);
            Assert.IsFalse(editor.Session.HasUnsavedChanges);
        }

        [Test]
        public void An_external_change_is_also_caught_on_the_way_out()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetName("Mine");
            editor.Files.Files["chain.json"] = "edited elsewhere";
            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Save);
            editor.Prompts.External.Enqueue(ExternalChangeChoice.Cancel);

            editor.Session.RequestQuit();

            Assert.AreEqual(0, editor.Shell.Exits, "the save did not happen, so the editor stays open");
            Assert.AreEqual("edited elsewhere", editor.Files.Files["chain.json"]);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);
        }

        [Test]
        public void An_opened_files_baseline_is_the_text_that_was_read()
        {
            Editor editor = Editor.WithSavedChain();
            string original = editor.Files.Files["chain.json"];

            // The same mission, laid out differently by hand: still the file that was opened.
            string reformatted = original.Replace("\n", "\r\n").Replace("  ", "\t");
            editor.Files.Files["chain.json"] = reformatted;
            editor.Open("chain.json");
            editor.Document.SetName("Edited");
            editor.Session.Save();

            Assert.AreEqual(0, editor.Prompts.ExternalAsked.Count);
            Assert.AreEqual("Edited", MissionJson.Read(editor.Files.Files["chain.json"]).Mission.Name);
        }

        // ------------------------------------------------------------ invalid field input

        [Test]
        public void Typed_field_text_is_taken_exactly_or_refused()
        {
            Assert.IsTrue(MissionFields.TryParseCoordinate("-12.5", out double coordinate, out _));
            Assert.AreEqual(-12.5, coordinate);
            Assert.IsTrue(MissionFields.TryParseCoordinate(" 1e3 ", out coordinate, out _));
            Assert.AreEqual(1000d, coordinate);
            foreach (string text in new[] { "", "abc", "1,5", "1.2.3", "NaN", "Infinity", "1e999", "2000000000", "--1", "1 2" })
            {
                Assert.IsFalse(MissionFields.TryParseCoordinate(text, out _, out string error), "\"" + text + "\" is not a coordinate");
                Assert.IsNotEmpty(error);
            }

            Assert.IsTrue(MissionFields.TryParseDistanceScale("0.5", out double scale, out _));
            Assert.AreEqual(0.5, scale);
            foreach (string text in new[] { "0", "-1", "", "one", "1e10" })
                Assert.IsFalse(MissionFields.TryParseDistanceScale(text, out _, out _), "\"" + text + "\" is not a scale");

            Assert.IsTrue(MissionFields.TryParseDarts("2147483647", out int darts, out _));
            Assert.AreEqual(int.MaxValue, darts);
            Assert.IsTrue(MissionFields.TryParseDarts("0", out darts, out _));
            foreach (string text in new[] { "-1", "1.5", "2147483648", "", "ten", "1e2", "+5" })
            {
                Assert.IsFalse(MissionFields.TryParseDarts(text, out _, out string error), "\"" + text + "\" is not a Dart count");
                StringAssert.Contains("2147483647", error);
            }

            Assert.AreEqual("0.30000000000000004", MissionFields.Format(0.1 + 0.2), "a field shows the whole value");
        }

        [Test]
        public void Save_does_not_quietly_drop_invalid_field_text()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetName("Edited");
            editor.Session.Select(Selection.Node("N2"));
            editor.Session.ReportInvalidInput("node.darts", "Starting Darts", true);
            int discarded = 0;
            editor.Session.InvalidInputDiscarded += () => discarded++;

            // Go back and correct it: nothing is saved, the text stays.
            editor.Prompts.DiscardInput.Enqueue(false);
            editor.Session.Save();
            Assert.AreEqual("Starting Darts", editor.Prompts.InvalidAsked[0]);
            Assert.AreEqual(1, editor.Files.Writes);
            Assert.IsTrue(editor.Session.HasUnsavedChanges);
            Assert.IsTrue(editor.Session.HasInvalidInput);
            Assert.AreEqual(0, discarded);

            // Discard it: the fields go back to the mission's values and the mission is saved.
            editor.Prompts.DiscardInput.Enqueue(true);
            editor.Session.Save();
            Assert.AreEqual(1, discarded);
            Assert.IsFalse(editor.Session.HasInvalidInput);
            Assert.AreEqual(2, editor.Files.Writes);
            Assert.IsFalse(editor.Session.HasUnsavedChanges);
        }

        [Test]
        public void Corrected_field_text_no_longer_holds_anything_up()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetName("Edited");
            editor.Session.ReportInvalidInput("mission.scale", "Distance scale", false);
            editor.Session.ClearInvalidInput("mission.scale");

            editor.Session.Save();

            Assert.AreEqual(0, editor.Prompts.InvalidAsked.Count);
            Assert.IsFalse(editor.Session.HasUnsavedChanges);
        }

        [Test]
        public void Leaving_the_mission_asks_about_invalid_field_text_first()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Session.ReportInvalidInput("mission.id", "Mission ID", false);

            editor.Prompts.DiscardInput.Enqueue(false);
            editor.Session.New();
            Assert.AreEqual(3, editor.Mission.Nodes.Count, "going back to correct the field cancels New");
            Assert.IsTrue(editor.Session.HasInvalidInput);

            editor.Prompts.DiscardInput.Enqueue(false);
            editor.Session.RequestQuit();
            Assert.AreEqual(0, editor.Shell.Exits);

            editor.Prompts.DiscardInput.Enqueue(false);
            editor.Session.Open();
            Assert.AreEqual(0, editor.Shell.OpenDialogs);

            editor.Prompts.DiscardInput.Enqueue(true);
            editor.Session.New();
            Assert.AreEqual(0, editor.Mission.Nodes.Count);
            Assert.IsFalse(editor.Session.HasInvalidInput);
        }

        [Test]
        public void Field_text_given_up_for_an_Open_stays_until_the_other_mission_really_opens()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Files.Files["bad.json"] = "{";
            editor.Session.ReportInvalidInput("mission.id", "Mission ID", false);
            int discarded = 0;
            editor.Session.InvalidInputDiscarded += () => discarded++;

            editor.Prompts.DiscardInput.Enqueue(true);
            editor.Open("bad.json");
            Assert.IsTrue(editor.Session.HasInvalidInput, "the open failed, so the pending text is still there");
            Assert.AreEqual(0, discarded);

            editor.Prompts.DiscardInput.Enqueue(true);
            editor.Shell.OpenAnswers.Enqueue(null);
            editor.Session.Open();
            Assert.IsTrue(editor.Session.HasInvalidInput, "so it is after a cancelled dialog");

            editor.Prompts.DiscardInput.Enqueue(true);
            editor.Open("chain.json");
            Assert.IsFalse(editor.Session.HasInvalidInput);
        }

        [Test]
        public void Saving_on_the_way_out_asks_about_invalid_text_once()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Document.SetName("Edited");
            editor.Session.ReportInvalidInput("mission.scale", "Distance scale", false);
            editor.Prompts.DiscardInput.Enqueue(true);
            editor.Prompts.Unsaved.Enqueue(UnsavedChoice.Save);

            editor.Session.RequestQuit();

            Assert.AreEqual(1, editor.Prompts.InvalidAsked.Count);
            Assert.AreEqual(1, editor.Shell.Exits);
            Assert.AreEqual("Edited", MissionJson.Read(editor.Files.Files["chain.json"]).Mission.Name);
        }

        [Test]
        public void Selecting_something_else_asks_about_invalid_text_in_the_selected_objects_fields()
        {
            Editor editor = Editor.WithSavedChain();
            editor.Session.Select(Selection.Node("N1"));
            editor.Session.ReportInvalidInput("node.x", "X", true);
            editor.Session.ReportInvalidInput("mission.id", "Mission ID", false);

            editor.Prompts.DiscardInput.Enqueue(false);
            editor.Session.ClickNode("N2");
            Assert.AreEqual("X", editor.Prompts.InvalidAsked[0], "only the selected object's fields would be put away");
            Assert.IsTrue(editor.Session.Selection.IsNode("N1"), "going back keeps the selection");

            editor.Prompts.DiscardInput.Enqueue(true);
            editor.Session.ClickNode("N2");
            Assert.IsTrue(editor.Session.Selection.IsNode("N2"));
            Assert.IsTrue(editor.Session.HasInvalidInput, "the mission's own field is still waiting to be corrected");

            editor.Session.ClickNode("N3");
            Assert.AreEqual(2, editor.Prompts.InvalidAsked.Count, "mission fields stay on screen whatever is selected");
            Assert.IsTrue(editor.Session.Selection.IsNode("N3"));
        }
    }
}
