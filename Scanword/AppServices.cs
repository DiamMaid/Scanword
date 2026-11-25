using System;
using System.IO;
using Avalonia.Platform;

namespace Scanword;

public static class AppServices
{
    private const string LevelsAssetUri = "avares://Scanword/Assets/levels.db";

    public static string GetDataDirectory()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(root))
            root = Path.GetTempPath();

        string directory = Path.Combine(root, "Scanword");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string EnsureLevelsDatabase(string dataDirectory)
    {
        string target = Path.Combine(dataDirectory, "levels.db");

        using Stream asset = AssetLoader.Open(new Uri(LevelsAssetUri));
        using FileStream file = File.Create(target);
        asset.CopyTo(file);

        return target;
    }
}
