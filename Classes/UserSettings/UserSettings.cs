using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class UserSettings {
    List<string> fileLines;
    string filePath;
    public UserSettings(string file) {
        this.filePath = file;
        try {
            fileLines = File.ReadAllLines(filePath).ToList();
        } catch {
            Clear();
        }
    }
    public string GetValueForKey(string key) {
        foreach (var line in fileLines) {
            if (line.StartsWith(key)) {
                return line.Split("=")[1];
            }
        }
        return null;
    }
    public bool GetBoolForKey(string key) {
        foreach (var line in fileLines) {
            if (line.StartsWith(key)) {
                return line.Split("=")[1] == "True";
            }
        }
        return false;
    }
    public void SetValueForKey(string key, string value) {
        string newLine = $"{key}={value}";
        foreach (var line in fileLines) {
            if (line.StartsWith(key)) {
                int i = fileLines.IndexOf(line);
                fileLines[i] = newLine;
                return;
            }
        }
        fileLines.Add(newLine);
    }
    public void SetValueForKey(string key, double value) {
        SetValueForKey(key, value.ToString("0.##"));
    }
    public void SetValueForKey(string key, bool value) {
        SetValueForKey(key, value.ToString());
    }
    public void Write() {
        File.WriteAllLines(filePath, fileLines);
    }
    public void Clear() {
        fileLines = new();
    }
}

