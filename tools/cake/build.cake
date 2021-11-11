#addin nuget:?package=Cake.Yarn&version=0.4.8
#addin nuget:?package=Cake.Git&version=1.1.0

string target = Argument("target", "pack");

string root = System.IO.Directory.GetParent(System.IO.Directory.GetCurrentDirectory().ToString()).Parent.FullName;
string src = root + Directory("src");
string nuspect_file = src + File("/d3.nuspec");
string d3_submodule_folder = src + Directory("d3");
string d3_dist_folder = d3_submodule_folder + Directory("dist");
string version = string.Empty;

Setup((context) => {
  version = XmlPeek(nuspect_file, "/p:package/p:metadata/p:version/text()",  new XmlPeekSettings {
    Namespaces = new Dictionary<string, string> {{ "p", "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd" }}
  });

  GitCheckout(d3_submodule_folder, $"v{version}");
  
  Yarn.FromPath(d3_submodule_folder).Install();
});

Task("test")
  .Description("Runs d3 tests")
  .Does(() => 
  {
    Yarn.FromPath(d3_submodule_folder).RunScript("test");
  });

Task("pack")
  .Description("Genrates libs assets and nuget package")
  .IsDependentOn("test")
  .Does(() => 
  {
    EnsureDirectoryExists(d3_dist_folder);
    CleanDirectory(d3_dist_folder);

    Yarn.FromPath(d3_submodule_folder).RunScript("rollup -c");

    CopyFile(d3_submodule_folder + File("/API.md"), d3_dist_folder + File("/API.md"));
    CopyFile(d3_submodule_folder + File("/CHANGES.MD"), d3_dist_folder + File("/CHANGES.MD"));
    CopyFile(d3_submodule_folder + File("/LICENSE"), d3_dist_folder + File("/LICENSE"));
    CopyFile(d3_submodule_folder + File("/README.md"), d3_dist_folder + File("/README.md"));

    var nuGetPackSettings = new NuGetPackSettings {
      Version = version,
      BasePath = d3_dist_folder,
      OutputDirectory = d3_dist_folder,
      NoPackageAnalysis = true,
      ReleaseNotes = new [] { "" },
      Files = new [] {
        new NuSpecContent { Source = "**/*", Target = "content/Scripts/d3"},
        new NuSpecContent { Source = "**/*", Target = "contentFiles/any/any/wwwroot/lib/d3"}
      },
    };

     NuGetPack(nuspect_file, nuGetPackSettings);
  });

RunTarget(target);