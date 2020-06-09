using FreeMote.Plugins;
using FreeMote.Psb;
using PaintDotNet;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace FreeMote.PaintDN
{
    static class PIMGSave
    {
        internal static byte[] Save(Document Input)
        {
            PSB OriPSB = null;
            IEnumerable<PsbDictionary> OriLayers = null;
            IEnumerable<PsbDictionary> OriGroups = null;
            IEnumerable<PsbDictionary> OriExtras = null;
            var Ref = Input.Metadata.GetUserValue("PSB");
            if (PIMGFileType.PSBCache.ContainsKey(Ref))
            {
                OriPSB = PIMGFileType.PSBCache[Ref];

                OriLayers = ((PsbList)OriPSB.Objects["layers"]).Cast<PsbDictionary>();
                OriGroups = OriLayers.EnumLayerGroup();
                OriExtras = OriLayers.EnumExtraData();
            }

            var Resources = new Dictionary<int, ImageMetadata>();
            var Groups = Input.Layers.ParseGroups(OriGroups, OriLayers.Count());
            var Layers = new PsbList();

            int ID = Groups.Count() + OriLayers.Count();

            foreach (var InputLayer in Input.Layers.Cast<BitmapLayer>().Reverse())
            {
                using (Bitmap LayerTex = InputLayer.Surface.CreateAliasedBitmap())
                using (Bitmap Texture = LayerTex.TrimBitmap(out Rectangle Bounds))
                {
                    var Group = Groups.FindLayerGroup(InputLayer.Name, out string LayerName);
                    var Layer = new PsbDictionary
                    {
                        ["left"] = new PsbNumber(Bounds.X),
                        ["top"] = new PsbNumber(Bounds.Y),
                        ["height"] = new PsbNumber(Bounds.Height),
                        ["width"] = new PsbNumber(Bounds.Width),

                        ["opacity"] = new PsbNumber(InputLayer.Opacity),
                        ["visible"] = new PsbNumber(InputLayer.Visible ? 1 : 0),

                        ["layer_id"] = new PsbNumber(++ID),
                        ["layer_type"] = new PsbNumber(0),

                        ["name"] = new PsbString(LayerName)
                    };

                    if (Group != null)
                        Layer["group_layer_id"] = Group["layer_id"];

                    var Resource = new ImageMetadata
                    {
                        Compress = PsbCompressType.Tlg,
                        Name = $"{ID}.tlg",
                        Resource = new PsbResource()
                    };
                    Resource.SetData(Texture);

                    int? SameID = null;

                    foreach (var Pair in Resources)
                    {
                        using (Bitmap Tex = Pair.Value.ToImage())
                        {
                            if (Tex.Equals(BMP: Texture))
                            {
                                SameID = Pair.Key;
                                break;
                            }
                        }
                    }

                    if (SameID == null)
                    {
                        Layers.Add(Layer);
                        Resources.Add(ID, Resource);
                        continue;
                    }

                    Layer["same_image"] = new PsbNumber(SameID.Value);
                    Layers.Add(Layer);
                }
            }


            var PSB = new PSB();
            PSB.Type = PsbType.Pimg;
            PSB.Objects = new PsbDictionary();

            foreach (var Resource in Resources)
                PSB.Objects[Resource.Value.Name] = Resource.Value.Resource;


            PSB.Objects["width"] = new PsbNumber(Input.Width);
            PSB.Objects["height"] = new PsbNumber(Input.Height);

            var Entries = new PsbList();
            Entries.AddRange(Layers);


            if (OriPSB != null)
            {
                //The PSB Reader don't give the paint.net any empty layers
                //This code is to copy the 'ignored' layers back to the output file
                foreach (var OriExtra in OriExtras)
                {
                    var Path = OriExtra.GetFullLayerPath(OriGroups);
                    var Group = Groups.FindLayerGroup(Path, out string Name);

                    OriExtra["name"] = new PsbString(Name);
                    if (Group != null)
                        OriExtra["group_layer_id"] = Group["layer_id"];

                    Entries.Add(OriExtra);
                }
                //A proper fix to this is create an static and resizable image pattern
                //to fill the empty layers when reading the PIMG
                //that this plugin can reconize and make transparent when exporting
                //All because the Paint.net layers must 'fill' the primary layer size
            }

            Entries.AddRange(Groups);
            PSB.Objects["layers"] = Entries;

            PSB.Merge();
            return PSB.Build();
        }
    }
}
