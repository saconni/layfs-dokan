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
    class LayeredFileContext : LayeredContext
    {
        public override bool IsWritable { get; set; } = false;

        public FileStream Stream = null;

        public LayeredFileContext(string fileName) : base(fileName)
        {
            
        }

        public override void Dispose()
        {
            if(Stream != null)
            {
                Stream.Dispose();
                Stream = null;
            }
        }

        public override void Delete()
        {
            if(IsWritable)
            {
                Utils.SetFileDisposition(Stream, true);
            }
        }

        public override FileInformation GetFileInformation()
        {
            FileInformation info = new FileInformation();
            Utils.CreateFileInformationFromFileStream(Stream, out info);
            return info;
        }

        public override FileSystemSecurity GetFileSystemSecurity()
        {
            return Stream.GetAccessControl();
        }

        public override void SetAttributes(FileAttributes attributes)
        {
            Utils.SetFileInformationToFileStream(Stream, attributes, null, null, null);
        }

        public override void SetFileTime(DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime)
        {
            Utils.SetFileInformationToFileStream(Stream, (FileAttributes)(-1), creationTime, lastAccessTime, lastWriteTime);
        }

        public override void SetFileSystemSecurity(FileSystemSecurity security)
        {
            Stream.SetAccessControl(security as FileSecurity);
        }
    }
}
