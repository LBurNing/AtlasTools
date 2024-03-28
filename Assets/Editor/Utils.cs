using System;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.IO;


public class Utils
{
    public static List<string> GetAllFileList(string path, string mark = ".cs", string mark1 = "")
    {
        List<string> files = new List<string>();
        string[] paths = Directory.GetFiles(path);
        foreach (string _p in paths)
        {
            if (_p.ToLower().EndsWith(mark.ToLower()) || (mark1 != "" && _p.ToLower().EndsWith(mark1.ToLower())))
                files.Add(_p);
        }
        string[] dirs = Directory.GetDirectories(path);
        foreach (string _d in dirs)
        {
            files.AddRange(GetAllFiles(_d, mark, mark1));
        }
        return files;
    }

    public static string[] GetAllFiles(string path, string mark = ".cs", string mark1 = "")
    {
        List<string> files = new List<string>();
        string[] paths = Directory.GetFiles(path);
        foreach (string _p in paths)
        {
            if (_p.ToLower().EndsWith(mark.ToLower()) || (mark1 != "" && _p.ToLower().EndsWith(mark1.ToLower())))
                files.Add(_p);
        }
        string[] dirs = Directory.GetDirectories(path);
        foreach (string _d in dirs)
        {
            files.AddRange(GetAllFiles(_d, mark, mark1));
        }
        return files.ToArray();
    }

    public static string[] GetAllFilesWithSubDir(string path)
    {
        List<string> files = new List<string>();
        string[] paths = Directory.GetFiles(path);
        foreach (string _p in paths)
        {
            files.Add(_p);
        }

        string[] dirs = Directory.GetDirectories(path);
        foreach (string _dir in dirs)
        {
            string[] dirFiles = GetAllFilesWithSubDir(_dir);
            foreach (string _p in dirFiles)
            {
                files.Add(_p);
            }
        }

        return files.ToArray();
    }

    public static string ArtworkPath2SystemPath(string path)
    {
        path = path.Replace("\\", "/");
        var p = Application.dataPath + "/" + path;
        return p.Replace("Assets/Assets", "Assets");
    }

    public static string SystemPath2ArtworkPath(string path)
    {
        path = path.Replace("\\", "/");
        return path.Replace(Application.dataPath, "Assets");
    }
}