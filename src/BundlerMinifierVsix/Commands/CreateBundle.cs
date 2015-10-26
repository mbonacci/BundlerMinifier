﻿
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BundlerMinifier;
using Microsoft.VisualStudio.Shell;

namespace BundlerMinifierVsix.Commands
{
    internal sealed class CreateBundle
    {
        private readonly Package _package;

        private CreateBundle(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            _package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(PackageGuids.guidBundlerCmdSet, PackageIds.CreateBundleId);
                var menuItem = new OleMenuCommand(AddBundle, menuCommandID);
                menuItem.BeforeQueryStatus += BeforeQueryStatus;
                commandService.AddCommand(menuItem);
            }
        }

        public static CreateBundle Instance
        {
            get;
            private set;
        }

        private IServiceProvider ServiceProvider
        {
            get
            {
                return _package;
            }
        }

        public static void Initialize(Package package)
        {
            Instance = new CreateBundle(package);
        }

        private void BeforeQueryStatus(object sender, EventArgs e)
        {
            var button = (OleMenuCommand)sender;
            var files = ProjectHelpers.GetSelectedItemPaths();

            if (files.Count() == 1)
                button.Text = "Minify File";
            else
                button.Text = "Bundle and Minify Files";

            button.Visible = BundleFileProcessor.IsSupported(files);
        }

        private void AddBundle(object sender, EventArgs e)
        {
            var item = ProjectHelpers.GetSelectedItems().FirstOrDefault();

            if (item == null || item.ContainingProject == null)
                return;

            string folder = item.ContainingProject.GetRootFolder();
            string configFile = Path.Combine(folder, Constants.CONFIG_FILENAME);
            IEnumerable<string> files = ProjectHelpers.GetSelectedItemPaths().Select(f => BundlerMinifier.FileHelpers.MakeRelative(configFile, f));
            string inputFile = item.Properties.Item("FullPath").Value.ToString();
            string outputFile = inputFile;

            if (files.Count() > 1)
            {
                outputFile = GetOutputFileName(inputFile, Path.GetExtension(files.First()));
            }

            if (string.IsNullOrEmpty(outputFile))
                return;

            BundlerMinifierPackage._dte.StatusBar.Progress(true, "Creating bundle", 0, 2);

            string relativeOutputFile = BundlerMinifier.FileHelpers.MakeRelative(configFile, outputFile);
            Bundle bundle = CreateBundleFile(files, relativeOutputFile);

            BundleHandler.AddBundle(configFile, bundle);

            BundlerMinifierPackage._dte.StatusBar.Progress(true, "Creating bundle", 1, 2);

            item.ContainingProject.AddFileToProject(configFile, "None");
            BundlerMinifierPackage._dte.StatusBar.Progress(true, "Creating bundle", 2, 2);

            BundleService.Process(configFile);
            BundlerMinifierPackage._dte.StatusBar.Progress(false, "Creating bundle");
            BundlerMinifierPackage._dte.StatusBar.Text = "Bundle created";
        }

        private static Bundle CreateBundleFile(IEnumerable<string> files, string outputFile)
        {
            var bundle = new Bundle
            {
                IncludeInProject = true,
                OutputFileName = outputFile
            };

            bundle.InputFiles.AddRange(files);
            return bundle;
        }

        //private static string MakeRelative(string baseFile, string file)
        //{
        //    Uri baseUri = new Uri(baseFile, UriKind.RelativeOrAbsolute);
        //    Uri fileUri = new Uri(file, UriKind.RelativeOrAbsolute);

        //    return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fileUri).ToString());
        //}

        private static string GetOutputFileName(string inputFile, string extension)
        {
            string ext = extension.TrimStart('.');

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.InitialDirectory = Path.GetDirectoryName(inputFile);
                dialog.DefaultExt = ext;
                dialog.FileName = "bundle";
                dialog.Filter = ext.ToUpperInvariant() + " File|*." + ext;

                DialogResult result = dialog.ShowDialog();

                if (result == DialogResult.OK)
                    return dialog.FileName;
            }

            return null;
        }
    }
}
