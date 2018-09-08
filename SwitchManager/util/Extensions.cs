using SwitchManager.nx.library;
using SwitchManager.nx.system;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SwitchManager.util
{
    public static class Extensions
    {
        public static string ToHex(this byte[] buffer)
        {
            return Miscellaneous.BytesToHex(buffer);
        }

        public static string ReadHex(this BinaryReader br, int numBytes)
        {
            return br.ReadBytes(numBytes).ToHex();
        }

        public static string ReadHex64(this BinaryReader br)
        {
            ulong l = br.ReadUInt64();
            return Miscellaneous.LongToHex(l);
        }

        public static string ReadAsciiNullTerminated(this BinaryReader br, int numBytes)
        {
            return br.ReadBytes(numBytes).DecodeAsciiNullTerminated();
        }

        public static string ReadUTF32NullTerminated(this BinaryReader br, int numBytes)
        {
            return br.ReadBytes(numBytes).DecodeUTF32NullTerminated();
        }

        public static string ReadUTF8NullTerminated(this BinaryReader br, int numBytes)
        {
            return br.ReadBytes(numBytes).DecodeUTF8NullTerminated();
        }

        public unsafe static string DecodeAsciiNullTerminated(this byte[] buffer, int index = 0)
        {
            fixed (byte* bytes = &buffer[index])
            {
                return new string((sbyte*)bytes);
            }
        }

        public unsafe static string DecodeUTF32NullTerminated(this byte[] buffer, int index = 0)
        {
            int count = 0;
            while (buffer[count] != 0) count++;

            fixed (byte* bytes = &buffer[index])
            {
                return Encoding.UTF32.GetString(bytes, count);
            }
        }

        public unsafe static string DecodeUTF8NullTerminated(this byte[] buffer, int index = 0)
        {
            int count = 0;

            fixed (byte* bytes = &buffer[index])
            {
                while (bytes[count] != 0) count++;
                return Encoding.UTF8.GetString(bytes, count);
            }
        }

        public static void InvokeOrExecute(this Dispatcher dispatcher, Action action)
        {
            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
            }
        }

        public static IEnumerable<SwitchCollectionItem> GetFavoriteTitles(this IEnumerable<SwitchCollectionItem> list)
        {
            return list.Where(i => i.IsFavorite);
        }

        public static IEnumerable<SwitchCollectionItem> GetDownloadedTitles(this IEnumerable<SwitchCollectionItem> list)
        {
            return list.Where(i => i.IsDownloaded);
        }

        public static IEnumerable<SwitchCollectionItem> GetGames(this IEnumerable<SwitchCollectionItem> list)
        {
            return list.Where(i => i.Title is SwitchGame && i.Title.IsGame);
        }

        public static IEnumerable<SwitchCollectionItem> GetDLC(this IEnumerable<SwitchCollectionItem> list)
        {
            return list.Where(i => i.Title is SwitchDLC && i.Title.IsDLC);
        }
        
        public static IEnumerable<SwitchCollectionItem> GetUpdates(this IEnumerable<SwitchCollectionItem> list)
        {
            return list.GetGames().SelectMany(i => ((SwitchGame)i.Title).Updates).Select(i => new SwitchCollectionItem(i));
        }
        
        public static IEnumerable<SwitchCollectionItem> GetTitlesNotDownloaded(this IEnumerable<SwitchCollectionItem> list)
        {
            return list.Where(i => !i.IsDownloaded);
        }

        public static IEnumerable<SwitchCollectionItem> GetDownloadedGames(this IEnumerable<SwitchCollectionItem> list)
        {
            return list.GetGames().GetDownloadedTitles();
        }

        public static IEnumerable<SwitchCollectionItem> GetGamesNotDownloaded(this IEnumerable<SwitchCollectionItem> list)
        {
            return list.GetGames().GetTitlesNotDownloaded();
        }

        public static IEnumerable<string> NumericSort(this IEnumerable<string> list)
        {
            int maxLen = list.Select(s => s.Length).Max();

            return list.Select(s => new
            {
                OrgStr = s,
                SortStr = Regex.Replace(s, @"(\d+)|(\D+)", m => m.Value.PadLeft(maxLen, char.IsDigit(m.Value[0]) ? ' ' : '\xffff'))
            })
            .OrderBy(x => x.SortStr)
            .Select(x => x.OrgStr);
        }

        public static IEnumerable<FileInfo> NumericSort(this IEnumerable<FileInfo> list)
        {
            int maxLen = list.Select(s => s.Name.Length).Max();

            return list.Select(s => new
            {
                OrgStr = s,
                SortStr = Regex.Replace(s.Name, @"(\d+)|(\D+)", m => m.Value.PadLeft(maxLen, char.IsDigit(m.Value[0]) ? ' ' : '\xffff'))
            })
            .OrderBy(x => x.SortStr)
            .Select(x => x.OrgStr);
        }

        public static long GetSize(this DirectoryInfo d)
        {
            long size = 0;
            // Add file sizes.
            foreach (FileInfo fi in d.EnumerateFiles())
            {
                size += fi.Length;
            }
            // Add subdirectory sizes.
            foreach (DirectoryInfo di in d.EnumerateDirectories())
            {
                size += di == null ? 0 : di.GetSize();
            }
            return size;
        }


    }
}
