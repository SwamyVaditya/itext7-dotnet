using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using iText.IO;
using iText.Layout;
using NUnitLite;

class TestRunner
{
    public static void Main(String[] args)
    {
        new AutoRun(typeof(TestRunner).GetAssembly()).Execute(args);
    }
}
