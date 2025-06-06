#nullable enable
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Net;
using System.IO.Compression;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;

namespace CraftSharp.Resource
{
    public static class ResourceDownloader
    {
        private const string VERSION_MANIFEST_URL = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
        private const string RESOURCE_DOWNLOAD_URL = "https://resources.download.minecraft.net";

        private static readonly char SP = Path.DirectorySeparatorChar;

        /// <summary>
        /// Download vanilla resource files, including default translation file 'en_us.json'.
        /// </summary>
        public static IEnumerator DownloadResource(string resVersion, Action<string, string> updateStatus, Action start, Action<bool> complete)
        {
            Debug.Log($"Downloading resource [{resVersion}]");

            start.Invoke();

            yield return null;

            bool succeeded = false;

            var webClient = new WebClient();
            var currentStage = string.Empty;

            webClient.DownloadProgressChanged += (_, args) =>
            {
                updateStatus(currentStage, $" {args.ProgressPercentage}%");
            };

            // Download version manifest
            Task<string>? downloadTask = webClient.DownloadStringTaskAsync(VERSION_MANIFEST_URL);
            currentStage = "resource.info.download_manifest";
            while (!downloadTask.IsCompleted) yield return null;

            if (downloadTask.IsCompletedSuccessfully) // Proceed to resource downloading
            {
                var manifestJson = Json.ParseJson(downloadTask.Result);
                var versionTargets = manifestJson.Properties["versions"].DataArray
                    .Where(x => x.Properties["id"].StringValue.Equals(resVersion));

                var jsonDataArray = versionTargets as Json.JSONData[] ?? versionTargets.ToArray();
                if (jsonDataArray.Length > 0)
                {
                    var versionInfoUri = jsonDataArray.First().Properties["url"].StringValue;
                    downloadTask = webClient.DownloadStringTaskAsync(versionInfoUri);
                    currentStage = "resource.info.get_version_info";
                    while (!downloadTask.IsCompleted) yield return null;

                    if (downloadTask.IsCompletedSuccessfully)
                    {
                        var infoJson = Json.ParseJson(downloadTask.Result);
                        var clientJarInfo = infoJson.Properties["downloads"].Properties["client"];

                        var jarUri = clientJarInfo.Properties["url"].StringValue;
                        // Download jar file
                        var jarDownloadTask = webClient.DownloadDataTaskAsync(jarUri);
                        currentStage = "resource.info.download_jar";
                        while (!jarDownloadTask.IsCompleted) yield return null;
                        if (jarDownloadTask.IsCompletedSuccessfully) // Jar downloaded, unzip it
                        {
                            var targetFolder = PathHelper.GetPackDirectoryNamed($"vanilla-{resVersion}");
                            var zipStream = new MemoryStream(jarDownloadTask.Result);
                            
                            updateStatus("resource.info.extract_asset", string.Empty);

                            Task.Run(() =>
                            {
                                using (var zipFile = new ZipArchive(zipStream, ZipArchiveMode.Read))
                                {
                                    // Extract asset files
                                    foreach (var entry in zipFile.Entries.Where(x => x.FullName.StartsWith("assets")))
                                    {
                                        var entryPath = new FileInfo($"{targetFolder}{SP}{entry.FullName}");
                                        if (!entryPath.Directory.Exists) // Create the folder if not present
                                        {
                                            entryPath.Directory.Create();
                                        }
                                        entry.ExtractToFile(entryPath.FullName);
                                    }
                                
                                    if (zipFile.GetEntry("pack.mcmeta") is not null) // Extract pack.mcmeta
                                    {
                                        zipFile.GetEntry("pack.mcmeta").ExtractToFile($"{targetFolder}{SP}pack.mcmeta");
                                    }
                                    else // Create pack.mcmeta
                                    {
                                        var metaText = "{ \"pack\": { \"description\": \"Meow~\", \"pack_format\": 4 } }";
                                        File.WriteAllText($"{targetFolder}{SP}pack.mcmeta", metaText);
                                    }

                                    Debug.Log("Resources successfully downloaded and extracted.");
                                }
                                
                                succeeded = true;
                            });
                            
                            while (!succeeded) yield return null;
                        }
                        else
                            Debug.LogWarning($"Failed to download client jar: {jarDownloadTask.Exception}");
                    }
                    else
                        Debug.LogWarning($"Failed to download version info from {versionInfoUri}.");
                }
                else
                    Debug.LogWarning($"Version [{resVersion}] is not found in manifest!");
            }
            else
                Debug.LogWarning("Failed to download version manifest.");
            
            // Dispose web client
            webClient.Dispose();

            yield return null;

            complete.Invoke(succeeded);
        }

        /// <summary>
        /// Download vanilla resource files, including default translation file 'en_us.json'.
        /// <br/>
        /// Do note that 'en_us.json' cannot be retrieved this way since it is not present in the
        /// asset list.
        /// </summary>
        public static IEnumerator DownloadLanguageJson(string resVersion, string langCode, Action<string, string> updateStatus, Action start, Action<bool> complete)
        {
            Debug.Log($"Downloading resource [{resVersion}]");

            start.Invoke();

            yield return null;

            bool succeeded = false;

            var webClient = new WebClient();
            var currentStage = string.Empty;
            
            webClient.DownloadProgressChanged += (_, args) =>
            {
                updateStatus(currentStage, $" {args.ProgressPercentage}%");
            };

            // Download version manifest
            Task<string>? downloadTask = webClient.DownloadStringTaskAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json");
            currentStage = "resource.info.download_manifest";
            while (!downloadTask.IsCompleted) yield return null;

            if (downloadTask.IsCompletedSuccessfully) // Proceed to resource downloading
            {
                var manifestJson = Json.ParseJson(downloadTask.Result);
                var versionTargets = manifestJson.Properties["versions"].DataArray.Where(x =>
                        x.Properties["id"].StringValue.Equals(resVersion));

                var jsonDataArray = versionTargets as Json.JSONData[] ?? versionTargets.ToArray();
                if (jsonDataArray.Length > 0)
                {
                    var versionInfoUri = jsonDataArray.First().Properties["url"].StringValue;
                    downloadTask = webClient.DownloadStringTaskAsync(versionInfoUri);
                    currentStage = "resource.info.get_version_info";
                    while (!downloadTask.IsCompleted) yield return null;

                    if (downloadTask.IsCompletedSuccessfully)
                    {
                        var infoJson = Json.ParseJson(downloadTask.Result);
                        var assetIndexUri = infoJson.Properties["assetIndex"].Properties["url"].StringValue;
                        
                        downloadTask = webClient.DownloadStringTaskAsync(assetIndexUri);
                        currentStage = "resource.info.get_asset_index";
                        while (!downloadTask.IsCompleted) yield return null;

                        if (downloadTask.IsCompletedSuccessfully)
                        {
                            Match match = Regex.Match(downloadTask.Result, $"minecraft/lang/{langCode}.json" + @""":\s\{""hash"":\s""([\d\w]{40})""");
                            
                            if (match is { Success: true, Groups: { Count: 2 } })
                            {
                                string hash = match.Groups[1].Value;
                                var langJsonUrl = $"{RESOURCE_DOWNLOAD_URL}/{hash[..2]}/{hash}";
                                
                                downloadTask = webClient.DownloadStringTaskAsync(langJsonUrl);
                                currentStage = "resource.info.download_lang_text";
                                while (!downloadTask.IsCompleted) yield return null;

                                if (downloadTask.IsCompletedSuccessfully)
                                {
                                    var targetPath = PathHelper.GetPackDirectoryNamed($"vanilla-{resVersion}{SP}assets{SP}minecraft{SP}lang{SP}{langCode}.json");
                                    var targetFileInfo = new FileInfo(targetPath);

                                    if (!Directory.Exists(targetFileInfo.DirectoryName)) // Create folder if not present
                                        Directory.CreateDirectory(targetFileInfo.DirectoryName);

                                    File.WriteAllText(targetFileInfo.FullName, downloadTask.Result);
                                    Debug.Log($"Successfully downloaded minecraft/lang/{langCode}.json for {resVersion}");

                                    succeeded = true;
                                }
                                else
                                    Debug.LogWarning($"Failed to download minecraft/lang/{langCode}.json.");
                            }
                            else
                                Debug.LogWarning($"Unable to find language json for {langCode} in asset index.");
                        }
                        else
                            Debug.LogWarning($"Failed to download asset index from {assetIndexUri}.");
                    }
                    else
                        Debug.LogWarning($"Failed to download version info from {versionInfoUri}.");
                }
                else
                    Debug.LogWarning($"Version [{resVersion}] is not found in manifest!");
            }
            else
                Debug.LogWarning("Failed to download version manifest.");
            
            // Dispose web client
            webClient.Dispose();

            yield return null;

            complete.Invoke(succeeded);
        }
    }
}
