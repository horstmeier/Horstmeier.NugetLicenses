namespace Horstmeier.NugetLicenses.Models;

public record ProjectInfo(
    string ProjectFilePath,
    string ProjectName,
    int PackageCount,
    bool LockFileEnabled,
    bool HasLockFile);
