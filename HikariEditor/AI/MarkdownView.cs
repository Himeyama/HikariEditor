using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Windows.UI;
using FontStyle = Windows.UI.Text.FontStyle;

namespace HikariEditor;

// アシスタント出力の Markdown を描画する軽量ビュー。
// 見出し・箇条書き・引用・コードフェンス・強調/コード/リンクのインラインに対応する。
// ストリーミングで Markdown プロパティが更新されるたびに全体を組み直す（チャット規模なら十分軽い）。
public sealed partial class MarkdownView : ContentControl
{
    // Consolas は日本語グリフを持たないため Noto Sans JP をフォールバックに添える
    static readonly FontFamily Mono = new("Consolas, Noto Sans JP, Segoe UI Variable");
    static readonly SolidColorBrush CodeBackground = new(Color.FromArgb(0x22, 0x80, 0x80, 0x80));
    static readonly SolidColorBrush GridLine = new(Color.FromArgb(0x40, 0x80, 0x80, 0x80));

    readonly StackPanel _panel = new() { Spacing = 4 };
    RichTextBlock? _current;   // テキスト系ブロックの追記先。コード塊が来たら確定する。

    public MarkdownView()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Content = _panel;
    }

    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(nameof(Markdown), typeof(string), typeof(MarkdownView),
            new PropertyMetadata(string.Empty, OnMarkdownChanged));

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MarkdownView)d).Render((string)e.NewValue);

    void Render(string markdown)
    {
        _panel.Children.Clear();
        _current = null;
        if (string.IsNullOrEmpty(markdown)) return;

        string[] lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        List<string> paragraph = [];

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            Paragraph p = new() { Margin = new Thickness(0, 0, 0, 4) };
            for (int k = 0; k < paragraph.Count; k++)
            {
                if (k > 0) p.Inlines.Add(new LineBreak());   // 段落内の改行はソフト改行として残す
                AppendInlines(p, paragraph[k]);
            }
            EnsureRich().Blocks.Add(p);
            paragraph.Clear();
        }

        int i = 0;
        while (i < lines.Length)
        {
            string line = lines[i];
            string trimmed = line.TrimStart();

            // コードフェンス（``` ... ```）
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                i++;
                List<string> code = [];
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                    code.Add(lines[i++]);
                if (i < lines.Length) i++;   // 閉じフェンスを読み飛ばす
                AddCodeBlock(string.Join("\n", code));
                continue;
            }

            // 水平線（---, ***, ___ が 3 つ以上）
            if (Regex.IsMatch(line, @"^\s*([-*_])(\s*\1){2,}\s*$"))
            {
                FlushParagraph();
                AddHorizontalRule();
                i++;
                continue;
            }

            // 表（ヘッダ行の直後が区切り行 |---|---| のとき）
            if (line.Contains('|') && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
            {
                FlushParagraph();
                List<string> header = SplitRow(line);
                List<string> aligns = SplitRow(lines[i + 1]);
                i += 2;
                List<List<string>> rows = [];
                while (i < lines.Length && lines[i].Contains('|') && lines[i].Trim().Length > 0)
                    rows.Add(SplitRow(lines[i++]));
                AddTable(header, aligns, rows);
                continue;
            }

            // 空行で段落を区切る
            if (line.Trim().Length == 0)
            {
                FlushParagraph();
                i++;
                continue;
            }

            // 見出し（# 〜 ######）
            Match heading = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
            if (heading.Success)
            {
                FlushParagraph();
                AddHeading(heading.Groups[1].Value.Length, heading.Groups[2].Value);
                i++;
                continue;
            }

            // 箇条書き / 番号付きリスト
            Match list = Regex.Match(line, @"^(\s*)([-*+]|\d+\.)\s+(.*)$");
            if (list.Success)
            {
                FlushParagraph();
                string marker = list.Groups[2].Value;
                string bullet = marker.EndsWith('.') ? marker + " " : "•  ";
                Paragraph p = new() { Margin = new Thickness(12, 0, 0, 2) };
                p.Inlines.Add(new Run { Text = bullet });
                AppendInlines(p, list.Groups[3].Value);
                EnsureRich().Blocks.Add(p);
                i++;
                continue;
            }

            // 引用
            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                FlushParagraph();
                Paragraph p = new() { Margin = new Thickness(12, 0, 0, 4) };
                AppendInlines(p, trimmed.TrimStart('>').TrimStart());
                EnsureRich().Blocks.Add(p);
                i++;
                continue;
            }

            paragraph.Add(line);
            i++;
        }
        FlushParagraph();
    }

    // テキスト系ブロックの追記先。コード塊などで途切れたら新しい RichTextBlock を起こす。
    RichTextBlock EnsureRich()
    {
        if (_current is null)
        {
            _current = new RichTextBlock { IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
            _panel.Children.Add(_current);
        }
        return _current;
    }

    void AddHeading(int level, string text)
    {
        double size = level switch { 1 => 24, 2 => 20, 3 => 17, 4 => 15, _ => 14 };
        Paragraph p = new() { Margin = new Thickness(0, level <= 2 ? 8 : 4, 0, 4) };
        AppendInlines(p, text);
        // 強調などで Run 以外のインラインになることもあるため種別を問わず適用する
        foreach (Inline inline in p.Inlines)
        {
            inline.FontSize = size;
            inline.FontWeight = FontWeights.SemiBold;
        }
        EnsureRich().Blocks.Add(p);
    }

    void AddHorizontalRule()
    {
        _current = null;
        _panel.Children.Add(new Border
        {
            Height = 1,
            Margin = new Thickness(0, 8, 0, 8),
            Background = GridLine,
            HorizontalAlignment = HorizontalAlignment.Stretch
        });
    }

    // 区切り行（|---|:--:|---:| のように各セルが - と任意の : だけ）か判定する。
    static bool IsTableSeparator(string line)
    {
        List<string> cells = SplitRow(line);
        if (cells.Count == 0) return false;
        foreach (string c in cells)
            if (!Regex.IsMatch(c, @"^:?-{1,}:?$"))
                return false;
        return true;
    }

    // 1 行を | 区切りのセル配列にする。外側の | は除去する。
    static List<string> SplitRow(string line)
    {
        string s = line.Trim();
        if (s.StartsWith('|')) s = s[1..];
        if (s.EndsWith('|')) s = s[..^1];
        List<string> cells = [];
        foreach (string c in s.Split('|'))
            cells.Add(c.Trim());
        return cells;
    }

    void AddTable(List<string> header, List<string> aligns, List<List<string>> rows)
    {
        _current = null;

        int cols = header.Count;
        foreach (List<string> r in rows)
            cols = Math.Max(cols, r.Count);
        if (cols == 0) return;

        // 区切り行のコロン位置から列ごとの寄せを決める
        TextAlignment[] colAlign = new TextAlignment[cols];
        for (int c = 0; c < cols; c++)
        {
            string a = c < aligns.Count ? aligns[c] : "";
            bool left = a.StartsWith(':'), right = a.EndsWith(':');
            colAlign[c] = left && right ? TextAlignment.Center
                        : right ? TextAlignment.Right
                        : TextAlignment.Left;
        }

        Grid grid = new() { HorizontalAlignment = HorizontalAlignment.Left };
        for (int c = 0; c < cols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        List<List<string>> all = [header, .. rows];
        for (int r = 0; r < all.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < cols; c++)
            {
                RichTextBlock rtb = new()
                {
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 360,           // 1 セルが伸び過ぎないよう上限を設けて折り返す
                    TextAlignment = colAlign[c],
                    IsTextSelectionEnabled = true
                };
                Paragraph p = new();
                AppendInlines(p, c < all[r].Count ? all[r][c] : "");
                if (r == 0)
                    foreach (Inline inline in p.Inlines)
                        inline.FontWeight = FontWeights.SemiBold;
                rtb.Blocks.Add(p);

                // 各セルは右・下の罫線だけ描き、外枠の上・左は外側 Border が担う
                Border cell = new()
                {
                    Child = rtb,
                    Padding = new Thickness(8, 4, 8, 4),
                    BorderBrush = GridLine,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Background = r == 0 ? CodeBackground : null
                };
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        Border outer = new()
        {
            Child = grid,
            BorderBrush = GridLine,
            BorderThickness = new Thickness(1, 1, 0, 0),
            Margin = new Thickness(0, 2, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // 横に長い表はここで水平スクロールできるようにする
        _panel.Children.Add(new ScrollViewer
        {
            Content = outer,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            HorizontalAlignment = HorizontalAlignment.Stretch
        });
    }

    void AddCodeBlock(string code)
    {
        _current = null;   // コード塊はテキスト RichTextBlock から独立させる
        Border border = new()
        {
            Background = CodeBackground,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = new TextBlock
            {
                Text = code,
                FontFamily = Mono,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            }
        };
        _panel.Children.Add(border);
    }

    // 1 行分のインライン（**太字** / *斜体* / `コード` / [リンク](url)）を解釈して追加する。
    static void AppendInlines(Paragraph p, string text)
    {
        System.Text.StringBuilder buffer = new();

        void Flush()
        {
            if (buffer.Length == 0) return;
            p.Inlines.Add(new Run { Text = buffer.ToString() });
            buffer.Clear();
        }

        int i = 0;
        while (i < text.Length)
        {
            char ch = text[i];

            // インラインコード
            if (ch == '`')
            {
                int end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    Flush();
                    p.Inlines.Add(new Run { Text = text[(i + 1)..end], FontFamily = Mono });
                    i = end + 1;
                    continue;
                }
            }

            // 太字（** または __）
            if ((ch == '*' || ch == '_') && i + 1 < text.Length && text[i + 1] == ch)
            {
                string delim = new(ch, 2);
                int end = text.IndexOf(delim, i + 2, StringComparison.Ordinal);
                if (end > i)
                {
                    Flush();
                    p.Inlines.Add(new Run { Text = text[(i + 2)..end], FontWeight = FontWeights.Bold });
                    i = end + 2;
                    continue;
                }
            }

            // 斜体（* または _）
            if (ch == '*' || ch == '_')
            {
                int end = text.IndexOf(ch, i + 1);
                if (end > i)
                {
                    Flush();
                    p.Inlines.Add(new Run { Text = text[(i + 1)..end], FontStyle = FontStyle.Italic });
                    i = end + 1;
                    continue;
                }
            }

            // リンク [text](url)
            if (ch == '[')
            {
                int close = text.IndexOf(']', i + 1);
                if (close > i && close + 1 < text.Length && text[close + 1] == '(')
                {
                    int paren = text.IndexOf(')', close + 2);
                    if (paren > close)
                    {
                        Flush();
                        Hyperlink link = new();
                        link.Inlines.Add(new Run { Text = text[(i + 1)..close] });
                        if (Uri.TryCreate(text[(close + 2)..paren], UriKind.Absolute, out Uri? uri))
                            link.NavigateUri = uri;
                        p.Inlines.Add(link);
                        i = paren + 1;
                        continue;
                    }
                }
            }

            buffer.Append(ch);
            i++;
        }
        Flush();
    }
}
