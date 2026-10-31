using System.Windows.Documents;

namespace MindMap.Abstractions.Viewers;

/// <summary>
/// 出している中身を印刷できることを名乗るもの。<see cref="IContentViewer"/> に重ねて実装する。
///
/// 任意にしてあるのは、印刷の仕方が中身によってまるで違うため（長い文書はページに分け、
/// 絵は 1 枚に収める）。実装していないビューアでは、ホストは印刷を出さない。
/// 画面をそのまま写さないのは、見えている範囲しか出ず、スクロールの先が切れるため。
///
/// 組み立てによく使う形は <see cref="PrintLayout"/> にある。
/// </summary>
public interface IPrintableViewer
{
    /// <summary>
    /// いま出している中身を、用紙に合わせて組み直す。UI スレッドで呼ばれる。
    ///
    /// 画面に出している要素は使い回さず、印刷用に作り直すこと（同じ要素は
    /// 2 か所に置けないうえ、画面の幅で折り返されたままになる）。
    /// 印刷できるものが無い（読めなかった、まだ読んでいない）ときは null を返す。
    /// </summary>
    DocumentPaginator? CreatePaginator(PrintPage page);
}
