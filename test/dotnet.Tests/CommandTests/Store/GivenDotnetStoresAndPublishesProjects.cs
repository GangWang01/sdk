// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Publish.Tests
{
    public class GivenDotnetStoresAndPublishesProjects : SdkTest
    {
        private static string _tfm = "netcoreapp3.1";
        private static string _arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        private static string _defaultConfiguration = "Debug";

        public GivenDotnetStoresAndPublishesProjects(ITestOutputHelper log) : base(log)
        {
        }

        [Fact(Skip = "https://github.com/dotnet/cli/issues/12482")]
        public void ItPublishesARunnablePortableApp()
        {
            var testAppName = "NewtonSoftDependentProject";
            var profileProjectName = "NewtonsoftProfile";

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;
            var rid = EnvironmentInfo.GetCompatibleRid();
            var localAssemblyCache = Path.Combine(testProjectDirectory, "localAssemblyCache");
            var intermediateWorkingDirectory = Path.Combine(testProjectDirectory, "workingDirectory");
            var profileProjectPath = _testAssetsManager.CopyTestAsset(profileProjectName).WithSource().Path;
            var profileProject = Path.Combine(profileProjectPath, $"{profileProjectName}.xml");

            new RestoreCommand(testInstance)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, "store",
                    "--manifest", profileProject,
                    "-f", _tfm,
                    "-r", rid,
                    "-o", localAssemblyCache,
                    "-w", intermediateWorkingDirectory)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? _defaultConfiguration;
            var profileFilter = Path.Combine(localAssemblyCache, _arch, _tfm, "artifact.xml");

            new DotnetPublishCommand(Log,
                    "-f", _tfm,
                    "--manifest", profileFilter)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, _tfm, "publish", $"{testAppName}.dll");

            new DotnetCommand(Log)
                .WithEnvironmentVariable("DOTNET_SHARED_STORE", localAssemblyCache)
                .Execute(outputDll)
                .Should().Pass()
                .And.HaveStdOutContaining("{}");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  Failed to load /private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, error: dlopen(/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), '/System/Volumes/Preboot/Cryptexes/OS/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (no such file), '/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64'))
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void AppFailsDueToMissingCache()
        {
            var testAppName = "NuGetConfigDependentProject";
            var profileProjectName = "NuGetConfigProfile";
            var targetManifestFileName = "NuGetConfigFilterProfile.xml";

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;
            var profileProjectPath = _testAssetsManager.CopyTestAsset(profileProjectName).WithSource().Path;
            var profileFilter = Path.Combine(profileProjectPath, targetManifestFileName);

            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? _defaultConfiguration;

            new DotnetPublishCommand(Log,
                    "-f", _tfm,
                    "--manifest", profileFilter)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, _tfm, "publish", $"{testAppName}.dll");

            new DotnetCommand(Log)
                .Execute(outputDll)
                .Should().Fail()
                .And.HaveStdErrContaining($"Unhandled exception. System.IO.FileNotFoundException:");
        }

        //  Windows only for now due to https://github.com/dotnet/cli/issues/7501
        [WindowsOnlyFact(Skip = "https://github.com/dotnet/cli/issues/12482")]
        public void ItPublishesAnAppWithMultipleProfiles()
        {
            var testAppName = "MultiDependentProject";
            var profileProjectName = "NewtonsoftProfile";
            var profileProjectName1 = "FluentProfile";

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;
            var rid = EnvironmentInfo.GetCompatibleRid();
            var localAssemblyCache = Path.Combine(testProjectDirectory, "lAC");
            var intermediateWorkingDirectory = Path.Combine(testProjectDirectory, "workingDirectory");

            var profileProjectPath = _testAssetsManager.CopyTestAsset(profileProjectName).WithSource().Path;
            var profileProject = Path.Combine(profileProjectPath, $"{profileProjectName}.xml");
            var profileFilter = Path.Combine(profileProjectPath, "NewtonsoftFilterProfile.xml");

            var profileProjectPath1 = _testAssetsManager.CopyTestAsset(profileProjectName1).WithSource().Path;
            var profileProject1 = Path.Combine(profileProjectPath1, $"{profileProjectName1}.xml");
            var profileFilter1 = Path.Combine(profileProjectPath1, "FluentFilterProfile.xml");

            new RestoreCommand(testInstance)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, "store",
                    "--manifest", profileProject,
                    "--manifest", profileProject1,
                    "-f", _tfm,
                    "-r", rid,
                    "-o", localAssemblyCache,
                    "-w", intermediateWorkingDirectory)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? _defaultConfiguration;

            new DotnetPublishCommand(Log,
                    "-f", _tfm,
                    "--manifest", profileFilter,
                    "--manifest", profileFilter1)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, _tfm, "publish", $"{testAppName}.dll");

            new DotnetCommand(Log)
                .WithEnvironmentVariable("DOTNET_SHARED_STORE", localAssemblyCache)
                .Execute(outputDll)
                .Should().Pass()
                .And.HaveStdOutContaining("{}");
        }
    }
}
