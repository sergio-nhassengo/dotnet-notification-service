using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FeatureCli;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintUsage();
    return args.Length == 0 ? 1 : 0;
}

var type = args[0];
if (type is not ("query" or "command"))
{
    Console.Error.WriteLine($"Unknown command '{type}'. Expected 'query' or 'command'.");
    PrintUsage();
    return 1;
}

string? feature = null;
string? entity = null;
string? name = null;
var projectName = "Application";
string? projectPath = null;
var dbContextType = "IApplicationDbContext";
string? dbContextNamespace = null;
var force = false;

for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--feature" or "-f":
            feature = RequireValue(args, ref i, "--feature");
            break;
        case "--entity" or "-e":
            entity = RequireValue(args, ref i, "--entity");
            break;
        case "--name" or "-n":
            name = RequireValue(args, ref i, "--name");
            break;
        case "--project" or "-p":
            projectName = RequireValue(args, ref i, "--project");
            break;
        case "--project-path":
            projectPath = RequireValue(args, ref i, "--project-path");
            break;
        case "--dbcontext":
            dbContextType = RequireValue(args, ref i, "--dbcontext");
            break;
        case "--dbcontext-namespace":
            dbContextNamespace = RequireValue(args, ref i, "--dbcontext-namespace");
            break;
        case "--force":
            force = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
            PrintUsage();
            return 1;
    }
}

if (string.IsNullOrWhiteSpace(feature) || string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(name))
{
    Console.Error.WriteLine("Missing required arguments. --feature, --entity and --name are all required.");
    PrintUsage();
    return 1;
}

string csprojPath;

if (projectPath is not null)
{
    csprojPath = Path.GetFullPath(projectPath);
    if (!File.Exists(csprojPath))
    {
        Console.Error.WriteLine($"--project-path '{projectPath}' does not exist.");
        return 1;
    }
}
else
{
    var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
    if (repoRoot is null)
    {
        Console.Error.WriteLine("Could not locate the repository root (no *.sln file found in any parent directory). Pass --project-path to point at the target .csproj directly.");
        return 1;
    }

    var matches = Directory.EnumerateFiles(repoRoot, $"{projectName}.csproj", SearchOption.AllDirectories)
        .Where(p => !IsUnderBuildOutput(p))
        .ToList();

    if (matches.Count == 0)
    {
        Console.Error.WriteLine($"Could not find '{projectName}.csproj' under '{repoRoot}'. Use --project to name a different project, or --project-path to point at it directly.");
        return 1;
    }

    if (matches.Count > 1)
    {
        Console.Error.WriteLine($"Found multiple '{projectName}.csproj' files under '{repoRoot}':");
        foreach (var match in matches)
        {
            Console.Error.WriteLine($"  {Path.GetRelativePath(repoRoot, match)}");
        }
        Console.Error.WriteLine("Use --project-path to pick one.");
        return 1;
    }

    csprojPath = matches[0];
}

var projectDir = Path.GetDirectoryName(csprojPath)!;
var rootNamespace = ReadRootNamespace(csprojPath);
dbContextNamespace ??= $"{rootNamespace}.Common.Interfaces";

var category = type == "query" ? "Queries" : "Commands";
var outputDir = Path.Combine(projectDir, "Features", feature, category, name);

if (Directory.Exists(outputDir) && Directory.EnumerateFileSystemEntries(outputDir).Any() && !force)
{
    Console.Error.WriteLine($"'{Path.GetRelativePath(Directory.GetCurrentDirectory(), outputDir)}' already exists and is not empty. Use --force to overwrite.");
    return 1;
}

Directory.CreateDirectory(outputDir);

var files = type == "query"
    ? FeatureTemplates.Query(name, feature, entity, rootNamespace, dbContextNamespace, dbContextType)
    : FeatureTemplates.Command(name, feature, entity, rootNamespace, dbContextNamespace, dbContextType);

foreach (var (fileName, content) in files)
{
    var path = Path.Combine(outputDir, fileName);
    File.WriteAllText(path, content);
    Console.WriteLine($"Created {Path.GetRelativePath(Directory.GetCurrentDirectory(), path)}");
}

return 0;

static string RequireValue(string[] args, ref int i, string optionName)
{
    if (i + 1 >= args.Length)
    {
        throw new InvalidOperationException($"Option '{optionName}' requires a value.");
    }

    return args[++i];
}

static bool IsUnderBuildOutput(string path)
{
    var separator = Path.DirectorySeparatorChar;
    return path.Contains($"{separator}bin{separator}") || path.Contains($"{separator}obj{separator}");
}

static string? FindRepoRoot(string startDirectory)
{
    var dir = new DirectoryInfo(startDirectory);
    while (dir is not null)
    {
        if (dir.EnumerateFiles("*.sln").Any())
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    return null;
}

static string ReadRootNamespace(string csprojPath)
{
    var document = XDocument.Load(csprojPath);
    var value = document.Descendants("RootNamespace").FirstOrDefault()?.Value;
    return string.IsNullOrWhiteSpace(value) ? Path.GetFileNameWithoutExtension(csprojPath) : value.Trim();
}

static void PrintUsage()
{
    Console.WriteLine("""
    Usage:
      feature query   --feature <FeatureFolder> --entity <EntityName> --name <QueryName> [options]
      feature command --feature <FeatureFolder> --entity <EntityName> --name <CommandName> [options]

    Options:
      -p, --project <Name>            .csproj to scaffold into, matched by file name (default: Application)
      --project-path <path>           Explicit path to the target .csproj, skips project discovery
      --dbcontext <TypeName>          DbContext interface type used by the handler (default: IApplicationDbContext)
      --dbcontext-namespace <ns>      Namespace of that interface (default: <RootNamespace>.Common.Interfaces)
      --force                         Overwrite the target folder if it already exists and isn't empty

    Example:
      feature query   --feature TodoLists --entity TodoList --name GetTodoListById
      feature command --feature TodoLists --entity TodoList --name DeleteTodoList

    Generates separated files under <ProjectDir>/Features/<FeatureFolder>/<Queries|Commands>/<Name>/.
    The target project is found by searching for '<Name>.csproj' from the nearest *.sln upward.
    """);
}
