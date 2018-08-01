﻿using System;
using System.IO;
using FreeMote.PsBuild;

namespace FreeMote.Tools.PsBuild
{
    class Program
    {
        //Not thread safe
        private static PsbSpec? _platform = null;
        //private static PsbPixelFormat _pixelFormat = PsbPixelFormat.None;
        private static uint? _key = null;
        private static ushort? _version = null;
        private static bool _noRename = false;

        static void Main(string[] args)
        {
            Console.WriteLine("FreeMote PSB Compiler");
            Console.WriteLine("by Ulysses, wdwxy12345@gmail.com");
            //if (TlgConverter.CanSaveTlg)
            //{
            //    Console.WriteLine("[INFO] TLG Plugin Enabled.");
            //}
            Console.WriteLine();

            if (args.Length <= 0 || args[0].ToLowerInvariant() == "/h" || args[0].ToLowerInvariant() == "?")
            {
                PrintHelp();
                return;
            }

            foreach (var s in args)
            {
                if (File.Exists(s))
                {
                    Compile(s);
                }
                else if (s.StartsWith("/v"))
                {
                    if (ushort.TryParse(s.Replace("/v", ""), out var ver))
                    {
                        _version = ver;
                    }
                }
                else if (s.StartsWith("/p"))
                {
                    if (Enum.TryParse(s.Replace("/p", ""), true, out PsbSpec platform))
                    {
                        _platform = platform;
                    }
                }
                //else if (s == "/no-tlg")
                //{
                //    TlgConverter.PreferManaged = true;
                //}
                //else if (s == "/tlg")
                //{
                //    TlgConverter.PreferManaged = false;
                //}
                else if (s == "/no-rename")
                {
                    _noRename = true;
                }
                else if (s == "/rename")
                {
                    _noRename = false;
                }
                //else if (s.StartsWith("/f"))
                //{
                //    if (Enum.TryParse(s.Replace("/f", ""), true, out PsbPixelFormat format))
                //    {
                //        _pixelFormat = format;
                //    }
                //}
                else if (s.StartsWith("/k"))
                {
                    if (uint.TryParse(s.Replace("/k", ""), out var key))
                    {
                        _key = key;
                    }
                    else
                    {
                        _key = null;
                    }
                }
            }

            Console.WriteLine("Done.");
        }

        private static void Compile(string s)
        {
            var name = Path.GetFileNameWithoutExtension(s);
            //var ext = Path.GetExtension(s);
            Console.WriteLine($"Compiling {name} ...");
            try
            {
                var filename = s + (_key == null ? _noRename ? ".psb" : "-pure.psb" : ".psb");
                PsbCompiler.CompileToFile(s, filename, null, _version, _key, _platform, true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Compile {name} failed.\r\n{e}");
            }
            Console.WriteLine($"Compile {name} succeed.");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Usage: .exe [Param] <PSB json path>");
            Console.WriteLine(@"Param:
/v<VerNumber> : Set compile version from [2,4] . Default: 3.
/k<CryptKey> : Set CryptKey. Default: none(Pure PSB). Requirement: uint, dec.
/p<Platform> : Set platform. Default: keep original platform. Support: krkr/win/common/ems.
    Warning: Platform ONLY works with .bmp/.png format textures.
/no-rename : Do not add `pure` in compiled filename.
");
            // /no-tlg : Always use managed TLG decoder (instead of TLG native plugin). Default: Use TLG native plugin when possible.
            Console.WriteLine("Example: PsBuild /v4 /k123456789 /pkrkr emote_sample.psb.json");
        }
    }
}

