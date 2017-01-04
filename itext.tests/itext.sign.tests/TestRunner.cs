using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using iText.IO;
using NUnitLite;

class TestRunner
{
    public static void Main(String[] args) {
        Assembly assembly;
#if !NETSTANDARD1_6
        assembly = typeof(TestRunner).Assembly;
#else
        assembly = typeof(TestRunner).GetTypeInfo().Assembly;
#endif
        new AutoRun(assembly).Execute(args);
    }
}
