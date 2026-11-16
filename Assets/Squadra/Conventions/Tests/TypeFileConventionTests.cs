using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace Squadra.Tests
{
    /// <summary>
    /// A check of how the code is organized, not of what it does: every C# file under
    /// <c>Assets/Squadra</c> declares exactly one top-level class, interface, struct or enum, and the
    /// file is named after it. Nested types are not looked at; they belong to the type around them.
    ///
    /// It compares the source files Unity compiles into each assembly with the top-level types that
    /// assembly ends up with, so it needs no parser. A file whose whole content is switched off by a
    /// platform directive has no type in the compiled assembly and would be reported on that platform.
    /// </summary>
    [Category("Conventions")]
    public class TypeFileConventionTests
    {
        const string ProjectCodeRoot = "Assets/Squadra/";

        [Test]
        public void Every_source_file_declares_one_top_level_type_named_like_the_file()
        {
            var problems = new List<string>();
            int checkedFiles = 0;

            foreach (UnityEditor.Compilation.Assembly compiled in CompilationPipeline.GetAssemblies())
            {
                var projectFiles = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var otherFileNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (string source in compiled.sourceFiles)
                {
                    string path = source.Replace('\\', '/');
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (!path.StartsWith(ProjectCodeRoot, StringComparison.Ordinal))
                    {
                        otherFileNames.Add(name);
                        continue;
                    }

                    if (!projectFiles.TryGetValue(name, out List<string> paths))
                        projectFiles.Add(name, paths = new List<string>());
                    paths.Add(path);
                    checkedFiles++;
                }

                if (projectFiles.Count == 0) continue;

                Dictionary<string, int> types = TopLevelTypes(compiled.name);
                if (types == null)
                {
                    problems.Add("The assembly " + compiled.name + " is not loaded, so its files could not be checked.");
                    continue;
                }

                foreach (KeyValuePair<string, List<string>> file in projectFiles)
                {
                    types.TryGetValue(file.Key, out int declared);
                    if (declared == file.Value.Count) continue;

                    problems.Add(string.Join(", ", file.Value) + ": " + compiled.name + " has " + declared
                        + " top-level type(s) named " + file.Key + " for " + file.Value.Count + " file(s) of that name.");
                }

                foreach (KeyValuePair<string, int> type in types)
                {
                    if (projectFiles.ContainsKey(type.Key) || otherFileNames.Contains(type.Key)) continue;

                    problems.Add("The top-level type " + type.Key + " in " + compiled.name
                        + " has no file named " + type.Key + ".cs. Give it one, or nest it in the type it serves.");
                }
            }

            Assert.That(checkedFiles, Is.GreaterThan(0), "No source files were found under " + ProjectCodeRoot + ".");
            Assert.That(problems, Is.Empty, "One top-level type per file, named like the file:\n" + string.Join("\n", problems));
        }

        // The top-level types of a loaded assembly by simple name, with how many there are of each;
        // null when the assembly is not loaded. Types the compiler or a source generator added are left out.
        static Dictionary<string, int> TopLevelTypes(string assemblyName)
        {
            System.Reflection.Assembly loaded = null;
            foreach (System.Reflection.Assembly candidate in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (candidate.GetName().Name == assemblyName) loaded = candidate;
            }

            if (loaded == null) return null;

            Type[] all;
            try
            {
                all = loaded.GetTypes();
            }
            catch (ReflectionTypeLoadException partial)
            {
                all = partial.Types;
            }

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Type type in all)
            {
                if (type == null || type.IsNested) continue;
                if (type.Name.StartsWith("<", StringComparison.Ordinal) || type.Name.StartsWith("__", StringComparison.Ordinal)) continue;
                if (type.IsDefined(typeof(CompilerGeneratedAttribute), false)) continue;

                string name = type.Name;
                int arity = name.IndexOf('`');
                if (arity >= 0) name = name.Substring(0, arity);
                counts[name] = counts.TryGetValue(name, out int count) ? count + 1 : 1;
            }

            return counts;
        }
    }
}
