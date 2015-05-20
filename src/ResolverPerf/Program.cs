using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using System.Threading;
using NuGet.Resolver;
using NuGet.Packaging;
using System.Diagnostics;

namespace ResolverPerf
{
    public class Program
    {
        public void Main(string[] args)
        {
            var target = new PackageIdentity("EntityFramework", NuGetVersion.Parse("7.0.0-beta4"));

            var targets = new PackageIdentity[] { target };

            var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var sources = new SourceRepository[] { repo };

            Stopwatch timer = new Stopwatch();
            timer.Start();
            var packages = ResolverGather.GatherPackageDependencyInfo(targets, Enumerable.Empty<PackageIdentity>(), NuGetFramework.Parse("net452"), sources, sources, repo, CancellationToken.None).Result;
            timer.Stop();

            // Uncomment this to cause a failure
            // packages = new HashSet<SourcePackageDependencyInfo>(PrunePackageTree.RemoveAllVersionsLessThan(packages, new PackageIdentity("Ix-Async", NuGetVersion.Parse("9.0.0"))));

            Console.WriteLine($"Gather: {timer.Elapsed}");

            PackageResolverContext context = new PackageResolverContext(DependencyBehavior.Lowest,
                new string[] { "entityframework" },
                new string[] { "entityframework" },
                Enumerable.Empty<PackageReference>(),
                Enumerable.Empty<PackageIdentity>(),
                packages);

            for (int i = 0; i < 20; i++)
            {
                timer.Restart();

                try
                {
                    PackageResolver resolver = new PackageResolver();
                    var solution = resolver.Resolve(context, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                timer.Stop();
                Console.WriteLine($"{i}: {timer.Elapsed}");
            }
        }
    }
}
