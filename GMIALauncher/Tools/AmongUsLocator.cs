﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.Win32;

namespace AOULauncher.Tools;

public static class AmongUsLocator
{
    private const string EosDllRelativePath = "Among Us_Data/Plugins/x86/GfxPluginEGS.dll";

    public static bool Is64Bit(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        return reader.PEHeaders.PEHeader?.Magic == PEMagic.PE32Plus;
    }

    public static IAmongUsLauncher? GetLauncher(string path)
    {
        if (!VerifyAmongUsDirectory(path))
        {
            return null;
        }

        if (File.Exists(Path.GetFullPath(EosDllRelativePath, path)))
        {
            return new EpicLauncher();
        }

        return new NormalLauncher(Path.Combine(path, "Among Us.exe"));
    }
    
    
    // return among us path by checking processes first, then registry
    public static string? FindAmongUs()
    {
        var processes = Process.GetProcessesByName("Among Us");
        if (processes.Length <= 0)
        {
            if (UpdatePathFromRegistry() is { } pathFromRegistry)
            {
                return VerifyAmongUsDirectory(pathFromRegistry) ? pathFromRegistry : null;
            }
            return null;
        }
        
        var path = Path.GetDirectoryName(processes.First().GetMainModuleFileName());
        
        return VerifyAmongUsDirectory(path) ? path : null;
    }

    // Finds among us from registry location
    private static string? UpdatePathFromRegistry()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
        
        var registryEntry = Registry.GetValue(@"HKEY_CLASSES_ROOT\amongus\DefaultIcon", "", null);

        if (registryEntry is not string path)
        {
            return null;
        }
        
        var indexOfExe = path.LastIndexOf("Among Us.exe", StringComparison.OrdinalIgnoreCase);
        return path.Substring(1,Math.Max(indexOfExe - 1,0));

    }

    public static bool VerifyAmongUsDirectory(string? path)
    {
        return path is not null && Directory.Exists(path) && File.Exists(Path.Combine(path, "Among Us.exe"));
    }

    /*public static string ParseVersion(string amongUsPath)
    {
        var globalGameManagersFile = Path.Combine(amongUsPath, "Among Us_Data", "globalgamemanagers");

        if (!File.Exists(globalGameManagersFile))
        {
            return "未知版本";
        }

        try
        {
            var bytes = File.ReadAllBytes(globalGameManagersFile);
            var pattern = Encoding.UTF8.GetBytes("public.app-category.games");
            var index = bytes.IndexOfPattern(pattern) + pattern.Length + 127;

            if (index >= bytes.Length)
            {
                return "未知版本";
            }

            return Encoding.UTF8.GetString(bytes.Skip(index).TakeWhile(x => x != 0).ToArray());
        }
        catch
        {
            return "未知版本";
        }
    }

    private static int IndexOfPattern(this byte[] source, byte[] pattern)
    {
        for (int i = 0; i < source.Length - pattern.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    found = false;
                    break;
                }
            }
            if (found)
            {
                return i;
            }
        }
        return -1;
    }*/
}