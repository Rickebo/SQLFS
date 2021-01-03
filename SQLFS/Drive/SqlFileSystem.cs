using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DokanNet;
using DokanNet.Logging;
using Serilog;
using FileAccess = DokanNet.FileAccess;

namespace SQLFS.Drive
{
    public class SqlFileSystem<T> : IDokanOperations where T : FileBase
    {
        private readonly ISqlFileSystemOptions<T> _options;
        private readonly Database.FsDatabase<T> _fsDatabase;

        private readonly string _root = "\\";

        private readonly FileSystemSecurity _securityTemplate;

        public SqlFileSystem(ISqlFileSystemOptions<T> options, Database.FsDatabase<T> fsDatabase)
        {
            _options = options;
            _fsDatabase = fsDatabase;
            _securityTemplate = File.Exists(_options.SecurityTemplate)
                    ? (FileSystemSecurity)new FileSecurity(_options.SecurityTemplate, AccessControlSections.All)
                    : (FileSystemSecurity)new DirectorySecurity(_options.SecurityTemplate, AccessControlSections.Access);
        }

        private string FormatFileName(string filename)
        {
            if (filename.StartsWith(_root))
                filename = filename.Substring(_root.Length);

            return filename;
        }

        public NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options,
            FileAttributes attributes, IDokanFileInfo info)
        {
            try
            {
                if (fileName == "\\" || fileName == "\\\\")
                    return DokanResult.Success;

                fileName = FormatFileName(fileName);

                if (info.IsDirectory)
                {
                    var dir = _fsDatabase.GetFileInfo(fileName).Result;

                    switch (mode)
                    {
                        case FileMode.Open:
                            {
                                if (dir == null)
                                    return DokanResult.PathNotFound;

                                if (!dir.IsDirectory)
                                    return DokanResult.NotADirectory;

                                info.Context = dir;
                                info.IsDirectory = true;

                                return DokanResult.Success;
                            }

                        case FileMode.CreateNew:
                            {
                                if (dir != null)
                                    return DokanResult.FileExists;

                                dir = _options.FileFactory.Create(fileName);
                                dir.IsDirectory = true;

                                return _fsDatabase.CreateFile(dir, false).Result > 0
                                    ? DokanResult.Success
                                    : DokanResult.InternalError;
                            }


                        case FileMode.Truncate:
                        case FileMode.Create:
                            break;
                    }

                    return DokanResult.Success;
                }
                else
                {
                    switch (mode)
                    {
                        case FileMode.Open:
                            {
                                var existingFile = _fsDatabase.GetFileInfo(fileName).Result;

                                if (existingFile == null)
                                    return DokanResult.FileNotFound;

                                if (existingFile.IsDirectory && (access & FileAccess.Delete) != 0 &&
                                    (access & FileAccess.Synchronize) == 0)
                                    return DokanResult.AccessDenied;

                                info.IsDirectory = existingFile.IsDirectory;
                                info.Context = new object();

                                return DokanResult.Success;
                            }


                        case FileMode.CreateNew:
                            {
                                var existingFile = _fsDatabase.GetFileInfo(fileName).Result;

                                if (existingFile != null)
                                    return DokanResult.FileExists;

                                break;
                            }

                        case FileMode.Truncate:
                            {
                                var existingFile = _fsDatabase.GetFileInfo(fileName).Result;

                                if (existingFile == null)
                                    return DokanResult.FileNotFound;


                                return SetFileLength(existingFile, 0);
                            }
                    }

                    T file;

                    if (mode == FileMode.Create || mode == FileMode.CreateNew)
                    {
                        var change = _fsDatabase.CreateFile(
                                _options.FileFactory.Create(fileName),
                                update: mode == FileMode.Create
                                ).Result;

                        if (change <= 0)
                            return DokanResult.InternalError;

                        file = _fsDatabase.GetFileInfo(fileName).Result;
                        info.Context = file;
                        info.IsDirectory = file.IsDirectory;

                        return DokanResult.Success;
                    }

                    file = _fsDatabase.GetFileInfo(fileName).Result;

                    if (file == null && mode == FileMode.OpenOrCreate)
                    {
                        if (_fsDatabase.CreateFile(_options.FileFactory.Create(fileName), update: false).Result <= 0)
                            return DokanResult.InternalError;

                        file = _fsDatabase.GetFileInfo(fileName).Result;
                    }

                    if (file == null)
                        return DokanResult.FileNotFound;

                    info.Context = file;
                    info.IsDirectory = file.IsDirectory;

                    return DokanResult.Success;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "CreateFile encountered an exception.");
                return DokanResult.InternalError;
            }
        }

        public void Cleanup(string fileName, IDokanFileInfo info)
        {

        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {

        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            try
            {
                if (fileName == "\\" || fileName == "\\\\")
                {
                    bytesRead = 0;
                    return DokanResult.Success;
                }

                fileName = FormatFileName(fileName);
                var file = _fsDatabase.ReadFile(fileName).Result;

                if (file == null)
                {
                    bytesRead = 0;
                    return DokanResult.FileNotFound;
                }

                if (file.IsDirectory)
                {
                    bytesRead = 0;
                    return DokanResult.FileNotFound;
                }

                bytesRead = (int)Math.Min(file.Data.Length - offset, buffer.Length);
                Array.Copy(file.Data, offset, buffer, 0, bytesRead);
                return DokanResult.Success;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ReadFile encountered an exception.");

                bytesRead = 0;
                return DokanResult.InternalError;
            }
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            try
            {
                if (fileName == "\\" || fileName == "\\\\")
                {
                    bytesWritten = 0;
                    return DokanResult.Success;
                }

                fileName = FormatFileName(fileName);
                var file = _fsDatabase.ReadFile(fileName).Result;
                var data = file.Data;

                if (offset == -1)
                    offset = data.Length;

                var fileBuffer = file.Data.Length >= buffer.Length + offset
                        ? file.Data
                        : new byte[buffer.Length + offset];

                if (fileBuffer != file.Data)
                    Array.Copy(file.Data, fileBuffer, file.Data.Length);

                Array.Copy(buffer, 0, fileBuffer, offset, buffer.Length);

                file.SetData(fileBuffer);

                bytesWritten = buffer.Length;

                return _fsDatabase.SaveFile(file).Result > 0
                        ? DokanResult.Success
                        : DokanResult.InternalError;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "WriteFile encountered an exception.");
                bytesWritten = 0;
                return DokanResult.InternalError;
            }
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            try
            {
                if (fileName == "\\" || fileName == "\\\\")
                {
                    fileInfo = new FileInformation
                    {
                        Attributes = FileAttributes.Directory,
                    };

                    return DokanResult.Success;
                }

                fileName = FormatFileName(fileName);
                var file = _fsDatabase.ReadFile(fileName).Result;

                var attributes = file.IsDirectory
                        ? FileAttributes.Directory
                        : FileAttributes.Normal;

                fileInfo = new FileInformation()
                {
                    CreationTime = file.CreationTime,
                    LastAccessTime = file.AccessTime,
                    LastWriteTime = file.LastModifyTime,
                    Length = file.Data.Length,
                    Attributes = attributes,
                    FileName = file.Name
                };

                return DokanResult.Success;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GetFileInformation encountered an exception.");
                fileInfo = default;
                return DokanResult.InternalError;
            }
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            return FindFilesInternal(fileName, "", out files, info);
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            return FindFilesInternal(fileName, searchPattern, out files, info);
        }

        private NtStatus FindFilesInternal(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            try
            {
                fileName = FormatFileName(fileName);
                fileName += searchPattern;
                fileName = fileName.Trim('*');

                var pattern = fileName + "*";
                var results = _fsDatabase.FindFiles(fileName, pattern).Result;

                files = new List<FileInformation>(results.Count);

                foreach (var file in results)
                {
                    var attributes = file.IsDirectory
                            ? FileAttributes.Directory
                            : FileAttributes.Normal;

                    files.Add(new FileInformation
                    {
                        Attributes = attributes,
                        CreationTime = file.CreationTime,
                        FileName = file.Name,
                        LastAccessTime = file.AccessTime,
                        LastWriteTime = file.LastModifyTime,
                        Length = file.Length
                    });
                }

                return DokanResult.Success;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FindFilesInternal encountered an exception.");

                files = null;
                return DokanResult.InternalError;
            }
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            try
            {
                fileName = FormatFileName(fileName);
                var file = _fsDatabase.GetFileInfo(fileName).Result;

                if (file == null)
                    return DokanResult.FileNotFound;

                file.IsDirectory = (attributes & FileAttributes.Directory) != 0 || info.IsDirectory;

                return _fsDatabase.UpdateFlags(file.Name, file.FileFlags).Result > 0
                        ? DokanResult.Success
                        : DokanResult.InternalError;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SetFileAttributes encountered an exception.");
                return DokanResult.InternalError;
            }
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime,
            IDokanFileInfo info)
        {
            try
            {
                fileName = FormatFileName(fileName);
                return _fsDatabase.UpdateTime(fileName, creationTime, lastAccessTime, lastWriteTime).Result > 0
                        ? DokanResult.Success
                        : DokanResult.InternalError;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SetFileTime encountered an exception.");
                return DokanResult.InternalError;
            }
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            try
            {
                fileName = FormatFileName(fileName);
                return _fsDatabase.DeleteFile(FormatFileName(fileName)).Result > 0
                        ? DokanResult.Success
                        : DokanResult.InternalError;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DeleteFile encountered an exception.");
                return DokanResult.InternalError;
            }
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            try
            {
                fileName = FormatFileName(fileName);
                return _fsDatabase.DeleteFile(FormatFileName(fileName)).Result > 0
                        ? DokanResult.Success
                        : DokanResult.InternalError;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DeleteDirectory encountered an exception.");
                return DokanResult.InternalError;
            }
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            try
            {
                return _fsDatabase.MoveFile(FormatFileName(oldName), FormatFileName(newName), replace).Result > 0
                        ? DokanResult.Success
                        : DokanResult.InternalError;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MoveFile encountered an exception.");
                return DokanResult.InternalError;
            }
        }

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            try
            {
                fileName = FormatFileName(fileName);
                var file = _fsDatabase.ReadFile(fileName).Result;

                return SetFileLength(file, length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SetEndOfFile encountered an exception.");
                return DokanResult.InternalError;
            }
        }

        private NtStatus SetFileLength(T file, long length)
        {
            var bytes = file.Data;
            var newBytes = new byte[length];

            if (length > 0)
                Array.Copy(bytes, 0, newBytes, 0, Math.Min(bytes.Length, newBytes.Length));

            file.SetData(newBytes);
            return _fsDatabase.SaveFile(file).Result > 0
                    ? DokanResult.Success
                    : DokanResult.InternalError;
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes,
            IDokanFileInfo info)
        {
            freeBytesAvailable = _options.FreeSpace;
            totalNumberOfBytes = _options.Space;
            totalNumberOfFreeBytes = freeBytesAvailable;

            return DokanResult.Success;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName,
            out uint maximumComponentLength, IDokanFileInfo info)
        {
            features = default;
            features |= FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                        FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                        FileSystemFeatures.UnicodeOnDisk;

            fileSystemName = "SQLFS";
            maximumComponentLength = 256;
            volumeLabel = _options.VolumeName;

            return DokanResult.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections,
            IDokanFileInfo info)
        {
            security = _securityTemplate;

            return DokanResult.Success;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections,
            IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus Mounted(IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus Unmounted(IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            streams = new FileInformation[0];
            return DokanResult.Success;
        }
    }
}
