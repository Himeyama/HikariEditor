using Microsoft.UI.Xaml.Controls;
using System.IO;

namespace HikariEditor
{
    class FileItem : TreeViewNode
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public string Icon1 { get; set; }
        public string Icon2 { get; set; }
        public string Color1 { get; set; }
        public string Color2 { get; set; }
        public bool Flag { get; set; }

        public FileItem(string fileName)
        {
            Path = fileName;
            Name = System.IO.Path.GetFileName(fileName);
            Flag = false;
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
    }
}
