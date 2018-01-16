﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FreeMote.Psb;
using Newtonsoft.Json;

namespace FreeMote.PsBuild
{

    /// <summary>
    /// Compile PSB File
    /// </summary>
    public static class PsbCompiler
    {
        /// <summary>
        /// Compile to file
        /// </summary>
        /// <param name="inputPath">Json file path</param>
        /// <param name="outputPath">Output path</param>
        /// <param name="inputResPath">Special resource Json file path</param>
        /// <param name="version">PSB version</param>
        /// <param name="cryptKey">CryptKey, if you need to use it outside FreeMote</param>
        /// <param name="platform">PSB Platform</param>
        public static void CompileToFile(string inputPath, string outputPath, string inputResPath = null, ushort version = 3, uint? cryptKey = null, PsbSpec? platform = null)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                throw new FileNotFoundException("Can not find input json file.");
            }

            if (string.IsNullOrEmpty(inputResPath) || !File.Exists(inputResPath))
            {
                inputResPath = Path.ChangeExtension(inputPath, ".resx.json");
                if (!File.Exists(inputResPath))
                {
                    inputResPath = Path.ChangeExtension(inputPath, ".res.json");
                }
            }

            string resJson = null;
            string baseDir = Path.GetDirectoryName(inputPath);
            if (File.Exists(inputResPath))
            {
                resJson = File.ReadAllText(inputResPath);
                baseDir = Path.GetDirectoryName(inputPath);
            }

            var result = Compile(File.ReadAllText(inputPath), resJson, baseDir, version, cryptKey, platform);

            File.WriteAllBytes(outputPath, result);
        }

        /// <summary>
        /// Compile Json to PSB
        /// </summary>
        /// <param name="inputJson">Json text</param>
        /// <param name="inputResJson">Resource Json text</param>
        /// <param name="baseDir">If resource Json uses relative paths (usually it does), specify the base dir</param>
        /// <param name="version">PSB version</param>
        /// <param name="cryptKey">CryptKey, if you need to use it outside FreeMote</param>
        /// <param name="spec">PSB Platform</param>
        /// <returns></returns>
        public static byte[] Compile(string inputJson, string inputResJson, string baseDir = null, ushort version = 3, uint? cryptKey = null,
            PsbSpec? spec = null)
        {
            //Parse
            PSB psb = Parse(inputJson, version);
            //Link
            if (!string.IsNullOrWhiteSpace(inputResJson))
            {
                psb.Link(inputResJson, baseDir);
            }
            //Build
            psb.Merge();
            if (spec != null && spec != psb.Platform)
            {
                psb.SwitchSpec(spec.Value, spec.Value.DefaultPixelFormat());
                psb.Merge();
            }
            var bytes = psb.Build();
            //Convert
            return cryptKey != null ? PsbFile.EncodeToBytes(cryptKey.Value, bytes, EncodeMode.Encrypt, EncodePosition.Auto) : bytes;
        }

        /// <summary>
        /// Load PSB From Json file
        /// </summary>
        /// <param name="inputPath">Json file path</param>
        /// <param name="inputResPath">Resource Json file</param>
        /// <param name="version">PSB version</param>
        /// <returns></returns>
        public static PSB LoadPsbFromJsonFile(string inputPath, string inputResPath = null, ushort version = 3)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                throw new FileNotFoundException("Can not find input json file.");
            }

            if (string.IsNullOrEmpty(inputResPath) || !File.Exists(inputResPath))
            {
                inputResPath = Path.ChangeExtension(inputPath, ".resx.json");
                if (!File.Exists(inputResPath))
                {
                    inputResPath = Path.ChangeExtension(inputPath, ".res.json");
                }
            }

            string resJson = null;
            string baseDir = Path.GetDirectoryName(inputPath);
            if (File.Exists(inputResPath))
            {
                resJson = File.ReadAllText(inputResPath);
                baseDir = Path.GetDirectoryName(inputPath);
            }

            //Parse
            PSB psb = Parse(File.ReadAllText(inputPath), version);
            //Link
            if (!string.IsNullOrWhiteSpace(resJson))
            {
                psb.Link(resJson, baseDir);
            }
            psb.Merge();
            return psb;
        }

        internal static PSB Parse(string json, ushort version)
        {
            PSB psb = new PSB(version)
            {
                Objects = JsonConvert.DeserializeObject<PsbDictionary>(json, new PsbTypeConverter())
            };
            psb.Type = psb.InferType();
            return psb;
        }

        internal static byte[] LoadImageBytes(string path, PsbCompressType compressType, PsbPixelFormat pixelFormat)
        {
            byte[] data;
            switch (Path.GetExtension(path)?.ToLowerInvariant())
            {
                case ".png":
                case ".bmp":
                    switch (compressType)
                    {
                        case PsbCompressType.RL:
                            data = RL.CompressImageFile(path, pixelFormat);
                            break;
                        case PsbCompressType.Tlg:
                            //TODO: TLG encode
                        default:
                            data = RL.GetPixelBytesFromImageFile(path, pixelFormat);
                            break;
                    }
                    break;
                case ".rl":
                    data = compressType == PsbCompressType.RL ? File.ReadAllBytes(path) : RL.Uncompress(File.ReadAllBytes(path));
                    break;
                case ".raw":
                    data = compressType == PsbCompressType.RL ? RL.Compress(File.ReadAllBytes(path)) : File.ReadAllBytes(path);
                    break;
                case ".tlg": //TODO: tlg encode
                default: //For `.bin`, you have to handle by yourself
                    data = File.ReadAllBytes(path);
                    break;
            }
            return data;
        }

        /// <summary>
        /// Link
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="resJson"></param>
        /// <param name="baseDir"></param>
        internal static void Link(this PSB psb, string resJson, string baseDir = null)
        {
            List<string> resPaths = JsonConvert.DeserializeObject<List<string>>(resJson);
            var resList = psb.CollectResources();
            foreach (var resPath in resPaths)
            {
                var resName = Path.GetFileNameWithoutExtension(resPath);
                var resMd = uint.TryParse(resName, out uint rid)
                    ? resList.FirstOrDefault(r => r.Index == rid)
                    : resList.FirstOrDefault(r =>
                        resName == $"{r.Part}{PsbResCollector.ResourceNameDelimiter}{r.Name}");
                if (resMd == null)
                {
                    Console.WriteLine($"[WARN]{resPath} is not used.");
                    continue;
                }
                var fullPath = Path.Combine(baseDir ?? "", resPath.Replace('/', '\\'));
                byte[] data = LoadImageBytes(fullPath, resMd.Compress/*psb.Platform.CompressType()*/, resMd.PixelFormat);
                resMd.Resource.Data = data;
            }
        }
    }
}
