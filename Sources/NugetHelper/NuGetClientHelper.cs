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

namespace NuGetClientHelper
{
    public static class NuGetClientHelper
    {
        public class NuGetLogger : ILogger
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

        private static ILogger _logger = new NuGetLogger();

        private static void ThrowException<T>(NuGetPackage p, IEnumerable<NuGetPackage> packages, string message) where T : Exception
        {
            var newPackages = new List<NuGetPackage>();
            newPackages.Add(p);
            newPackages.AddRange(packages);
            ThrowException<T>(newPackages, message);
        }

        private static void ThrowException<T>(IEnumerable<NuGetPackage> packages, string message) where T : Exception
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

        public static void CheckPackagesConsistency(IReadOnlyList<NuGetPackage> packages, bool forceMinMatch = false, bool ignoreDependencies = false)
        {
            foreach (var p in packages)
            {
                var filtered = packages.Where(x => x.Identity.Id == p.Identity.Id && x.Identity.MinVersion != p.Identity.MinVersion);

                if (filtered.Count() != 0)
                {
                    ThrowException<Exceptions.MultiplePackagesFoundException>(p, filtered, $"The packet with id {p.Identity.Id} is present in multiple version.");
                }

                if (!ignoreDependencies)
                {
                    foreach (var d in p.Dependencies)
                    {
                        var dependecyFoundInList = packages.Where(x => x.Identity.Id == d.PackageDependency.Id);
                        if (dependecyFoundInList.Count() == 0)
                        {
                            throw new Exceptions.DependencyNotFoundException(d, p);
                        }
                        else if (dependecyFoundInList.Count() > 1)
                        {
                            ThrowException<Exceptions.MultipleDependenciesFoundException>(dependecyFoundInList, $"The dependency {d} of the packet with id {p.Identity.Id} is present multiple times.");
                        }
                        else
                        {
                            var dependencyCandidate = dependecyFoundInList.Single();

                            if (!d.PackageDependency.VersionRange.Satisfies(dependencyCandidate.Identity.VersionRange.MinVersion))
                            {
                                ThrowException<Exceptions.InvalidDependencyFoundException>(dependecyFoundInList, $"The dependency {d} of the packet with id {p.Identity.Id} is not present in a supported version : {d.PackageDependency.VersionRange.PrettyPrint()} vs {dependencyCandidate.Identity.VersionRange.PrettyPrint()}.");
                            }
                            else if (forceMinMatch && d.ForceMinVersion)
                            {
                                var minVersionString = d.PackageDependency.VersionRange.ToNonSnapshotRange().MinVersion.ToString();

                                if (
                                    minVersionString.Replace(dependencyCandidate.Identity.MinVersion, "").Replace(".", "").Where(x => x != '0').Any() &&
                                    dependencyCandidate.Identity.MinVersion.Replace(minVersionString, "").Replace(".", "").Where(x => x != '0').Any()
                                   )
                                {
                                    ThrowException<Exceptions.InvalidMinVersionDependencyFoundException>(dependecyFoundInList, $"The dependency {d} of the packet with id {p.Identity.Id} would satisfy the needs, but forceMinMatch is set to true : {d.PackageDependency.VersionRange.PrettyPrint()} vs {dependencyCandidate.Identity.VersionRange.PrettyPrint()}.");
                                }
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<NuGetPackage> InstallPackages(IReadOnlyList<NuGetPackageInfo> packages, bool autoInstallDependencis, Action<string> installedProgress, string requestedFramework)
        {
            return InstallPackages(packages, autoInstallDependencis, installedProgress, NuGetFramework.ParseFolder(requestedFramework));
        }

        public static IEnumerable<NuGetPackage> InstallPackages(IReadOnlyList<NuGetPackageInfo> packages, bool autoInstallDependencis, Action<string> installedProgress, NuGetFramework requestedFramework)
        {
            //var frameworkCandidates = packages.Select(x => NuGetFramework.ParseFolder(x.TargetFramework));
            //requestedFramework = new FrameworkReducer().ReduceUpwards(frameworkCandidates).FirstOrDefault();
            //if (requestedFramework == null)
            //{
            //    throw new ArgumentOutOfRangeException(nameof(packages), $"It has not been possible to find a common framework among the one specified in the package list. Please provide the required value in the parameter {nameof(requestedFramework)}");
            //}

            var installed = new List<NuGetPackage>();
            foreach (var p in packages)
            {
                //Always go through the installation method, to gather the dependency information
                InstallPackage(p, requestedFramework, autoInstallDependencis, installed);

                string extraMsg = "";
                if (autoInstallDependencis)
                {
                    extraMsg = " and its dependencies";
                }
                installedProgress?.Invoke($"Installed: {p.Identity}{extraMsg}");
            }
            return installed;
        }

        private static void InstallPackage(NuGetPackageInfo requestedPackage, NuGetFramework requestedFramework, bool autoInstallDependencis, List<NuGetPackage> installed)
        {
            Exception threadEx = null;

            var th = new Thread(() =>
                           {
                               try
                               {
                                   var t = PerformPackageActionAsync(requestedPackage, requestedFramework, autoInstallDependencis, installed, (package, framework, toInstallPackage, settings, cache, installedPackagesList) => InstallPackageActionAsync(package, framework, toInstallPackage, settings, cache, installedPackagesList));
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
                throw new Exceptions.PackageInstallationException(requestedPackage, threadEx);
            }
        }

        /// <summary>
        /// This method looks for the requestedPackage and downloads it. 
        /// No installation nor dependency check is performed.
        /// If the package already exists, it will be not overwritten.
        /// </summary>
        /// <param name="requestedPackage"></param>
        /// <param name="destinationDirectory"></param>
        public static void DownloadPackage(NuGetPackageInfo requestedPackage, string destinationDirectory)
        {
            Exception threadEx = null;

            var th = new Thread(() =>
            {
                try
                {
                    var t = PerformPackageActionAsync(requestedPackage, null, false, null, (package, framework, toInstallPackage, settings, cache, installedPackagesList) => DownloadPackageActionAsync(package, toInstallPackage, settings, cache, destinationDirectory));
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
        private static async Task InstallPackageActionAsync(NuGetPackageInfo requestedPackage,
                                                            NuGetFramework requestedFramework,
                                                            SourcePackageDependencyInfo packageToInstall,
                                                            ISettings settings,
                                                            SourceCacheContext cacheContext,
                                                            List<NuGetPackage> installedPackages)
        {
            var packetRoot = Path.GetFullPath(requestedPackage.RootPath);
            //var packagePathResolver = new PackagePathResolver(Path.Combine(packetRoot, packageToInstall.Id, packageToInstall.Version.ToFullString()), true);
            var packagePathResolver = new PackagePathResolver(packetRoot);

            var packageExtractionContext = new PackageExtractionContext(PackageSaveMode.Defaultv3, XmlDocFileSaveMode.None, ClientPolicyContext.GetClientPolicy(settings, _logger), _logger);

            //Check if the package was previousely installed in this session
            var knownPackage = installedPackages.Where((x) => x.Identity.Id == packageToInstall.Id).FirstOrDefault();
            var packageToInstallVersionRange = VersionRange.Parse(packageToInstall.Version.OriginalVersion);
            if (knownPackage == null || !packageToInstallVersionRange.Satisfies(knownPackage.Identity.VersionRange.MinVersion))
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

                string targetFramework = null;
                NuGetPackageInfo packageInfo;
                var packageType = NuGetDotNetPackageType.NonStandardDotNetPackage;

                if (requestedPackage.Identity.Id != packageToInstall.Id) //Was not the first requested id, must be a dependency.
                {
                    var nearest = GetNearestFramework(packageReader, requestedFramework);

                    if (nearest == null)
                    {
                        throw new Exceptions.TargetFrameworkNotFoundException($"The current package {packageToInstall.Id} V{packageToInstall.Version} was installed as dependecy of {requestedPackage}. The parent package framework {requestedFramework} is unknown/incompatible with the dependency's available frameworks.");
                    }
                    else
                    {
                        targetFramework = nearest.Value.Framework.GetShortFolderName();
                        var version = packageToInstall.Version;
                        try
                        {   //If available, get the NuSpec version
                            version = packageReader.NuspecReader.GetVersion();
                        }
                        catch (NuGet.Packaging.Core.PackagingException) { }
                        packageType = nearest.Value.Type;
                        packageInfo = new NuGetPackageInfo(packageToInstall.Id, version.OriginalVersion, 
                                                            packageToInstall.Source.PackageSource.Source,
                                                            requestedPackage.PackageType, 
                                                            requestedPackage.RootPath, 
                                                            requestedPackage.DependenciesForceMinVersion);
                    }
                }
                else
                {
                    if (requestedPackage.PackageType == NuGetPackageType.DotNet)
                    {
                        var nearest = GetNearestFramework(packageReader, requestedFramework);
                        if(nearest == null)
                        {
                            throw new Exceptions.TargetFrameworkNotFoundException($"The current package {packageToInstall.Id} V{packageToInstall.Version} requested framework {requestedFramework} is unknown/incompatible with the available frameworks.");
                        }
                        packageType = nearest.Value.Type;
                        targetFramework = nearest.Value.Framework.GetShortFolderName();
                    }

                    if (requestedPackage.Source.AbsoluteUri != packageToInstall.Source.PackageSource.Source)
                    {
                        //Possible Dependency Confusion attack
                        throw new Exceptions.DependencyConfusionException($"The requested package has been found in {packageToInstall.Source.PackageSource.Source} instead of the required URI {requestedPackage.Source.AbsoluteUri}. Update the package source if this is intended");
                    }
                    packageInfo = new NuGetPackageInfo(requestedPackage.Identity.Id, requestedPackage.Identity.VersionRange.OriginalString,
                                                       requestedPackage.Source.AbsoluteUri, requestedPackage.DependencySources?.Select(x => x.AbsoluteUri).ToArray(),
                                                       requestedPackage.PackageType, 
                                                       requestedPackage.RootPath, 
                                                       requestedPackage.DependenciesForceMinVersion, 
                                                       requestedPackage.CustomContentPath);
                }
                var newlyInstalled = new NuGetPackage(packageInfo, packageType, targetFramework);

                newlyInstalled.AddDependencies(packageToInstall.Dependencies);
                installedPackages.Add(newlyInstalled);
            }
        }

        [Obsolete]
        private static bool CheckFrameworkMatch(PackageReaderBase packageReader, NuGetFramework targetFramework, ref NuGetDotNetPackageType type)
        {
            var frameworkReducer = new FrameworkReducer();
            Dictionary<NuGetDotNetPackageType, Func<IEnumerable<FrameworkSpecificGroup>>> getter = new Dictionary<NuGetDotNetPackageType, Func<IEnumerable<FrameworkSpecificGroup>>>
            {
                { NuGetDotNetPackageType.DotNetImplementationAssembly, () => packageReader.GetItems(NuGetPackage.DotNetImplementationAssemblyPath) },
                { NuGetDotNetPackageType.DotNetCompileTimeAssembly, () => packageReader.GetItems(NuGetPackage.DotNetCompileTimeAssemblyPath) },
            };

            foreach (var get in getter)
            {
                var items = get.Value();
                var targetFrameworkString = targetFramework.GetFrameworkString();
                var match = items.Where(x => targetFrameworkString == x.TargetFramework.GetFrameworkString());
                if (match.Any())
                {
                    type = get.Key;
                    return true;
                }
            }
            return false;
        }

        private static (NuGetDotNetPackageType Type, NuGetFramework Framework)? GetNearestFramework(PackageReaderBase packageReader, NuGetFramework targetFramework)
        {
            var frameworkReducer = new FrameworkReducer();
            Dictionary<NuGetDotNetPackageType, Func<IEnumerable<FrameworkSpecificGroup>>> getter = new Dictionary<NuGetDotNetPackageType, Func<IEnumerable<FrameworkSpecificGroup>>>
            {
                { NuGetDotNetPackageType.DotNetImplementationAssembly, () => packageReader.GetItems(NuGetPackage.DotNetImplementationAssemblyPath) },
                { NuGetDotNetPackageType.DotNetCompileTimeAssembly, () => packageReader.GetItems(NuGetPackage.DotNetCompileTimeAssemblyPath) },
            };

            NuGetFramework nearest = null;
            bool anyItemFound = false;
            foreach (var get in getter)
            {
                var items = get.Value();
                if (items.Any())
                {
                    anyItemFound = true;
                    var libFrameworks = items.Select(x => x.TargetFramework);
                    if (libFrameworks.Any())
                    {
                        nearest = frameworkReducer.GetNearest(targetFramework, libFrameworks);
                    }
                    else
                    {
                        nearest = NuGetFramework.AnyFramework;
                    }

                    if (nearest != null)
                    {
                        return (get.Key, nearest);
                    }
                }
            }
            if (!anyItemFound) //The package is empty, probably it's a "dependency collector", treat it as Any
            {
                return (NuGetDotNetPackageType.DotNetImplementationAssembly, NuGetFramework.AnyFramework);
            }
            return null;
        }

        private static async Task DownloadPackageActionAsync(NuGetPackageInfo requestedPackage, SourcePackageDependencyInfo packageToInstall, ISettings settings, SourceCacheContext cacheContext, string destinationDirectory)
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
        static async Task PerformPackageActionAsync(NuGetPackageInfo requestedPackage, NuGetFramework requestedFramework, bool autoInstallDependencis, List<NuGetPackage> installedPackages, Func<NuGetPackageInfo, NuGetFramework, SourcePackageDependencyInfo, ISettings, SourceCacheContext, List<NuGetPackage>, Task> action)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var packageId = requestedPackage.Identity.Id;
            var packageVersion = requestedPackage.Identity.VersionRange.MinVersion;
            var settings = Settings.LoadDefaultSettings(root: requestedPackage.RootPath);
            var providers = Repository.Provider.GetCoreV3();
            var sourceRepositoryProvider = new SourceRepositoryProvider(new PackageSourceProvider(settings), providers);
            
            var repositories = new List<SourceRepository>();
            if (requestedPackage.Source != null)
            {
                repositories.Add(Repository.CreateSource(providers, new PackageSource(requestedPackage.Source.ToString())));
            }
            foreach (var depSource in requestedPackage.DependencySources)
            {
                repositories.Add(Repository.CreateSource(providers, new PackageSource(depSource.ToString())));
            }

            //Do not add all repositories from the source provider.
            //Each package should be found in the provided "Source" or "DependencySource" properties.
            //Alternatively, a different ISettings object could be used (instead of the default machine-wide one
            //repositories.AddRange(sourceRepositoryProvider.GetRepositories());
            //Add nuget.org V3 as default for backwards compatibility
            repositories.Add(Repository.CreateSource(providers, new PackageSource("https://api.nuget.org/v3/index.json")));

            using (var cacheContext = new SourceCacheContext())
            {
                var availablePackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);

                var dependencyWalkLevel = autoInstallDependencis? -1 : 0;
                await GetPackageDependencyInfo(dependencyWalkLevel,
                    new PackageIdentity(packageId, packageVersion),
                    requestedFramework, cacheContext, _logger, repositories, availablePackages, installedPackages);
                
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
                    await action(requestedPackage, requestedFramework, packageToInstall, settings, cacheContext, installedPackages);
                }
            }
        }

        static async Task GetPackageDependencyInfo(int resolveDependencyLevels, PackageIdentity package,
            NuGetFramework framework,
            SourceCacheContext cacheContext,
            ILogger logger,
            IEnumerable<SourceRepository> repositories,
            ISet<SourcePackageDependencyInfo> availablePackages,
            List<NuGetPackage> installedPackages)
        {
            if (availablePackages.Contains(package))
            {
                return;
            }

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
                        var knownPackageIdentification = 
                            installedPackages.Where((x) => x.Identity.Id == dependency.Id)
                                             .Select(x => x.Identity).FirstOrDefault()
                            ?? availablePackages.Where((x) => x.Id == dependency.Id)
                                                .Select(x => new NuGetPackageIdentity(x.Id, x.Version.ToString())).FirstOrDefault();

                        if (knownPackageIdentification == null || !dependency.VersionRange.Satisfies(knownPackageIdentification.VersionRange.MinVersion))
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
                    throw new Exceptions.PackageNotFoundException(package, repositories);
                }
                else
                {
                    throw new Exceptions.DependencyNotFoundException(package, availablePackages.First(), repositories);
                }
            }
        }
    }
}
