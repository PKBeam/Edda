using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

public class RecentOpenedFolders {
    int historySize;
    List<string> fileLines;
    string filePath;
    public RecentOpenedFolders(string file, int historySize) {
        this.filePath = file;
        try {
            fileLines = File.ReadAllLines(filePath).ToList();
        } catch {
            Clear();
        }
        this.historySize = historySize;
    }
    public void AddRecentlyOpened(string name, string path) {
        var fileString = $"{name}={path}";
        if (fileLines.Contains(fileString)) {
            fileLines.Remove(fileString);
        } 

        fileLines.Insert(0, fileString);
        if (fileLines.Count > historySize) {
            fileLines.RemoveAt(fileLines.Count - 1);
        }
    }
    // most recent first
    public List<(string, string)> GetRecentlyOpened() {
        List<(string, string)> result = new();
        foreach (var line in fileLines) {
            var splitLine = line.Split("=");
            result.Add((splitLine[0], splitLine[1]));
        }
        return result;
    }
    public void Write() {
        File.WriteAllLines(filePath, fileLines);
    }
    public void Clear() {
        fileLines = new();
    }
}

