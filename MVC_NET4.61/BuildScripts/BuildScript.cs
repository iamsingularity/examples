﻿using System.Collections.Generic;
using System;
using System.IO;
using System.Xml;
using FlubuCore.Context;
using FlubuCore.Packaging;
using FlubuCore.Packaging.Filters;
using FlubuCore.Scripting;
using FlubuCore.Tasks.Iis;
using Newtonsoft.Json;

//#ref System.Xml.XmlDocument, System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089
//#ass .\packages\Newtonsoft.Json.9.0.1\lib\net45\Newtonsoft.Json.dll
//#imp .\BuildScripts\BuildHelper.cs

/// <summary>
/// Flubu build script example for .net. Flubu Default targets for .net are not included.
/// Most of them are created in this buildScipt tho. (load.solution, generate,commonAssinfo, compile) as a build script example
/// How to test and debug build script example is in NetCore_csproj project. 
/// </summary>
public class BuildScript : DefaultBuildScript
{
    protected override void ConfigureBuildProperties(IBuildPropertiesContext context)
    {
        context.Properties.Set(BuildProps.NUnitConsolePath,
            @"packages\NUnit.ConsoleRunner.3.6.0\tools\nunit3-console.exe");
        context.Properties.Set(BuildProps.ProductId, "FlubuExample");
        context.Properties.Set(BuildProps.ProductName, "FlubuExample");
        context.Properties.Set(BuildProps.SolutionFileName, "FlubuExample.sln");
        context.Properties.Set(BuildProps.BuildConfiguration, "Release");
    }

    protected override void ConfigureTargets(ITaskContext session)
    {
        var loadSolution = session.CreateTarget("load.solution")
            .SetAsHidden()
            .AddTask(x => x.LoadSolutionTask());

        var updateVersion = session.CreateTarget("update.version")
            .DependsOn(loadSolution)
            .SetAsHidden()
            .Do(TargetFetchBuildVersion);

        session.CreateTarget("generate.commonassinfo")
           .SetDescription("Generates common assembly info")
           .DependsOn(updateVersion)
           .TaskExtensions().GenerateCommonAssemblyInfo().BackToTarget();

        var compile = session.CreateTarget("compile")
            .SetDescription("Compiles the solution.")
            .AddTask(x => x.CompileSolutionTask()
                ////ForMember example: run build.exe compile -c=Debug to pass argument to build configuration. If -c is not specified default specifued value is used. 
                /// Rum build.exe compile help for detailed target help with all arguments you can pass through to target.
                /// this is just example and in real scenario is recomended to set BuildConfiguration only through build properties as other tasks might use it.  
                .ForMember(y => y.BuildConfiguration("Release"), "c", "The build configuration solution will be build."))
            .DependsOn("generate.commonassinfo");

        var unitTest = session.CreateTarget("unit.tests")
            .SetDescription("Runs unit tests")
            .DependsOn(loadSolution)
            .AddTask(x => x.NUnitTaskForNunitV3("FlubuExample.Tests"))
            .AddTask(x => x.NUnitTaskForNunitV3("FlubuExample.Tests2"));

        var runExternalProgramExample = session.CreateTarget("run.libz")
            .AddTask(x => x.RunProgramTask(@"packages\LibZ.Tool\1.2.0\tools\libz.exe"));
            //// Pass any arguments...
            //// .WithArguments());

        var package = session.CreateTarget("Package")
            .SetDescription("Packages mvc example for deployment")
            .Do(TargetPackage);
        
       var rebuild = session.CreateTarget("Rebuild")
            .SetDescription("Rebuilds the solution.")
            .SetAsDefault()
            .DependsOn(compile, unitTest, package);

        var refAssemblyExample = session.CreateTarget("Referenced.Assembly.Example").Do(TargetReferenceAssemblyExample);

        ////Run build.exe Rebuild.Server -exampleArg=someValue to pass to argument
        var doAsyncExample = session.CreateTarget("DoAsync.Example")
           .DoAsync((Action<ITaskContextInternal, string>)DoAsyncExample, session.ScriptArgs["exampleArg"])
           .DoAsync((Action<ITaskContextInternal>)DoAsyncExample2);
  
        session.CreateTarget("iis.install").Do(IisInstall);

        session.CreateTarget("Rebuild.Server")
          .SetDescription("Rebuilds the solution with some additional examples.")
          .DependsOn(rebuild, refAssemblyExample, doAsyncExample);
    }

    public static void IisInstall(ITaskContext context)
    {
        context.Tasks().IisTasks().CreateAppPoolTask("SomeAppPoolName")
            .ManagedRuntimeVersion("No Managed Code")
            .Mode(CreateApplicationPoolMode.DoNothingIfExists)
            .Execute(context);

        context.Tasks()
            .IisTasks()
            .CreateWebsiteTask()
            .WebsiteName("SomeWebSiteName")
            .BindingProtocol("Http")
            .Port(2000)
            .PhysicalPath("SomePhysicalPath")
            .ApplicationPoolName("SomeAppPoolName")
            .WebsiteMode(CreateWebApplicationMode.DoNothingIfExists)
            .Execute(context);
    }

    public static void TargetFetchBuildVersion(ITaskContext context)
    {
        var version = context.Tasks().FetchBuildVersionFromFileTask().Execute(context);
        int svnRevisionNumber = 0; //in real scenario you would fetch revision number from subversion.
        int buildNumber = 0; // in real scenario you would fetch build version from build server.
        version = new System.Version(version.Major, version.Minor, buildNumber, svnRevisionNumber);
        context.Properties.Set(BuildProps.BuildVersion, version);
    }
 
    public static void TargetPackage(ITaskContext context)
    {
        FilterCollection installBinFilters = new FilterCollection();
        installBinFilters.Add(new RegexFileFilter(@".*\.xml$"));
        installBinFilters.Add(new RegexFileFilter(@".svn"));

        context.Tasks().PackageTask("builds")
            .AddDirectoryToPackage("FlubuExample", "FlubuExample", false, new RegexFileFilter(@"^.*\.(svc|asax|aspx|config|js|html|ico|bat|cgn)$").NegateFilter())
            .AddDirectoryToPackage("FlubuExample\\Bin", "FlubuExample\\Bin", false, installBinFilters)
            .AddDirectoryToPackage("FlubuExample\\Content", "FlubuExample\\Content", true)
            .AddDirectoryToPackage("FlubuExample\\Images", "FlubuExample\\Images", true)
            .AddDirectoryToPackage("FlubuExample\\Scripts", "FlubuExample\\Scripts", true)
            .AddDirectoryToPackage("FlubuExample\\Views", "FlubuExample\\Views", true)
            .ZipPackage("FlubuExample.zip")
            .Execute(context);
    }

    public void TargetReferenceAssemblyExample(ITaskContext context)
    {
        ////How to get Assembly Qualified name for #ref
        /// typeof(XmlDocument).AssemblyQualifiedName;
        XmlDocument xml = new XmlDocument();
    }

    public void DoAsyncExample(ITaskContext context, string param)
    {
        Console.WriteLine(string.Format("Example {0}", param));
    }

    public void DoAsyncExample2(ITaskContext context)
    {
        var exampleSerialization = JsonConvert.SerializeObject("Example serialization");
        var deserialized = JsonConvert.DeserializeObject<string>(exampleSerialization);
        Console.WriteLine(deserialized);
    
        ExternalMethodExample();
    }

    public void ExternalMethodExample()
    {
        BuildHelper.SomeMethod();
    }
}
