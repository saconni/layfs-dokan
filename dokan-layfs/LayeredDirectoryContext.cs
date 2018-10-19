using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using DokanNet;

namespace dokan_layfs
{
    class LayeredDirectoryContext : LayeredContext
    {
        public override bool IsWritable
        {
            get
            {
                return WriteDirInfo != null;
            }
        }

        public DirectoryInfo ReadOnlyDirInfo = null;
        public DirectoryInfo WriteDirInfo = null;

        public LayeredDirectoryContext(string fileName) : base(fileName)
        {

        }

        public override void Dispose()
        {
            // do nothing
        }

        public override void Delete()
        {
            if(IsWritable)
            {
                WriteDirInfo.Delete();
            }
        }

        public override FileInformation GetFileInformation()
        {
            FileInformation info = Utils.CreateFileInformation(WriteDirInfo ?? ReadOnlyDirInfo);
            return info;
        }

        public override FileSystemSecurity GetFileSystemSecurity()
        {
            return (WriteDirInfo ?? ReadOnlyDirInfo).GetAccessControl();
        }

        public override void SetAttributes(FileAttributes attributes)
        {
            if(IsWritable)
            {
                Utils.SetFileInformationToDirectoryInfo(WriteDirInfo, attributes, null, null, null);
            }
        }

        public override void SetFileTime(DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime)
        {
            if(IsWritable)
            {
                Utils.SetFileInformationToDirectoryInfo(WriteDirInfo, (FileAttributes)(-1), creationTime, lastAccessTime, lastWriteTime);
            }
        }

        public override void SetFileSystemSecurity(FileSystemSecurity security)
        {
            if(IsWritable)
            {
                WriteDirInfo.SetAccessControl(security as DirectorySecurity);
            }
        }
    }
}
