MkvFlacToAac
============

A simple C# app/script that transcodes all FLAC audio tracks in an MKV file to AAC encoded audio tracks.

This is done utilizing the following third party components:

* [MKVToolnix](http://www.bunkus.org/videotools/mkvtoolnix/) (mkvInfo, mkvExtract, mkvMerge)
* [The official FLAC tools](http://flac.sourceforge.net/download.html)
* [Nero AAC Encoder](http://www.nero.com/enu/company/about-nero/nero-aac-codec.php)

However due to the nature of the configuration different encoders/decoders could be used.
The meat of the logic is in the construction of the mkvMerge statement.

Why did you make this script
----------------------------

Popcorn MKV Audio converter could not handle corrupt FLAC blocks (it occurs more frequently than you'd think)
And I really dislike flac since in my opinion it takes up useless space (especially the 24 bit variety)


What exactly does this script do
--------------------------------

This script performs the following tasks:

1. Scans a folder for mkv files
2. Detects which mkv files have FLAC encoded audio tracks in them
3. It extracts each FLAC aduio stream
4. The flac gets decoded to WAV/PCM
5. The WAV/PCM is encoded as AAC
6. A new mkv file is constructed based upon the original mkv file but with the FLAC audio streams replaced by AAC encoded streams

Take note that the Segment UID is transferred over to the new mkv file as well, this is to maintain external chapter support.


How is this script used
-----------------------

TranscodeFLACtoAAC.exe [source folder] [target folder]

The two folders have to be different because original file names are preserved.


How is this script compiled
---------------------------

I use visual studio 2010, but any C# 4.0 compiler will do, no external libraries are required.


What do you intend to improve
-----------------------------

Cleanup mainly, right now this script is a bit of a mess and still does not give a 100% correct output. (mainly chapter issues)
I need to test much more with exotic situations (forced tracks etc)
I would also like to get all console output streamed to the application console, but I am too stuipid ATM to figure this out and I settled for the error output for now.
