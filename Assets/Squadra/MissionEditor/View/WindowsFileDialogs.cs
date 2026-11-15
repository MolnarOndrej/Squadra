#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;

namespace Squadra
{
    /// <summary>
    /// The Windows Open and Save As dialogs, called straight from the system's common dialog library.
    /// Each call blocks until the author has chosen or cancelled, with the editor's window as the
    /// dialog's owner. Nothing here needs the Unity Editor or any package.
    /// </summary>
    public static class WindowsFileDialogs
    {
        const string Filter = "Squadra missions (*.json)\0*.json\0All files (*.*)\0*.*\0\0";
        const string DefaultExtension = "json";
        const int PathCapacity = 32768; // characters: the longest path Windows can hand back

        const int OFN_OVERWRITEPROMPT = 0x00000002;
        const int OFN_HIDEREADONLY = 0x00000004;
        const int OFN_NOCHANGEDIR = 0x00000008;
        const int OFN_PATHMUSTEXIST = 0x00000800;
        const int OFN_FILEMUSTEXIST = 0x00001000;
        const int OFN_EXPLORER = 0x00080000;

        [StructLayout(LayoutKind.Sequential)]
        struct OpenFileName
        {
            public int structSize;
            public IntPtr owner;
            public IntPtr instance;
            public IntPtr filter;
            public IntPtr customFilter;
            public int maxCustomFilter;
            public int filterIndex;
            public IntPtr file;
            public int maxFile;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public IntPtr initialDirectory;
            public IntPtr title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public IntPtr defaultExtension;
            public IntPtr customData;
            public IntPtr hook;
            public IntPtr templateName;
            public IntPtr reserved;
            public int reservedNumber;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", SetLastError = true)]
        static extern bool GetOpenFileName(ref OpenFileName dialog);

        [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", SetLastError = true)]
        static extern bool GetSaveFileName(ref OpenFileName dialog);

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        /// <summary>Asks for an existing mission file. Null when the author cancels.</summary>
        public static string Open(string title, string folder)
        {
            return Show(title, folder, null, false);
        }

        /// <summary>
        /// Asks where to save. The dialog asks before an existing file is chosen, and adds
        /// <c>.json</c> to a name typed without an extension. Null when the author cancels.
        /// </summary>
        public static string Save(string title, string folder, string suggestedFileName)
        {
            return Show(title, folder, suggestedFileName, true);
        }

        // The dialog keeps pointers into these buffers while it is open, so they are allocated by hand
        // and released when it has returned.
        static string Show(string title, string folder, string fileName, bool save)
        {
            IntPtr file = Marshal.AllocHGlobal(PathCapacity * 2);
            IntPtr filter = Marshal.StringToHGlobalUni(Filter);
            IntPtr caption = Marshal.StringToHGlobalUni(title ?? "");
            IntPtr directory = string.IsNullOrEmpty(folder) ? IntPtr.Zero : Marshal.StringToHGlobalUni(folder);
            IntPtr extension = Marshal.StringToHGlobalUni(DefaultExtension);
            try
            {
                string initial = fileName ?? "";
                if (initial.Length >= PathCapacity) initial = "";
                char[] characters = (initial + "\0").ToCharArray();
                Marshal.Copy(characters, 0, file, characters.Length);

                var dialog = new OpenFileName
                {
                    structSize = Marshal.SizeOf(typeof(OpenFileName)),
                    owner = GetActiveWindow(),
                    filter = filter,
                    filterIndex = 1,
                    file = file,
                    maxFile = PathCapacity,
                    initialDirectory = directory,
                    title = caption,
                    defaultExtension = extension,
                    flags = OFN_EXPLORER | OFN_NOCHANGEDIR | OFN_HIDEREADONLY | OFN_PATHMUSTEXIST
                        | (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST),
                };

                bool chosen = save ? GetSaveFileName(ref dialog) : GetOpenFileName(ref dialog);
                if (!chosen) return null;

                string path = Marshal.PtrToStringUni(file);
                return string.IsNullOrEmpty(path) ? null : path;
            }
            finally
            {
                Marshal.FreeHGlobal(file);
                Marshal.FreeHGlobal(filter);
                Marshal.FreeHGlobal(caption);
                if (directory != IntPtr.Zero) Marshal.FreeHGlobal(directory);
                Marshal.FreeHGlobal(extension);
            }
        }
    }
}
#endif
