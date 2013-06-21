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

        private static readonly String pathToMkvExtract = ConfigurationManager.AppSettings["pathToMkvExtract"];
        private static readonly String mkvExtractBaseParameters = ConfigurationManager.AppSettings["mkvExtractParameters"];
        private static readonly Int32 extractTimeOut = Int32.Parse(ConfigurationManager.AppSettings["mkvExtractTimeout"]);

        private static readonly String pathToFlacTool = ConfigurationManager.AppSettings["pathToFlac"];
        private static readonly String flacBaseParameters = ConfigurationManager.AppSettings["flacParameters"];
        private static readonly Int32 decodeTimeout = Int32.Parse(ConfigurationManager.AppSettings["flacTimeout"]);

        private static readonly String pathToNeroAacEnc = ConfigurationManager.AppSettings["pathToNeroAacEnc"];
        private static readonly String neroAacEncBaseParameters = ConfigurationManager.AppSettings["neroAacEncParameters"];
        private static readonly Int32 encodeTimeout = Int32.Parse(ConfigurationManager.AppSettings["neroAacEncTimeout"]);

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
                    FileInfo flacFile = new FileInfo(tempFolder.FullName + Path.DirectorySeparatorChar + 
                        mkvFile.VideoFile.Name + '.' + track.MkvToolsTrackNumber + ".flac");
                    String mkvExtractParameters = String.Format(mkvExtractBaseParameters, 
                        mkvFile.VideoFile.FullName, flacFile.FullName, track.MkvToolsTrackNumber);

                    Process mkvExtractProcess = StartExternalProcess(pathToMkvExtract, mkvExtractParameters);
                    Console.WriteLine(mkvExtractProcess.StandardError.ReadToEnd());
                    mkvExtractProcess.WaitForExit(extractTimeOut * 1000);

                    if (mkvExtractProcess.HasExited)
                    {
                        if (mkvExtractProcess.ExitCode != 0 || !flacFile.Exists)
                        {
                            Console.WriteLine("Error with mkvextract");
                        }
                        else
                        {
                            track.ExternalFileRef = EncodePcmToAAC(DecodeFlacToPCM(flacFile));
                        }
                    }
                    else
                    {
                        Console.WriteLine("MKV extract application took longer than {0} seconds.", extractTimeOut);
                        mkvExtractProcess.Kill();
                    }
                }
            }
        }

        private static FileInfo DecodeFlacToPCM(FileInfo flacFile)
        {
            FileInfo targetFile =
                new FileInfo(flacFile.FullName.Substring(0, flacFile.FullName.Length - flacFile.Extension.Length) + ".wav");
            //No need to use tempFolder here flacFile is already in the tempFolder.

            String flacParameters = String.Format(flacBaseParameters, flacFile.FullName, targetFile.FullName);

            Process flacDecodeProcess = StartExternalProcess(pathToFlacTool, flacParameters);
            Console.WriteLine(flacDecodeProcess.StandardError.ReadToEnd());
            flacDecodeProcess.WaitForExit(decodeTimeout * 1000);

            if (flacDecodeProcess.HasExited)
            {
                if (flacDecodeProcess.ExitCode != 0 || !targetFile.Exists)
                {
                    Console.WriteLine("Error with flac tool");
                }
                else
                {
                    return targetFile;
                }
            }
            else
            {
                Console.WriteLine("flac decode application took longer than {0} seconds.", decodeTimeout);
                flacDecodeProcess.Kill();
            }

            throw new Exception("Could not decode FLAC");
        }

        private static FileInfo EncodePcmToAAC(FileInfo pcmFile)
        {
            FileInfo targetFile =
                new FileInfo(pcmFile.FullName.Substring(0, pcmFile.FullName.Length - pcmFile.Extension.Length) + ".mp4");
            //No need to use tempFolder here pcmFile is already in the tempFolder.
            String neroAacEncParameters = String.Format(neroAacEncBaseParameters, pcmFile.FullName, targetFile.FullName);

            Process aacEncode = StartExternalProcess(pathToNeroAacEnc, neroAacEncParameters);
            Console.WriteLine(aacEncode.StandardError.ReadToEnd());
            aacEncode.WaitForExit(decodeTimeout * 1000);

            if (aacEncode.HasExited)
            {
                if (aacEncode.ExitCode != 0 || !targetFile.Exists)
                {
                    Console.WriteLine("Error with neroAacEnc tool");
                }
                else
                {
                    pcmFile.Delete();
                    return targetFile;
                }
            }
            else
            {
                Console.WriteLine("AAC encode application took longer than {0} seconds.", encodeTimeout);
                aacEncode.Kill();
            }

            throw new Exception("Could not encode AAC");
        }

        private static void RemuxFile(VideoFileInfo mkvFile, DirectoryInfo targetFolder)
        {
            FileInfo target = new FileInfo(targetFolder.FullName + Path.DirectorySeparatorChar + mkvFile.VideoFile.Name);
            if (target.Exists)
            {
                Console.WriteLine("WARNING: The target file {0} already exists, skipping.", target.FullName);
                return;
            }
            String mkvMergeParams = PrepareMkvMergeParameters(mkvFile, target);
 
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
                    mkvMergeParams.AppendFormat(" --track-name \"0:{0}\"", reworkedTrackName);
                    mkvMergeParams.AppendFormat(" --language 0:{0}", track.Language);
                    mkvMergeParams.AppendFormat(" --compression 0:none");
                    if (!track.Default)      //yes is the default value of this flag, there is no need to specify this case.
                        mkvMergeParams.Append(" --default-track 0:no");
                    if(track.Forced)
                        mkvMergeParams.Append(" --forced-track 0:yes");
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
