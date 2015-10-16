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
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE80;
using Microsoft.VisualStudio;

namespace BundlerMinifierVsix.Commands
{
    internal sealed class BundleOnSave : IVsFileChangeEvents
    {
        private readonly Package _package;
        private DTE2 _dte;
        private IVsFileChangeEx _fileChangeService;
        private IDictionary<string, IEnumerable<uint>> _directoryMonitors = new Dictionary<string, IEnumerable<uint>>();

        private bool _raiseEvents = true;

        public static void Initialize(Package package)
        {
            Instance = new BundleOnSave(package);
        }

        private IServiceProvider ServiceProvider
        {
            get
            {
                return _package;
            }
        }

        public static BundleOnSave Instance
        {
            get;
            private set;
        }

        private BundleOnSave(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            _package = package;

            _fileChangeService = this.ServiceProvider.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;
            _dte = this.ServiceProvider.GetService(typeof(DTE)) as DTE2;

            if (_fileChangeService != null && _dte != null)
            {
                _dte.Application.Events.SolutionEvents.ProjectAdded += HandleProjectAdded;
                _dte.Application.Events.SolutionEvents.ProjectRemoved += HandleProjectRemoved;

                // Solution "Opened" calls ProjectAdded for each Project in the solution, but closing a solution does not call ProjectRemoved.
                _dte.Application.Events.SolutionEvents.BeforeClosing += HandleSolutionClosing;
            }
        }

        private void HandleProjectAdded(Project project)
        {
            BundleConfigChanged(project.GetConfigFile());
        }

        public int FilesChanged(uint cChanges, string[] rgpszFile, uint[] rggrfChange)
        {
            foreach (var fileChanged in rgpszFile)
            {
                if (_raiseEvents)
                {
                    var project = GetProjectFromItem(fileChanged);

                    if (project != null)
                    {
                        var configFile = project.GetConfigFile();

                        BundleService.Process(configFile);
                    }
                }
            }

            return VSConstants.S_OK;
        }

        public int DirectoryChanged(string pszDirectory)
        {
            return VSConstants.S_OK; // No directory monitoring
        }

        public void BundleConfigChanged(string configFilePath)
        {
            if (!string.IsNullOrEmpty(configFilePath) && File.Exists(configFilePath))
            {
                ResetMonitor(configFilePath);
                MonitorChanges(configFilePath);
            }
        }

        private void ResetMonitor(string configFilePath)
        {
            IEnumerable<uint> directoryMonitorRefs;

            if (_directoryMonitors.TryGetValue(configFilePath, out directoryMonitorRefs))
            {
                foreach (var monitorRef in directoryMonitorRefs)
                {
                    _fileChangeService.UnadviseDirChange(monitorRef);
                }

                _directoryMonitors.Remove(configFilePath);
            }
        }

        private void MonitorChanges(string configFilePath)
        {
            var projectRootPath = GetProjectRootPath(configFilePath);

            if (projectRootPath != null)
            {
                var bundles = BundleHandler.GetBundles(configFilePath);
                var watchesAdded = new List<uint>();

                foreach (var bundle in bundles)
                {
                    uint watcherReference;
                    foreach (var inputFile in bundle.InputFiles)
                    {
                        _fileChangeService.AdviseFileChange($"{projectRootPath}{inputFile}", (uint)_VSFILECHANGEFLAGS.VSFILECHG_Time, this, out watcherReference);
                        watchesAdded.Add(watcherReference);
                    }
                }

                if (watchesAdded.Any())
                {
                    _directoryMonitors.Add(configFilePath, watchesAdded);
                }
            }
        }

        private Project GetProjectFromItem(string filePath)
        {
            var item = BundlerMinifierPackage._dte.Solution.FindProjectItem(filePath);

            if (item != null && item.ContainingProject != null)
            {
                return item.ContainingProject;
            }

            return null;
        }

        private string GetProjectRootPath(string configFilePath)
        {
            var project = GetProjectFromItem(configFilePath);

            if (project != null)
            {
                return project.GetRootFolder();
            }

            return null;
        }

        private void HandleProjectRemoved(Project project)
        {
            BundleConfigChanged(project.GetConfigFile());
        }

        private void HandleSolutionClosing()
        {
            var projects = _dte.Application?.Solution?.GetAllProjects();

            if (projects != null)
            {
                foreach (var project in projects)
                {
                    HandleProjectRemoved(project);
                }
            }
        }

        public void StopListening()
        {
            _raiseEvents = false;
        }

        public void ResumeListening()
        {
            _raiseEvents = true;
        }
    }
}
