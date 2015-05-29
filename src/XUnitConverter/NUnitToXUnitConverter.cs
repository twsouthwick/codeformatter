// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace XUnitConverter
{
    public sealed class NUnitToXUnitConverter : TestFrameworkToXUnitConverter
    {
        private static readonly ICollection<string> s_namespaces = new HashSet<string>(StringComparer.Ordinal)
        {
            "N:NUnit.Framework",
        };

        protected override IEnumerable<string> AttributesToRemove
        {
            get
            {
                yield return "TestFixtureAttribute";
            }
        }

        protected override string TestMethodName { get; } = "TestAttribute";

        protected override ICollection<string> TestNamespaces { get; } = s_namespaces;
    }
}
