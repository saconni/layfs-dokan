using DokanNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace dokan_layfs
{
    abstract class LayeredContext
    {
        public abstract bool IsWritable { get; set; }

        public string FileName { get; private set; }

        public LayeredContext(string fileName)
        {
            FileName = fileName;
        }

        public abstract void Dispose();

        public abstract void Delete();

        public abstract FileInformation GetFileInformation();

        public abstract FileSystemSecurity GetFileSystemSecurity();

        public abstract void SetAttributes(FileAttributes attributes);

        public abstract void SetFileTime(DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime);

        public abstract void SetFileSystemSecurity(FileSystemSecurity security);
    }
}
