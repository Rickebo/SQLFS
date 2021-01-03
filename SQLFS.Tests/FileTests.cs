using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DokanNet;
using DokanNet.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Serilog;
using SQLFS.Database;
using SQLFS.Drive;
using FileAccess = System.IO.FileAccess;

namespace SQLFS.Tests
{
    [TestClass]
    public class FileTests
    {
        private SqlFileSystem<FileBase> _sqlFs;
        private Thread _mountThread;

        private const string Mount = "T:\\";

        private int _currentFileCount = 1;

        [TestInitialize]
        public void Initialize()
        {
            
            var prefix = "-wd=";
            var wdArg = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.InvariantCulture));

            if (wdArg != null)
                Environment.CurrentDirectory = wdArg.Substring(prefix.Length);

            var settingsContent = File.ReadAllText("settings.json");
            var settings = JsonConvert.DeserializeObject<SQLFS.Tests.Settings<FileBase>>(settingsContent);

            settings.SecurityTemplate ??= Environment.CurrentDirectory;
            settings.FileTemplate ??= FileBase.Create(null);
            settings.FileFactory ??= new TestFileFactory();

            var db = new FsDatabase<FileBase>(settings, settings.FileFactory, settings.FileTemplate);

            var _ = db.DropTables().Result;
            _ = db.CreateTables().Result;

            var drive = new SqlFileSystem<FileBase>(settings, db);
            const DokanOptions options = DokanOptions.NetworkDrive;

            _mountThread = new Thread(() => 
                    drive.Mount(Mount, options, Environment.ProcessorCount / 2, logger: new ConsoleLogger()));

            _mountThread.Start();
            
            while (!Directory.Exists(Mount))
                Thread.Sleep(25);
        }

        private string GetFileName() => 
            Path.Combine(Mount, $"test-file{_currentFileCount++}.test");

        [TestCleanup]
        public void Cleanup()
        {
            Dokan.Unmount(Mount[0]);
            //_mountThread.Abort();
        }

        [TestMethod]
        public void TestCreateFile()
        {
            var filePath = GetFileName();
            File.Create(filePath);

            Assert.IsTrue(File.Exists(filePath));
        }

        [TestMethod]
        public void TestReadWrite()
        {
            var filePath = GetFileName();
            var fs = File.Create(filePath);

            var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

            fs.Write(bytes);
            fs.Close();

            var readBytes = File.ReadAllBytes(filePath);

            Assert.AreEqual(bytes.Length, readBytes.Length);
            Assert.IsTrue(bytes.SequenceEqual(readBytes));
        }

        [TestMethod]
        public void TestReadWriteShorter()
        {
            var filePath = GetFileName();
            var fs = File.Create(filePath);

            var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            var bytesShorter = new byte[] { 1, 9, 3, 4, 5, 6, 7, 8, 9, 10 };

            fs.Write(bytes);
            fs.Close();

            fs = File.Open(filePath, FileMode.Truncate, FileAccess.Write);
            fs.Seek(0, SeekOrigin.Begin);
            fs.Write(bytesShorter);
            fs.Close();

            var readBytes = File.ReadAllBytes(filePath);

            Assert.AreEqual(bytesShorter.Length, readBytes.Length);
            Assert.IsTrue(bytesShorter.SequenceEqual(readBytes));
        }

        [TestMethod]
        public void TestSetLength()
        {
            var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            var bytesShorter = new byte[] { 1, 2, 3, 4, 5, 6, 7 };

            var filePath = GetFileName();
            File.WriteAllBytes(filePath, bytes);

            var fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite);
            fs.SetLength(bytesShorter.Length);
            fs.Close();

            var readBytes = File.ReadAllBytes(filePath);

            Assert.AreEqual(bytesShorter.Length, readBytes.Length);
            Assert.IsTrue(bytesShorter.SequenceEqual(readBytes));
        }

        [TestMethod]
        public void TestCreateOverwrite()
        {
            var filename = GetFileName();

            var fs = File.Create(filename);

            var byteA = new byte[] { 1, 2, 3, 4, 5 };
            var byteB = new byte[] { 1, 2, 3, 4 };

            fs.Write(byteA);
            fs.Close();

            fs = File.Create(filename);
            fs.Write(byteB);
            fs.Close();

            var readBytes = File.ReadAllBytes(filename);

            Assert.AreEqual(byteB.Length, readBytes.Length);
            Assert.IsTrue(byteB.SequenceEqual(readBytes));
        }
    }
}
