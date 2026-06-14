using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HikariEditor;

// ロールごとに見た目を切り替える。ユーザーは吹き出し、アシスタントは全面、
// ツールは控えめな 1 行。各テンプレートは AIPanel.xaml のリソースで定義する。
public partial class ChatTemplateSelector : DataTemplateSelector
{
    public DataTemplate? User { get; set; }
    public DataTemplate? Assistant { get; set; }
    public DataTemplate? Tool { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) =>
        item is ChatMessage message
            ? message.Role switch
            {
                ChatRole.User => User,
                ChatRole.Tool => Tool,
                _ => Assistant
            }
            : Assistant;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) =>
        SelectTemplateCore(item);
}
