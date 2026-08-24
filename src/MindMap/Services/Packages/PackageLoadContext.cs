using System.Reflection;
using System.Runtime.Loader;

namespace MindMap.Services.Packages;

/// <summary>
/// パッケージ 1 つぶんの読み込み先。パッケージが同梱した DLL は
/// <c>.deps.json</c> から解決するので、それぞれが違う版のライブラリを持てる。
/// </summary>
internal sealed class PackageLoadContext : AssemblyLoadContext
{
    /// <summary>
    /// ホストと必ず同じものを使うアセンブリ。
    ///
    /// パッケージのフォルダーにも同じ DLL が置かれる（参照すればコピーされる）ので、
    /// そのまま解決させると 2 つ読み込まれ、同じ名前の別の型になってしまう。
    /// そうなるとファクトリのキャストが必ず失敗する。ここを null で返して
    /// ホスト側のものに寄せるのが要。
    /// </summary>
    private static readonly string[] Shared = ["MindMap.Abstractions"];

    private readonly AssemblyDependencyResolver _resolver;

    public PackageLoadContext(string mainAssemblyPath)
        // 取り外しはしない。WPF が画面から参照を握るため、collectible にしても実際には外れない。
        : base(isCollectible: false) =>
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (Array.Exists(Shared, name => name == assemblyName.Name))
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);

        // null を返すと既定の読み込み先に回る。WPF などフレームワークのぶんはそちらで解決される。
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
