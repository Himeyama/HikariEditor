using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HikariEditor;

// 会話一覧に表示する 1 行の種別。テンプレート選択にも使う。
//   User      … ユーザー発言（吹き出し・右寄せ）
//   Assistant … アシスタント発言（全面）
//   Tool      … ツール実行の状況表示（控えめな 1 行）
// {Binding} はフレームワーク側アセンブリからリフレクションで参照するため public にする。
public enum ChatRole
{
    User,
    Assistant,
    Tool
}

// ストリーミング中に Text を逐次更新するため INotifyPropertyChanged にする。
// バインド先の TextBlock がトークン到着のたびに伸びていく。
public class ChatMessage : INotifyPropertyChanged
{
    public ChatRole Role { get; }

    string _text;
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            OnPropertyChanged();
        }
    }

    public ChatMessage(ChatRole role, string text = "")
    {
        Role = role;
        _text = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
