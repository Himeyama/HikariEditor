using Microsoft.UI.Xaml.Controls;

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
    }
}
