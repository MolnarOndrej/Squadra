using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Squadra.MissionEditing;

namespace Squadra.Tests
{
    /// <summary>
    /// Exercises mission files on a real disk, in a folder of their own under the temporary directory:
    /// what a save leaves behind when it works and when it cannot, and how files are read.
    /// </summary>
    public class MissionFilesTests
    {
        string folder;
        LocalMissionFiles files;

        [SetUp]
        public void CreateFolder()
        {
            folder = Path.Combine(Path.GetTempPath(), "squadra-mission-files-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            files = new LocalMissionFiles();
        }

        [TearDown]
        public void RemoveFolder()
        {
            if (!Directory.Exists(folder)) return;

            foreach (string file in Directory.GetFiles(folder)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(folder, true);
        }

        string At(string name)
        {
            return Path.Combine(folder, name);
        }

        [Test]
        public void A_save_creates_the_file_and_leaves_nothing_else_in_the_folder()
        {
            files.WriteAtomically(At("new.json"), "{ \"first\": true }");

            Assert.AreEqual("{ \"first\": true }", File.ReadAllText(At("new.json")));
            Assert.AreEqual(1, Directory.GetFiles(folder).Length, "no temporary or companion file remains");
            Assert.IsTrue(files.Exists(At("new.json")));
        }

        [Test]
        public void A_save_replaces_the_whole_of_an_existing_file()
        {
            File.WriteAllText(At("mission.json"), new string('x', 10000));

            files.WriteAtomically(At("mission.json"), "short");
            files.WriteAtomically(At("mission.json"), "shorter still, then longer");

            Assert.AreEqual("shorter still, then longer", File.ReadAllText(At("mission.json")));
            Assert.AreEqual(1, Directory.GetFiles(folder).Length);
        }

        [Test]
        public void Files_are_UTF_8_without_a_byte_order_mark_and_read_back_exactly()
        {
            const string text = "{ \"name\": \"Žluťoučký kůň — 惑星 🚀\" }\n";

            files.WriteAtomically(At("unicode.json"), text);

            byte[] bytes = File.ReadAllBytes(At("unicode.json"));
            CollectionAssert.AreEqual(new UTF8Encoding(false).GetBytes(text), bytes);
            MissionFileContent read = files.Read(At("unicode.json"));
            Assert.AreEqual(MissionFileStatus.Ok, read.Status);
            Assert.AreEqual(text, read.Text);
        }

        [Test]
        public void A_byte_order_mark_written_by_another_program_is_not_part_of_the_text()
        {
            File.WriteAllBytes(At("bom.json"), new byte[] { 0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}' });

            Assert.AreEqual("{}", files.Read(At("bom.json")).Text);
        }

        [Test]
        public void A_file_that_is_not_there_or_not_text_is_reported_not_guessed()
        {
            MissionFileContent missing = files.Read(At("nowhere.json"));
            Assert.AreEqual(MissionFileStatus.Missing, missing.Status);
            Assert.IsNull(missing.Text);
            Assert.IsFalse(files.Exists(At("nowhere.json")));

            Assert.AreEqual(MissionFileStatus.Missing, files.Read(Path.Combine(folder, "no-folder", "a.json")).Status);

            File.WriteAllBytes(At("binary.json"), new byte[] { (byte)'{', 0xFF, 0xFE, 0x80, (byte)'}' });
            MissionFileContent binary = files.Read(At("binary.json"));
            Assert.AreEqual(MissionFileStatus.Unreadable, binary.Status);
            StringAssert.Contains("UTF-8", binary.Error);
        }

        [Test]
        public void A_save_that_cannot_replace_the_file_leaves_it_whole_and_cleans_up()
        {
            File.WriteAllText(At("locked.json"), "the previous save");
            File.SetAttributes(At("locked.json"), FileAttributes.ReadOnly);

            var problem = Assert.Throws<MissionFileException>(() => files.WriteAtomically(At("locked.json"), "new text"));

            Assert.IsNotEmpty(problem.Message);
            Assert.AreEqual("the previous save", File.ReadAllText(At("locked.json")));
            Assert.AreEqual(1, Directory.GetFiles(folder).Length, "the temporary file is removed after a failure");

            // The same save succeeds once the obstacle is gone.
            File.SetAttributes(At("locked.json"), FileAttributes.Normal);
            files.WriteAtomically(At("locked.json"), "new text");
            Assert.AreEqual("new text", File.ReadAllText(At("locked.json")));
        }

        [Test]
        public void A_save_while_another_program_holds_the_file_open_fails_without_harming_it()
        {
            File.WriteAllText(At("busy.json"), "the previous save");

            using (new FileStream(At("busy.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.Throws<MissionFileException>(() => files.WriteAtomically(At("busy.json"), "new text"));
            }

            Assert.AreEqual("the previous save", File.ReadAllText(At("busy.json")));
            Assert.AreEqual(1, Directory.GetFiles(folder).Length);
        }

        [Test]
        public void A_save_into_a_folder_that_does_not_exist_fails_with_an_explanation()
        {
            string path = Path.Combine(folder, "missing", "mission.json");

            var problem = Assert.Throws<MissionFileException>(() => files.WriteAtomically(path, "text"));

            StringAssert.Contains("does not exist", problem.Message);
            Assert.IsFalse(Directory.Exists(Path.Combine(folder, "missing")), "no folder is created behind the author's back");
        }
    }
}
