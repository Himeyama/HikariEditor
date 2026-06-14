using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Text;

namespace HikariEditor;

// バイナリファイルを 16 進ダンプとして表示するタブの中身。
// 1 行 16 バイトで「オフセット | 16 進 | ASCII」の定番レイアウトを組む。
public sealed partial class HexView : UserControl
{
    // 巨大ファイルで UI が固まらないよう、表示するのは先頭 1 MB までに制限する
    const int MaxBytes = 1024 * 1024;

    public HexView(string fileName)
    {
        InitializeComponent();
        HexText.Text = BuildDump(fileName);
    }

    static string BuildDump(string fileName)
    {
        byte[] bytes;
        long totalLength;
        try
        {
            using FileStream fs = File.OpenRead(fileName);
            totalLength = fs.Length;
            int count = (int)Math.Min(totalLength, MaxBytes);
            bytes = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = fs.Read(bytes, offset, count - offset);
                if (read == 0) break;
                offset += read;
            }
        }
        catch (IOException e)
        {
            return $"ファイルを開けませんでした: {e.Message}";
        }

        StringBuilder sb = new();
        StringBuilder ascii = new();
        for (int i = 0; i < bytes.Length; i += 16)
        {
            sb.Append($"{i:X8}  ");
            ascii.Clear();
            for (int j = 0; j < 16; j++)
            {
                if (i + j < bytes.Length)
                {
                    byte b = bytes[i + j];
                    sb.Append($"{b:X2} ");
                    // 印字可能な ASCII 以外はドットで表す
                    ascii.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
                }
                else
                {
                    sb.Append("   ");
                    ascii.Append(' ');
                }
                if (j == 7) sb.Append(' ');   // 8 バイトごとに区切りを入れる
            }
            sb.Append(' ').Append(ascii).Append('\n');
        }

        if (totalLength > MaxBytes)
            sb.Append($"\n... 先頭 {MaxBytes:N0} バイトのみ表示しています（全 {totalLength:N0} バイト）\n");

        return sb.ToString();
    }
}
