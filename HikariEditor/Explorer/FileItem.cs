using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.IO;

namespace HikariEditor;

class FileItem : TreeViewNode
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Dirname { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string WithoutName { get; set; } = string.Empty;
    public string Icon1 { get; set; } = string.Empty;
    public string Icon2 { get; set; } = string.Empty;
    public string Color1 { get; set; } = string.Empty;
    public string Color2 { get; set; } = string.Empty;

    // SVG だけ右クリックメニューに「テキストで開く／WebView で開く」を出すための表示制御
    public Visibility SvgVisibility =>
        Extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

    public FileItem(string dirName, string fileName)
    {
        Path = $"{dirName}\\{fileName}";
        InitFileItem();
    }

    public FileItem(string fileName)
    {
        Path = fileName;
        InitFileItem();
    }

    void InitFileItem()
    {
        if (string.IsNullOrEmpty(Path)) return;
        Dirname = System.IO.Path.GetDirectoryName(Path) ?? "";
        if (File.Exists(Dirname))
        {
            Dirname = System.IO.Path.GetDirectoryName(Dirname + "\\temp.dat") ?? "";
        }
        Name = System.IO.Path.GetFileName(Path);
        Extension = System.IO.Path.GetExtension(Path);
        WithoutName = System.IO.Path.GetFileNameWithoutExtension(Path);
        Path = $"{Dirname}\\{WithoutName}{Extension}";
    }

    public bool CreateFile(MainWindow mainWindow)
    {
        if (File.Exists(Path))
        {
            Error.Dialog("作成失敗", "同名のファイルが既に存在しています。", mainWindow.Content.XamlRoot);
            return false;
        }
        if (!Directory.Exists(Dirname))
        {
            Error.Dialog("作成失敗", "選択している項目はフォルダではありません。", mainWindow.Content.XamlRoot);
            return false;
        }
        if (Directory.Exists(Path))
        {
            Error.Dialog("作成失敗", "同名のフォルダが存在しています。", mainWindow.Content.XamlRoot);
            return false;
        }
        using (File.Create(Path)) { }
        return true;
    }

    public bool CreateDirectory(MainWindow mainWindow)
    {
        if (File.Exists(Path))
        {
            Error.Dialog("作成失敗", "同名のファイルが既に存在しています。", mainWindow.Content.XamlRoot);
            return false;
        }
        if (Directory.Exists(Path))
        {
            Error.Dialog("作成失敗", "同名のフォルダが既に存在しています。", mainWindow.Content.XamlRoot);
            return false;
        }
        Directory.CreateDirectory(Path);
        return true;
    }

    public void Save(string src, string? newline)
    {
        string normalized = newline switch
        {
            "LF" => ToLF(src),
            "CRLF" => ToCRLF(src),
            _ => src
        };
        File.WriteAllText(Path, normalized);
    }

    static string ToLF(string src) => src.Replace("\r\n", "\n");

    static string ToCRLF(string src) => ToLF(src).Replace("\n", "\r\n");

    public void Extract()
    {
        if (System.IO.Path.GetExtension(Path).Trim() != ".tgz") return;
        string? extDir = System.IO.Path.GetDirectoryName(Path);
        if (extDir == null) return;

        using Stream stream = File.OpenRead(Path);
        IReader reader = ReaderFactory.OpenReader(stream);
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryToDirectory(extDir, new ExtractionOptions()
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });
            }
        }
    }
}
