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

        public static IEnumerable<NugetPackage> InstallPackages(IEnumerable<NugetPackage> packages, bool autoInstallDependencis, Action<string> installedProgress)
        {
            var installed = new List<NugetPackage>();
            foreach (var p in packages)
            {
                if (!Directory.Exists(p.FullPath))
                {
                    InstallPackage(p, autoInstallDependencis, installed);
                }
                else
                {
                    installed.Add(p);
                }
                
                string extraMsg = "";
                if (autoInstallDependencis)
                {
                    extraMsg = " and its dependencies";
                }
                installedProgress?.Invoke($"Installed: {p.Id}, Version: {p.Version}{extraMsg}");
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
                                   var t = PerformPackageActionAsync(requestedPackage, autoInstallDependencis, (package, toInstallPackage, settings, cache, logger) => InstallPackageActionAsync(package, toInstallPackage, settings, cache, logger, installed));
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
                    var t = PerformPackageActionAsync(requestedPackage, false, (package, toInstallPackage, settings, cache, logger) => DownloadPackageActionAsync(package, toInstallPackage, settings, cache, logger, destinationDirectory));
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
        private static async void InstallPackageActionAsync(NugetPackage requestedPackage,
                                                            SourcePackageDependencyInfo packageToInstall,
                                                            ISettings settings,
                                                            SourceCacheContext cacheContext,
                                                            ILogger logger,
                                                            List<NugetPackage> installedPackages)
        {
            var packetRoot = Path.GetFullPath(requestedPackage.RootPath);
            var packagePathResolver = new PackagePathResolver(packetRoot);
            var frameworkReducer = new FrameworkReducer();
            var nuGetFramework = NuGetFramework.ParseFolder(requestedPackage.TargetFramework);

            var packageExtractionContext = new PackageExtractionContext(PackageSaveMode.Defaultv3, XmlDocFileSaveMode.None, ClientPolicyContext.GetClientPolicy(settings, logger), logger);
            
            //Check if the package was previousely installed in this session
            if (installedPackages.Where((x) => x.Id == packageToInstall.Id && x.Version.ToString() == packageToInstall.Version.ToFullString()).FirstOrDefault() == null)
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
                        logger, CancellationToken.None);
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

                var newlyInstalled = requestedPackage;
                if (requestedPackage.Id != packageToInstall.Id) //Was not the first requested id, must be a dependency.
                {
                    var libItems = packageReader.GetLibItems();
                    var nearest = frameworkReducer.GetNearest(nuGetFramework, libItems.Select(x => x.TargetFramework));
                    if (nearest == null)
                    {
                        throw new NullReferenceException(string.Format("The current package {0} was installed as dependecy of {1}. The parent package framework is unknown/invalid for the dependency. Ensure to explicitly install the dependency before the current parent package."));
                    }
                    newlyInstalled = new NugetPackage(packageToInstall.Id, packageToInstall.Version.ToFullString(),
                                        nearest.GetShortFolderName(),
                                        packageToInstall.Source.PackageSource.Source,
                                        null, true, requestedPackage.RootPath);
                }

                newlyInstalled.AddDependencies(packageToInstall.Dependencies);

                installedPackages.Add(newlyInstalled);
            }
        }

        private static async void DownloadPackageActionAsync(NugetPackage requestedPackage, SourcePackageDependencyInfo packageToInstall, ISettings settings, SourceCacheContext cacheContext, ILogger logger, string destinationDirectory)
        {
            string packagePath = Path.Combine(destinationDirectory, packageToInstall.ToString());
            if (!File.Exists(packagePath))
            {
                var downloadResource = await packageToInstall.Source.GetResourceAsync<DownloadResource>(CancellationToken.None);
                var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                    packageToInstall,
                    new PackageDownloadContext(cacheContext),
                    SettingsUtility.GetGlobalPackagesFolder(settings),
                    logger, CancellationToken.None);

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
        static async Task PerformPackageActionAsync(NugetPackage requestedPackage, bool autoInstallDependencis, Action<NugetPackage, SourcePackageDependencyInfo, ISettings, SourceCacheContext, ILogger> action)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            //var logger = NullLogger.Instance;
            var logger = new NugetLogger();
            var packageId = requestedPackage.Id;
            var packageVersion = new NuGetVersion(requestedPackage.Version);
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

                await GetPackageDependencyInfo(-1,
                    new PackageIdentity(packageId, packageVersion),
                    nuGetFramework, cacheContext, logger, repositories, availablePackages);

                var resolverContext = new PackageResolverContext(
                    DependencyBehavior.Lowest,
                    new[] { packageId },
                    Enumerable.Empty<string>(),
                    Enumerable.Empty<PackageReference>(),
                    Enumerable.Empty<PackageIdentity>(),
                    availablePackages,
                    repositories.Select(s => s.PackageSource),
                    logger);

                //var resolver = new PackageResolver();
                //var packagesFound = resolver.Resolve(resolverContext, CancellationToken.None);
                //var packagesToInstall = packagesFound.Select(p => availablePackages.Single(x => PackageIdentityComparer.Default.Equals(x, p)));
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
                    action(requestedPackage, packageToInstall, settings, cacheContext, logger);
                }
            }
        }

        static async Task GetPackageDependencyInfo(int resolveDependencyLevels, PackageIdentity package,
            NuGetFramework framework,
            SourceCacheContext cacheContext,
            ILogger logger,
            IEnumerable<SourceRepository> repositories,
            ISet<SourcePackageDependencyInfo> availablePackages)
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
                catch (Exception e)
                {
                    logger.LogWarning(string.Format("Unable to fetch the package info from the repository {0}.\n\nError: {1}", sourceRepository.PackageSource.ToString(), string.Join("\n", new AggregateException(e).InnerExceptions.Select(x => x?.Message))));
                    continue;
                }

                if (dependencyInfo == null) continue;
                packageFound = true;
                availablePackages.Add(dependencyInfo);
                if (resolveDependencyLevels < 0 || resolveDependencyLevels > 0)
                {
                    resolveDependencyLevels--;
                    foreach (var dependency in dependencyInfo.Dependencies)
                    {
                        await GetPackageDependencyInfo(resolveDependencyLevels,
                            new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion),
                            framework, cacheContext, logger, repositories, availablePackages);
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
