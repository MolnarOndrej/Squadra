using System;
using System.IO;
using System.Security;
using System.Text;

namespace Squadra.MissionEditing
{
    public enum MissionFileStatus
    {
        Ok,

        /// <summary>There is no file at the path.</summary>
        Missing,

        /// <summary>There is a file, but its text could not be read.</summary>
        Unreadable,
    }

    /// <summary>What was found at a path: the file's text, or why there is none.</summary>
    public readonly struct MissionFileContent
    {
        MissionFileContent(MissionFileStatus status, string text, string error)
        {
            Status = status;
            Text = text;
            Error = error;
        }

        public MissionFileStatus Status { get; }
        public string Text { get; }
        public string Error { get; }

        public static MissionFileContent Of(string text)
        {
            return new MissionFileContent(MissionFileStatus.Ok, text, null);
        }

        public static MissionFileContent Missing(string error = null)
        {
            return new MissionFileContent(MissionFileStatus.Missing, null, error ?? "The file does not exist.");
        }

        public static MissionFileContent Unreadable(string error)
        {
            return new MissionFileContent(MissionFileStatus.Unreadable, null, error);
        }

        /// <summary>True when both describe the same state of a file.</summary>
        public bool Same(MissionFileContent other)
        {
            return Status == other.Status && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }
    }

    /// <summary>A save that did not happen. The message is written for the author.</summary>
    public sealed class MissionFileException : Exception
    {
        public MissionFileException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Where mission files live. The editor reads and writes whole files through this and nothing else,
    /// so a test, or another kind of storage, can stand in for the disk.
    /// </summary>
    public interface IMissionFileStore
    {
        MissionFileContent Read(string path);

        bool Exists(string path);

        /// <summary>
        /// Replaces the file at <paramref name="path"/> with <paramref name="text"/>, or creates it.
        /// Either the whole new text is there afterwards or the previous file is untouched; a
        /// <see cref="MissionFileException"/> means the latter.
        /// </summary>
        void WriteAtomically(string path, string text);
    }

    /// <summary>
    /// Mission files on the local disk, as UTF-8. A save writes the whole new text to a temporary file
    /// in the destination folder, flushes it to the disk, and then puts it in the destination's place
    /// in one step; the destination is never opened for writing, so a failure cannot leave it cut
    /// short. This is not a lock: two programs saving the same file still overwrite one another.
    /// </summary>
    public sealed class LocalMissionFiles : IMissionFileStore
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public MissionFileContent Read(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
                return MissionFileContent.Of(Utf8.GetString(bytes, start, bytes.Length - start));
            }
            catch (FileNotFoundException)
            {
                return MissionFileContent.Missing();
            }
            catch (DirectoryNotFoundException)
            {
                return MissionFileContent.Missing("The folder of the file does not exist.");
            }
            catch (DecoderFallbackException)
            {
                return MissionFileContent.Unreadable("The file is not UTF-8 text, so it is not a mission file.");
            }
            catch (Exception problem) when (IsFileProblem(problem))
            {
                return MissionFileContent.Unreadable(Describe(problem));
            }
        }

        public bool Exists(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch (Exception problem) when (IsFileProblem(problem))
            {
                return false;
            }
        }

        public void WriteAtomically(string path, string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            string temporary = null;
            try
            {
                string destination = Path.GetFullPath(path);
                string folder = Path.GetDirectoryName(destination);
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                    throw new MissionFileException("The folder \"" + folder + "\" does not exist.");

                byte[] bytes = Utf8.GetBytes(text);
                temporary = Path.Combine(folder, "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                // An existing file is replaced in place; a new one is moved in, which refuses to
                // overwrite a file that appeared in the meantime.
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
                temporary = null;
            }
            catch (MissionFileException)
            {
                throw;
            }
            catch (Exception problem) when (IsFileProblem(problem))
            {
                throw new MissionFileException(Describe(problem), problem);
            }
            finally
            {
                if (temporary != null) TryDelete(temporary);
            }
        }

        static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception problem) when (IsFileProblem(problem))
            {
                // The destination is intact either way; a stray temporary file is the lesser harm.
            }
        }

        static bool IsFileProblem(Exception problem)
        {
            return problem is IOException || problem is UnauthorizedAccessException || problem is SecurityException
                || problem is NotSupportedException || problem is ArgumentException;
        }

        static string Describe(Exception problem)
        {
            if (problem is UnauthorizedAccessException || problem is SecurityException)
                return "Access was denied. Check that the file is not read-only or open in another program, "
                    + "and that you may write to this folder.";
            if (problem is PathTooLongException) return "The path is too long.";
            if (problem is ArgumentException || problem is NotSupportedException) return "The path is not a valid file name.";
            return problem.Message;
        }
    }
}
