#addin nuget:?package=Cake.Json&version=11.0.0
#addin nuget:?package=Cake.Http&version=5.1.0
#addin nuget:?package=Newtonsoft.Json&version=13.0.4
#addin nuget:?package=Cake.Yarn&version=2.0.0

string target = Argument("target", "pack");

string root = System.IO.Directory.GetParent(System.IO.Directory.GetCurrentDirectory().ToString()).Parent.FullName;
string src = root + Directory("src");
string nuspect_file = src + File("d3.nuspec");
string d3_submodule_folder = src + Directory("d3");
string d3_dist_folder = d3_submodule_folder + Directory("dist");
string version = string.Empty;
string vVersion = string.Empty;

Setup((context) =>
{
  version = XmlPeek(nuspect_file, "/p:package/p:metadata/p:version/text()", new XmlPeekSettings
  {
    Namespaces = new Dictionary<string, string> { { "p", "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd" } }
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

    CopyFile(d3_submodule_folder + File("API.md"), d3_dist_folder + File("API.md"));
    CopyFile(d3_submodule_folder + File("CHANGES.md"), d3_dist_folder + File("CHANGES.md"));
    CopyFile(d3_submodule_folder + File("LICENSE"), d3_dist_folder + File("LICENSE"));
    CopyFile(d3_submodule_folder + File("README.md"), d3_dist_folder + File("README.md"));
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
      .Where(j => j["tag_name"].ToString().Equals(vVersion))
      .FirstOrDefault();

    if (release is null)
    {
      throw new Exception($"Release not found. Check if {vVersion} already exists.");
    }

    Func<string, string> getGitOutput = arguments =>
    {
      var startInfo = new System.Diagnostics.ProcessStartInfo
      {
        FileName = "git",
        Arguments = arguments,
        WorkingDirectory = root,
        RedirectStandardOutput = true,
        UseShellExecute = false
      };

      using (var process = System.Diagnostics.Process.Start(startInfo))
      {
        if (process is null)
          throw new Exception("Unable to start git.");

        string output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        if (process.ExitCode != 0)
          throw new Exception($"git {arguments} failed with exit code {process.ExitCode}.");

        return output;
      }
    };

    string currentBranch = getGitOutput("rev-parse --abbrev-ref HEAD");
    string currentCommit = getGitOutput("rev-parse HEAD");
    
    var settings = new XmlPokeSettings
    {
      Namespaces = new Dictionary<string, string> { { "p", "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd" } }
    };

    XmlPoke(File(nuspect_file), "p:package/p:metadata/p:repository/@branch", currentBranch, settings);
    XmlPoke(File(nuspect_file), "p:package/p:metadata/p:repository/@commit", currentCommit, settings);

    DotNetPack(nuspect_file, new DotNetPackSettings
    {
      OutputDirectory = d3_dist_folder,
      WorkingDirectory = d3_dist_folder,
    });
  });

Task("push")
  .Description("Pushes nuget package")
  .IsDependentOn("pack")
  .Does(() =>
  {
    NuGetPush(d3_dist_folder + File($"d3.{version}.nupkg"), new NuGetPushSettings
    {
      Source = "https://api.nuget.org/v3/index.json",
      ApiKey = EnvironmentVariable("NUGET_API_KEY")
    });
  });

RunTarget(target);