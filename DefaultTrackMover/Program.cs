using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MKVInfoParser;

namespace DefaultTrackMover
{
    class Program
    {
        private static readonly String pathToMkvInfo = ConfigurationManager.AppSettings["pathToMkvInfo"];
        private static readonly String mkvInfoBaseParameters = ConfigurationManager.AppSettings["mkvInfoParameters"];
        private static readonly Int32 infoTimeOut = Int32.Parse(ConfigurationManager.AppSettings["mkvInfoTimeout"]);

        private static readonly String pathToMkvMerge = ConfigurationManager.AppSettings["pathToMkvMerge"];
        private static readonly String mkvMergeParameters = ConfigurationManager.AppSettings["mkvMergeParameters"];
        private static readonly Int32 mergeTimeout = Int32.Parse(ConfigurationManager.AppSettings["mkvMergeTimeout"]);

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
                return ShowUsage();
            }

            MoveDefaultTracks(startDirectory);

#if DEBUG       //VS does not halt after execution in debug mode.
            Console.WriteLine("Finished");
            Console.ReadKey();
#endif

            return 0;
        }

        private static void MoveDefaultTracks(DirectoryInfo startDirectory)
        {
            List<VideoFileInfo> mkvFiles = GetAllVideoFileInfos(startDirectory);            
            List<VideoFileInfo> defaultNotFirst = new List<VideoFileInfo>();
            List<VideoFileInfo> japaneseAudioNotdefault = new List<VideoFileInfo>();
            List<VideoFileInfo> japaneseSubs = new List<VideoFileInfo>();
            foreach(var mkvFile in mkvFiles)
            {
                var audioTracks = mkvFile.Tracks
                    .Where(t => t.TrackType == TrackType.Audio)
                    .OrderBy(t => t.MkvToolsTrackNumber);
                var subTracks = mkvFile.Tracks
                    .Where(t => t.TrackType == TrackType.Subtitle)
                    .OrderBy(t => t.MkvToolsTrackNumber);

                if (audioTracks.Any(t => t.Language == "jpn" && t.Default == false)
                    && audioTracks.Any(t => t.Language != "jpn" && t.Default == true))
                {
                    japaneseAudioNotdefault.Add(mkvFile);
                }

                if (subTracks.Any(t => t.Language == "jpn"))
                {
                    japaneseSubs.Add(mkvFile);
                }

                if (audioTracks.Any(t => t.Default))
                {
                    if (audioTracks.First().Default == false)
                    {
                        defaultNotFirst.Add(mkvFile);
                        continue;
                    }
                }

                if (subTracks.Any(t => t.Default))
                {
                    if (subTracks.First().Default == false)
                    {
                        defaultNotFirst.Add(mkvFile);
                        continue;
                    }
                }
            }

            using (TextWriter writer = new StreamWriter(
                new FileInfo(tempFolder.FullName + Path.DirectorySeparatorChar + "defaultTracksNotFirst.txt").OpenWrite()))
            {
                defaultNotFirst.ToList().ForEach(f => writer.WriteLine(f.VideoFile.FullName));
            }

            using (TextWriter writer = new StreamWriter(
                new FileInfo(tempFolder.FullName + Path.DirectorySeparatorChar + "japaneseAudioNotdefault.txt").OpenWrite()))
            {
                japaneseAudioNotdefault.ToList().ForEach(f => writer.WriteLine(f.VideoFile.FullName));
            }

            using (TextWriter writer = new StreamWriter(
                new FileInfo(tempFolder.FullName + Path.DirectorySeparatorChar + "japaneseSubs.txt").OpenWrite()))
            {
                japaneseSubs.ToList().ForEach(f => writer.WriteLine(f.VideoFile.FullName));
            }

            foreach (var videoFile in defaultNotFirst)
            {
                String trackOrderParam = ConstructTrackOrder(videoFile);
                FileInfo rearrangedFile =
                    new FileInfo(tempFolder.FullName + Path.DirectorySeparatorChar + videoFile.VideoFile.Name);
                String parameters = string.Format(
                    mkvMergeParameters, videoFile.VideoFile.FullName,
                    rearrangedFile.FullName,
                    trackOrderParam);

                Console.WriteLine("mkvMerge Command:");
                Console.WriteLine(pathToMkvMerge + " " + parameters);

                Process mkvMergeProcess = StartExternalProcess(pathToMkvMerge, parameters);
                Console.WriteLine(mkvMergeProcess.StandardError.ReadToEnd());
                mkvMergeProcess.WaitForExit(mergeTimeout * 1000);

                if (mkvMergeProcess.HasExited)
                {
                    if (mkvMergeProcess.ExitCode != 0 || !rearrangedFile.Exists)
                    {
                        Console.WriteLine("Error with mkvmerge tool when rearranging tracks for {0}",
                            videoFile.VideoFile.FullName);
                    }
                    else
                    {
                        //TODO: copy
                        Console.WriteLine("Merged {0}",
                           rearrangedFile.FullName);
                    }
                }
                else
                {
                    Console.WriteLine("MKV Info application took longer than {0} seconds when retrieving info for {1}",
                        mergeTimeout, videoFile.VideoFile.FullName);
                    mkvMergeProcess.Kill();
                }
            }            

            //Construct: mkvmerge.exe -o C:\\temp\\Initial.D.s01e01.Ultimate.Tofu.Guy.Drift.DVD-a-s.rem.mkv C:\\temp\\Initial.D.s01e01.Ultimate.Tofu.Guy.Drift.DVD-a-s.mkv --track-order 0:0,0:2,0:1,0:4,0:3
        }

        private static String ConstructTrackOrder(VideoFileInfo videoFile)
        {
            List<Int32>trackNumbers = new List<Int32>(videoFile.Tracks.Count);
            videoFile.Tracks.Where(t => t.TrackType == TrackType.Video)
                .OrderBy(t => t.MkvToolsTrackNumber)
                .ToList().ForEach(t => trackNumbers.Add(t.MkvToolsTrackNumber));

            videoFile.Tracks.Where(t => t.TrackType == TrackType.Audio && t.Default == true)
                .OrderBy(t => t.MkvToolsTrackNumber)
                .ToList().ForEach(t => trackNumbers.Add(t.MkvToolsTrackNumber));
            videoFile.Tracks.Where(t => t.TrackType == TrackType.Audio && t.Default == false)
                .OrderBy(t => t.MkvToolsTrackNumber)
                .ToList().ForEach(t => trackNumbers.Add(t.MkvToolsTrackNumber));

            videoFile.Tracks.Where(t => t.TrackType == TrackType.Subtitle && t.Default == true)
                .OrderBy(t => t.MkvToolsTrackNumber)
                .ToList().ForEach(t => trackNumbers.Add(t.MkvToolsTrackNumber));
            videoFile.Tracks.Where(t => t.TrackType == TrackType.Subtitle && t.Default == false)
                .OrderBy(t => t.MkvToolsTrackNumber)
                .ToList().ForEach(t => trackNumbers.Add(t.MkvToolsTrackNumber));

            videoFile.Tracks.Where(t => t.TrackType == TrackType.Other)
                .OrderBy(t => t.MkvToolsTrackNumber)
                .ToList().ForEach(t => trackNumbers.Add(t.MkvToolsTrackNumber));

            StringBuilder mkvMergeTrackOrder = new StringBuilder();
            trackNumbers.ForEach(n => mkvMergeTrackOrder.AppendFormat("0:{0},", n));
            mkvMergeTrackOrder.Remove(mkvMergeTrackOrder.Length - 1, 1);

            return mkvMergeTrackOrder.ToString();
        }

        private static List<VideoFileInfo> GetAllVideoFileInfos(DirectoryInfo workingDirectory)
        {
            List<VideoFileInfo> mkvInfos = new List<VideoFileInfo>();
            foreach (FileInfo mkvFile in workingDirectory.GetFiles("*.mkv", SearchOption.AllDirectories))
            {
                FileInfo mkvInfoFile;
                String mkvInfoParameters;
                try
                {
                    mkvInfoFile = new FileInfo(tempFolder.FullName + Path.DirectorySeparatorChar + mkvFile.Name + ".info");
                    mkvInfoParameters = string.Format(mkvInfoBaseParameters, mkvFile.FullName, mkvInfoFile.FullName);                    
                }
                catch(PathTooLongException)
                {
                    Console.WriteLine("Path too long for {0}", mkvFile.Name);
                    continue;
                }
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
            Console.WriteLine("Default Track mover usage:");
            Console.WriteLine(AppDomain.CurrentDomain.FriendlyName + " [start folder]");
            return 1;
        }
    }
}
