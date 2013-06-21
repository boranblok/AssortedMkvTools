using System;
using System.Collections.Generic;
using System.IO;

namespace MKVInfoParser
{
    //Generation statement: mkvinfo <MKVFILE> --ui-language en --output-charset UTF-8 -r <INFOFILE>
    public class MKVInfoProcessor
    {
        private readonly static String segmentUid = "| + Segment UID: ";
        private readonly static String trackStart = "| + A track";

        public static VideoFileInfo ParseMKVInfo(String mkvInfoFile)
        {
            FileInfo infoFile = new FileInfo(mkvInfoFile);
            VideoFileInfo videoInfo = new VideoFileInfo();
            using (TextReader reader = infoFile.OpenText())
            {
                String line = reader.ReadLine();
                while (line != trackStart && !String.IsNullOrEmpty(line))
                {
                    if (line.StartsWith(segmentUid))
                        videoInfo.SegmentUID = line.Substring(segmentUid.Length);
                    line = reader.ReadLine();
                }
                if (line != trackStart)
                    throw new Exception("Invalid info file format or MKV without tracks");

                videoInfo.Tracks = ExtractTrackInfo(reader);                
            }
            return videoInfo;
        }

        private static Int32 LineLevel(String line)
        {
            return line.IndexOf('+');
        }

        private static List<TrackInfo> ExtractTrackInfo(TextReader reader)
        {
            List<TrackInfo> trackInfoList = new List<TrackInfo>();
            TrackInfo trackInfo = new TrackInfo();
            String line = reader.ReadLine();
            while (LineLevel(line) > 2)
            {
                ParseTrackInfoLine(trackInfo, line);
                line = reader.ReadLine();
            }
            trackInfoList.Add(trackInfo);
            if (line == trackStart)
            {
                trackInfoList.AddRange(ExtractTrackInfo(reader));
            }
            return trackInfoList;
        }

        private static void ParseTrackInfoLine(TrackInfo trackInfo, String line)
        {
            Int32 indexOfPlus = line.IndexOf('+');
            Int32 indexOfColon = line.IndexOf(':');
            if (indexOfPlus > -1 && indexOfColon > -1 && line.Length > indexOfColon + 2)
            {
                String key = line.Substring(indexOfPlus + 2, indexOfColon - (indexOfPlus + 2));
                String content = line.Substring(indexOfColon + 2);

                switch (key)
                {
                    case "Track number":
                        ExtractTrackNumbers(trackInfo, content);
                        break;
                    case "Track type":
                        trackInfo.TrackType = ExtracTrackType(content);
                        break;
                    case "Enabled":
                        trackInfo.Enabled = ParseBoolean(content);
                        break;
                    case "Default flag":
                        trackInfo.Default = ParseBoolean(content);
                        break;
                    case "Forced flag":
                        trackInfo.Forced = ParseBoolean(content);
                        break;
                    case "Codec ID":
                        trackInfo.Codec = content;
                        break;
                    case "Language":
                        trackInfo.Language = content;
                        break;
                    case "Name":
                        trackInfo.Name = content;
                        break;
                }
            }
        }

        private static void ExtractTrackNumbers(TrackInfo trackInfo, String content)
        {
            Int32 indexOfOpenParen = content.IndexOf('(');
            Int32 indexOfColon = content.LastIndexOf(':');
            Int32 indexOfCloseParen = content.LastIndexOf(')');
            if (indexOfOpenParen > 1 && indexOfColon > indexOfOpenParen + 1 && indexOfCloseParen > indexOfColon + 1)
            {
                String trackNum = content.Substring(0, indexOfOpenParen - 1);
                String mkvToolsTrackNum = content.Substring(indexOfColon + 2, indexOfCloseParen - (indexOfColon + 2));
                trackInfo.TrackNumber = Int32.Parse(trackNum);
                trackInfo.MkvToolsTrackNumber = Int32.Parse(mkvToolsTrackNum);
            }
        }

        private static TrackType ExtracTrackType(String content)
        {
            switch (content)
            {
                case "video":
                    return TrackType.Video;
                case "audio":
                    return TrackType.Audio;
                case "subtitles":
                    return TrackType.Subtitle;
            }
            return TrackType.Other;
        }

        private static Boolean ParseBoolean(String content)
        {
            switch (content)
            {
                case "1":
                    return true;
            }
            return false;
        }
    }
}
