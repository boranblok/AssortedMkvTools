using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using MKVInfoParser;
using System.Configuration;

namespace FlacFinder
{
    class Program
    {
        private static readonly String pathToMkvInfo = ConfigurationManager.AppSettings["pathToMkvInfo"];
        private static readonly String mkvInfoBaseParameters = ConfigurationManager.AppSettings["mkvInfoParameters"];
        private static readonly Int32 infoTimeOut = Int32.Parse(ConfigurationManager.AppSettings["mkvInfoTimeout"]);

        private static readonly String flacCodecID = ConfigurationManager.AppSettings["FlacCodecId"];
        private static readonly DirectoryInfo tempFolder = new DirectoryInfo(ConfigurationManager.AppSettings["TempFolder"]);
        static Int32 Main(string[] args)
        {
            if (args.Length != 1)
            {
                return ShowUsage();
            }
            DirectoryInfo startDirectory = new DirectoryInfo(args[0]);
            if (!startDirectory.Exists)
            {
                Console.WriteLine("The start folder does not exist.");
            }

            FindFlacFiles(startDirectory);

#if DEBUG       //VS does not halt after execution in debug mode.
            Console.WriteLine("Finished");
            Console.ReadKey();
#endif

            return 0;
        }

        private static void FindFlacFiles(DirectoryInfo directory)
        {
            List<VideoFileInfo> mkvFiles = GetAllVideoFileInfos(directory);
            var mkvFilesWithFlac = mkvFiles.Where(M => M.Tracks.Where(T => T.Codec == flacCodecID).Count() > 0);
            List<String> directories = new List<String>();
            foreach (VideoFileInfo mkvFileWithFlac in mkvFilesWithFlac)
            {
                if (!directories.Contains(mkvFileWithFlac.VideoFile.DirectoryName))
                    directories.Add(mkvFileWithFlac.VideoFile.DirectoryName);
            }
            directories.Sort();
            Console.WriteLine(String.Join("\r\n", directories));
            WriteDirectoriesToFile(directories);
        }

        private static void WriteDirectoriesToFile(List<String> directories)
        {
            FileInfo outputFile = new FileInfo(tempFolder.FullName + Path.DirectorySeparatorChar + "foldersWithFlac.txt");
            using (TextWriter writer = new StreamWriter(outputFile.OpenWrite()))
            {
                writer.WriteLine(String.Join("\r\n", directories));
            }
        }

        private static List<VideoFileInfo> GetAllVideoFileInfos(DirectoryInfo workingDirectory)
        {
            List<VideoFileInfo> mkvInfos = new List<VideoFileInfo>();
            foreach (FileInfo mkvFile in workingDirectory.GetFiles("*.mkv", SearchOption.AllDirectories))
            {
                FileInfo mkvInfoFile =
                    new FileInfo(tempFolder.FullName + Path.DirectorySeparatorChar + mkvFile.Name + ".info");
                String mkvInfoParameters = string.Format(mkvInfoBaseParameters, mkvFile.FullName, mkvInfoFile.FullName);
                //Console.WriteLine("mkvInfo Command:");
                //Console.WriteLine(pathToMkvInfo + " " + mkvInfoParameters);

                Process mkvInfoProcess = StartExternalProcess(pathToMkvInfo, mkvInfoParameters);
                Console.WriteLine(mkvInfoProcess.StandardError.ReadToEnd());
                mkvInfoProcess.WaitForExit(infoTimeOut * 1000);

                if (mkvInfoProcess.HasExited)
                {
                    if (mkvInfoProcess.ExitCode != 0 || !mkvInfoFile.Exists)
                    {
                        Console.WriteLine("Error with mkvinfo tool when retrieving info for {0}", mkvFile.FullName);
                    }
                    else
                    {
                        try
                        {
                            VideoFileInfo mkvInfo = MKVInfoParser.MKVInfoProcessor.ParseMKVInfo(mkvInfoFile.FullName);

                            mkvInfo.VideoFile = mkvFile;
                            mkvInfos.Add(mkvInfo);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Error parsing info for file {0}", mkvFile.FullName);
                        }
                        finally
                        {
                            mkvInfoFile.Delete();
                        }
                    }
                }
                else
                {
                    Console.WriteLine("MKV Info application took longer than {0} seconds when retrieving info for {1}",
                        infoTimeOut, mkvFile.FullName);
                    mkvInfoProcess.Kill();
                }
            }
            return mkvInfos;
        }

        private static Process StartExternalProcess(String application, String parameters)
        {
            ProcessStartInfo processDef = new ProcessStartInfo();
            processDef.FileName = application;
            processDef.Arguments = parameters;
            processDef.CreateNoWindow = true;
            processDef.RedirectStandardError = true;
            processDef.UseShellExecute = false;
            return Process.Start(processDef);
        }

        private static Int32 ShowUsage()
        {
            Console.WriteLine("FLAC Finder usage:");
            Console.WriteLine(AppDomain.CurrentDomain.FriendlyName + " [start folder]");
            return 1;
        }
    }
}
