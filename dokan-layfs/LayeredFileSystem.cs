using DokanNet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using FileAccess = DokanNet.FileAccess;

namespace dokan_layfs
{
    class LayeredFileSystem : IDokanOperations
    {
        private string _readOnlyPath = default(string);
        private string _writePath = default(string);
        private string _volumeLabel = default(string);

        private const FileAccess ReadAttributes = FileAccess.ReadData | FileAccess.WriteData | FileAccess.AppendData |
                                                  FileAccess.Execute | FileAccess.WriteAttributes |
                                                  FileAccess.GenericExecute | FileAccess.GenericWrite |
                                                  FileAccess.GenericRead | FileAccess.Delete;

        private const FileAccess DataWriteAccess = FileAccess.WriteData | FileAccess.AppendData |
                                                   FileAccess.Delete | FileAccess.WriteAttributes |
                                                   FileAccess.GenericWrite;

        private static string NormalizePath(string path)
        {
            path = Path.GetFullPath(path);

            if (path.EndsWith(@"\"))
            {
                path.Substring(0, path.Length - 1);
            }

            if (!Directory.Exists(path))
            {
                throw new ArgumentException($"{path} doesn't exists or can't be accessed");
            }

            return path;
        }

        private string GetReadOnlyPath(string fileName)
        {
            return _readOnlyPath + fileName;
        }

        private string GetWritePath(string fileName)
        {
            return _writePath + fileName;
        }

        public LayeredFileSystem(string readOnlyPath, string writePath, string volumeLabel)
        {
            _readOnlyPath = NormalizePath(readOnlyPath);
            _writePath = NormalizePath(writePath);
            _volumeLabel = volumeLabel;
        }

        public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            try
            {
                var ret = UnsafeCreateFile(fileName, access, share, mode, options, attributes, info);
                return ret;
            }
            catch (UnauthorizedAccessException) // don't have access rights
            {
                (info.Context as LayeredContext)?.Dispose();

                return DokanResult.AccessDenied;
            }
            catch (DirectoryNotFoundException)
            {
                (info.Context as LayeredContext)?.Dispose();

                return DokanResult.PathNotFound;
            }
            catch (Exception ex)
            {
                (info.Context as LayeredContext)?.Dispose();

                var hr = (uint)Marshal.GetHRForException(ex);
                switch (hr)
                {
                    case 0x80070020: //Sharing violation
                        return DokanResult.SharingViolation;
                    default:
                        throw;
                }
            }
        }

        public NtStatus UnsafeCreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            string writePath = GetWritePath(fileName);
            string readOnlyPath = GetReadOnlyPath(fileName);

            if (info.IsDirectory)
            {
                if (mode == FileMode.CreateNew)
                {
                    if (Directory.Exists(writePath))
                    {
                        return DokanResult.FileExists;
                    }
                    else if (File.Exists(writePath))
                    {
                        return DokanResult.AlreadyExists;
                    }
                    else if (Directory.Exists(readOnlyPath))
                    {
                        return DokanResult.FileExists;
                    }
                    else if (File.Exists(readOnlyPath))
                    {
                        return DokanResult.AlreadyExists;
                    }

                    info.Context = new LayeredDirectoryContext(fileName)
                    {
                        WriteDirInfo = Directory.CreateDirectory(writePath)
                    };
                    return DokanResult.Success;
                }
                else if (mode == FileMode.Open)
                {
                    var context = new LayeredDirectoryContext(fileName);

                    if(Directory.Exists(writePath))
                    {
                        context.WriteDirInfo = new DirectoryInfo(writePath);
                    }
                    else if(File.Exists(writePath))
                    {
                        return DokanResult.NotADirectory;
                    }

                    if(Directory.Exists(readOnlyPath))
                    {
                        context.ReadOnlyDirInfo = new DirectoryInfo(readOnlyPath);
                    }
                    else if(context.WriteDirInfo == null)
                    {
                        if(File.Exists(readOnlyPath))
                        {
                            return DokanResult.NotADirectory;
                        }
                        else
                        {
                            return DokanResult.PathNotFound;
                        }
                    }

                    info.Context = context;
                    return DokanResult.Success;
                }
                else
                {
                    // unkown operation
                    return DokanResult.Unsuccessful;
                }
            }
            else
            {
                var writable = false;
                var pathExists = false;
                var pathIsDirectory = Directory.Exists(writePath);

                if (pathIsDirectory)
                {
                    pathExists = true;
                }
                else if (File.Exists(writePath))
                {
                    writable = true;
                    pathExists = true;
                }
                else
                {
                    if (pathIsDirectory = Directory.Exists(readOnlyPath))
                    {
                        pathExists = true;
                        pathIsDirectory = true;
                    }
                    else
                    {
                        pathExists = File.Exists(readOnlyPath);
                    }
                }

                var readAttributes = (access & ReadAttributes) == 0;
                var readAccess = (access & DataWriteAccess) == 0;

                switch (mode)
                {
                    case FileMode.Open:
                        if (!pathExists)
                        {
                            return DokanResult.FileNotFound;
                        }
                        else
                        {
                            if (pathIsDirectory)
                            {
                                info.IsDirectory = true;

                                if ((access & FileAccess.Delete) == FileAccess.Delete
                                    && (access & FileAccess.Synchronize) != FileAccess.Synchronize)
                                {
                                    // it is a DeleteFile request on a directory
                                    return DokanResult.AccessDenied;
                                }

                                // call it again, with the IsDirectory set to true
                                return UnsafeCreateFile(fileName, access, share, mode, options, attributes, info);
                            }
                            else if(readAttributes)
                            {
                                info.Context = new LayeredReadAttributesContext(fileName, writable ? writePath : readOnlyPath, pathIsDirectory);
                                return DokanResult.Success;
                            }
                        }
                        break;

                    case FileMode.CreateNew:
                        if(writable)
                        {
                            if(pathExists)
                                return DokanResult.FileExists;
                        }
                        writable = true;
                            
                        break;

                    case FileMode.Create:
                        writable = true;
                        break;

                    case FileMode.Truncate:
                        if (!pathExists)
                            return DokanResult.FileNotFound;
                        break;

                    case FileMode.OpenOrCreate:
                        if (!pathExists)
                            writable = true;
                        break;

                    default:
                        throw new Exception($"Unhandled FileMode {mode.ToString("g")}");
                }

                if (writable)
                {
                    var path = Path.GetDirectoryName(writePath);
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                }
                else if(!readAccess)
                {
                    var path = Path.GetDirectoryName(writePath);
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);

                    File.Copy(readOnlyPath, writePath, true);

                    FileInfo finfo = new FileInfo(readOnlyPath);
                    File.SetCreationTime(writePath, finfo.CreationTime);
                    File.SetLastAccessTime(writePath, finfo.LastAccessTime);
                    File.SetLastWriteTime(writePath, finfo.LastWriteTime);

                    writable = true;
                }

                LayeredFileContext context = new LayeredFileContext(fileName, writable ? writePath : readOnlyPath, writable);
                info.Context = context;

                try
                {
                    context.Stream = new FileStream(writable ? writePath : readOnlyPath, mode,
                            readAccess ? System.IO.FileAccess.Read : System.IO.FileAccess.ReadWrite, share, 4096, options);
                }
                catch (Exception ex)
                {
                    var hr = (uint)Marshal.GetHRForException(ex);
                    switch (hr)
                    {
                        case 0x80070020: //Sharing violation
                            return DokanResult.SharingViolation;
                        default:
                            throw;
                    }
                }

                if (mode == FileMode.CreateNew || mode == FileMode.Create) // files are always created as Archive
                {
                    attributes |= FileAttributes.Archive;
                    context.SetAttributes(attributes);
                }

                return DokanResult.Success;
            }
        }

        public void Cleanup(string fileName, DokanFileInfo info)
        {
            LayeredContext context = info.Context as LayeredContext;
            if (info != null)
            {
                if (info.DeleteOnClose)
                {
                    context.Delete();
                }

                context.Dispose();
            }
        }

        public void CloseFile(string fileName, DokanFileInfo info)
        {
            (info.Context as LayeredContext)?.Dispose();
        }

        public NtStatus DeleteDirectory(string fileName, DokanFileInfo info)
        {
            LayeredDirectoryContext context = info.Context as LayeredDirectoryContext;
            if (!context.IsWritable)
            {
                return DokanResult.AccessDenied;
            }
            else
            {
                IList<FileInformation> list;
                FindFiles(fileName, out list, info);
                if (list.Count > 0)
                    return DokanResult.DirectoryNotEmpty;
                return DokanResult.Success;
            }
        }

        public NtStatus DeleteFile(string fileName, DokanFileInfo info)
        {
            return DokanResult.Success;
            /*
            LayeredContext context = info.Context as LayeredContext;
            if (!context.IsWritable)
            {
                return DokanResult.AccessDenied;
            }
            else
            {
                return DokanResult.Success;  
            }
            */
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, DokanFileInfo info)
        {
            return FindFilesWithPattern(fileName, "*", out files, info);
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, DokanFileInfo info)
        {
            if (null != searchPattern)
                searchPattern = searchPattern.Replace('<', '*').Replace('>', '?').Replace('"', '.');
            else
                searchPattern = "*";

            LayeredDirectoryContext context = info.Context as LayeredDirectoryContext;

            SortedList<string, FileSystemInfo> list = new SortedList<string, FileSystemInfo>();

            if(context.WriteDirInfo != null)
            {
                IEnumerable e = context.WriteDirInfo.EnumerateFileSystemInfos(searchPattern);

                foreach(FileSystemInfo fsi in e)
                {
                    list.Add(fsi.Name, fsi);
                }
            }

            if(context.ReadOnlyDirInfo != null)
            {
                IEnumerable e = context.ReadOnlyDirInfo.EnumerateFileSystemInfos(searchPattern);

                foreach(FileSystemInfo fsi in e)
                {
                    if(!list.ContainsKey(fsi.Name))
                    {
                        list.Add(fsi.Name, fsi);
                    }
                }
            }

            files = list.Values.Select(fsinfo => Utils.CreateFileInformation(fsinfo)).ToArray();

            return DokanResult.Success;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, DokanFileInfo info)
        {
            streams = new FileInformation[0];
            return DokanResult.NotImplemented;
        }

        public NtStatus FlushFileBuffers(string fileName, DokanFileInfo info)
        {
            try
            {
                (info.Context as LayeredFileContext)?.Stream.Flush(true);
                return DokanResult.Success;
            }
            catch (IOException)
            {
                return DokanResult.DiskFull;
            }
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, DokanFileInfo info)
        {
            var dinfo = new DriveInfo(_writePath);
            freeBytesAvailable = dinfo.TotalFreeSpace;
            totalNumberOfBytes = dinfo.TotalSize;
            totalNumberOfFreeBytes = dinfo.AvailableFreeSpace;
            return DokanResult.Success;
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, DokanFileInfo info)
        {
            LayeredContext context = info.Context as LayeredContext;

            if(context == null)
            {
                fileInfo = new FileInformation();
                return DokanResult.InvalidHandle;
            }
            else
            {
                fileInfo = context.GetFileInformation();
                // fileInfo.FileName = Path.GetFileName(fileName);
                fileInfo.FileName = fileName;
                return DokanResult.Success;
            }
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            security = null;

            LayeredContext context = info.Context as LayeredContext;

            if (context == null)
            {
                return DokanResult.InvalidHandle;
            }

            try
            {
                security = context.GetFileSystemSecurity();
                return DokanResult.Success;
            }
            catch (UnauthorizedAccessException)
            {
                return DokanResult.AccessDenied;
            }
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, DokanFileInfo info)
        {
            volumeLabel = _volumeLabel;
            fileSystemName = "NTFS";
            maximumComponentLength = 256;

            features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                       FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                       FileSystemFeatures.UnicodeOnDisk;

            return DokanResult.Success;
        }

        public NtStatus LockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            var context = info.Context as LayeredFileContext;
            if(context == null)
            {
                return DokanResult.InvalidHandle;
            }

            try
            {
                // context.MakeWritable();
                context.Stream.Lock(offset, length);
                return DokanResult.Success;
            }
            catch (IOException)
            {
                return DokanResult.AccessDenied;
            }
        }

        public NtStatus Mounted(DokanFileInfo info)
        {
            return DokanResult.Success;
        }

        private static bool FileOrDirectoryExists(string path)
        {
            return (Directory.Exists(path) || File.Exists(path));
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, DokanFileInfo info)
        {
            if(FileOrDirectoryExists(GetWritePath(newName)) || FileOrDirectoryExists(GetReadOnlyPath(newName)))
            {
                return DokanResult.FileExists;
            }

            var oldpath = GetWritePath(oldName);
            var newpath = GetWritePath(newName);

            if (FileOrDirectoryExists(oldpath))
            {
                if (!Utils.MoveFileExW(oldpath, newpath, replace ? 1U/*MOVEFILE_REPLACE_EXISTING*/ : 0))
                    throw Marshal.GetExceptionForHR(Marshal.GetLastWin32Error());

                return DokanResult.Success;
            }
            else
            {
                return DokanResult.AccessDenied;
            }
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, DokanFileInfo info)
        {
            bytesRead = 0;

            if (info.Context == null)
            {
                return DokanResult.InvalidHandle;
            }

            var stream = (info.Context as LayeredFileContext).Stream;
            

            try
            {
                lock(stream)
                {
                    stream.Position =offset;
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                }
            }
            catch (Exception)
            {
                return DokanResult.Unsuccessful;
            }

            return DokanResult.Success;
        }

/*
        public void MakeWritable(LayeredFileContext context)
        {
            if(!context.IsWritable)
            {
                var path = Path.GetDirectoryName(GetWritePath(context.FileName));
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                path = GetWritePath(context.FileName);
                var readOnlyPath = GetReadOnlyPath(context.FileName);

                var fileInfo = context.GetFileInformation();

                context.Stream.Dispose();

                File.Copy(readOnlyPath, path, true);

                context.Stream = new FileStream(
                    path,
                    FileMode.Open,
                    System.IO.FileAccess.ReadWrite,
                    FileShare.Read | FileShare.Write | FileShare.Delete,
                    4096, 0);

            }
        }
*/

        public NtStatus SetAllocationSize(string fileName, long length, DokanFileInfo info)
        {
            var context = info.Context as LayeredFileContext;
            // context.MakeWritable();
            context.Stream.SetLength(length);
            return DokanResult.Success;           
        }

        public NtStatus SetEndOfFile(string fileName, long length, DokanFileInfo info)
        {
            var context = info.Context as LayeredFileContext;
            // context.MakeWritable();
            context.Stream.SetLength(length);
            return DokanResult.Success;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, DokanFileInfo info)
        {
            try
            {
                if (attributes != 0)
                {
                    (info.Context as LayeredContext).SetAttributes(attributes);
                }
                return DokanResult.Success;
            }
            catch (UnauthorizedAccessException)
            {
                return DokanResult.AccessDenied;
            }
            catch (FileNotFoundException)
            {
                return DokanResult.FileNotFound;
            }
            catch (DirectoryNotFoundException)
            {
                return DokanResult.PathNotFound;
            }
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            var context = info.Context as LayeredContext;
            context.SetFileSystemSecurity(security);
            return DokanResult.Success;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, DokanFileInfo info)
        {
            var context = info.Context as LayeredContext;
            context.SetFileTime(creationTime, lastAccessTime, lastWriteTime);
            return DokanResult.Success;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            try
            {
                (info.Context as LayeredFileContext).Stream.Lock(offset, length);
                return DokanResult.Success;
            }
            catch (IOException)
            {
                return DokanResult.AccessDenied;
            }
        }

        public NtStatus Unmounted(DokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, DokanFileInfo info)
        {
            var context = info.Context as LayeredFileContext;

            bytesWritten = 0;

            try
            {
                // context.MakeWritable();
                lock(context.Stream)
                {
                    context.Stream.Position = offset;
                    context.Stream.Write(buffer, 0, buffer.Length);
                }
                bytesWritten = buffer.Length;
                return DokanResult.Success;
            }
            catch(IOException ex)
            {
                return DokanResult.AccessDenied;
            }
        }
    }
}
