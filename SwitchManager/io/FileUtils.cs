using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SwitchManager.util;

namespace SwitchManager.io
{
    public class FileUtils
    {
        public static string BuildPath(params string[] components)
        {
            if (components == null || components.Length == 0 || string.IsNullOrWhiteSpace(components[0]))
                return null;

            string path = components[0];

            for (int i = 1; i < components.Length; i++)
            {
                string c = components[i];
                while (c[0] == Path.DirectorySeparatorChar || c[0] == Path.AltDirectorySeparatorChar)
                    c = c.Substring(1);

                while (c[c.Length - 1] == Path.DirectorySeparatorChar || c[c.Length - 1] == Path.AltDirectorySeparatorChar)
                    c = c.Substring(0, c.Length - 1);

                path = path + Path.DirectorySeparatorChar + components[i];
            }

            return path;
        }
        public static string BuildFilePath(string dir, string name, string extension)
        {
            string path = dir + Path.DirectorySeparatorChar + name + "." + extension;
            
            return path;
        }

        public static FileStream OpenReadStream(string path)
        {
            return File.OpenRead(path);
        }

        public static FileStream OpenReadStream(FileInfo file)
        {
            return file.OpenRead();
        }

        public static FileStream OpenWriteStream(string path, bool append = false, int timeoutMs = 500)
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
                        string dir = Path.GetDirectoryName(path);
                        if (!DirectoryExists(path))
                            Directory.CreateDirectory(dir);
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
            if (FileUtils.FileExists(file))
            {
                File.Delete(file);
                //GC.Collect();
                //GC.WaitForPendingFinalizers();
            }
        }

        public static void DeleteFile(FileInfo file)
        {
            if (FileUtils.FileExists(file))
            {
                file.Delete();
                //GC.Collect();
                //GC.WaitForPendingFinalizers();
            }
        }

        public static bool FileExists(string file)
        {
            return !string.IsNullOrEmpty(file) && File.Exists(file);
        }

        public static bool FileExists(FileInfo file)
        {
            return file != null && file.Exists;
        }

        internal static bool DirectoryExists(string path)
        {
            return !string.IsNullOrEmpty(path) && Directory.Exists(path);
        }

        internal static bool DirectoryExists(DirectoryInfo dir)
        {
            return dir != null && dir.Exists;
        }

        public static async Task MoveDirectory(string from, string to, bool replace = false)
        {
            DirectoryInfo fromDir = new DirectoryInfo(from);
            DirectoryInfo toDir = new DirectoryInfo(to);
            if (!DirectoryExists(toDir)) toDir.Create();

            foreach (var fromFile in fromDir.EnumerateFiles())
            {
                string destFile = toDir.FullName + Path.DirectorySeparatorChar + fromFile.Name;
                var toFile = new FileInfo(destFile);
                await MoveFile(fromFile, toFile, replace).ConfigureAwait(false);                
            }
            DeleteDirectory(fromDir);
        }

        public static async Task MoveFile(FileInfo from, FileInfo to, bool replace)
        {
            string fromRoot = Path.GetPathRoot(from.FullName);
            string toRoot = Path.GetPathRoot(to.FullName);

            if (object.Equals(fromRoot, toRoot))
            {
                // If file exists, replace if asked to, skip otherwise
                if (FileUtils.FileExists(to))
                {
                    if (replace)
                        FileUtils.DeleteFile(to);
                    else
                        return;
                }
                from.MoveTo(to.FullName);
            }
            else
            {
                await CopyFileAsync(from, to, replace).ConfigureAwait(false);
                DeleteFile(from);
            }
        }

        public static async Task CopyFileAsync(FileInfo from, FileInfo to, bool replace)
        {
            if (FileUtils.FileExists(to))
                if (replace)
                    FileUtils.DeleteFile(to);
                else
                    return;

            using (var fromStream = new JobFileStream(from.FullName, $"Copying file {from.Name} to directory {to.DirectoryName}.", from.Length, 0))
            {

                using (var toStream = OpenWriteStream(to.FullName))
                    await fromStream.CopyToAsync(toStream).ConfigureAwait(false);
            }
        }

        public static async Task CopyFileAsync(FileInfo from, DirectoryInfo to, bool replace)
        {
            string toPath = Path.Combine(to.FullName, from.Name);
            await CopyFileAsync(from, new FileInfo(toPath), replace).ConfigureAwait(false);
        }

        internal static bool DirectoryHasFile(DirectoryInfo to, string name)
        {
            var files = to.GetFiles(name);
            if (files != null && files.Length > 0) return true;

            return false;
        }

        internal static DateTime? GetCreationDate(string path)
        {
            if (!FileExists(path)) return null;
            var f = new FileInfo(path);
            return f?.CreationTime;
        }

        /// <summary>
        /// Gets the parent directory of any file or directory path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        internal static string GetParentDirectory(string path)
        {
            FileInfo fi = new FileInfo(path);
            if (fi.Exists)
                return fi.DirectoryName;
            else
            {
                DirectoryInfo di = new DirectoryInfo(path);
                if (di.Exists)
                    return fi.DirectoryName;
            }

            return null;
        }
    }
}