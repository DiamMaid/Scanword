using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Scanword.Domain;
using Scanword.Generator;

Console.OutputEncoding = Encoding.UTF8;

string? root = FindSolutionRoot();
if (root is null)
{
    Console.Error.WriteLine("Scanword.slnx не найден ни от CWD, ни от BaseDirectory. Запустите из папки решения или передайте путь аргументом.");
    return 1;
}

string assetsDir = Path.Combine(root, "Scanword", "Assets");
Directory.CreateDirectory(assetsDir);
string databasePath = Path.Combine(assetsDir, "levels.db");
string dumpPath = Path.Combine(root, "Scanword.Generator", "output", "levels_dump.json");

var specs = new (string Id, string Title, Difficulty Difficulty, int Words, int MaxDimension, int Seed)[]
{
    ("level-001", "Разминка", Difficulty.Easy, 6, 9, 101),
    ("level-002", "Классика", Difficulty.Normal, 8, 11, 202),
    ("level-003", "Мастер", Difficulty.Hard, 10, 13, 303)
};

var levels = new List<ScanwordLevel>();

foreach (var spec in specs)
{
    ScanwordLevel? level = null;

    for (int attempt = 0; attempt < 500 && level is null; attempt++)
    {
        var random = new Random(spec.Seed + attempt);
        CrosswordLayout? layout = new CrosswordLayoutEngine()
            .Generate(RussianDictionary.Entries, spec.Words, spec.MaxDimension, random);
        if (layout is null)
            continue;

        int estimatedMinutes = (int)Math.Ceiling(spec.Words / 2.0);
        ScanwordLevel candidate = ScanwordLevelBuilder.Build(
            layout, spec.Id, 1, spec.Difficulty, spec.Title, estimatedMinutes);

        LevelValidationResult validation = LevelValidator.Validate(candidate);
        if (!validation.IsValid)
        {
            foreach (InvariantViolation violation in validation.Violations.Take(3))
                Console.Error.WriteLine($"  [попытка {attempt}] инвариант {violation.InvariantNumber}: {violation.Message}");
            continue;
        }

        level = candidate;
    }

    if (level is null)
    {
        Console.Error.WriteLine($"FAIL {spec.Id}: за 500 попыток не получено валидной раскладки на {spec.Words} слов.");
        return 1;
    }

    levels.Add(level);
    Console.WriteLine(
        $"OK {level.Id} «{level.Meta.Title}» {level.Width}x{level.Height}, слов: {level.Words.Count} — " +
        string.Join(", ", level.Words.Select(w => w.Answer)));
}

LevelsDatabaseWriter.Write(databasePath, dumpPath, levels);
Console.WriteLine($"levels.db -> {databasePath}");
Console.WriteLine($"JSON-дамп -> {dumpPath}");
return 0;

static string? FindSolutionRoot()
{
    foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Scanword.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
    }

    return null;
}
