﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// ReSharper disable InconsistentNaming

namespace FreeMote
{
    public static class Consts
    {
        /// <summary>
        /// (string)
        /// </summary>
        public const string PsbShellType = "PsbShellType";

        /// <summary>
        /// (uint?)
        /// </summary>
        public const string CryptKey = "CryptKey";

        /// <summary>
        /// (bool)
        /// <para>Fast: 0x9C BestCompression: 0xDA NoCompression/Low: 0x01</para>
        /// </summary>
        public const string PsbZlibFastCompress = "PsbZlibFastCompress";

        /// <summary>
        /// (List) Archive sources
        /// </summary>
        public const string Context_ArchiveSource = "ArchiveSource";
        /// <summary>
        /// (string) MDF Seed (key + filename)
        /// </summary>
        public const string Context_MdfKey = "MdfKey";
        /// <summary>
        /// (int) MDF Key length
        /// </summary>
        public const string Context_MdfKeyLength = "MdfKeyLength";

        /// <summary>
        /// 0x075BCD15
        /// </summary>
        public const uint Key1 = 123456789;
        /// <summary>
        /// 0x159A55E5
        /// </summary>
        public const uint Key2 = 362436069;
        /// <summary>
        /// 0x1F123BB5
        /// </summary>
        public const uint Key3 = 521288629;

        /// <summary>
        /// Perform 16 byte data align or not (when build)
        /// </summary>
        public static bool PsbDataStructureAlign { get; set; } = true;

        public static Encoding PsbEncoding { get; set; } = Encoding.UTF8;

        /// <summary>
        /// Take more memory when loading, but maybe faster
        /// </summary>
        public static bool InMemoryLoading { get; set; } = true;
        /// <summary>
        /// Use more inferences to make loading fast, set to False when something is wrong
        /// </summary>
        public static bool FastMode { get; set; } = true;

        /// <summary>
        /// Use hex numbers in json to keep all float numbers correct
        /// </summary>
        public static bool JsonUseHexNumber { get; set; } = false;

        /// <summary>
        /// Collapse arrays in json
        /// </summary>
        public static bool JsonArrayCollapse { get; set; } = true;

        /// <summary>
        /// Always use double instead of float
        /// </summary>
        public static bool JsonUseDoubleOnly { get; set; } = false;


        public static string ToStringForPsb(this PsbPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PsbPixelFormat.None:
                case PsbPixelFormat.WinRGBA8:
                case PsbPixelFormat.CommonRGBA8:
                    return "RGBA8";
                case PsbPixelFormat.DXT5:
                    return "DXT5";
                case PsbPixelFormat.WinRGBA4444:
                case PsbPixelFormat.CommonRGBA4444:
                    return "RGBA4444";
                default:
                    return pixelFormat.ToString();
                    //throw new ArgumentOutOfRangeException(nameof(pixelFormat), pixelFormat, null);
            }
        }

        /// <summary>
        /// Read a <see cref="uint"/> from <see cref="BinaryReader"/>, and then encode using <see cref="PsbStreamContext"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="br"></param>
        /// <returns></returns>
        public static uint ReadUInt32(this PsbStreamContext context, BinaryReader br)
        {
            return BitConverter.ToUInt32(context.Encode(br.ReadBytes(4)), 0);
        }

        /// <summary>
        /// Read bytes from <see cref="BinaryReader"/>, and then encode using <see cref="PsbStreamContext"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="br"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static byte[] ReadBytes(this PsbStreamContext context, BinaryReader br, int count)
        {
            return context.Encode(br.ReadBytes(count));
        }

        /// <summary>
        /// Read a <see cref="ushort"/> from <see cref="BinaryReader"/>, and then encode using <see cref="PsbStreamContext"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="br"></param>
        /// <returns></returns>
        public static ushort ReadUInt16(this PsbStreamContext context, BinaryReader br)
        {
            return BitConverter.ToUInt16(context.Encode(br.ReadBytes(2)), 0);
        }

        /// <summary>
        /// Encode a value and write using <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="value"></param>
        /// <param name="bw"></param>
        public static void Write(this PsbStreamContext context, uint value, BinaryWriter bw)
        {
            bw.Write(context.Encode(BitConverter.GetBytes(value)));
        }

        /// <summary>
        /// Encode a value and write using <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="value"></param>
        /// <param name="bw"></param>
        public static void Write(this PsbStreamContext context, ushort value, BinaryWriter bw)
        {
            bw.Write(context.Encode(BitConverter.GetBytes(value)));
        }

        public static string ReadStringZeroTrim(this BinaryReader br)
        {
            var pos = br.BaseStream.Position;
            var length = 0;
            while (br.ReadByte() > 0)
            {
                length++;
            }
            br.BaseStream.Position = pos;
            var str = PsbEncoding.GetString(br.ReadBytes(length));
            br.ReadByte(); //skip \0 - fail if end without \0
            return str;
        }

        public static void WriteStringZeroTrim(this BinaryWriter bw, string str)
        {
            //bw.Write(str.ToCharArray());
            bw.Write(PsbEncoding.GetBytes(str));
            bw.Write((byte)0);
        }

        /// <summary>
        /// Big-Endian Write
        /// </summary>
        /// <param name="bw"></param>
        /// <param name="num"></param>
        public static void WriteBE(this BinaryWriter bw, uint num)
        {
            bw.Write(BitConverter.GetBytes(num).Reverse().ToArray());
        }

        public static void Pad(this BinaryWriter bw, int length, byte paddingByte = 0x0)
        {
            if (length <= 0)
            {
                return;
            }

            if (paddingByte == 0x0)
            {
                bw.Write(new byte[length]);
                return;
            }

            for (int i = 0; i < length; i++)
            {
                bw.Write(paddingByte);
            }
        }

        /// <summary>
        /// 查找一个byte数组在另一个byte数组第一次出现位置
        /// </summary>
        /// <param name="array">被查找的数组（大）</param>
        /// <param name="array2">要查找的数组（小）</param>
        /// <returns>找到返回索引，找不到返回-1</returns>
        internal static int FindIndex(byte[] array, byte[] array2)
        {
            int i, j;

            for (i = 0; i < array.Length; i++)
            {
                if (i + array2.Length <= array.Length)
                {
                    for (j = 0; j < array2.Length; j++)
                    {
                        if (array[i + j] != array2[j]) break;
                    }

                    if (j == array2.Length) return i;
                }
                else
                    break;
            }

            return -1;
        }
    }

    //REF: https://stackoverflow.com/a/24987840/4374462
    public static class ListExtras
    {
        //    list: List<T> to resize
        //    size: desired new size
        // element: default value to insert

        public static void Resize<T>(this List<T> list, int size, T element = default(T))
        {
            int count = list.Count;

            if (size < count)
            {
                list.RemoveRange(size, count - size);
            }
            else if (size > count)
            {
                if (size > list.Capacity)   // Optimization
                    list.Capacity = size;

                list.AddRange(Enumerable.Repeat(element, size - count));
            }
        }

        public static void EnsureSize<T>(this List<T> list, int size, T element = default(T))
        {
            if (list.Count < size)
            {
                list.Resize(size, element);
            }
        }
        public static void Set<T>(this List<T> list, int index, T value, T defaultValue = default(T))
        {
            if (list.Count < index + 1)
            {
                list.Resize(index + 1, defaultValue);
            }

            list[index] = value;
        }
    }
}