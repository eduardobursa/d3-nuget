#addin nuget:?package=Cake.Http&version=1.2.2
#addin nuget:?package=Cake.Json&version=6.0.1
#addin nuget:?package=Newtonsoft.Json&version=13.0.1
#addin nuget:?package=Cake.Yarn&version=0.4.8
#addin nuget:?package=Cake.Git&version=1.1.0

string target = Argument("target", "pack");

string root = System.IO.Directory.GetParent(System.IO.Directory.GetCurrentDirectory().ToString()).Parent.FullName;
string src = root + Directory("src");
string nuspect_file = src + File("/d3.nuspec");
string d3_submodule_folder = src + Directory("d3");
string d3_dist_folder = d3_submodule_folder + Directory("dist");
string version = string.Empty;
string vVersion = string.Empty;

Setup((context) => {
  version = XmlPeek(nuspect_file, "/p:package/p:metadata/p:version/text()",  new XmlPeekSettings {
    Namespaces = new Dictionary<string, string> {{ "p", "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd" }}
  });

  vVersion = $"v{version}";
  
  Yarn.FromPath(d3_submodule_folder).Install();

  EnsureDirectoryExists(d3_dist_folder);
  CleanDirectory(d3_dist_folder);
});

Task("test")
  .Description("Runs tests")
  .Does(() => 
  {
    Yarn.FromPath(d3_submodule_folder).RunScript("test");
  });

Task("bundle")
  .Description("Create nuget package assets")
  .Does(() => 
  {
    Yarn.FromPath(d3_submodule_folder).RunScript("rollup -c");

    CopyFile(d3_submodule_folder + File("/API.md"), d3_dist_folder + File("/API.md"));
    CopyFile(d3_submodule_folder + File("/CHANGES.md"), d3_dist_folder + File("/CHANGES.md"));
    CopyFile(d3_submodule_folder + File("/LICENSE"), d3_dist_folder + File("/LICENSE"));
    CopyFile(d3_submodule_folder + File("/README.md"), d3_dist_folder + File("/README.md"));
  });

Task("pack")
  .Description("Genrates d3 lib assets and nuget package")
  .IsDependentOn("test")
  .IsDependentOn("bundle")
  .Does(() => 
  {
    string responseBody = HttpGet("https://api.github.com/repos/d3/d3/releases");
    var d3_releases = JArray.Parse(responseBody);

    var release = d3_releases
      .Where(jo => jo["tag_name"].ToString().Equals(vVersion))
      .FirstOrDefault();

    if(release is null)
      Warning($"Release not found. Check if {vVersion} already exists.");

    var nuGetPackSettings = new NuGetPackSettings {
      Version = version,
      BasePath = d3_dist_folder,
      OutputDirectory = d3_dist_folder,
      NoPackageAnalysis = true,
      ReleaseNotes = new [] { release?["body"].ToString() },
      Files = new [] {
        new NuSpecContent { Source = "**/*", Target = "content/Scripts/d3"},
        new NuSpecContent { Source = "**/*", Target = "contentFiles/any/any/wwwroot/lib/d3"}
      },
    };

    NuGetPack(nuspect_file, nuGetPackSettings);
  });

Task("push")
  .Description("Pushes nuget package")
  .Does(() => 
  {
    NuGetPush(d3_dist_folder + File($"/d3.{version}.nupkg"), new NuGetPushSettings {
      ApiKey = EnvironmentVariable("NUGET_API_KEY")
    });
  });

RunTarget(target);