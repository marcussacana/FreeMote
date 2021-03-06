﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using FreeMote.Psb;
using VGAudio.Containers.At9;
using VGAudio.Containers.Wave;

namespace FreeMote.Plugins.Audio
{
    [Export(typeof(IPsbAudioFormatter))]
    [ExportMetadata("Name", "FreeMote.At9")]
    [ExportMetadata("Author", "Ulysses")]
    [ExportMetadata("Comment", "At9 support via VGAudio.")]
    public class At9Formatter : IPsbAudioFormatter
    {
        public const string At9BitRate = "At9BitRate";

        public List<string> Extensions { get; } = new List<string> {".at9"};

        private const string EncoderTool = "at9tool.exe";

        public string ToolPath { get; set; } = null;

        public At9Formatter()
        {
            var toolPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "", "Tools", EncoderTool);
            if (File.Exists(toolPath))
            {
                ToolPath = toolPath;
            }
        }

        public bool CanToArchData(byte[] wave, Dictionary<string, object> context = null)
        {
            if (File.Exists(ToolPath))
            {
                return true;
            }
            return false;
        }

        public bool CanToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            if (archData is Atrac9ArchData)
            {
                return true;
            }

            return false;
        }

        public IArchData ToArchData(byte[] wave, string waveExt, Dictionary<string, object> context = null)
        {
            if (!File.Exists(ToolPath))
            {
                return null;
            }

            var tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, wave);
            var tempOutFile = Path.GetTempFileName();

            byte[] outBytes = null;
            try
            {
                int bitRate = 96;
                if (context != null)
                {
                    if (context.ContainsKey(At9BitRate) && context[At9BitRate] is int br)
                    {
                        bitRate = br;
                    }
                    else
                    {
                        context[At9BitRate] = bitRate;
                    }
                }
                //br 96 for 1 channel, br 192 for 2 channels (need 2ch sample!)
                ProcessStartInfo info = new ProcessStartInfo(ToolPath, $"-e -br {bitRate} \"{tempFile}\" \"{tempOutFile}\"")
                {
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process process = Process.Start(info);
                process?.WaitForExit();

                outBytes = File.ReadAllBytes(tempOutFile);
                File.Delete(tempFile);
                File.Delete(tempOutFile);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            var arch = new Atrac9ArchData
            {
                Data = new PsbResource {Data = outBytes}
            };

            return arch;
        }

        public bool TryGetArchData(PSB psb, PsbDictionary dic, out IArchData data)
        {
            data = null;
            if (psb.Platform == PsbSpec.ps4 || psb.Platform == PsbSpec.vita)
            {
                if (dic.Count == 1 && dic["archData"] is PsbResource res)
                {
                    data = new Atrac9ArchData
                    {
                        Data = res
                    };

                    return true;
                }

                return false;
            }

            return false;
        }

        public byte[] ToWave(IArchData archData, Dictionary<string, object> context = null)
        {
            At9Reader reader = new At9Reader();
            //var format = reader.ReadFormat();
            var data = reader.Read(archData.Data.Data);
            using MemoryStream oms = new MemoryStream();
            WaveWriter writer = new WaveWriter();
            writer.WriteToStream(data, oms, new WaveConfiguration {Codec = WaveCodec.Pcm16Bit}); //only 16Bit supported
            return oms.ToArray();
        }
    }
}