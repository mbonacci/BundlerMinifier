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
        private static List<FileSystemWatcher> _fileSystemWatchers { get; set; } = new List<FileSystemWatcher>();

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
            var projectRootPath = project.GetRootFolder();
            var configFileLocation = project.GetConfigFile();
            var watcher = new FileSystemWatcher(projectRootPath);

            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.EnableRaisingEvents = true;
            watcher.IncludeSubdirectories = true;
            watcher.Changed += (sender, fileArgs) => WatchedFileChanged(sender, fileArgs, projectRootPath, configFileLocation);
            watcher.Filter = FilterForFileExtension(fileExtension);

            _fileSystemWatchers.Add(watcher);

            return watcher;
        }

        private static string FilterForFileExtension(string fileExtension) => $"*{fileExtension}";

        private static void WatchedFileChanged(object sender, FileSystemEventArgs fileSystemEventArgs, string projectRootPath, string configFileLocation)
        {
            if (string.IsNullOrEmpty(configFileLocation)) return;

            var relativePath = BundlerMinifier.FileHelpers.MakeRelative(projectRootPath, fileSystemEventArgs.FullPath);

            if (relativePath.Contains("node_modules")) return;

            var fileSystemWatcher = sender as FileSystemWatcher;
            
            var bundles = BundleHandler.GetBundles(configFileLocation);
            var bundleInputPaths = bundles.SelectMany(x => x.InputFiles);

            if (bundleInputPaths.Contains(relativePath))
                BundleService.Process(configFileLocation, fileSystemWatcher);
        }


        private static FileSystemWatcher GetWatcher(Project project, string fileExtension)
        {
            var filterValue = FilterForFileExtension(fileExtension);

            return _fileSystemWatchers.FirstOrDefault(x => x.Path.Equals(project.GetRootFolder()) && x.Filter.Equals(filterValue));
        }

        public static void Remove(Project project)
        {
            var configFilePath = project.GetConfigFile();

            if (!string.IsNullOrEmpty(configFilePath) && File.Exists(configFilePath))
            {
                var bundles = BundleHandler.GetBundles(configFilePath);
                var fileExtensions = GetWatchedFileExtensions(bundles);

                foreach (var fileExtension in fileExtensions)
                {
                    var fileWatcher = GetWatcher(project, fileExtension);

                    if (fileWatcher != null)
                    {
                        fileWatcher.Dispose();
                        _fileSystemWatchers.Remove(fileWatcher);
                    }
                }
            }
        }

        public static void Initialize(IEnumerable<Project> projects)
        {
            foreach (Project project in projects)
                Initialize(project);
        }

        public static void Remove(IEnumerable<Project> projects)
        {
            foreach (Project project in projects)
                Remove(project);
        }
    }
}
