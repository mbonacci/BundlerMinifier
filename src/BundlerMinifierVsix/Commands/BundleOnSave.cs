using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BundlerMinifier;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NuGet.VisualStudio;

namespace BundlerMinifierVsix.Commands
{
    internal sealed class BundleOnSave
    {
        private readonly string[] fileExtensions = { ".css", ".less", ".cshtml", ".js" };
        private static List<FileSystemWatcher> _fileSystemWatchers { get; set; }

        public static void InitializeSolution(Solution solution)
        {
            foreach (Project project in solution.GetAllProjects())
            {
                Initialize(project);
            }
        }
        public static void RemoveSolution(Solution solution)
        {
            foreach (Project project in solution.GetAllProjects())
            {
                Remove(project);
            }
        }

        public static void Initialize(Project project)
        {
            var configFilePath = project.GetConfigFile();

            if (!string.IsNullOrEmpty(configFilePath) && File.Exists(configFilePath))
            {
                var bundles = BundleHandler.GetBundles(configFilePath);
                var fileExtensions = GetWatchedFileExtensions(bundles);

                foreach (var fileExtension in fileExtensions)
                {
                    var fileWatcher = GetWatcher(project, fileExtension);

                    if (fileWatcher == null)
                    {
                        AddWatcher(project, fileExtension);
                    }
                }
            }
        }

        private static IEnumerable<string> GetWatchedFileExtensions(IEnumerable<Bundle> bundles)
        {
            var inputFilePaths = bundles.Select(x => x.InputFiles.FirstOrDefault()).Where(x => x != null);

            return inputFilePaths.Select(Path.GetExtension).Where(x => x != null);
        }

        private static FileSystemWatcher AddWatcher(Project project, string fileExtension)
        {
            var watcher = new FileSystemWatcher(project.GetRootFolder());

            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
            watcher.EnableRaisingEvents = true;
            watcher.IncludeSubdirectories = true;
            watcher.Changed += WatchedFileChanged;
            watcher.Filter = $"*.{fileExtension}";

            _fileSystemWatchers.Add(watcher);

            return watcher;
        }

        private static void WatchedFileChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            
        }

        private static FileSystemWatcher GetWatcher(Project project, string fileExtension)
        {
            return _fileSystemWatchers.FirstOrDefault(x => x.Path.Equals(project.GetRootFolder()) && x.Filter.Equals($"*.{fileExtension}"));
        }

        public static void Remove(Project project)
        {
            var configFilePath = project.GetConfigFile();

            if (!string.IsNullOrEmpty(configFilePath) && File.Exists(configFilePath))
            {
                var fileWatcher = GetWatcher(project);

                if (fileWatcher != null)
                {
                    fileWatcher.Dispose();
                    _fileSystemWatchers.Remove(fileWatcher);
                }
            }
        }
    }
}
