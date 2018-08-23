using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SwitchManager.util
{
    public static class Extensions
    {
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
