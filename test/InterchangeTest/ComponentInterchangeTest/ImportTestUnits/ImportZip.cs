﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using System.IO;
using GME.CSharp;
using System.Text.RegularExpressions;

namespace ComponentImporterUnitTests
{
    public class ImportZip : IUseFixture<ImportZipFixture>
    {
        private static string testPath = Path.Combine(Common._importModelDirectory, "ImportZip");
        public static string mgaPath = Path.Combine(testPath, "InputModel.mga");
        private static string manifestFilePath = Path.Combine(testPath, "manifest.project.json");

        ImportZipFixture fixture;
        public void SetFixture(ImportZipFixture data)
        {
            fixture = data;
        }

        [Fact]
        public void ImportZIP()
        {
            var zipPath = Path.Combine(testPath, "TestModel.zip");

            // Import the ZIP file.
            // Check that we run without exception, have a manifest file,
            // have a folder for the component, and have 2 files in there.
            
            // Delete manifest and any subfolders
            File.Delete(manifestFilePath);
            if (Directory.Exists(Path.Combine(testPath, "components")))
                Directory.Delete(Path.Combine(testPath, "components"), true);
            
            var mgaProject = Common.GetProject(mgaPath);
            Assert.True(mgaProject != null, "Could not load MGA project.");

            bool resultIsNull = false;
            var mgaGateway = new MgaGateway(mgaProject);
            mgaProject.CreateTerritoryWithoutSink(out mgaGateway.territory);
            mgaGateway.PerformInTransaction(delegate
            {
                var importer = new CyPhyComponentImporter.CyPhyComponentImporterInterpreter();
                importer.Initialize(mgaProject);

                var result = importer.ImportFile(mgaProject, testPath, zipPath);
                if (result == null)
                    resultIsNull = true;

            });
            Assert.False(resultIsNull, "Exception occurred during import.");
            Assert.False(File.Exists(manifestFilePath), "Manifest erroneously generated");
        }

        [Fact]
        public void ZipIsMissingResource()
        {
            var zipPath = Path.Combine(testPath, "TestModel_missingResource.zip");

            var mgaProject = Common.GetProject(mgaPath);
            Assert.False(mgaProject == null, "Could not load MGA project.");

            var mgaGateway = new MgaGateway(mgaProject);
            mgaProject.CreateTerritoryWithoutSink(out mgaGateway.territory);
            mgaGateway.PerformInTransaction(delegate
            {
                var importer = new CyPhyComponentImporter.CyPhyComponentImporterInterpreter();
                importer.Initialize(mgaProject);

                Assert.DoesNotThrow(delegate
                {
                    var result = importer.ImportFile(mgaProject, testPath, zipPath);
                });
            });
        }

        [Fact]
        public void ZipIsMissingACM()
        {
            var zipPath = Path.Combine(testPath, "TestModel_noACM.zip");

            var mgaProject = Common.GetProject(mgaPath);
            Assert.False(mgaProject == null, "Could not load MGA project.");

            var mgaGateway = new MgaGateway(mgaProject);
            mgaProject.CreateTerritoryWithoutSink(out mgaGateway.territory);
            mgaGateway.PerformInTransaction(delegate
            {
                var importer = new CyPhyComponentImporter.CyPhyComponentImporterInterpreter();
                importer.Initialize(mgaProject);

                Assert.DoesNotThrow(delegate
                {
                    var result = importer.ImportFile(mgaProject, testPath, zipPath);
                });
            });
        }
    }
}