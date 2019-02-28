using log4net;
using SwitchManager.nx.system;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.util
{
    public static class Hactool
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(Hactool));

        private static string hactoolPath;
        private static string keysPath;

        public static void Initialize(string hactoolPath, string keysPath)
        {
            Hactool.hactoolPath = hactoolPath;
            Hactool.keysPath = keysPath;
        }

        public static async Task<bool> VerifyNCA(string ncaPath, SwitchTitle title)
        {
            string hactoolExe = (hactoolPath);
            string keysFile = (keysPath);
            string tkey = title.TitleKey;

            // NOTE: Using single quotes here instead of single quotes fucks up windows, it CANNOT handle single quotes
            // Anything surrounded in single quotes will throw an error because the file/folder isn't found
            // Must use escaped double quotes!
            string commandLine = $" -k \"{keysFile}\"" +
                                 $" --titlekey=\"{tkey}\"" +
                                 $" \"{ncaPath}\"";
            try
            {
                return await Task.Run(delegate
                {
                    ProcessStartInfo hactoolSI = new ProcessStartInfo()
                    {
                        FileName = hactoolExe,
                        WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
                        Arguments = commandLine,
                        UseShellExecute = false,
                        //RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };
                    Process hactool = Process.Start(hactoolSI);

                    string errors = hactool.StandardError.ReadToEnd();
                    hactool.WaitForExit();

                    if (errors.Contains("Error: section 0 is corrupted!") ||
                        errors.Contains("Error: section 1 is corrupted!"))
                    {
                        logger.Error("NCA title key verification failed");
                        return false;
                    }
                    logger.Info("NCA title key verification successful");
                    return true;
                });
            }
            catch (Exception e)
            {
                throw new HactoolFailedException("Hactool decryption failed!", e);
            }
        }

        /// <summary>
        /// Decrypts the NCA specified by ncaPath and spits it out into the provided directory, or into a directory named after the base file name if no output directory is provided.
        /// </summary>
        /// <param name="fpath"></param>
        /// <returns></returns>
        public static async Task<DirectoryInfo> DecryptNCA(string ncaPath, string titlekey = null, string outDir = null)
        {
            string fName = Path.GetFileNameWithoutExtension(ncaPath); // fName = os.path.basename(fPath).split()[0]
            if (outDir == null)
                outDir = Path.GetDirectoryName(ncaPath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(ncaPath);
            DirectoryInfo outDirInfo = new DirectoryInfo(outDir);
            outDirInfo.Create();

            string hactoolExe = (hactoolPath);
            string keysFile = (keysPath);
            string exefsPath = (outDir + Path.DirectorySeparatorChar + "exefs");
            string romfsPath = (outDir + Path.DirectorySeparatorChar + "romfs");
            string section0Path = (outDir + Path.DirectorySeparatorChar + "section0");
            string section1Path = (outDir + Path.DirectorySeparatorChar + "section1");
            string section2Path = (outDir + Path.DirectorySeparatorChar + "section2");
            string section3Path = (outDir + Path.DirectorySeparatorChar + "section3");
            string headerPath = (outDir + Path.DirectorySeparatorChar + "Header.bin");

            // NOTE: Using single quotes here instead of single quotes fucks up windows, it CANNOT handle single quotes
            // Anything surrounded in single quotes will throw an error because the file/folder isn't found
            // Must use escaped double quotes!
            string commandLine = $" -k \"{keysFile}\"" +
                                 (titlekey == null ? "" : $" --titlekey=\"{titlekey}\"") +
                                 $" --exefsdir=\"{exefsPath}\"" +
                                 $" --romfsdir=\"{romfsPath}\"" +
                                 $" --section0dir=\"{section0Path}\"" +
                                 $" --section1dir=\"{section1Path}\"" +
                                 $" --section2dir=\"{section2Path}\"" +
                                 $" --section3dir=\"{section3Path}\"" +
                                 $" --header=\"{headerPath}\"" +
                                 $" \"{ncaPath}\"";


            try
            {
                return await Task.Run(delegate
                {
                    ProcessStartInfo hactoolSI = new ProcessStartInfo()
                    {
                        FileName = hactoolExe,
                        WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
                        Arguments = commandLine,
                        UseShellExecute = false,
                        //RedirectStandardOutput = true,
                        //RedirectStandardError = true,
                        CreateNoWindow = true,
                    };
                    Process hactool = Process.Start(hactoolSI);

                    //string errors = hactool.StandardError.ReadToEnd();
                    //string output = hactool.StandardOutput.ReadToEnd();

                    hactool.WaitForExit();

                    if (outDirInfo.GetDirectories().Length == 0)
                        throw new HactoolFailedException($"Running hactool failed, output directory {outDir} is empty!");
                    return outDirInfo;
                });
            }
            catch (Exception e)
            {
                throw new HactoolFailedException("Hactool decryption failed!", e);
            }
        }
    }
}
