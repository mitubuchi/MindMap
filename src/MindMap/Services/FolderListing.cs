using System.IO;
using System.Runtime.InteropServices;

namespace MindMap.Services;

/// <summary>
/// フォルダーの直下にあるファイルとフォルダーを、子ノードにする順に並べて返す。
///
/// 掘るのは 1 階層だけ。サブフォルダーまで辿ると大きなフォルダーで固まるうえ、
/// 一度に何千ものノードが並んでもマップとして読めないため。下の階層が欲しいときは、
/// 作った子フォルダーのノードで同じ操作を繰り返してもらう。
/// View から切り離してあるのは、並び順と除外の規則を単体で確かめられるようにするため。
/// </summary>
public static class FolderListing
{
    /// <summary>
    /// 直下の項目を返す。フォルダーが先、ファイルが後で、それぞれエクスプローラーと
    /// 同じ並び（数字を数として比べる。「2」が「10」より前）にする。
    /// 隠し属性・システム属性の付いたものは除く（desktop.ini や Thumbs.db など、
    /// エクスプローラーの既定でも見えないものがノードとして並ぶのを避ける）。
    /// </summary>
    /// <returns>フォルダーが無い・読めないときは null。中身が空なら空のリスト。</returns>
    public static IReadOnlyList<string>? TryList(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                return null;
            }

            var directory = new DirectoryInfo(folder);

            // 読めない子があっても全体を諦めず、読めるものだけ並べる。
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            };

            var folders = directory.EnumerateDirectories("*", options)
                .Select(d => d.FullName)
                .OrderBy(Path.GetFileName, LogicalComparer.Instance);
            var files = directory.EnumerateFiles("*", options)
                .Select(f => f.FullName)
                .OrderBy(Path.GetFileName, LogicalComparer.Instance);

            return folders.Concat(files).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>エクスプローラーと同じ並び（StrCmpLogicalW）で名前を比べる。</summary>
    private sealed class LogicalComparer : IComparer<string?>
    {
        public static readonly LogicalComparer Instance = new();

        public int Compare(string? x, string? y) => StrCmpLogicalW(x ?? string.Empty, y ?? string.Empty);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string psz1, string psz2);
    }
}
