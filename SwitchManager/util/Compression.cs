using System;
using System.IO;
using log4net;
using SwitchManager.io;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SwitchManager.util
{
    /// <summary>
    /// Handles compression and decompression of switch files. For now, does it with external tools, but I may write my own C# code at some point.
    /// </summary>
    public class Compression
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(Compression));

        // Hard coded exe
        private static readonly string exePath = @".\nsz\nsz.exe";

        // Hard coded keys file, however the tools don't seem to allow passing in keys file, dammit
        private static readonly string keysPath = @".\hactool\keys.txt";

        /// <summary>
        /// Unpacks an NCZ file, specified by nczPath, into a new NCA file.
        /// Doesn't currently work, because the NSZ tool doesn't support directly decompressing NCZs.
        /// </summary>
        /// <param name="nczPath">Path to the input NCZ file.</param>
        /// <param name="outDir">Directory in which to output the NCA file.</param>
        /// <returns>Decompressed file path if the operation succeeded, otherwise null.</returns>
        public static async Task<string> UnpackNCZ(string nczPath, string outDir)
        {
            string fName = Path.GetFileNameWithoutExtension(nczPath);
            string outfile = FileUtils.BuildPath(outDir, fName, ".nca");

            string exe = (exePath);

            // NOTE: Using single quotes here instead of single quotes fucks up windows, it CANNOT handle single quotes
            // Anything surrounded in single quotes will throw an error because the file/folder isn't found
            // Must use escaped double quotes!
            string commandLine = $" -D --overwrite --verify" +
                                 $" --output \"{outDir}\"" +
                                 $" \"{nczPath}\"";


            try
            {
                return await Task.Run(delegate
                {
                    ProcessStartInfo psi = new ProcessStartInfo()
                    {
                        FileName = exe,
                        WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
                        Arguments = commandLine,
                        UseShellExecute = false,
                        //RedirectStandardOutput = true,
                        //RedirectStandardError = true,
                        CreateNoWindow = true,
                    };
                    Process process = Process.Start(psi);

                    //string errors = hactool.StandardError.ReadToEnd();
                    //string output = hactool.StandardOutput.ReadToEnd();

                    process.WaitForExit();

                    if (!File.Exists(outfile))
                        throw new Exception($"Decompressing NCZ failed, {outfile} is missing!");
                    return outfile;
                });
            }
            catch (Exception e)
            {
                throw new Exception("Decompressing NCZ failed!", e);
            }
        }

        /// <summary>
        /// Unpacks an NSZ file, specified by nszPath, into a new NSP file.
        /// </summary>
        /// <param name="nszPath">Path to the input NSZ file.</param>
        /// <param name="outDir">Directory in which to output the NCA file.</param>
        /// <returns>Decompressed file path if the operation succeeded, otherwise null.</returns>
        public static async Task<string> UnpackNSZ(string nszPath, string outDir)
        {
            string fName = Path.GetFileNameWithoutExtension(nszPath);
            string outfile = FileUtils.BuildFilePath(outDir, fName, "nsp");

            string exe = (exePath);

            // NOTE: Using single quotes here instead of single quotes fucks up windows, it CANNOT handle single quotes
            // Anything surrounded in single quotes will throw an error because the file/folder isn't found
            // Must use escaped double quotes!
            string commandLine = $" -D --overwrite --verify" +
                                 $" --output \"{outDir}\"" +
                                 $" \"{nszPath}\"";


            try
            {
                return await Task.Run(delegate
                {
                    ProcessStartInfo psi = new ProcessStartInfo()
                    {
                        FileName = exe,
                        WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
                        Arguments = commandLine,
                        UseShellExecute = false,
                        //RedirectStandardOutput = true,
                        //RedirectStandardError = true,
                        CreateNoWindow = true,
                    };
                    Process process = Process.Start(psi);

                    //string errors = hactool.StandardError.ReadToEnd();
                    //string output = hactool.StandardOutput.ReadToEnd();

                    process.WaitForExit();

                    if (!FileUtils.FileExists(outfile))
                        throw new Exception($"Decompressing NSZ failed, {outfile} is missing!");
                    return outfile;
                });
            }
            catch (Exception e)
            {
                throw new Exception("Decompressing NSZ failed!", e);
            }
        }
    }
}
