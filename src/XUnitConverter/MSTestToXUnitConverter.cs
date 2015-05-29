﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace XUnitConverter
{
    public sealed class MSTestToXUnitConverter : TestFrameworkToXUnitConverter
    {
        private static readonly ICollection<string> s_namespaces = new HashSet<string>(StringComparer.Ordinal)
        {
            "N:Microsoft.VisualStudio.TestPlatform.UnitTestFramework",
            "N:Microsoft.Bcl.Testing",
            "N:Microsoft.VisualStudio.TestTools.UnitTesting",
            "N:CoreFXTestLibrary"
        };

        protected override IEnumerable<string> AttributesToRemove
        {
            get
            {
                yield return "ContractsRequiredAttribute";
                yield return "TestClassAttribute";
            }
        }

        protected override string TestMethodName { get; } = "TestMethodAttribute";

        protected override ICollection<string> TestNamespaces { get; } = s_namespaces;
    }
}
