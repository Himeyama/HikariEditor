using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace HikariEditor;

// ドラッグでパネルをリサイズするハンドル。
// WinUI 3 には GridSplitter が無く、ホバー時のカーソル変更には protected な
// ProtectedCursor を設定する必要がある。Border は sealed で継承できないため、
// Background / BorderBrush を持てる非 sealed の ContentControl を継承して実現する。
public partial class ResizeHandle : ContentControl
{
    public ResizeHandle()
    {
        ApplyCursor();
    }

    Orientation _orientation = Orientation.Horizontal;

    // ハンドルバーの向き。Horizontal は横長バー（上下リサイズ）、
    // Vertical は縦長バー（左右リサイズ）を表す。
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            ApplyCursor();
        }
    }

    void ApplyCursor()
    {
        InputSystemCursorShape shape = _orientation == Orientation.Vertical
            ? InputSystemCursorShape.SizeWestEast   // 縦長バー → 左右リサイズ
            : InputSystemCursorShape.SizeNorthSouth; // 横長バー → 上下リサイズ
        ProtectedCursor = InputSystemCursor.Create(shape);
    }
}
