using System;
using System.Collections.Generic;
using System.IO;

namespace MKVInfoParser
{
    public class VideoFileInfo
    {
        public List<TrackInfo> Tracks { get; set; }
        public FileInfo VideoFile { get; set; }
        public String SegmentUID { get; set; }
        
        public VideoFileInfo()
        {
            Tracks = new List<TrackInfo>();
        }
    }
}
