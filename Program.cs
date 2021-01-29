﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

class SubMerger {
    private static int Main(string[] args) {
        // MOVIE/SE E:\DOWNLOAD\TestMovie.2018.German.1080p.BluRay.x264-RAiNBOW\*.mkv                                                      (maybe folder Subs and Sample!) (subfolders >= 2)
        // COMPLETE E:\DOWNLOAD\TestSeries.S01.German.1080p.BluRay.x264-RAiNBOW\Test.S01E01.German.1080p.BluRay.x264-RAiNBOW\*.mkv
        //                                  args[0] = folder                   |              subfolder   

        string path = args[0];
        string name = args.Length > 1 ? args[1] : DateTime.Now.ToString("dd-MM_HH-mm");

        using StreamWriter log = new StreamWriter(path + name + ".log") {
            AutoFlush = true // writes any text instantly to the file, with false it only writes when returning
        };
        
        try {
            string[] dirArray = Directory.GetDirectories(path);
            int dirArrayCount = dirArray.Length;
            Array.Sort(dirArray); // sorts from A-Z to have a correct episode order
            Output.WriteLine(log, "Start Time: " + DateTime.Now.ToString("dd.MM HH:mm:ss") + "\n - " + path + "\\(" + dirArrayCount + ")\n");

            int i = 0;
            if(dirArrayCount > 1 && !Directory.Exists(path + @"\Sample")) { // #1: Check for multiple folders (indicates a full season) #2: If a sample folder exists it's more likely a movie or single episode
                int countEngSubs = Folder.CountExistingSubfiles(path);

                if(countEngSubs != dirArrayCount) {
                    int missingSubs = dirArrayCount - countEngSubs;
                    Output.WriteLine(log, "WARNING! Subtitles are MISSING (MISSING: " + missingSubs + ") (FOUND: " + countEngSubs + ", TOTAL: " + dirArrayCount + ")");
                    Folder.WriteAllMissingSubtitles(dirArray, log);
                }

                foreach(string episodeFolder in dirArray) {
                    i++;
                    Output.WriteLine(log, DateTime.Now.ToString("HH:mm:ss") + " | (M) mkvmerge: #" + i.ToString() + " of " + dirArrayCount + " \t(" + episodeFolder.Remove(0, path.Length).Remove(0, 1) + ")\t");
                    Folder.MoveSubs(episodeFolder);
                    if(Directory.Exists(episodeFolder + @"\Subs")) { Folder.MoveFiles(episodeFolder); }
                    if(Directory.GetFiles(episodeFolder).Length > 1) { mkvmerge.Start(episodeFolder); }
                    Output.Write(log, "\tcompleted\n");
                }

                Output.Write(log, "Script finished");
                return 0; // 0 -> Success
            } else { // < 1 for single episodes or movies
                Output.WriteLine(log, DateTime.Now.ToString("HH:mm:ss") + " (S) mkvmerge in progress");
                // moves all subtitles files to the root of the mkv
                Folder.MoveSubs(path);
                if(Directory.GetFiles(path).Length > 1) { mkvmerge.Start(path); }
                Output.Write(log, "\n" + DateTime.Now.ToString("HH:mm:ss") + " done");
                return 0; // 0 -> Success
            }
        } catch(Exception e) { log.WriteLine(e.ToString()); return 1; }
    }
}
class Output {
    public static void Write(StreamWriter log, string output) {
        Console.Write(output);
        log.Write(output);
    }
    public static void WriteLine(StreamWriter log, string output) {
        Console.WriteLine(output);
        log.WriteLine(output);
    }
}
class Folder {
    public static void MoveSubs(string inputPath) {
        string subtitlesPath = inputPath + @"\Subs";
        if(Directory.Exists(subtitlesPath)) {
            try {
                IEnumerable<FileInfo> files = Directory.GetFiles(subtitlesPath).Select(f => new FileInfo(f));       // get every file from the
                foreach(var file in files) { File.Move(file.FullName, Path.Combine(inputPath, file.Name)); }
                if(IsDirectoryEmpty(subtitlesPath)) { Directory.Delete(subtitlesPath); };
            } catch(Exception e) { Console.WriteLine(e.ToString()); }
        }
    }
    public static void MoveFiles(string episodeFolder) {
        string subFolder = episodeFolder + @"\Subs";
        try {
            IEnumerable<FileInfo> files = Directory.GetFiles(subFolder).Select(f => new FileInfo(f));
            foreach(var file in files) {File.Move(file.FullName, Path.Combine(episodeFolder, file.Name));}
            if(IsDirectoryEmpty(subFolder)) {Directory.Delete(subFolder);};
        } catch(Exception e) { Console.WriteLine(e.ToString()); }
    }
    public static bool IsDirectoryEmpty(string path) {
        return !Directory.EnumerateFileSystemEntries(path).Any();
    }
    public static void RenameFile(string from, string to) {
        if(File.Exists(from) && File.Exists(to)) {
            File.Delete(to);
            File.Move(from, to);
        }
    }
    public static void DeleteSubtitleFiles(string[] subFiles) {
        foreach(string subFile in subFiles) {File.Delete(subFile);}
    }
    public static bool SubfilesExist(string episodeFolder, string wildcard) {
        return Directory.GetFiles(episodeFolder, wildcard + ".idx").Any() && Directory.GetFiles(episodeFolder, wildcard + ".sub").Any();
    }
    public static void WriteAllMissingSubtitles(string[] subfolders, StreamWriter log) {
        foreach(string subfolder in subfolders)
            if(!Directory.Exists(subfolder + @"\Subs\"))
                log.WriteLine("|- " + subfolder.Remove(0, 0).Remove(0, 1));
        log.Write("\n");
    }
    public static int CountExistingSubfiles(string folder) {
        int countEngSubs = 0;
        foreach(string subfolder in Directory.GetDirectories(folder))
            if(Directory.Exists(subfolder + @"\Subs\"))
                foreach(string subsfolder in Directory.GetFiles(subfolder + @"\Subs", "*eng.sub"))
                    countEngSubs++;
        return countEngSubs;
    }
}
class mkvmerge {
    public static void Start(string episodeFolder) {
        try {
            string mkvInputPath = Directory.GetFiles(episodeFolder, "*.mkv").First();
            string mkvInputName = Path.GetFileNameWithoutExtension(mkvInputPath);
            string mkvOutputPath = mkvInputPath.Remove(mkvInputPath.Length - 4, 4); mkvOutputPath += "_new.mkv";
            string[] subtitlesIdx = Directory.GetFiles(episodeFolder, "*.idx");
            string[] subtitlesSub = Directory.GetFiles(episodeFolder, "*.sub");

            string subtitleEngFull = mkvInputName + "*eng";
            string subtitleEngForced = mkvInputName + "*eng*forced";
            string subtitleGerFull = mkvInputName;
            string subtitleGerForced = mkvInputName + "*forced";

            string mkvmerge = "& 'C:\\Program Files\\MKVToolNix\\mkvmerge.exe' -o '" + mkvOutputPath + "' -q --no-subtitles '" + mkvInputPath + "' ";
            if(Folder.SubfilesExist(episodeFolder, subtitleEngFull)) {
                mkvmerge += "--language 0:eng " +
                            "--track-name 0:Full '"
                            + Directory.GetFiles(episodeFolder, subtitleEngFull + ".idx")[0] + "' '"
                            + Directory.GetFiles(episodeFolder, subtitleEngFull + ".sub")[0] + "' ";
            }
            if(Folder.SubfilesExist(episodeFolder, subtitleEngForced)) {
                mkvmerge += "--language 0:eng " +
                            "--track-name 0:Forced '"
                            + Directory.GetFiles(episodeFolder, subtitleEngForced + ".idx")[0] + "' '"
                            + Directory.GetFiles(episodeFolder, subtitleEngForced + ".sub")[0] + "' ";
            }
            if(Folder.SubfilesExist(episodeFolder, subtitleGerFull)) {
                mkvmerge += "--language 0:ger " +
                            "--track-name 0:Full '"
                            + Directory.GetFiles(episodeFolder, subtitleGerFull + ".idx")[0] + "' '"
                            + Directory.GetFiles(episodeFolder, subtitleGerFull + ".sub")[0] + "' ";
            }
            if(Folder.SubfilesExist(episodeFolder, subtitleGerForced)) {
                mkvmerge += "--language 0:ger " +
                            "--track-name 0:Forced '"
                            + Directory.GetFiles(episodeFolder, subtitleGerForced + ".idx")[0] + "' '"
                            + Directory.GetFiles(episodeFolder, subtitleGerForced + ".sub")[0] + "' ";
            }

            if(File.Exists(mkvOutputPath)) { File.Delete(mkvOutputPath); }
            RunCommand(mkvmerge);
            Folder.RenameFile(mkvOutputPath, mkvInputPath);
            Folder.DeleteSubtitleFiles(subtitlesIdx);
            Folder.DeleteSubtitleFiles(subtitlesSub);
        } catch(Exception e) { Console.WriteLine(e.ToString()); }
    }
    public static void RunCommand(string command) {
        ProcessStartInfo cmdsi = new ProcessStartInfo("powershell.exe");
        cmdsi.Arguments = command;
        Process cmd = Process.Start(cmdsi);
        // status output in percentage
        cmd.WaitForExit();
    }
}