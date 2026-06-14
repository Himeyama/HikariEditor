using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace HikariEditor;

public sealed partial class Editor : Page
{
    readonly List<string> _tabPaths = [];

    public List<string> TabPaths => _tabPaths;
    public MainWindow? MainWindow { get; set; }
    public int Counter { get; set; }

    public Editor()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        MainWindow = e.Parameter as MainWindow;
        MainWindow!.editor = this;
        base.OnNavigatedTo(e);
    }

    // クリップボードのテキストを選択中タブの Monaco に貼り付ける
    public void CallPasteFunction(string text)
    {
        if (Tabs.SelectedItem is not TabViewItem tab) return;
        (tab.Content as EditorUnit)?.Paste(text);
    }

    // 選択中タブの Monaco に選択範囲のコピーを要求する
    public void CallCopyFunction()
    {
        if (Tabs.SelectedItem is not TabViewItem tab) return;
        (tab.Content as EditorUnit)?.RequestCopy();
    }

    private void TabViewCloseTab(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        CloseTab(args.Tab);
    }

    // 指定したファイルのタブが開いていれば閉じる（エクスプローラーからの削除時に使用）
    public void CloseTabByPath(string fileName)
    {
        if (!TabPaths.Contains(fileName)) return;
        if (Tabs.FindName(fileName) is TabViewItem tab)
            CloseTab(tab);
    }

    private void CloseTab(TabViewItem tab)
    {
        TabPaths.Remove(tab.Name);
        Tabs.TabItems.Remove(tab);
        if (TabPaths.Count == 0)
        {
            MainWindow!.editorFrame.Height = 0;
            MainWindow.previewFrame.Height = 0;
        }
    }

    // 拡張子に応じて開き方を変える。forceKind を渡すと分類を上書きできる
    // （SVG をテキスト／WebView のどちらで開くか、右クリックメニューから選ぶ用途）。
    internal void OpenFile(FileItem file, FileKind? forceKind = null)
    {
        if (!File.Exists(file.Path)) return;

        // 既に開いている場合、通常クリック（forceKind なし）は選択するだけ。
        // 開き方を指定したときは一旦閉じてから開き直し、別モードで表示できるようにする。
        if (TabPaths.Contains(file.Path) && Tabs.FindName(file.Path) is TabViewItem existing)
        {
            if (forceKind is null)
            {
                existing.IsSelected = true;
                return;
            }
            CloseTab(existing);
        }

        FileKind kind = forceKind ?? FileKinds.Classify(file.Path);
        UIElement content = kind switch
        {
            FileKind.Text => new EditorUnit(file.Path, this),
            FileKind.Binary => new HexView(file.Path),
            _ => new MediaUnit(file.Path)   // Image / Video / Pdf / Svg
        };

        TabPaths.Add(file.Path);
        TabViewItem newTab = new()
        {
            IconSource = new SymbolIconSource() { Symbol = IconFor(kind) },
            Header = file.Name,
            Content = content,
            Name = file.Path,
            IsSelected = true
        };
        Tabs.TabItems.Add(newTab);
        MainWindow!.editorFrame.Height = double.NaN;
    }

    static Symbol IconFor(FileKind kind) => kind switch
    {
        FileKind.Image or FileKind.Svg => Symbol.Pictures,
        FileKind.Video => Symbol.Video,
        FileKind.Pdf => Symbol.Page2,
        FileKind.Binary => Symbol.Calculator,
        _ => Symbol.Document
    };

    // Monaco からの保存メッセージを処理する
    public void OnSave(string fileName, string src, string newline)
    {
        FileItem fileItem = new(fileName);
        fileItem.Save(src, newline);
        MainWindow!.StatusBar.Text = $"{fileItem.Name} を保存しました。";
        LogPage.AddLog(MainWindow, $"{fileItem.Name} を保存しました。");
        Counter++;
        DelayResetStatusBar(1000);
        if (fileItem.Extension == ".tex")
        {
            MainWindow.StatusBar.Text = $"{fileItem.Name} を保存しました。TeX のコンパイルを実行しています...";
            LogPage.AddLog(MainWindow, "LaTeX のコンパイルを実行しています...");
            Counter++;
            DelayResetStatusBar(1000);
            _ = LaTeX.Compile(MainWindow, fileItem, this);
        }
    }

    // Monaco からの自動保存メッセージを処理する
    public void OnAutoSave(string fileName, string src, string newline)
    {
        if (!MainWindow!.AutoSave.IsChecked)
            return;
        FileItem fileItem = new(fileName);
        fileItem.Save(src, newline);
        MainWindow.StatusBar.Text = $"{fileItem.Name} を自動保存しました。";
        LogPage.AddLog(MainWindow, $"{fileItem.Name} を自動保存しました。");
        Counter++;
        DelayResetStatusBar(1000);
    }

    // Monaco から届いた選択テキストをクリップボードへ書き込む
    public static void OnCopy(string text)
    {
        DataPackage dataPackage = new();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }

    public async void DelayResetStatusBar(int sec)
    {
        int count = Counter;
        await Task.Delay(TimeSpan.FromMilliseconds(sec));
        if (count >= Counter)
        {
            MainWindow!.StatusBar.Text = "";
        }
    }

    private void EditorTabChange(object sender, SelectionChangedEventArgs e)
    {
        if (((TabView)sender).SelectedItem is not TabViewItem selectedItem) return;
        string extension = Path.GetExtension(selectedItem.Name);
        MainWindow!.rightArea.ColumnDefinitions[1].Width = extension == ".tex"
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        // 選択中タブのファイルの改行コードをステータスバーへ反映する
        if (selectedItem.Content is EditorUnit unit)
            MainWindow.NLBtn.Content = unit.Newline;
    }

    // ステータスバーの LF/CRLF ボタンが切り替えられたとき、選択中タブへ適用して保存する
    public void ApplyNewline(string newline)
    {
        if (Tabs.SelectedItem is not TabViewItem tab) return;
        (tab.Content as EditorUnit)?.SetNewline(newline);
    }
}
