
namespace MKVInfoParser
{    
    class Program
    {        
        static void Main(string[] args)
        {
            VideoFileInfo vfi = MKVInfoProcessor.ParseMKVInfo("mkvinfo.txt");
            return;
        }

    }
}
