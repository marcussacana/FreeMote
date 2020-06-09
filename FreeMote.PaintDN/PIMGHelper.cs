using FreeMote.Psb;
using PaintDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace FreeMote.PaintDN
{
    static class PIMGHelper
    {
        internal static Bitmap GetAbsoluteImage(this Bitmap Bitmap, Size Background, int ImgX, int ImgY, int ImgWidth, int ImgHeight) =>
            Bitmap.GetAbsoluteImage(Background, new Rectangle(ImgX, ImgY, ImgWidth, ImgHeight));
        internal static Bitmap GetAbsoluteImage(this Bitmap Bitmap, int BGWidth, int BGHeight, int ImgX, int ImgY, int ImgWidth, int ImgHeight) =>
            Bitmap.GetAbsoluteImage(new Size(BGWidth, BGHeight), new Rectangle(ImgX, ImgY, ImgWidth, ImgHeight));
        internal static Bitmap GetAbsoluteImage(this Bitmap Bitmap, int BGWidth, int BGHeight, Rectangle Image) =>
            Bitmap.GetAbsoluteImage(new Size(BGWidth, BGHeight), Image);
        internal static Bitmap GetAbsoluteImage(this Bitmap Bitmap, Size Background, Rectangle Image)
        {
            Bitmap Output = new Bitmap(Background.Width, Background.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(Output))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(Bitmap, Image);
                g.Flush();
                return Output;
            }
        }

        //Stolen from: http://csharpexamples.com/c-fast-bitmap-compare/
        internal static bool Equals(this Bitmap SRC, Bitmap BMP)
        {
            if (SRC == null || BMP == null)
                return false;
            if (Equals((object)SRC, BMP))
                return true;
            if (!SRC.Size.Equals(BMP.Size) || !SRC.PixelFormat.Equals(BMP.PixelFormat))
                return false;

            int bytes = SRC.Width * SRC.Height * (Image.GetPixelFormatSize(SRC.PixelFormat) / 8);

            bool result = true;
            byte[] b1bytes = new byte[bytes];
            byte[] b2bytes = new byte[bytes];

            BitmapData bitmapData1 = SRC.LockBits(new Rectangle(0, 0, SRC.Width, SRC.Height), ImageLockMode.ReadOnly, SRC.PixelFormat);
            BitmapData bitmapData2 = BMP.LockBits(new Rectangle(0, 0, BMP.Width, BMP.Height), ImageLockMode.ReadOnly, BMP.PixelFormat);

            Marshal.Copy(bitmapData1.Scan0, b1bytes, 0, bytes);
            Marshal.Copy(bitmapData2.Scan0, b2bytes, 0, bytes);

            for (int n = 0; n <= bytes - 1; n++)
            {
                if (b1bytes[n] != b2bytes[n])
                {
                    result = false;
                    break;
                }
            }

            SRC.UnlockBits(bitmapData1);
            BMP.UnlockBits(bitmapData2);

            return result;
        }

        //Stolen From: https://stackoverflow.com/a/4821100
        internal static Bitmap TrimBitmap(this Bitmap Source, out Rectangle Region)
        {
            Region = default;
            BitmapData data = null;
            try
            {
                data = Source.LockBits(new Rectangle(0, 0, Source.Width, Source.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                byte[] buffer = new byte[data.Height * data.Stride];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                int xMin = int.MaxValue,
                    xMax = int.MinValue,
                    yMin = int.MaxValue,
                    yMax = int.MinValue;

                bool foundPixel = false;

                // Find xMin
                for (int x = 0; x < data.Width; x++)
                {
                    bool stop = false;
                    for (int y = 0; y < data.Height; y++)
                    {
                        byte alpha = buffer[y * data.Stride + 4 * x + 3];
                        if (alpha != 0)
                        {
                            xMin = x;
                            stop = true;
                            foundPixel = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                // Image is empty...
                if (!foundPixel)
                    return null;

                // Find yMin
                for (int y = 0; y < data.Height; y++)
                {
                    bool stop = false;
                    for (int x = xMin; x < data.Width; x++)
                    {
                        byte alpha = buffer[y * data.Stride + 4 * x + 3];
                        if (alpha != 0)
                        {
                            yMin = y;
                            stop = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                // Find xMax
                for (int x = data.Width - 1; x >= xMin; x--)
                {
                    bool stop = false;
                    for (int y = yMin; y < data.Height; y++)
                    {
                        byte alpha = buffer[y * data.Stride + 4 * x + 3];
                        if (alpha != 0)
                        {
                            xMax = x;
                            stop = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                // Find yMax
                for (int y = data.Height - 1; y >= yMin; y--)
                {
                    bool stop = false;
                    for (int x = xMin; x <= xMax; x++)
                    {
                        byte alpha = buffer[y * data.Stride + 4 * x + 3];
                        if (alpha != 0)
                        {
                            yMax = y;
                            stop = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                Region = Rectangle.FromLTRB(xMin, yMin, xMax, yMax);
            }
            finally
            {
                if (data != null)
                    Source.UnlockBits(data);
            }

            Bitmap dest = new Bitmap(Region.Width, Region.Height);
            Rectangle destRect = new Rectangle(0, 0, Region.Width, Region.Height);
            using (Graphics g = Graphics.FromImage(dest))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(Source, destRect, Region, GraphicsUnit.Pixel);
                g.Flush();
            }
            return dest;
        }

        static char Separator => PIMGFileType.PathSpliter;

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        public static Rectangle GetPIMGRectangle(this PsbDictionary Meta)
        {
            var X = Meta["left"].GetInt();
            var Y = Meta["top"].GetInt();
            var Width = Meta["width"].GetInt();
            var Height = Meta["height"].GetInt();
            return new Rectangle(X, Y, Width, Height);
        }

        public static IEnumerable<PsbDictionary> EnumLayerInfo(this IEnumerable<PsbDictionary> Info)
        {
            return (from x in Info where x["layer_type"].GetInt() == 0 && x["layer_id"].GetInt() != -1 select x);
        }
        public static IEnumerable<PsbDictionary> EnumLayerGroup(this IEnumerable<PsbDictionary> Info)
        {
            return (from x in Info where x["layer_type"].GetInt() == 2 && x["layer_id"].GetInt() != -1 select x);
        }
        public static IEnumerable<PsbDictionary> EnumExtraData(this IEnumerable<PsbDictionary> Info)
        {
            return (from x in Info where
                     (x["layer_type"].GetInt() != 2 && x["layer_type"].GetInt() != 0) ||
                      x["layer_id"].GetInt() == -1
                    select x);
        }

        public static IEnumerable<string> EnumLayerNames(this IEnumerable<PsbDictionary> Layers)
        {
            return (from x in Layers where x.ContainsKey("name") select (string)(PsbString)x["name"]);
        }

        public static ImageMetadata FindResourceByID(this List<ImageMetadata> Resources, int LayerID)
        {
            return (from x in Resources where Path.GetFileNameWithoutExtension(x.Name) == LayerID.ToString() select x).Single();
        }

        public static PsbDictionary FindLayerGroup(this IEnumerable<PsbDictionary> Info, PsbDictionary LayerInfo)
        {
            if (!LayerInfo.ContainsKey("group_layer_id"))
                return null;

            return Info.FindLayerGroup(LayerInfo["group_layer_id"].GetInt());
        }

        public static string GetFullLayerPath(this PsbDictionary Info, IEnumerable<PsbDictionary> Groups)
        {
            var LayerGroup = Groups.FindLayerGroup(Info);
            string Path = (PsbString)Info["name"];
            if (LayerGroup != null)
                Path = LayerGroup.GetFullLayerPath(Groups) + Separator + Path;
            return Path;
        }
        public static PsbDictionary FindLayerGroup(this IEnumerable<PsbDictionary> Info, string FullPath, out string Name)
        {
            Name = FullPath;
            if (!FullPath.Contains(Separator))
                return null;

            Name = FullPath.Split(Separator).Last();

            var Parent = FullPath.Substring(0, FullPath.LastIndexOf(Separator)).Split(Separator).Last();
            var Groups = Info.EnumLayerGroup();
            return (from x in Groups where (PsbString)x["name"] == Parent select x).Single();
        }
        public static PsbDictionary FindLayerGroup(this IEnumerable<PsbDictionary> Info, int GroupID)
        {
            return (from x in Info where x["layer_id"].GetInt() == GroupID select x).Single();
        }

        public static IEnumerable<PsbDictionary> ParseGroups(this IEnumerable<Layer> Layers, IEnumerable<PsbDictionary> OriGroups = null, int BaseID = -1)
        {
            if (OriGroups == null)
                OriGroups = new PsbDictionary[0];

            int Entries = BaseID;
            var Paths = (from x in Layers
                         where x.Name.Contains(Separator)
                         select x.Name.Substring(0, x.Name.LastIndexOf(Separator)));

            Paths = Paths.Concat(from x in OriGroups select (string)(PsbString)x["name"]).Distinct();

            Dictionary<string, int> GroupsMap = new Dictionary<string, int>();
            foreach (var Path in Paths)
            {
                var Parts = Path.Split(Separator).Reverse().ToArray();
                for (int i = 0; i < Parts.Length; i++)
                {
                    var Part = Parts[i];

                    string Parent = null;
                    if (i + 1 < Parts.Length)
                        Parent = Parts[i + 1];

                    if (GroupsMap.ContainsKey(Part))
                        continue;

                    GroupsMap.Add(Part, ++Entries);

                    var Group = new PsbDictionary
                    {
                        ["height"] = new PsbNumber(0),
                        ["width"] = new PsbNumber(0),
                        ["left"] = new PsbNumber(0),
                        ["top"] = new PsbNumber(0),

                        ["opacity"] = new PsbNumber(255),
                        ["visible"] = new PsbNumber(1),

                        ["type"] = new PsbNumber(13),//???

                        ["layer_type"] = new PsbNumber(2),
                        ["layer_id"] = new PsbNumber(Entries),

                        ["name"] = new PsbString(Part)
                    };

                    if (Parent != null)
                    {
                        int ParentID = GroupsMap.ContainsKey(Parent) ? GroupsMap[Parent] : Entries + 1;
                        Group["group_layer_id"] = new PsbNumber(ParentID);
                    }

                    yield return Group;
                }
            }
        }
    }
}
