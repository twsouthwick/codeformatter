﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Xunit;

namespace XUnitConverter.Tests
{
    public class MSTestToXUnitConverterTests : ConverterTestBase
    {
        protected override XUnitConverter.ConverterBase CreateConverter()
        {
            return new XUnitConverter.MSTestToXUnitConverter();
        }

        [Fact]
        public async Task TestUpdatesUsingStatements()
        {
            var text = @"
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.Composition.UnitTests
{
}
";

            var expected = @"
using System;
using Xunit;

namespace System.Composition.UnitTests
{
}
";
            await Verify(text, expected);
        }

        [Fact(Skip = "This behavior should be moved to a different converter")]
        public async Task TestUpdatesUsingStatementsWithIfDefs()
        {
            var text = @"
using System;
#if NETFX_CORE
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#elif PORTABLE_TESTS
using Microsoft.Bcl.Testing;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;

#endif
namespace System.Composition.UnitTests
{
}
";

            var expected = @"
using System;
using Xunit;

namespace System.Composition.UnitTests
{
}
";
            await Verify(text, expected);
        }

        [Fact]
        public async Task TestRemovesTestClassAttributes()
        {
            var text = @"
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.Composition.UnitTests
{
    [TestClass]
    public class MyTestClass
    {
    }
}
";

            var expected = @"
using System;
using Xunit;

namespace System.Composition.UnitTests
{
    public class MyTestClass
    {
    }
}
";
            await Verify(text, expected);
        }

        [Fact]
        public async Task TestUpdatesTestMethodAttributes()
        {
            var text = @"
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.Composition.UnitTests
{
    public class MyTestClass
    {
        [TestMethod]
        public void MyTestMethod()
        {
        }
    }
}
";

            var expected = @"
using System;
using Xunit;

namespace System.Composition.UnitTests
{
    public class MyTestClass
    {
        [Fact]
        public void MyTestMethod()
        {
        }
    }
}
";
            await Verify(text, expected);
        }

        [Fact]
        public async Task TestUpdatesAsserts()
        {
            var text = @"
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.Composition.UnitTests
{
    public class MyTestClass
    {
        public void MyTestMethod()
        {
            object obj = new object();

            Assert.AreEqual(1, 1);
            Assert.AreNotEqual(1, 2);
            Assert.IsNull(null);
            Assert.IsNotNull(obj);
            Assert.AreSame(obj, obj);
            Assert.AreNotSame(obj, new object());
            Assert.IsTrue(true);
            Assert.IsFalse(false);
            Assert.IsInstanceOfType(string.Empty, typeof(String));
        }
    }
}
";

            var expected = @"
using System;
using Xunit;

namespace System.Composition.UnitTests
{
    public class MyTestClass
    {
        public void MyTestMethod()
        {
            object obj = new object();

            Assert.Equal(1, 1);
            Assert.NotEqual(1, 2);
            Assert.Null(null);
            Assert.NotNull(obj);
            Assert.Same(obj, obj);
            Assert.NotSame(obj, new object());
            Assert.True(true);
            Assert.False(false);
            Assert.IsAssignableFrom(typeof(String), string.Empty);
        }
    }
}
";
            await Verify(text, expected);
        }

        [Fact]
        public async Task TestInnerNamespace()
        {
            var text = @"
namespace System.Composition.UnitTests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MyTestClass
    {
        [TestMethod]
        public void Test()
        {
        }
    }
}
";

            var expected = @"
namespace System.Composition.UnitTests
{
    using System;
    using Xunit;

    public class MyTestClass
    {
        [Fact]
        public void Test()
        {
        }
    }
}
";
            await Verify(text, expected);
        }
    }
}
