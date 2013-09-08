MkvFlacToAac
============

A simple C# app/script that transcodes all FLAC audio tracks in an MKV file to AAC encoded audio tracks.

This is done utilizing the following third party tools:

* [MKVToolnix](http://www.bunkus.org/videotools/mkvtoolnix/) (mkvInfo, mkvExtract, mkvMerge)
* [ffmpeg](http://www.ffmpeg.org/)
* [Nero AAC Encoder](http://www.nero.com/enu/company/about-nero/nero-aac-codec.php)

However due to the nature of the configuration different encoders/decoders could be used.
The meat of the logic is in the construction of the mkvMerge statement.

Why did you make this script
----------------------------

Popcorn MKV Audio converter (or rather eac3to) cannot handle corrupt FLAC blocks (it occurs more frequently than you'd think)
And I really dislike FLAC since in my opinion it takes up useless space (especially the 24 bit variety)


What exactly does this script do
--------------------------------

This script performs the following tasks:

1. Scans a folder for mkv files
2. Detects which mkv files have FLAC encoded audio tracks in them
3. It extracts each FLAC audio stream and pipes it as WAV/PCM into nero AAC encoder which encodes it as AAC
4. A new mkv file is constructed based upon the original mkv file but with the FLAC audio streams replaced by AAC encoded streams

Take note that the Segment UID is transferred over to the new mkv file as well, this is to maintain external chapter support.


How is this script used
-----------------------

Before you can use this script you need to modify the *TranscodeFLACtoAAC.exe.config* file to point to the correct location of the required external tools. In the config you can also modify the commandline arguments passed to these external tools. (For instance to change the AAC target quality) The only exception to this is mkvMerge which is a bit too complex to put into configuration.

Once the configuration is completed you can run the utility with the command:

    TranscodeFLACtoAAC.exe [source folder] [target folder]

The two folders have to be different because original files are preserved.


How is this script compiled
---------------------------

I use visual studio 2010, but any C# 4.0 compiler will do, no external libraries are required.


What do you intend to improve
-----------------------------

Cleanup mainly, right now this script is a bit of a mess and still does not give a 100% identical output to the original.
I need to test much more with exotic situations (forced tracks etc)
I would also like to get all console output streamed to the application console, but I am too stuipid ATM to figure this out and I settled for the error output for now.


FlacFinder
==========

What exactly does this script do
--------------------------------

This script recursively searches a folder and all its subfolders for mkv files that contain audio streams with the FLAC codec.
The found files are listed into a textfile (for possible input into the MkvFlacToAac tool)

How is this script used
-----------------------

Before you can use this script you need to modify the *FlacFinder.exe.config* file to point to the correct location of the required external tools.

Once the configuration is completed you can run the utility with the command:

    FlacFinder.exe [start folder] {output folder}

If the output folder is not specified the resulting textfile will be written to the exe location (This **will** give problems in UAC protected folders)
