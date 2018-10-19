using DokanNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace dokan_layfs
{
    class Utils
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FILE_BASIC_INFO
        {
            public UInt64 CreationTime;
            public UInt64 LastAccessTime;
            public UInt64 LastWriteTime;
            public UInt64 ChangeTime;
            public UInt32 FileAttributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILE_DISPOSITION_INFO
        {
            public Boolean DeleteFile;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public UInt32 dwFileAttributes;
            public UInt64 ftCreationTime;
            public UInt64 ftLastAccessTime;
            public UInt64 ftLastWriteTime;
            public UInt32 dwVolumeSerialNumber;
            public UInt32 nFileSizeHigh;
            public UInt32 nFileSizeLow;
            public UInt32 nNumberOfLinks;
            public UInt32 nFileIndexHigh;
            public UInt32 nFileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Boolean SetFileInformationByHandle(
            IntPtr hFile,
            Int32 FileInformationClass,
            ref FILE_BASIC_INFO lpFileInformation,
            UInt32 dwBufferSize);


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Boolean SetFileInformationByHandle(
            IntPtr hFile,
            Int32 FileInformationClass,
            ref FILE_DISPOSITION_INFO lpFileInformation,
            UInt32 dwBufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Boolean GetFileInformationByHandle(
            IntPtr hFile,
            out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern Boolean MoveFileExW(
            [MarshalAs(UnmanagedType.LPWStr)] String lpExistingFileName,
            [MarshalAs(UnmanagedType.LPWStr)] String lpNewFileName,
            UInt32 dwFlags);

        public static void SetFileInformationToFileStream(
            FileStream stream,
            FileAttributes fileAttributes,
            DateTime? creationTime,
            DateTime? lastAccessTime,
            DateTime? lastWriteTime)
        {
            if (0 == fileAttributes)
                fileAttributes = System.IO.FileAttributes.Normal;

            FILE_BASIC_INFO info = default(FILE_BASIC_INFO);

            if (unchecked((UInt32)(-1)) != (UInt32)fileAttributes)
                info.FileAttributes = (UInt32)fileAttributes;

            if (creationTime.HasValue)
                info.CreationTime = (ulong)creationTime.Value.ToFileTimeUtc();

            if (lastAccessTime.HasValue)
                info.LastAccessTime = (ulong)lastAccessTime.Value.ToFileTimeUtc();

            if (lastWriteTime.HasValue)
                info.LastWriteTime = (ulong)lastWriteTime.Value.ToFileTimeUtc();

            if (!SetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(),
                0/*FileBasicInfo*/, ref info, (UInt32)Marshal.SizeOf(info)))
                throw Marshal.GetExceptionForHR(Marshal.GetLastWin32Error());
        }

        public static void SetFileInformationToDirectoryInfo(
            DirectoryInfo dirInfo,
            FileAttributes fileAttributes,
            DateTime? creationTime,
            DateTime? lastAccessTime,
            DateTime? lastWriteTime)
        {
            if (0 == fileAttributes)
                fileAttributes = System.IO.FileAttributes.Normal;

            if (unchecked((UInt32)(-1)) != (UInt32)fileAttributes)
                dirInfo.Attributes = fileAttributes;
            if (creationTime.HasValue)
                dirInfo.CreationTimeUtc = creationTime.Value;
            if (lastAccessTime.HasValue)
                dirInfo.LastAccessTimeUtc = lastAccessTime.Value;
            if (lastWriteTime.HasValue)
                dirInfo.LastWriteTimeUtc = lastWriteTime.Value;
        }

        public static void SetFileDisposition(FileStream stream, bool safe)
        {
            FILE_DISPOSITION_INFO info;
            info.DeleteFile = true;
            if (!SetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(),
                4/*FileDispositionInfo*/, ref info, (UInt32)Marshal.SizeOf(info)))
                if (!safe)
                    throw Marshal.GetExceptionForHR(Marshal.GetLastWin32Error());
        }

        public static void CreateFileInformationFromFileStream(FileStream stream, out FileInformation fileInfo)
        {
            BY_HANDLE_FILE_INFORMATION info;
            if (!GetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(), out info))
                throw Marshal.GetExceptionForHR(Marshal.GetLastWin32Error());
            
            fileInfo = new FileInformation()
            {
                Attributes = (FileAttributes)info.dwFileAttributes,
                CreationTime = DateTime.FromFileTime((long)info.ftCreationTime),
                LastAccessTime = DateTime.FromFileTime((long)info.ftLastAccessTime),
                LastWriteTime = DateTime.FromFileTime((long)info.ftLastWriteTime),
                Length = info.nFileSizeHigh << 32 | info.nFileSizeLow
            };
        }

        public static FileInformation CreateFileInformation(FileSystemInfo fsinfo)
        {
            return new FileInformation()
            {
                FileName = fsinfo.Name,
                Attributes = fsinfo.Attributes,
                CreationTime = fsinfo.CreationTimeUtc,
                LastAccessTime = fsinfo.LastAccessTimeUtc,
                LastWriteTime = fsinfo.LastWriteTimeUtc,
                Length = (fsinfo as FileInfo)?.Length ?? 0
            };
        }
    }
}
