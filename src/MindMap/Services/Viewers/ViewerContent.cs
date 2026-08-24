using System.IO;

namespace MindMap.Services.Viewers;

/// <summary>ビューアに渡す、出す対象。いまはリンク先のファイルだけ。</summary>
public sealed record ViewerContent(string FilePath)
{
    /// <summary>小文字の拡張子（"." 付き）。どのビューアで出すかを決めるのに使う。</summary>
    public string Extension { get; } = Path.GetExtension(FilePath).ToLowerInvariant();
}
