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

namespace BundlerMinifierVsix.Commands
{
    internal sealed class BundleOnSave : IVsFileChangeEvents
    {
        private readonly Package _package;
        private DTE2 _dte;
        private IVsFileChangeEx _fileChangeService;
        private IDictionary<string, uint> _

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
            var configFilePath = project.GetConfigFile();

            if(!string.IsNullOrEmpty(configFilePath) && File.Exists(configFilePath))
            {
                MonitorChanges(project, configFilePath);
            }
        }

        public int FilesChanged(uint cChanges, string[] rgpszFile, uint[] rggrfChange)
        {

        }

        public int DirectoryChanged(string pszDirectory)
        {

        }

        private void MonitorChanges(Project project, string configFilePath)
        {

        }

        private void HandleProjectRemoved(Project project)
        {

        }
        
        private void HandleSolutionClosing()
        {
            var projects = _dte.Application?.Solution?.GetAllProjects();

            if(projects != null)
            {
                foreach(var project in projects)
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
