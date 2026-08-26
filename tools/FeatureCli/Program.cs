using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FeatureCli;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintUsage();
    return args.Length == 0 ? 1 : 0;
}

var type = args[0];
if (type is not ("query" or "command" or "crud"))
{
    Console.Error.WriteLine($"Unknown command '{type}'. Expected 'query', 'command' or 'crud'.");
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
string? plural = null;
var keyType = "int";
string? propertiesSpec = null;
var entityProjectName = "Domain";
string? entityPath = null;

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
        case "--plural":
            plural = RequireValue(args, ref i, "--plural");
            break;
        case "--key-type":
            keyType = RequireValue(args, ref i, "--key-type");
            break;
        case "--properties":
            propertiesSpec = RequireValue(args, ref i, "--properties");
            break;
        case "--entity-project":
            entityProjectName = RequireValue(args, ref i, "--entity-project");
            break;
        case "--entity-path":
            entityPath = RequireValue(args, ref i, "--entity-path");
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

if (string.IsNullOrWhiteSpace(feature) || string.IsNullOrWhiteSpace(entity) || (type != "crud" && string.IsNullOrWhiteSpace(name)))
{
    Console.Error.WriteLine(type == "crud"
        ? "Missing required arguments. --feature and --entity are both required."
        : "Missing required arguments. --feature, --entity and --name are all required.");
    PrintUsage();
    return 1;
}

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());

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

if (type == "crud")
{
    plural ??= $"{entity}s";

    PropertySpec[] properties;
    if (propertiesSpec is not null)
    {
        properties = ParseProperties(propertiesSpec);
    }
    else
    {
        var entityFile = FindEntityFile(entityPath, repoRoot, entityProjectName, entity);
        var detected = entityFile is not null ? ExtractProperties(entityFile) : [];

        if (detected.Length > 0)
        {
            properties = detected;
            var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), entityFile!);
            Console.WriteLine($"Detected {detected.Length} propert{(detected.Length == 1 ? "y" : "ies")} from '{relativePath}': {string.Join(", ", detected.Select(p => $"{p.Name}:{p.Type}"))}");
        }
        else
        {
            properties = ParseProperties(null);
            Console.WriteLine(entityFile is not null
                ? $"No scalar properties found on '{entity}' in '{Path.GetRelativePath(Directory.GetCurrentDirectory(), entityFile)}' - defaulting to \"Title:string\". Pass --properties to override."
                : $"Could not find '{entity}.cs' under the '{entityProjectName}' project to auto-detect properties - defaulting to \"Title:string\". Pass --properties to specify them, --entity-path to point at the file, or --entity-project if it's not called 'Domain'.");
        }
    }

    var fileSets = FeatureTemplates.Crud(entity, plural, feature, rootNamespace, dbContextNamespace, dbContextType, keyType, properties);

    var outputDirs = fileSets
        .Select(set => Path.Combine(projectDir, "Features", feature, set.Category, set.Name))
        .ToList();

    var conflicting = outputDirs.FirstOrDefault(dir => Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any() && !force);
    if (conflicting is not null)
    {
        Console.Error.WriteLine($"'{Path.GetRelativePath(Directory.GetCurrentDirectory(), conflicting)}' already exists and is not empty. Use --force to overwrite.");
        return 1;
    }

    foreach (var set in fileSets)
    {
        var outputDir = Path.Combine(projectDir, "Features", feature, set.Category, set.Name);
        Directory.CreateDirectory(outputDir);

        foreach (var (fileName, content) in set.Files)
        {
            var path = Path.Combine(outputDir, fileName);
            File.WriteAllText(path, content);
            Console.WriteLine($"Created {Path.GetRelativePath(Directory.GetCurrentDirectory(), path)}");
        }
    }

    return 0;
}

var category = type == "query" ? "Queries" : "Commands";
var outputDirSingle = Path.Combine(projectDir, "Features", feature, category, name!);

if (Directory.Exists(outputDirSingle) && Directory.EnumerateFileSystemEntries(outputDirSingle).Any() && !force)
{
    Console.Error.WriteLine($"'{Path.GetRelativePath(Directory.GetCurrentDirectory(), outputDirSingle)}' already exists and is not empty. Use --force to overwrite.");
    return 1;
}

Directory.CreateDirectory(outputDirSingle);

var files = type == "query"
    ? FeatureTemplates.Query(name!, feature, entity, rootNamespace, dbContextNamespace, dbContextType)
    : FeatureTemplates.Command(name!, feature, entity, rootNamespace, dbContextNamespace, dbContextType);

foreach (var (fileName, content) in files)
{
    var path = Path.Combine(outputDirSingle, fileName);
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

static string? FindEntityFile(string? explicitPath, string? repoRoot, string entityProjectName, string entityName)
{
    if (explicitPath is not null)
    {
        var full = Path.GetFullPath(explicitPath);
        return File.Exists(full) ? full : null;
    }

    if (repoRoot is null)
    {
        return null;
    }

    var matches = Directory.EnumerateFiles(repoRoot, $"{entityName}.cs", SearchOption.AllDirectories)
        .Where(p => !IsUnderBuildOutput(p))
        .ToList();

    if (matches.Count == 0)
    {
        return null;
    }

    if (matches.Count == 1)
    {
        return matches[0];
    }

    // Prefer a match that sits under a directory named after the entity project (e.g. ".../Domain/Entities/Foo.cs").
    return matches.FirstOrDefault(p => p.Split(Path.DirectorySeparatorChar)
        .Any(segment => segment.Equals(entityProjectName, StringComparison.OrdinalIgnoreCase))) ?? matches[0];
}

static PropertySpec[] ExtractProperties(string filePath)
{
    // A conservative list of BCL scalar types - anything else (another entity, a collection, an unknown
    // custom type) is assumed to be a navigation property or too ambiguous to scaffold and is skipped.
    var scalarTypes = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal)
    {
        "string", "bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong",
        "float", "double", "decimal", "char", "DateTime", "DateTimeOffset", "TimeSpan", "Guid"
    };

    var text = File.ReadAllText(filePath);

    var regex = new Regex(
        @"public\s+(?<type>[A-Za-z_]\w*\??)\s+(?<name>[A-Za-z_]\w*)\s*\{\s*get;\s*(?:set|init);\s*\}",
        RegexOptions.Multiline);

    var results = new System.Collections.Generic.List<PropertySpec>();

    foreach (Match match in regex.Matches(text))
    {
        var propName = match.Groups["name"].Value;
        var propType = match.Groups["type"].Value;

        if (propName is "Id" or "Created" or "CreatedBy" or "LastModified" or "LastModifiedBy")
        {
            continue;
        }

        if (!scalarTypes.Contains(propType.TrimEnd('?')))
        {
            continue;
        }

        results.Add(new PropertySpec(propName, propType));
    }

    return results.ToArray();
}

static PropertySpec[] ParseProperties(string? spec)
{
    if (string.IsNullOrWhiteSpace(spec))
    {
        return [new PropertySpec("Title", "string")];
    }

    return spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(part =>
        {
            var pieces = part.Split(':', 2);
            var propName = pieces[0].Trim();
            var propType = pieces.Length > 1 && !string.IsNullOrWhiteSpace(pieces[1]) ? pieces[1].Trim() : "string";
            return new PropertySpec(propName, propType);
        })
        .ToArray();
}

static void PrintUsage()
{
    Console.WriteLine("""
    Usage:
      feature query   --feature <FeatureFolder> --entity <EntityName> --name <QueryName> [options]
      feature command --feature <FeatureFolder> --entity <EntityName> --name <CommandName> [options]
      feature crud    --feature <FeatureFolder> --entity <EntityName> [options]

    Options:
      -p, --project <Name>            .csproj to scaffold into, matched by file name (default: Application)
      --project-path <path>           Explicit path to the target .csproj, skips project discovery
      --dbcontext <TypeName>          DbContext interface type used by the handler (default: IApplicationDbContext)
      --dbcontext-namespace <ns>      Namespace of that interface (default: <RootNamespace>.Common.Interfaces)
      --force                         Overwrite the target folder if it already exists and isn't empty

    'crud'-only options:
      --plural <Name>                 DbSet/query name for the entity (default: <EntityName>s)
      --key-type <Type>               Type of the entity's Id (default: int)
      --properties <spec>             Comma-separated Name:Type pairs for the entity, e.g. "Title:string,Notes:string?"
                                       If omitted, properties are auto-detected from the entity's own class file
                                       (found by searching for <EntityName>.cs) - only pass this to override.
      --entity-project <Name>         Project to search for <EntityName>.cs when auto-detecting (default: Domain)
      --entity-path <path>            Explicit path to the entity's .cs file, skips entity-file discovery

    Examples:
      feature query   --feature TodoLists --entity TodoList --name GetTodoListById
      feature command --feature TodoLists --entity TodoList --name DeleteTodoList
      feature crud    --feature Categories --entity Category --plural Categories
      feature crud    --feature Categories --entity Category --properties "Title:string,Description:string?"

    'query'/'command' generate separated files under <ProjectDir>/Features/<FeatureFolder>/<Queries|Commands>/<Name>/.
    'crud' generates a full working set for one entity - Create/Update/Delete commands and GetById/GetAll
    queries (with handlers, validators, and a shared Dto), each under its own <Name>/ folder, following the
    exact same pattern as the TodoLists feature already in this template. Create the entity class first
    (e.g. Domain/Entities/Category.cs) and 'crud' will read its properties automatically - no --properties
    needed unless the entity doesn't exist yet or you want a different property set than the entity has.
    The target project is found by searching for '<Name>.csproj' from the nearest *.sln upward.
    """);
}
