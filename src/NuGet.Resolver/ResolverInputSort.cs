using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Resolver
{
    public static class ResolverInputSort
    {
        /// <summary>
        /// Order package trees into a flattened list
        /// 
        /// Package Id (Parent count)
        /// Iteration 1: A(0) -> B(1) -> D(2)
        ///              C(0) -> D(2)
        ///             [Select A]
        /// 
        /// Iteration 2: B(0) -> D(2)
        ///              C(0) -> D(2)
        ///             [Select B]
        /// 
        /// Iteration 2: C(0) -> D(1)
        ///             [Select C]
        ///
        /// Result: A, B, C, D
        /// </summary>
        public static List<List<ResolverPackage>> TreeFlatten(List<List<ResolverPackage>> grouped, PackageResolverContext context)
        {
            var sorted = new List<List<ResolverPackage>>();

            var groupIds = grouped.Select(group => group.First().Id).ToList();

            // find all dependencies for each id
            var dependencies = grouped.Select(group => new SortedSet<string>(group.SelectMany(g => g.Dependencies)
                .Where(d => d != null)
                .Select(d => d.Id), StringComparer.OrdinalIgnoreCase))
                .ToList();

            //  track all parents of an id
            var parents = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

            for (int i=0; i < grouped.Count; i++)
            {
                var parentsForId = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int j = 0; j < grouped.Count; j++)
                {
                    if (i != j && dependencies[j].Contains(groupIds[i]))
                    {
                        parentsForId.Add(groupIds[j]);
                    }
                }

                parents.Add(groupIds[i], parentsForId);
            }

            var idsToSort = new List<string>(groupIds);

            // Loop through the package ids taking the best one each time
            // and removing it from the parent list.
            while (idsToSort.Count > 0)
            {
                // 1. Installed, target, then new package
                // 2. Lowest number of parents remaining goes first
                // 3. Highest number of dependencies goes first
                // 4. Fallback to string sort
                var nextId = idsToSort.OrderBy(id => TreeFlattenPriority(id, context))
                    .ThenBy(id => parents[id].Count)
                    .ThenBy(id => parents.Values.Where(parentIds => parentIds.Contains(id)).Count())
                    .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .First();

                // Find the group for the best id
                var nextGroup = grouped.Where(group => StringComparer.OrdinalIgnoreCase.Equals(group.First().Id, nextId)).Single();
                sorted.Add(nextGroup);

                // Remove the id from the parent list now that we have found a place for it
                foreach (var parentIds in parents.Values)
                {
                    parentIds.Remove(nextId);
                }

                // Complete the id
                grouped.Remove(nextGroup);
                idsToSort.Remove(nextId);
            }

            return sorted;
        }

        /// <summary>
        /// Packages occuring first are more likely to get their preferred version, for this 
        /// reason installed packages should go first, then targets.
        /// </summary>
        private static int TreeFlattenPriority(string id, PackageResolverContext context)
        {
            // Targets go in the middle
            // this needs to be checked first since the target may also exist in the installed packages (upgrade)
            if (context.TargetIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                return 1;
            }

            // Installed packages go first
            if (context.PackagesConfig.Select(package => package.PackageIdentity.Id).Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                return 0;
            }

            // New dependencies go last
            return 2;
        }
    }
}
