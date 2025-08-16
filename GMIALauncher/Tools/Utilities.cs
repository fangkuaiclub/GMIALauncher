﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace AOULauncher.Tools;

internal static class Utilities {
    
    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] uint dwFlags, [Out] StringBuilder lpExeName, [In, Out] ref uint lpdwSize);

    public static string? GetMainModuleFileName(this Process process, int buffer = 1024) {
        var fileNameBuilder = new StringBuilder(buffer);
        var bufferLength = (uint)fileNameBuilder.Capacity + 1;
        return QueryFullProcessImageName(process.Handle, 0, fileNameBuilder, ref bufferLength) ?
            fileNameBuilder.ToString() : null;
    }

    public static void BackupFolder(string baseDirectory, string folder)
    {
        var source = new DirectoryInfo(Path.Combine(baseDirectory, folder));
        var backup = new DirectoryInfo(Path.Combine(baseDirectory, folder+"_lp_backup"));
        
        if (!source.Exists)
        {
            return;
        }
        
        if (!backup.Exists)
        {
            source.MoveTo(backup.FullName);
        }
        else
        {
            Console.Out.WriteLine("backup folder exists!!");
        }
    }

    public static void RestoreBackupFolder(string baseDirectory, string folder)
    {
        var source = new DirectoryInfo(Path.Combine(baseDirectory, folder));
        var backup = new DirectoryInfo(Path.Combine(baseDirectory, folder+"_lp_backup"));

        if (!backup.Exists)
        {
            return;
        }

        if (source.Exists)
        {
            source.Delete(true);
        }
        
        backup.MoveTo(source.FullName);
    }
    
    
    public static void KillAmongUs()
    {
        foreach (var process in Process.GetProcessesByName("Among Us"))
        {
            process.Kill();
        }
    }
    
    public static async Task<T?> DownloadJson<T>(this HttpClient httpClient, string url, JsonTypeInfo<T> context)
    {
        var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        return JsonSerializer.Deserialize(await reader.ReadToEndAsync(), context);
    }

    public static async Task DownloadFile(this HttpClient httpClient, string name, string directory, string url)
    {
        Directory.CreateDirectory(directory);
        await using var stream = await httpClient.GetStreamAsync(url);
        await using var destination = new FileStream(Path.Combine(directory,name), FileMode.OpenOrCreate);
        destination.SetLength(0);
        await stream.CopyToAsync(destination);
    }
    
    public static async Task<ZipArchive> DownloadZip(this HttpClient httpClient, string name, string directory, ModPackData.ZipData zipData)
    {
        Directory.CreateDirectory(directory);
        
        var file = new FileInfo(Path.Combine(directory, name));

        if (!file.Exists || !FileToHash(file.FullName).Equals(zipData.Hash, StringComparison.OrdinalIgnoreCase))
        {
            await httpClient.DownloadFile(name, directory, zipData.Link);
        }

        return ZipFile.OpenRead(file.FullName);
    }
    
    public static string FileToHash(string path)
    {
        if (!File.Exists(path)) return "";
        
        var array = SHA256.HashData(File.ReadAllBytes(path));
        return string.Concat(array.Select(x => x.ToString("x2")));
    }
    
    public static bool IsPluginUpdated(string path, string hash, out bool present)
    {
        present = false;
        var file = new FileInfo(path);
        
        if (!file.Exists) return false;
        
        present = true;
        
        return FileToHash(path).Equals(hash, StringComparison.OrdinalIgnoreCase);
    }
    
    public static bool IsPluginUpdated(string path, string hash)
    {
        var file = new FileInfo(path);
        
        return file.Exists && FileToHash(path).Equals(hash, StringComparison.OrdinalIgnoreCase);
    }
    
    public static bool IsPluginUpdated(string path, ModPackData.ModInfo modInfo)
    {
        var file = new FileInfo(path);
        
        return file.Exists && FileToHash(path).Equals(modInfo.Hash, StringComparison.OrdinalIgnoreCase);
    }

    public static void CopyDirectory(string sourcePath, string targetPath)
    {
        //Now Create all of the directories
        foreach (var dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
        }

        //Copy all the files & Replaces any files with the same name
        foreach (var newPath in Directory.GetFiles(sourcePath, "*.*",SearchOption.AllDirectories))
        {
            File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
        }
    }
}