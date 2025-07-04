// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.List;
using Microsoft.DotNet.Cli.Commands.Tool.Update;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal class ToolInstallLocalCommand : CommandBase
{
    private readonly IToolManifestFinder _toolManifestFinder;
    private readonly IToolManifestEditor _toolManifestEditor;
    private readonly ILocalToolsResolverCache _localToolsResolverCache;
    private readonly ToolInstallLocalInstaller _toolLocalPackageInstaller;
    private readonly IReporter _reporter;
    private readonly PackageId? _packageId;
    private readonly bool _allowPackageDowngrade;

    private readonly string _explicitManifestFile;
    private readonly bool _createManifestIfNeeded;
    private readonly bool _allowRollForward;
    private readonly bool _updateAll;

    internal RestoreActionConfig restoreActionConfig;

    public ToolInstallLocalCommand(
        ParseResult parseResult,
        PackageId? packageId = null,
        IToolPackageDownloader toolPackageDownloader = null,
        IToolManifestFinder toolManifestFinder = null,
        IToolManifestEditor toolManifestEditor = null,
        ILocalToolsResolverCache localToolsResolverCache = null,
        IReporter reporter = null,
        string runtimeJsonPathForTests = null
        )
        : base(parseResult)
    {
        _updateAll = parseResult.GetValue(ToolUpdateCommandParser.UpdateAllOption);
        var packageIdArgument = parseResult.GetValue(ToolInstallCommandParser.PackageIdentityArgument).Id;
        _packageId = packageId ?? (packageIdArgument is not null ? new PackageId(packageIdArgument) : null);
        _explicitManifestFile = parseResult.GetValue(ToolInstallCommandParser.ToolManifestOption);

        _createManifestIfNeeded = parseResult.GetValue(ToolInstallCommandParser.CreateManifestIfNeededOption);

        _reporter = reporter ?? Reporter.Output;

        _toolManifestFinder = toolManifestFinder ??
                              new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
        _toolManifestEditor = toolManifestEditor ?? new ToolManifestEditor();
        _localToolsResolverCache = localToolsResolverCache ?? new LocalToolsResolverCache();

        restoreActionConfig = new RestoreActionConfig(DisableParallel: parseResult.GetValue(ToolCommandRestorePassThroughOptions.DisableParallelOption),
            NoCache: parseResult.GetValue(ToolCommandRestorePassThroughOptions.NoCacheOption) || parseResult.GetValue(ToolCommandRestorePassThroughOptions.NoHttpCacheOption),
            IgnoreFailedSources: parseResult.GetValue(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption),
            Interactive: parseResult.GetValue(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption));

        _toolLocalPackageInstaller = new ToolInstallLocalInstaller(parseResult, toolPackageDownloader, runtimeJsonPathForTests, restoreActionConfig);
        _allowRollForward = parseResult.GetValue(ToolInstallCommandParser.RollForwardOption);
        _allowPackageDowngrade = parseResult.GetValue(ToolInstallCommandParser.AllowPackageDowngradeOption);
    }

    public override int Execute()
    {
        if (_updateAll)
        {
            var toolListCommand = new ToolListLocalCommand(_parseResult, (IToolManifestInspector)_toolManifestFinder);
            var toolIds = toolListCommand.GetPackages(null);
            foreach (var toolId in toolIds)
            {
                ExecuteInstallCommand(toolId.Item1.PackageId);
            }
            return 0;
        }
        else
        {
            return ExecuteInstallCommand((PackageId) _packageId);
        }
    }

    private int ExecuteInstallCommand(PackageId packageId)
    {
        FilePath manifestFile = GetManifestFilePath();

        (FilePath? manifestFileOptional, string warningMessage) =
            _toolManifestFinder.ExplicitManifestOrFindManifestContainPackageId(_explicitManifestFile, packageId);

        if (warningMessage != null)
        {
            _reporter.WriteLine(warningMessage.Yellow());
        }

        manifestFile = manifestFileOptional ?? GetManifestFilePath();
        var existingPackageWithPackageId = _toolManifestFinder.Find(manifestFile).Where(p => p.PackageId.Equals(packageId));

        if (!existingPackageWithPackageId.Any())
        {
            return InstallNewTool(manifestFile, packageId);
        }

        var existingPackage = existingPackageWithPackageId.Single();
        var toolDownloadedPackage = _toolLocalPackageInstaller.Install(manifestFile, packageId);

        InstallToolUpdate(existingPackage, toolDownloadedPackage, manifestFile, packageId);

        _localToolsResolverCache.SaveToolPackage(
            toolDownloadedPackage,
            _toolLocalPackageInstaller.TargetFrameworkToInstall);

        return 0;
    }

    public int InstallToolUpdate(ToolManifestPackage existingPackage, IToolPackage toolDownloadedPackage, FilePath manifestFile, PackageId packageId)
    {
        if (existingPackage.Version > toolDownloadedPackage.Version && !_allowPackageDowngrade)
        {
            throw new GracefulException(
                [
                    string.Format(
                        CliCommandStrings.UpdateLocalToolToLowerVersion,
                        toolDownloadedPackage.Version.ToNormalizedString(),
                        existingPackage.Version.ToNormalizedString(),
                        manifestFile.Value)
                ],
                isUserError: false);
        }
        else if (existingPackage.Version == toolDownloadedPackage.Version)
        {
            _reporter.WriteLine(
                string.Format(
                    CliCommandStrings.UpdateLocaToolSucceededVersionNoChange,
                    toolDownloadedPackage.Id,
                    existingPackage.Version.ToNormalizedString(),
                    manifestFile.Value));
        }
        else
        {
            _toolManifestEditor.Edit(
                manifestFile,
                packageId,
                toolDownloadedPackage.Version,
                [toolDownloadedPackage.Command.Name]);
            _reporter.WriteLine(
                string.Format(
                    CliCommandStrings.UpdateLocalToolSucceeded,
                    toolDownloadedPackage.Id,
                    existingPackage.Version.ToNormalizedString(),
                    toolDownloadedPackage.Version.ToNormalizedString(),
                    manifestFile.Value).Green());
        }

        _localToolsResolverCache.SaveToolPackage(
            toolDownloadedPackage,
            _toolLocalPackageInstaller.TargetFrameworkToInstall);

        return 0;
    }

    public int InstallNewTool(FilePath manifestFile, PackageId packageId)
    {
        IToolPackage toolDownloadedPackage =
            _toolLocalPackageInstaller.Install(manifestFile, packageId);

        _toolManifestEditor.Add(
            manifestFile,
            toolDownloadedPackage.Id,
            toolDownloadedPackage.Version,
            [toolDownloadedPackage.Command.Name],
            _allowRollForward);

        _localToolsResolverCache.SaveToolPackage(
            toolDownloadedPackage,
            _toolLocalPackageInstaller.TargetFrameworkToInstall);

        _reporter.WriteLine(
            string.Format(
                CliCommandStrings.LocalToolInstallationSucceeded,
                toolDownloadedPackage.Command.Name,
                toolDownloadedPackage.Id,
                toolDownloadedPackage.Version.ToNormalizedString(),
                manifestFile.Value).Green());

        return 0;
    }

    public FilePath GetManifestFilePath()
    {
        return string.IsNullOrWhiteSpace(_explicitManifestFile)
            ? _toolManifestFinder.FindFirst(_createManifestIfNeeded)
            : new FilePath(_explicitManifestFile);
    }
}
