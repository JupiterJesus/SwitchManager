using System;
using System.Diagnostics;
using System.IO;
using SwitchManager.util;

namespace SwitchManager.io
{
    public class FileUtils
    {
        internal static FileStream OpenWriteStream(string path, bool append = false, int timeoutMs = 500)
        {
            var time = Stopwatch.StartNew();
            while (time.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        if (append)
                        {
                            return new FileStream(path, FileMode.Append, FileAccess.Write);
                        }
                        else
                        {
                            return new FileStream(path, FileMode.Truncate, FileAccess.Write);
                        }
                    }
                    else
                    {
                        return new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                    }
                }
                catch (IOException e)
                {
                    // access error
                    if (e.HResult != -2147024864)
                        throw;
                }
            }

            throw new TimeoutException($"Failed to get a write handle to {path} within {timeoutMs}ms.");
        }

        internal static FileStream OpenReadWriteStream(string path, bool append = false, int timeoutMs = 500)
        {
            var time = Stopwatch.StartNew();
            while (time.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                }
                catch (IOException e)
                {
                    // access error
                    if (e.HResult != -2147024864)
                        throw;
                }
            }

            throw new TimeoutException($"Failed to get a write handle to {path} within {timeoutMs}ms.");
        }

        internal static long? GetFileSystemSize(string romPath)
        {
            if (string.IsNullOrWhiteSpace(romPath))
                return null;

            if (File.Exists(romPath))
                return new FileInfo(romPath).Length;
            else if (Directory.Exists(romPath))
                return new DirectoryInfo(romPath).GetSize();
            else
                return null;
        }

        internal static void DeleteDirectory(string dir, bool recursive = false)
        {
            if (string.IsNullOrWhiteSpace(dir)) return;

            //GC.Collect();
            //GC.WaitForPendingFinalizers();
            if (Directory.Exists(dir))
            {
                if (recursive)
                {
                    foreach (var f in Directory.EnumerateFiles(dir))
                        DeleteFile(f);

                    foreach (var d in Directory.EnumerateDirectories(dir))
                        DeleteDirectory(d, recursive);
                }
                Directory.Delete(dir);
            }
        }

        internal static void DeleteDirectory(DirectoryInfo dir, bool recursive = false)
        {
            if (dir == null) return;

            //GC.Collect();
            //GC.WaitForPendingFinalizers();
            if (dir.Exists)
            {
                if (recursive)
                {
                    foreach (var f in dir.EnumerateFiles())
                        DeleteFile(f.FullName);

                    foreach (var d in dir.EnumerateDirectories())
                        DeleteDirectory(d, recursive);
                }
                dir.Delete();
            }
        }

        internal static void DeleteFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return;

            if (File.Exists(file))
            {
                File.Delete(file);
                //GC.Collect();
                //GC.WaitForPendingFinalizers();
            }
        }
    }
}