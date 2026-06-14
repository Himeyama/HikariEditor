using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace HikariEditor
{
    public sealed partial class Editor : Page
    {
        readonly List<string> tabs = new() { };
        MainWindow? mainWindow;
        private int counter = 0;

        public List<string> Tabs1 => tabs;

        public MainWindow? MainWindow { get => mainWindow; set => mainWindow = value; }
        public int Counter { get => counter; set => counter = value; }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            MainWindow = e.Parameter as MainWindow;
            MainWindow!.editor = this;
            base.OnNavigatedTo(e);
        }

        public Editor()
        {
            InitializeComponent();
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
            Tabs1.Remove(args.Tab.Name);
            sender.TabItems.Remove(args.Tab);
            if (Tabs1.Count == 0)
            {
                MainWindow!.editorFrame.Height = 0;
                MainWindow.previewFrame.Height = 0;
            }
        }

        public void AddTab(string fileName, string shortFileName)
        {
            // タブが存在する場合
            if (Tabs1.Contains(fileName))
            {
                TabViewItem tab = (TabViewItem)Tabs.FindName(fileName);
                if (tab != null)
                    tab.IsSelected = true;
                return;
            }
            TabListAdd(fileName);
            if (!File.Exists(fileName)) return;
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

        void TabListAdd(string fileName)
        {
            if (Tabs1.Contains(fileName)) return;
            Tabs1.Add(fileName);
        }

        // Monaco からの保存メッセージを処理する
        public void OnSave(string fileName, string src)
        {
            FileItem fileItem = new(fileName);
            fileItem.Save(src, MainWindow!.NLBtn.Content.ToString());
            MainWindow.StatusBar.Text = $"{fileItem.Name} を保存しました。";
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
        public void OnAutoSave(string fileName, string src)
        {
            if (!MainWindow!.AutoSave.IsChecked)
                return;
            FileItem fileItem = new(fileName);
            fileItem.Save(src, MainWindow.NLBtn.Content.ToString());
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
            FrameworkElement selectedItem = (FrameworkElement)((TabView)sender).SelectedItem;
            if (selectedItem == null) return;
            string fileName = ((FrameworkElement)((TabView)sender).SelectedItem).Name;
            string extension = System.IO.Path.GetExtension(fileName);
            if (extension == ".tex")
            {
                MainWindow!.rightArea.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                MainWindow!.rightArea.ColumnDefinitions[1].Width = new GridLength(0);
            }
        }
    }
}
