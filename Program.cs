using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using CommandLine;

namespace NugetPackageUpdater
{

    public class Options
    {
        [Option('c', "current", Required = false, HelpText = "Current package name (MyPackage.1.0.1.nupkg)")]
        public string CurrentPackageName { get; set; }

        [Option('n', "new", Required = false, HelpText = "New package name (MyPackage.1.0.2.nupkg)")]
        public string NewPackageName { get; set; }
    }

    class Program
    {
        public static Options Options;
        public static string BinReleaseDir = $"{Path.Combine(Environment.CurrentDirectory, "bin\\Release")}";
        public static string ReleasesDir = $"{Path.Combine(Environment.CurrentDirectory, "Releases")}";
        public static string ProjectRoot = $"{Environment.CurrentDirectory}";
        public static string CurrentPackageFullName;
        public static string NewPackageFullName;
        public static string TempPackageExtractDir;
        public static string[] ExcludeExtensions = { "xml", "pdb" };
        public static string LibNetVersion;
        public static string LibNetDir;
        public static string NuspecFile;

        static void Main(string[] args)
        {
            Console.WriteLine(string.Join(" ", args));
            try
            {
                Parser.Default.ParseArguments<Options>(args).WithParsed(o => Options = o);
                

                CurrentPackageFullName = Path.Combine(ProjectRoot, Options.CurrentPackageName);
                NewPackageFullName = Path.Combine(ProjectRoot, Options.NewPackageName);
                TempPackageExtractDir = Path.Combine(ReleasesDir, Options.CurrentPackageName);
                

                if (Directory.Exists(TempPackageExtractDir))
                {
                    Directory.Delete(TempPackageExtractDir, true);
                }

                // Extract current package
                Console.WriteLine($"Extracting {Options.CurrentPackageName} to {TempPackageExtractDir} ...");
                ZipFile.ExtractToDirectory(CurrentPackageFullName, TempPackageExtractDir, Encoding.UTF8);
                LibNetVersion = Directory.EnumerateDirectories(Path.Combine(TempPackageExtractDir, "lib")).FirstOrDefault();
                LibNetVersion = new DirectoryInfo(LibNetVersion).Name;
                Console.WriteLine($"Lib .net version : {LibNetVersion}");
                LibNetDir = Path.Combine(TempPackageExtractDir, "lib", LibNetVersion);

                if (!Directory.Exists(LibNetDir))
                    Directory.CreateDirectory(LibNetDir);
                else
                {
                    Directory.Delete(LibNetDir, true);
                    Directory.CreateDirectory(LibNetDir);
                }

                var releasedFile = Directory.EnumerateFiles(BinReleaseDir,
                        "*",
                        SearchOption.AllDirectories)
                    // ReSharper disable once PossibleNullReferenceException
                    .Where(f => !ExcludeExtensions.Any(exc => Path.GetExtension(f)
                        .Contains(exc)));


                // Copy all files to the temp directory
                releasedFile.ToList().ForEach(f =>
                {
                    var destFile = Path.Combine(LibNetDir, f.Replace(BinReleaseDir, null).Trim('/', '\\'));
                    var destDir = Path.GetDirectoryName(destFile);

                    if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    File.Copy(f, destFile, true);

                });

                
                NuspecFile = Directory.EnumerateFiles(TempPackageExtractDir, "*.nuspec")
                    .FirstOrDefault();

                var newVersion = Regex.Match(Path.GetFileName(NewPackageFullName), @"([0-9].[0-9].[0-9]+)").Groups[1]
                    .Value;

                UpdateNuspec(NuspecFile, newVersion);

                if (File.Exists(NewPackageFullName))
                    File.Delete(NewPackageFullName);

                System.IO.Compression.ZipFile.CreateFromDirectory(TempPackageExtractDir,NewPackageFullName, CompressionLevel.Optimal, false);
                
                if (Directory.Exists(TempPackageExtractDir))
                {
                    Directory.Delete(TempPackageExtractDir, true);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
            }
        }

        public static void UpdateNuspec(string nuspec, string newVersion)
        {
            var doc = new XmlDocument();
            doc.Load(nuspec);

            var versNode = doc["package"]["metadata"]["version"];
            
            if (versNode != null)
            {
                versNode.InnerText = newVersion;
            }



            doc.Save(NuspecFile);
        }
    }
}
