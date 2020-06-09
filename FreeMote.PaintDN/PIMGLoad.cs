using FreeMote.Plugins;
using FreeMote.Psb;
using PaintDotNet;
using System;
using System.IO;
using System.Linq;

namespace FreeMote.PaintDN
{
    static class PIMGLoad
    {

        internal static Document Load(Stream Input)
        {
            var PSB = new PSB(Input);
            if (PSB.Type != PsbType.Pimg)
                throw new Exception("Unsuported PSB Type");


            var Width = PSB.Objects["width"].GetInt();
            var Height = PSB.Objects["height"].GetInt();

            var Resources = PSB.CollectResources<ImageMetadata>();

            var Doc = new Document(Width, Height);
            Doc.DpuUnit = MeasurementUnit.Pixel;

            var PSBLayers = ((PsbList)PSB.Objects["layers"]).Cast<PsbDictionary>();

            var LayersNames = PSBLayers.EnumLayerNames();

            //Find Spliter characters that aren't used in any layer name of the current PSB
            var Separators = PIMGFileType.PossibleSpliters
                .Where(y => !LayersNames.Where(x => x.Contains(y)).Any())
                .Select(y => y);

            PIMGFileType.PathSpliter = Separators.First();


            var LayerGroups = PSBLayers.EnumLayerGroup();
            var IMGLayers = PSBLayers.EnumLayerInfo();

            foreach (var IMGLayer in IMGLayers.Reverse())
            {

                var LayerID = IMGLayer["layer_id"].GetInt();
                if (IMGLayer.ContainsKey("same_image"))
                    LayerID = IMGLayer["same_image"].GetInt();

                var LayerBounds = IMGLayer.GetPIMGRectangle();

                var LayerName = IMGLayer.GetFullLayerPath(LayerGroups);
                var LayerOpacity = (byte)IMGLayer["opacity"].GetInt();
                var LayerVisible = IMGLayer["visible"].GetInt() != 0;

                var LayerRes = Resources.FindResourceByID(LayerID);

                using (var Image = LayerRes.ToImage())
                using (var BGImage = Image.GetAbsoluteImage(Width, Height, LayerBounds))
                //The Line above is where we convert the 'relative' layer to a absolute one.
                //And to can export the pimg we must revert that by trimming the empty space of all layers
                //This can works but will be a trouble if we have empty layers, becuase we can't say the
                //position and the size of the empty layer, to avoid this problem, this tool ignore any empty layers.
                //But this cause another problem too, read more in the comment in the SaveT function
                {
                    var LayerSurface = Surface.CopyFromBitmap(BGImage);
                    var Layer = new BitmapLayer(LayerSurface);
                    Layer.Name = LayerName;
                    Layer.Visible = LayerVisible;

                    Doc.Layers.Add(Layer);
                }
            }

            var NewID = PIMGFileType.PSBCache.Count.ToString();
            Doc.Metadata.SetUserValue("PSB", NewID);
            PIMGFileType.PSBCache[NewID] = PSB;

            return Doc;
        }
    }
}
