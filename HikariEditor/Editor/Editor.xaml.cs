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
        TabPaths.Remove(args.Tab.Name);
        sender.TabItems.Remove(args.Tab);
        if (TabPaths.Count == 0)
        {
            MainWindow!.editorFrame.Height = 0;
            MainWindow.previewFrame.Height = 0;
        }
    }

    public void AddTab(string fileName, string shortFileName)
    {
        // タブが存在する場合は選択して終わり
        if (TabPaths.Contains(fileName))
        {
            if (Tabs.FindName(fileName) is TabViewItem tab)
                tab.IsSelected = true;
            return;
        }
        if (!File.Exists(fileName)) return;
        TabPaths.Add(fileName);
        EditorUnit frame = new(fileName, this);
        TabViewItem newTab = new()
        {
            IconSource = new SymbolIconSource() { Symbol = Symbol.Document },
            Header = shortFileName,
            Content = frame,
            Name = fileName,
            IsSelected = true
        };
        Tabs.TabItems.Add(newTab);
    }

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
