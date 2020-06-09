using FreeMote.Plugins;
using FreeMote.Psb;
using PaintDotNet;
using PaintDotNet.Clipboard;
using PaintDotNet.PropertySystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace FreeMote.PaintDN
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public class PIMGFileType : PropertyBasedFileType, IFileTypeFactory
    {
        internal static string FreeMoteDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        internal readonly static char[] PossibleSpliters = new char[] { '/', '\\', '>', '|', '→', '—' };
        public static char PathSpliter = '/';

        static FileTypeOptions FileTypeOptions = new FileTypeOptions()
        {
            LoadExtensions = new List<string>(new string[] { ".pimg" }),
            SaveExtensions = new List<string>(new string[] { ".pimg" }),
            SupportsLayers = true,
            SupportsCancellation = false
        };

        internal static Dictionary<string, PSB> PSBCache = new Dictionary<string, PSB>();

        public PIMGFileType() : base("PIMG", FileTypeOptions) { }

        static bool Initialized;
        static void Initialize()
        {
            if (Initialized)
                return;

            Initialized = true;

            FreeMount.Init(FreeMoteDir);
        }

        public FileType[] GetFileTypeInstances()
        {
            return new FileType[] { new PIMGFileType() };
        }

        public override PropertyCollection OnCreateSavePropertyCollection()
        {
            List<Property> props = new List<Property>();
            return new PropertyCollection(props);
        }

        protected override Document OnLoad(Stream input)
        {
            Initialize();
            return PIMGLoad.Load(input);
        }

        protected override void OnSaveT(Document Input, Stream Output, PropertyBasedSaveConfigToken Token, Surface ScratchSurface, ProgressEventHandler progressCallback)
        {
            Initialize();
            var Buffer = PIMGSave.Save(Input);
            Output.Write(Buffer, 0, Buffer.Length);
            Output.Flush();
        }
    }
}
