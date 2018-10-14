using DokanNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dokan_layfs
{
    class Program
    {
        class CommandLineUsageException : Exception
        {

        }

        static string WritePath = null;
        static string ReadOnlyPath = null;
        static string MountPoint = null;
        static string VolumeLabel = "LayFs";
        static bool DebugMode = false;
        static int ThreadCount = 3;

        private static void argtos(string[] args, ref int i, ref string v)
        {
            if (args.Length > ++i)
                v = args[i];
            else
                throw new CommandLineUsageException();
        }

        private static void argtol(string[] args, ref int i, ref int v)
        {
            int r;
            if (args.Length > ++i)
                v = Int32.TryParse(args[i], out r) ? r : v;
            else
                throw new CommandLineUsageException();
        }

        private static void ReadArguments(string[] args)
        {
            int i = 0;
            for (i = 0; args.Length > i; i++)
            {
                string a = args[i];

                if ('-' != a[0])
                    break;

                switch (a[1])
                {
                    case '?':
                        throw new CommandLineUsageException();

                    case 'r':
                        argtos(args, ref i, ref ReadOnlyPath);
                        break;

                    case 'w':
                        argtos(args, ref i, ref WritePath);
                        break;

                    case 'm':
                        argtos(args, ref i, ref MountPoint);
                        break;

                    case 'd':
                        DebugMode = true;
                        break;

                    case 'l':
                        argtos(args, ref i, ref VolumeLabel);
                        break;

                    case 't':
                        argtol(args, ref i, ref ThreadCount);
                        break;

                    default:
                        throw new CommandLineUsageException();
                }
            }
            if (args.Length > i)
                throw new CommandLineUsageException();
        }

        static void Main(string[] args)
        {
            try
            {
                ReadArguments(args);
                
                if(ReadOnlyPath == null || WritePath == null || MountPoint == null)
                {
                    throw new CommandLineUsageException();
                }

                var fs = new LayeredFileSystem(ReadOnlyPath, WritePath, VolumeLabel);

                DokanOptions opt = DokanOptions.RemovableDrive;

                if(DebugMode)
                {
                    opt = opt | DokanOptions.DebugMode;
                }

                try
                {
                    fs.Mount(MountPoint, opt, ThreadCount, DebugMode ? null : new DokanNet.Logging.NullLogger());
                }
                catch (DllNotFoundException ex)
                {
                    if (ex.Message.Contains("dokan1.dll"))
                        Console.WriteLine("you need Dokan to run this app");
                    else
                        throw;
                }
            }
            catch (CommandLineUsageException)
            {
                Console.Write(
                "usage: layfs OPTIONS\n" +
                "\n" +
                "options:\n" +
                "    -d                  [enable debug output]\n" +
                "    -r Directory        [read only file system]\n" +
                "    -w Directory        [write file system]\n" +
                "    -m MountPoint       [i.e: X:\\]\n");
            }
            catch (Exception ex)
            {
                Console.Write($"Unexpected exception: \n {ex.Message}");
            }
        }
    }
}
