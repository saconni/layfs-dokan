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
    class LayeredReadAttributesContext : LayeredContext
    {
        private string _realPath;
        private bool _isDirectory;

        public LayeredReadAttributesContext(string fileName, string realPath, bool isDirectory) : base(fileName)
        {
            _realPath = realPath;
            _isDirectory = isDirectory;
        }

        public override bool IsWritable { get => false; }

        public override void Delete()
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            // do nothing
        }

        public override FileInformation GetFileInformation()
        {
            FileSystemInfo finfo;
            if (_isDirectory)
            {
                finfo = new DirectoryInfo(_realPath);
            }
            else
            {
                finfo = new FileInfo(_realPath);
            }

            var fileInfo = Utils.CreateFileInformation(finfo);

            return fileInfo;
        }

        public override FileSystemSecurity GetFileSystemSecurity()
        {
            return _isDirectory
                ? (FileSystemSecurity)Directory.GetAccessControl(_realPath)
                : File.GetAccessControl(_realPath);
        }

        public override void SetAttributes(FileAttributes attributes)
        {
            if (attributes != 0)
                File.SetAttributes(_realPath, attributes);
        }

        public override void SetFileSystemSecurity(FileSystemSecurity security)
        {
            throw new NotImplementedException();
        }

        public override void SetFileTime(DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime)
        {
            throw new NotImplementedException();
        }
    }
}
