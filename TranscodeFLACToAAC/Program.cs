using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MKVInfoParser;

namespace TranscodeFLACtoAAC
{
    class Program
    {
        private static readonly String pathToMkvInfo = ConfigurationManager.AppSettings["pathToMkvInfo"];
        private static readonly String mkvInfoBaseParameters = ConfigurationManager.AppSettings["mkvInfoParameters"];
        private static readonly Int32 infoTimeOut = Int32.Parse(ConfigurationManager.AppSettings["mkvInfoTimeout"]);

        private static readonly String pathToFFMpeg = ConfigurationManager.AppSettings["pathToFFMpeg"];
        private static readonly String ffMpegBaseParameters = ConfigurationManager.AppSettings["ffMpegParameters"];

        private static readonly String pathToNeroAacEnc = ConfigurationManager.AppSettings["pathToNeroAacEnc"];
        private static readonly String neroAacEncBaseParameters = ConfigurationManager.AppSettings["neroAacEncParameters"];
        private static readonly Int32 encodeTimeout = Int32.Parse(ConfigurationManager.AppSettings["transcodeTimeout"]);

        private static readonly String pathToMkvMerge = ConfigurationManager.AppSettings["pathToMkvMerge"];
        private static readonly Int32 mergeTimeout = Int32.Parse(ConfigurationManager.AppSettings["mkvMergeTimeout"]);


        private static readonly String flacCodecID = ConfigurationManager.AppSettings["FlacCodecId"];
        private static readonly DirectoryInfo tempFolder = new DirectoryInfo(ConfigurationManager.AppSettings["TempFolder"]);

        static Int32 Main(string[] args)
        {
            if(!tempFolder.Exists)
            {
                Console.WriteLine("The temp folder does not exist, check " 
                    + AppDomain.CurrentDomain.FriendlyName + ".config for correctness.");
                return 1;
            }

            if (args.Length != 2)
            {
                WriteUsage();
                return 2;
            }
            DirectoryInfo sourceFolder = new DirectoryInfo(args[0]);
            if (!sourceFolder.Exists)
            {
                Console.WriteLine("Source folder does not exist");
                WriteUsage();
                return 2;
            }
            DirectoryInfo targetFolder = new DirectoryInfo(args[1]);
            if (sourceFolder.FullName.ToUpperInvariant().TrimEnd('\\')
                .Equals(targetFolder.FullName.ToUpperInvariant().TrimEnd('\\')))
            {
                Console.WriteLine("Source and target folder cannot be equal");
                WriteUsage();
                return 2;
            }
            if (!targetFolder.Exists)
                targetFolder.Create();

            TranscodeFlacForAllFilesInFolder(sourceFolder, targetFolder);
           
#if DEBUG       //VS does not halt after execution in debug mode.
            Console.WriteLine("Finished");
            Console.ReadKey();
#endif
            return 0;
        }

        private static void WriteUsage()
        {
            Console.WriteLine("FLAC Converter usage:");
            Console.WriteLine(AppDomain.CurrentDomain.FriendlyName + " [input folder] [output folder]");
            Console.WriteLine("both parameters are required and output folder is required to be different from input folder.");
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

        private static void TranscodeFlacForAllFilesInFolder(DirectoryInfo sourceFolder, DirectoryInfo targetFolder)
        {
            List<VideoFileInfo> mkvFiles = GetAllVideoFileInfos(sourceFolder);
            var mkvFilesWithFlac = mkvFiles.Where(M => M.Tracks.Where(T => T.Codec == flacCodecID).Count() > 0);
            foreach (VideoFileInfo mkvFile in mkvFilesWithFlac)
            {
                Console.WriteLine("Transcoding FLAC streams for {0}", mkvFile.VideoFile.FullName);
                TranscodeFlac(mkvFile, targetFolder);
            }
        }

        private static List<VideoFileInfo> GetAllVideoFileInfos(DirectoryInfo workingDirectory)
        {
            List<VideoFileInfo> mkvInfos = new List<VideoFileInfo>();
            foreach (FileInfo mkvFile in workingDirectory.GetFiles("*.mkv"))
            {
                FileInfo mkvInfoFile =
                    new FileInfo(tempFolder.FullName + Path.DirectorySeparatorChar + mkvFile.Name + ".info");
                String mkvInfoParameters = string.Format(mkvInfoBaseParameters, mkvFile.FullName, mkvInfoFile.FullName);
                Console.WriteLine("mkvInfo Command:");
                Console.WriteLine(pathToMkvInfo + " " + mkvInfoParameters);

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
                        VideoFileInfo mkvInfo = MKVInfoParser.MKVInfoProcessor.ParseMKVInfo(mkvInfoFile.FullName);
                        mkvInfo.VideoFile = mkvFile;
                        mkvInfos.Add(mkvInfo);
                        mkvInfoFile.Delete();
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

        private static void TranscodeFlac(VideoFileInfo mkvFile, DirectoryInfo targetFolder)
        {
            ConvertFlacToAac(mkvFile);
            RemuxFile(mkvFile, targetFolder);
        }

        private static void ConvertFlacToAac(VideoFileInfo mkvFile)
        {
            foreach (TrackInfo track in mkvFile.Tracks)
            {
                if (track.Codec == flacCodecID)
                {
                    Console.WriteLine("Transcoding FLAC streams {0}(id:{1}) from {2}",
                        track.Name, track.TrackNumber, mkvFile.VideoFile.FullName);
                    FileInfo aacFile = new FileInfo(tempFolder.FullName + Path.DirectorySeparatorChar + 
                        mkvFile.VideoFile.Name + '.' + track.MkvToolsTrackNumber + ".mp4");
                    String ffmpegParameters =
                        String.Format(ffMpegBaseParameters, mkvFile.VideoFile.FullName, track.MkvToolsTrackNumber);
                    String neroAacParameters = String.Format(neroAacEncBaseParameters, aacFile.FullName);


                    String fullCommand = pathToFFMpeg + " " + ffmpegParameters + 
                        " | " + pathToNeroAacEnc + " " + neroAacParameters;
                        //We pipe the stream from ffmpeg directly into the neroAAC encoder.
                    Console.WriteLine("ffmpeg to neroAAC Command:");
                    Console.WriteLine(fullCommand);

                    Process transcodeProcess = StartExternalProcess("cmd", "/C \"" + fullCommand.Replace("\"", "\"\"") + "\"");
                    Console.WriteLine(transcodeProcess.StandardError.ReadToEnd());
                    transcodeProcess.WaitForExit(encodeTimeout * 1000);

                    if (transcodeProcess.HasExited)
                    {
                        if (transcodeProcess.ExitCode != 0 || !aacFile.Exists)
                        {
                            Console.WriteLine("Error with transcoding");
                        }
                        else
                        {
                            track.ExternalFileRef = aacFile;
                        }
                    }
                    else
                    {
                        Console.WriteLine("MKV transcode application took longer than {0} seconds.", encodeTimeout);
                        transcodeProcess.Kill();
                    }
                }
            }
        }        

        private static void RemuxFile(VideoFileInfo mkvFile, DirectoryInfo targetFolder)
        {
            FileInfo target = new FileInfo(targetFolder.FullName + Path.DirectorySeparatorChar + mkvFile.VideoFile.Name);
            Console.WriteLine("Remuxing {0} to {1}", mkvFile.VideoFile.FullName, target.FullName);
            String mkvMergeParams = PrepareMkvMergeParameters(mkvFile, target);
            Console.WriteLine("MkvMerge Command:");
            Console.WriteLine(pathToMkvMerge + " " + mkvMergeParams);
 
            Process mkvMergeProcess = StartExternalProcess(pathToMkvMerge, mkvMergeParams);
            Console.WriteLine(mkvMergeProcess.StandardError.ReadToEnd());
            mkvMergeProcess.WaitForExit(mergeTimeout * 1000);

            if (mkvMergeProcess.HasExited)
            {
                target.Refresh();
                if (mkvMergeProcess.ExitCode != 0 || !target.Exists)
                {
                    Console.WriteLine("Error with mkvMerge");
                }
                else
                {
                    foreach (TrackInfo track in mkvFile.Tracks)
                    {
                        if (track.ExternalFileRef != null && track.ExternalFileRef.Exists)
                            track.ExternalFileRef.Delete();
                    }
                }
            }
            else
            {
                Console.WriteLine("MKV merge application took longer than {0} seconds.", mergeTimeout);
                mkvMergeProcess.Kill();
            }
        }

        private static String PrepareMkvMergeParameters(VideoFileInfo mkvFile, FileInfo target)
        {
            StringBuilder mkvMergeParams = new StringBuilder();
            mkvMergeParams.AppendFormat("-o \"{0}\"", target.FullName);

            if (!String.IsNullOrWhiteSpace(mkvFile.SegmentUID))
                mkvMergeParams.AppendFormat(" --segment-uid \"{0}\"", mkvFile.SegmentUID);  //we keep the same segmentUID as source to preserve external chapter support. (OP/ED's etc)

            List<String> audioIdsToSkip = new List<String>();
            foreach (TrackInfo track in mkvFile.Tracks)
            {
                if (track.TrackType == TrackType.Video)
                {
                    mkvMergeParams.AppendFormat(" --compression {0}:none", track.MkvToolsTrackNumber);
                }
                if (track.TrackType == TrackType.Audio && track.Codec == flacCodecID)
                {
                    audioIdsToSkip.Add(track.MkvToolsTrackNumber.ToString());
                }
            }
            mkvMergeParams.AppendFormat(" --audio-tracks !{0}", String.Join(",", audioIdsToSkip));
            mkvMergeParams.AppendFormat(" \"{0}\"", mkvFile.VideoFile.FullName);

            foreach (TrackInfo track in mkvFile.Tracks)
            {
                if (track.TrackType == TrackType.Audio && track.Codec == flacCodecID)
                {
                    String reworkedTrackName = ReplaceFlacIndicator(track.Name);
                    if(!String.IsNullOrWhiteSpace(reworkedTrackName))
                        mkvMergeParams.AppendFormat(" --track-name \"0:{0}\"", reworkedTrackName);
                    if (String.IsNullOrWhiteSpace(track.Language))  //eng is the default track language, this is sometimes ommited in the source files resulting in an empty language code.
                        mkvMergeParams.AppendFormat(" --language 0:eng");
                    else
                        mkvMergeParams.AppendFormat(" --language 0:{0}", track.Language); ;
                    mkvMergeParams.AppendFormat(" --compression 0:none");
                    if (!track.Default)      //yes is the default value of this flag, there is no need to specify this case.
                        mkvMergeParams.Append(" --default-track 0:no");
                    if(track.Forced)
                        mkvMergeParams.Append(" --forced-track 0:yes");
                    //Ensure we only grab audio from this file
                    mkvMergeParams.Append(" --no-video --no-subtitles --no-buttons --no-track-tags");
                    mkvMergeParams.Append(" --no-chapters --no-attachments --no-global-tags");
                    mkvMergeParams.AppendFormat(" --audio-tracks 0 \"{0}\"", track.ExternalFileRef.FullName);
                }
            }

            List<String> trackOrder = new List<String>();
            Int32 externalFileCounter = 0;
            foreach (TrackInfo track in mkvFile.Tracks)
            {
                if (track.TrackType == TrackType.Audio && track.Codec == flacCodecID)
                {
                    externalFileCounter += 1;
                    trackOrder.Add(String.Format("{0}:0", externalFileCounter));
                }
                else
                {
                    trackOrder.Add(String.Format("0:{0}", track.MkvToolsTrackNumber));
                }
            }
            mkvMergeParams.AppendFormat(" --track-order {0}", String.Join(",", trackOrder));

            return mkvMergeParams.ToString();
        }

        private static string ReplaceFlacIndicator(String trackName)
        {
            if (String.IsNullOrWhiteSpace(trackName))
                return trackName;

            Int32 indexOfFlac = trackName.IndexOf("FLAC", StringComparison.OrdinalIgnoreCase);
            if (indexOfFlac > -1)
            {
                String newName = trackName.Substring(0, indexOfFlac);
                newName += "AAC";
                if (indexOfFlac + 4 < trackName.Length)
                    newName += trackName.Substring(indexOfFlac + 4);

                return newName;
            }
            return trackName;
        }
    }
}
