using System;
using System.IO;

namespace MKVInfoParser
{
    public class TrackInfo
    {
        public Int32 TrackNumber { get; set; }
        public Int32 MkvToolsTrackNumber { get; set; }
        public TrackType TrackType { get; set; }
        public Boolean Enabled { get; set; }
        public Boolean Default { get; set; }
        public Boolean Forced { get; set; }
        public String Codec { get; set; }
        public String Language { get; set; }
        public String Name { get; set; }
        public FileInfo ExternalFileRef { get; set; }
    }
}
