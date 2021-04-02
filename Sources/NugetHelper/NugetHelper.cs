using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Resolver;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.IO;
using NuGet.Packaging;
using NuGet.Packaging.Signing;

namespace NugetHelper
{
    public static class NugetHelper
    {
        public class NugetLogger : ILogger
        {
            public void Log(LogLevel level, string data)
            {
                switch (level)
                {
                    case LogLevel.Debug:
                        LogDebug(data);
                        break;
                    case LogLevel.Error:
                        LogError(data);
                        break;
                    case LogLevel.Information:
                        LogInformation(data);
                        break;
                    case LogLevel.Minimal:
                        LogMinimal(data);
                        break;
                    case LogLevel.Warning:
                        LogWarning(data);
                        break;
                    case LogLevel.Verbose:
                        LogVerbose(data);
                        break;
                    default:
                        LogDebug(data);
                        break;
                }
            }

            public Task LogAsync(LogLevel level, string data)
            {
                throw new NotImplementedException();
            }

            public void LogDebug(string data)
            {
                System.Diagnostics.Debug.WriteLine(data, "DEBUG");
            }

            public void LogVerbose(string data)
            {
                System.Diagnostics.Debug.WriteLine(data, "TRACE");
            }

            public void LogInformation(string data)
            {
                System.Diagnostics.Debug.WriteLine(data, "INFO");
            }

            public void LogInformationSummary(string data)
            {
                System.Diagnostics.Debug.WriteLine(data, "INFO");
            }

            public void LogMinimal(string data)
            {
                System.Diagnostics.Debug.WriteLine(data, "MINIMAL");
            }

            public void LogWarning(string data)
            {
                System.Diagnostics.Debug.WriteLine(data, "WARNING");
            }

            public void LogError(string data)
            {
                System.Diagnostics.Debug.WriteLine(data, "ERROR");
            }

            public void LogErrorSummary(string data)
            {
                System.Diagnostics.Debug.WriteLine(data, "ERROR");
            }

            public void Log(ILogMessage message)
            {
                throw new NotImplementedException();
            }

            public Task LogAsync(ILogMessage message)
            {
                throw new NotImplementedException();
            }
        }

        private static ILogger _logger = new NugetLogger();

        private static void ThrowException<T>(NugetPackage p, IEnumerable<NugetPackage> packages, string message) where T : Exception
        {
            var newPackages = new List<NugetPackage>();
            newPackages.Add(p);
            newPackages.AddRange(packages);
            ThrowException<T>(newPackages, message);
        }

        private static void ThrowException<T>(IEnumerable<NugetPackage> packages, string message) where T : Exception
        {
            throw (T)Activator.CreateInstance(typeof(T), string.Format("{0}\n\nAffected packages:\n{1}", message, string.Join("\n", packages)));
        }

        private static void ThrowException<T>(IEnumerable<PackageDependency> packages, string message) where T : Exception
        {
            throw (T)Activator.CreateInstance(typeof(T), string.Format("{0}\n\nAffected packages:\n{1}", message, string.Join("\n", packages)));
        }

        public static void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        public static void CheckPackagesConsistency(IReadOnlyList<NugetPackage> packages, bool forceMinMatch = false, bool ignoreDependencies = false)
        {
            foreach (var p in packages)
            {
                var filtered = packages.Where(x => x.Id == p.Id && x.MinVersion != p.MinVersion);

                if (filtered.Count() != 0)
                {
                    ThrowException<Exceptions.MultiplePackagesFoundException>(p, filtered, $"The packet with id {p.Id} is present in multiple version.");
                }

                if (!ignoreDependencies)
                {
                    foreach (var d in p.Dependencies)
                    {
                        var dependecyFoundInList = packages.Where(x => x.Id == d.PackageDependency.Id);
                        if (dependecyFoundInList.Count() == 0)
                        {
                            ThrowException<Exceptions.DependencyNotFoundException>(new[] { d.PackageDependency }, $"The dependency {d} of the packet with id {p.Id} is not present.");
                        }
                        else if (dependecyFoundInList.Count() > 1)
                        {
                            ThrowException<Exceptions.MultipleDependenciesFoundException>(dependecyFoundInList, $"The dependency {d} of the packet with id {p.Id} is present multiple times.");
                        }
                        else
                        {
                            var dependencyCandidate = dependecyFoundInList.Single();

                            if (!d.PackageDependency.VersionRange.Satisfies(dependencyCandidate.VersionRange.MinVersion))
                            {
                                ThrowException<Exceptions.InvalidDependencyFoundException>(dependecyFoundInList, $"The dependency {d} of the packet with id {p.Id} is not present in a supported version : {d.PackageDependency.VersionRange.PrettyPrint()} vs {dependencyCandidate.VersionRange.PrettyPrint()}.");
                            }
                            else if (forceMinMatch && d.ForceMinVersion)
                            {
                                var minVersionString = d.PackageDependency.VersionRange.ToNonSnapshotRange().MinVersion.ToString();

                                if (
                                    minVersionString.Replace(dependencyCandidate.MinVersion, "").Replace(".", "").Where(x => x != '0').Any() &&
                                    dependencyCandidate.MinVersion.Replace(minVersionString, "").Replace(".", "").Where(x => x != '0').Any()
                                   )
                                {
                                    ThrowException<Exceptions.InvalidMinVersionDependencyFoundExceptio>(dependecyFoundInList, $"The dependency {d} of the packet with id {p.Id} would satisfy the needs, but forceMinMatch is set to true : {d.PackageDependency.VersionRange.PrettyPrint()} vs {dependencyCandidate.VersionRange.PrettyPrint()}.");
                                }
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<NugetPackage> InstallPackages(IReadOnlyList<NugetPackage> packages, bool autoInstallDependencis, Action<string> installedProgress)
        {
            var installed = new List<NugetPackage>();
            foreach (var p in packages)
            {
                //Always go through the installation method, to gather the dependency information
                InstallPackage(p, autoInstallDependencis, installed);

                string extraMsg = "";
                if (autoInstallDependencis)
                {
                    extraMsg = " and its dependencies";
                }
                installedProgress?.Invoke($"Installed: {p.Id}, Version: {p.MinVersion}{extraMsg}");
            }
            return installed;
        }

        private static void InstallPackage(NugetPackage requestedPackage, bool autoInstallDependencis, List<NugetPackage> installed)
        {
            Exception threadEx = null;

            var th = new Thread(() =>
                           {
                               try
                               {
                                   var t = PerformPackageActionAsync(requestedPackage, autoInstallDependencis, installed, (package, toInstallPackage, settings, cache, installedPackagesList) => InstallPackageActionAsync(package, toInstallPackage, settings, cache, installedPackagesList));
                                   t.Wait();
                               }
                               catch (Exception e)
                               {
                                   threadEx = e;
                               }
                           });
            th.IsBackground = true;
            //th.SetApartmentState(ApartmentState.MTA);
            th.Start();
            th.Join();
            if (threadEx != null)
            {
                throw new Exception(string.Format("Unable to install package {0} or one of its dependencies. See inner exception for more details", requestedPackage.ToString()), threadEx);
            }
        }

        /// <summary>
        /// This method looks for the requestedPackage and downloads it. 
        /// No installation nor dependency check is performed.
        /// If the package already exists, it will be not overwritten.
        /// </summary>
        /// <param name="requestedPackage"></param>
        /// <param name="destinationDirectory"></param>
        public static void DownloadPackage(NugetPackage requestedPackage, string destinationDirectory)
        {
            Exception threadEx = null;

            var th = new Thread(() =>
            {
                try
                {
                    var t = PerformPackageActionAsync(requestedPackage, false, null, (package, toInstallPackage, settings, cache, installedPackagesList) => DownloadPackageActionAsync(package, toInstallPackage, settings, cache, destinationDirectory));
                    t.Wait();
                }
                catch (Exception e)
                {
                    threadEx = e;
                }
            });
            th.IsBackground = true;
            //th.SetApartmentState(ApartmentState.MTA);
            th.Start();
            th.Join();
            if (threadEx != null)
            {
                throw new Exception(string.Format("Unable to download package {0} or one of its dependencies. See inner exception for more details", requestedPackage.ToString()), threadEx);
            }
        }

        /// <summary>
        /// This method looks for the requestedPackage and installes (unpacks) it.
        /// </summary>
        /// <param name="requestedPackage"></param>
        /// <param name="installedPackages"></param>
        /// <returns></returns>
        private static async Task InstallPackageActionAsync(NugetPackage requestedPackage,
                                                            SourcePackageDependencyInfo packageToInstall,
                                                            ISettings settings,
                                                            SourceCacheContext cacheContext,
                                                            List<NugetPackage> installedPackages)
        {
            var packetRoot = Path.GetFullPath(requestedPackage.RootPath);
            //var packagePathResolver = new PackagePathResolver(Path.Combine(packetRoot, packageToInstall.Id, packageToInstall.Version.ToFullString()), true);
            var packagePathResolver = new PackagePathResolver(packetRoot);

            var nuGetFramework = NuGetFramework.ParseFolder(requestedPackage.TargetFramework);

            var packageExtractionContext = new PackageExtractionContext(PackageSaveMode.Defaultv3, XmlDocFileSaveMode.None, ClientPolicyContext.GetClientPolicy(settings, _logger), _logger);

            //Check if the package was previousely installed in this session
            var knownPackage = installedPackages.Where((x) => x.Id == packageToInstall.Id).FirstOrDefault();
            var packageToInstallVersionRange = VersionRange.Parse(packageToInstall.Version.OriginalVersion);
            if (knownPackage == null || !packageToInstallVersionRange.Satisfies(knownPackage.VersionRange.MinVersion))
            {
                PackageReaderBase packageReader;
                //Check if the package is already installed in the file system
                var installedPath = packagePathResolver.GetInstalledPath(packageToInstall);
                if (installedPath == null)
                {
                    var downloadResource = await packageToInstall.Source.GetResourceAsync<DownloadResource>(CancellationToken.None);

                    var download = downloadResource.GetDownloadResourceResultAsync(
                        packageToInstall,
                        new PackageDownloadContext(cacheContext),
                        SettingsUtility.GetGlobalPackagesFolder(settings),
                        _logger, CancellationToken.None);
                    download.Wait();
                    var downloadResult = download.Result;

                    await PackageExtractor.ExtractPackageAsync(
                        downloadResult.PackageSource,
                        downloadResult.PackageStream,
                        packagePathResolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    packageReader = downloadResult.PackageReader;
                }
                else
                {
                    packageReader = new PackageFolderReader(installedPath);
                }

                NugetPackage newlyInstalled = null;
                if (requestedPackage.Id != packageToInstall.Id) //Was not the first requested id, must be a dependency.
                {
                    var nearest = GetNearestFramework(packageReader, nuGetFramework);

                    if (nearest == null)
                    {
                        throw new Exceptions.TargetFrameworkNotFoundException($"The current package {packageToInstall.Id} V{packageToInstall.Version} was installed as dependecy of {requestedPackage}. The parent package framework {nuGetFramework} is unknown/incompatible with the dependency's available frameworks.");
                    }
                    else
                    {
                        var version = packageToInstall.Version;
                        try
                        {   //If available, get the NuSpec version
                            version = packageReader.NuspecReader.GetVersion();
                        }
                        catch (NuGet.Packaging.Core.PackagingException) { }

                        newlyInstalled = new NugetPackage(packageToInstall.Id, version.OriginalVersion,
                                        nearest.Item2.GetShortFolderName(),
                                        packageToInstall.Source.PackageSource.Source,
                                        null, nearest.Item1, requestedPackage.RootPath, requestedPackage.DependenciesForceMinVersion);
                    }
                }
                else
                {
                    NugetPackageType packageType = requestedPackage.PackageType;

                    if (packageType != NugetPackageType.Other)
                    {
                        if (!CheckFrameworkMatch(packageReader, nuGetFramework, ref packageType))
                        {
                            throw new Exceptions.TargetFrameworkNotFoundException($"The current package {packageToInstall.Id} V{packageToInstall.Version} requested framework {nuGetFramework} is unknown/incompatible with the available frameworks.");
                        }
                    }

                    if (requestedPackage.Source.AbsoluteUri != packageToInstall.Source.PackageSource.Source)
                    {
                        //Possible Dependency Confusion attack
                        throw new Exceptions.DependencyConfusionException($"The requested package has been found in {packageToInstall.Source.PackageSource.Source} instead of the required URI {requestedPackage.Source.AbsoluteUri}. Update the pakcage source if this is intended");
                    }
                    newlyInstalled = new NugetPackage(requestedPackage.Id, requestedPackage.VersionRange.OriginalString,
                                    requestedPackage.TargetFramework,
                                    requestedPackage.Source.AbsoluteUri,
                                    null, packageType, requestedPackage.RootPath, requestedPackage.DependenciesForceMinVersion);
                }

                newlyInstalled.AddDependencies(packageToInstall.Dependencies);

                newlyInstalled.LoadLibraries();
                
                installedPackages.Add(newlyInstalled);
            }
        }

        private static bool CheckFrameworkMatch(PackageReaderBase packageReader, NuGetFramework targetFramework, ref NugetPackageType type)
        {
            var frameworkReducer = new FrameworkReducer();
            Dictionary<NugetPackageType, Func<IEnumerable<FrameworkSpecificGroup>>> getter = new Dictionary<NugetPackageType, Func<IEnumerable<FrameworkSpecificGroup>>>
            {
                { NugetPackageType.DotNetImplementationAssembly, () => packageReader.GetItems(NugetPackage.DotNetImplementationAssemblyPath) },
                { NugetPackageType.DotNetCompileTimeAssembly, () => packageReader.GetItems(NugetPackage.DotNetCompileTimeAssemblyPath) },
            };

            foreach (var get in getter)
            {
                var items = get.Value();
                var targetFrameworkString = targetFramework.GetFrameworkString();
                var match = items.Where(x => targetFrameworkString == x.TargetFramework.GetFrameworkString());
                if (match.Count() > 0)
                {
                    type = get.Key;
                    return true;
                }
            }
            return false;
        }

        private static Tuple<NugetPackageType, NuGetFramework> GetNearestFramework(PackageReaderBase packageReader, NuGetFramework targetFramework)
        {
            var frameworkReducer = new FrameworkReducer();
            Dictionary<NugetPackageType, Func<IEnumerable<FrameworkSpecificGroup>>> getter = new Dictionary<NugetPackageType, Func<IEnumerable<FrameworkSpecificGroup>>>
            {
                { NugetPackageType.DotNetImplementationAssembly, () => packageReader.GetItems(NugetPackage.DotNetImplementationAssemblyPath) },
                { NugetPackageType.DotNetCompileTimeAssembly, () => packageReader.GetItems(NugetPackage.DotNetCompileTimeAssemblyPath) },
            };

            NuGetFramework nearest = null;

            foreach (var get in getter)
            {
                var items = get.Value();
                var libFrameworks = items.Select(x => x.TargetFramework);
                nearest = frameworkReducer.GetNearest(targetFramework, libFrameworks);
                if (nearest != null)
                {
                    return new Tuple<NugetPackageType, NuGetFramework>(get.Key, nearest);
                }
            }
            return null;
        }

        private static async Task DownloadPackageActionAsync(NugetPackage requestedPackage, SourcePackageDependencyInfo packageToInstall, ISettings settings, SourceCacheContext cacheContext, string destinationDirectory)
        {
            string packagePath = Path.Combine(destinationDirectory, packageToInstall.ToString());
            if (!File.Exists(packagePath))
            {
                var downloadResource = await packageToInstall.Source.GetResourceAsync<DownloadResource>(CancellationToken.None);
                var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                    packageToInstall,
                    new PackageDownloadContext(cacheContext),
                    SettingsUtility.GetGlobalPackagesFolder(settings),
                    _logger, CancellationToken.None);

                Directory.CreateDirectory(destinationDirectory);
                downloadResult.PackageStream.CopyToFile(packagePath);
            }
        }

        /// <summary>
        /// This method looks for the requestedPackage and downloads it. 
        /// If requested, it also installs the dependencies associated with the requestedPackage
        /// </summary>
        /// <param name="requestedPackage"></param>
        /// <param name="autoInstallDependencis"></param>
        /// <returns></returns>
        static async Task PerformPackageActionAsync(NugetPackage requestedPackage, bool autoInstallDependencis, List<NugetPackage> installedPackages, Func<NugetPackage, SourcePackageDependencyInfo, ISettings, SourceCacheContext, List<NugetPackage>, Task> action)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var packageId = requestedPackage.Id;
            var packageVersion = requestedPackage.VersionRange.MinVersion;
            var nuGetFramework = NuGetFramework.ParseFolder(requestedPackage.TargetFramework);
            var settings = Settings.LoadDefaultSettings(root: requestedPackage.RootPath);
            var providers = Repository.Provider.GetCoreV3();
            var sourceRepositoryProvider = new SourceRepositoryProvider(settings, providers);
            var repositories = new List<SourceRepository>();
            if (requestedPackage.Source != null)
            {
                repositories.Add(Repository.CreateSource(providers, new PackageSource(requestedPackage.Source.ToString())));
            }
            repositories.AddRange(sourceRepositoryProvider.GetRepositories());

            using (var cacheContext = new SourceCacheContext())
            {
                var availablePackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);

                var dependencyWalkLevel = autoInstallDependencis? -1 : 0;
                await GetPackageDependencyInfo(dependencyWalkLevel,
                    new PackageIdentity(packageId, packageVersion),
                    nuGetFramework, cacheContext, _logger, repositories, availablePackages, installedPackages);
                
                var resolverContext = new PackageResolverContext(
                    DependencyBehavior.Lowest,
                    new[] { packageId },
                    Enumerable.Empty<string>(),
                    Enumerable.Empty<PackageReference>(),
                    Enumerable.Empty<PackageIdentity>(),
                    availablePackages,
                    repositories.Select(s => s.PackageSource),
                    _logger);

                IEnumerable<SourcePackageDependencyInfo> packagesToInstall = null;
                if (autoInstallDependencis)
                {
                    packagesToInstall = availablePackages;
                }
                else
                {
                    packagesToInstall = availablePackages.Take(1);
                }

                foreach (var packageToInstall in packagesToInstall)
                {
                    await action(requestedPackage, packageToInstall, settings, cacheContext, installedPackages);
                }
            }
        }

        static async Task GetPackageDependencyInfo(int resolveDependencyLevels, PackageIdentity package,
            NuGetFramework framework,
            SourceCacheContext cacheContext,
            ILogger logger,
            IEnumerable<SourceRepository> repositories,
            ISet<SourcePackageDependencyInfo> availablePackages,
            List<NugetPackage> installedPackages)
        {
            if (availablePackages.Contains(package)) return;
            var packageFound = false;
            foreach (var sourceRepository in repositories)
            {
                SourcePackageDependencyInfo dependencyInfo = null;
                try
                {
                    var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
                    dependencyInfo = await dependencyInfoResource.ResolvePackage(package, framework, cacheContext, logger, CancellationToken.None);
                }
                catch (NuGet.Protocol.Core.Types.FatalProtocolException e)
                {
                    throw new Exception((string.Format("Fatal error while fetching the package {0} from the repository {1}.\nPlease check your internet/VPN connections\nAdditional information: {2}", package.Id, sourceRepository.PackageSource.ToString(), string.Join("\n", new AggregateException(e).InnerExceptions.Select(x => x?.Message)))));
                }
                catch (Exception e)
                {
                    logger.LogError(string.Format("Unable to fetch the package info from the repository {0}.\n\nError: {1}", sourceRepository.PackageSource.SourceUri, string.Join("\n", new AggregateException(e).InnerExceptions.Select(x => x?.Message))));
                    continue;
                }

                if (dependencyInfo == null)
                {
                    logger.LogVerbose($"The package {package.Id} was not found under the repository {sourceRepository.PackageSource.SourceUri}");
                    continue;
                }

                packageFound = true;
                availablePackages.Add(dependencyInfo);
                if (resolveDependencyLevels < 0 || resolveDependencyLevels > 0)
                {
                    resolveDependencyLevels--;
                    foreach (var dependency in dependencyInfo.Dependencies)
                    {
                        var knownPackage = installedPackages.Where((x) => x.Id == dependency.Id).FirstOrDefault();
                        if (knownPackage == null || !dependency.VersionRange.Satisfies(knownPackage.VersionRange.MinVersion))
                        {
                            await GetPackageDependencyInfo(resolveDependencyLevels,
                            new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion),
                            framework, cacheContext, logger, repositories, availablePackages, installedPackages);
                        }
                    }
                }
                break;
            }
            if (!packageFound)
            {
                if (availablePackages.Count() == 0) //The missing package is the parent one
                {
                    throw new Exception(string.Format("Package {0} V{1} not found in any of the provided repositories: {2}. See the log for more error details.", package.Id, package.Version, String.Join(", ", repositories)));
                }
                else
                {
                    var parent = availablePackages.First();
                    throw new Exception(string.Format("Package {0} V{1} dependency of {2} V{3} not found in any of the provided repositories: {4}. See the log for more error details.", package.Id, package.Version, parent.Id, parent.Version, String.Join(", ", repositories)));
                }
            }
        }
    }
}
