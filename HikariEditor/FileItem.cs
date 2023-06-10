﻿using Microsoft.UI.Xaml.Controls;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.IO;

namespace HikariEditor
{
    class FileItem : TreeViewNode
    {
#nullable disable
        public string Path { get; set; }
        public string Name { get; set; }
        public string Dirname { get; set; }
        public string Extension { get; set; }
        public string WithoutName { get; set; }
        public string Icon1 { get; set; }
        public string Icon2 { get; set; }
        public string Color1 { get; set; }
        public string Color2 { get; set; }
        public bool Flag { get; set; }

        void InitFileItem()
        {
            if (Path == null || Path == "") return;
            Dirname = System.IO.Path.GetDirectoryName(Path) ?? "";
            if (File.Exists(Dirname))
            {
                Dirname = System.IO.Path.GetDirectoryName(Dirname + "\\temp.dat") ?? "";
            }
            Name = System.IO.Path.GetFileName(Path);
            Extension = System.IO.Path.GetExtension(Path);
            WithoutName = System.IO.Path.GetFileNameWithoutExtension(Path);
            Path = $"{Dirname}\\{WithoutName}{Extension}";
            Flag = false;
        }

        public FileItem(string dirName, string fileName)
        {
            (Name, Dirname, Extension, WithoutName) = ("", "", "", "");
            Path = $"{dirName}\\{fileName}";
            InitFileItem();
        }

        public FileItem(string fileName)
        {
            (Name, Dirname, Extension, WithoutName) = ("", "", "", "");
            Path = fileName;
            InitFileItem();
        }

        public bool CreateFile(MainWindow mainWindow)
        {
            if (!File.Exists(Path))
            {
                if (Directory.Exists(Dirname))
                {
                    if (!Directory.Exists(Path))
                        using (File.Create(Path))
                        {
                            ;
                        }
                    else
                    {
                        Error.Dialog("作成失敗", "同名のフォルダが存在しています。", mainWindow.Content.XamlRoot);
                        return false;
                    }
                }
                else
                {
                    Error.Dialog("作成失敗", "選択している項目はフォルダではありません。", mainWindow.Content.XamlRoot);
                    return false;
                }

            }
            else
            {
                Error.Dialog("作成失敗", "同名のファイルが既に存在しています。", mainWindow.Content.XamlRoot);
                return false;
            }
            return true;
        }

        public bool CreateDirectory(MainWindow mainWindow)
        {
            if (!Directory.Exists(Path))
            {
                if (File.Exists(Path))
                {
                    Error.Dialog("作成失敗", "同名のファイルが既に存在しています。", mainWindow.Content.XamlRoot);
                    return false;
                }
                else
                {
                    Directory.CreateDirectory(Path);
                }
            }
            else
            {
                Error.Dialog("作成失敗", "同名のフォルダが既に存在しています。", mainWindow.Content.XamlRoot);
                return false;
            }
            return true;
        }

        public void Save(string src, string NL)
        {
            if (NL == "LF")
            {
                src = ToLF(src);
            }
            else if (NL == "CRLF")
            {
                src = ToCRLF(src);
            }
            File.WriteAllText(this.Path, src);
        }

        string ToLF(string src)
        {
            return src.Replace("\r\n", "\n");
        }

        string ToCRLF(string src)
        {
            src = ToLF(src);
            return src.Replace("\n", "\r\n");
        }

        public void Extract()
        {
            string ext = System.IO.Path.GetExtension(Path).Trim();
            string extDir = System.IO.Path.GetDirectoryName(Path);
            if (ext == ".tgz")
            {
                using Stream stream = File.OpenRead(Path);
                IReader reader = ReaderFactory.Open(stream);
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
            else return;
        }
    }
}
