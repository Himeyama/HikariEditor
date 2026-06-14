using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;

namespace HikariEditor;

// 画像・動画・PDF・SVG を WebView2 で表示するタブの中身。
// WebView2 はローカルファイルの URI をそのまま開けば組み込みビューアで描画する。
public sealed partial class MediaUnit : UserControl
{
    readonly string _fileName;

    public MediaUnit(string fileName)
    {
        InitializeComponent();
        _fileName = fileName;
        // ビジュアルツリーに追加されてから WebView2 を初期化する
        Loaded += OnLoaded;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (!File.Exists(_fileName)) return;
        // ローカルパスを与えると file:// URI として解釈され、組み込みビューアで開く
        WebView.Source = new Uri(_fileName);
    }
}
