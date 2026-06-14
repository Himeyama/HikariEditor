using System;
using System.Collections.Generic;
using System.IO;

namespace HikariEditor;

// ファイルの開き方を決めるための分類。Text はエディタ、Image/Video/Pdf/Svg は
// WebView、Binary はバイナリ（16進）ビューで開く。
enum FileKind { Text, Image, Video, Pdf, Svg, Binary }

static class FileKinds
{
    static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp", ".avif" };

    static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".webm", ".ogg", ".ogv", ".mov", ".m4v" };

    public static FileKind Classify(string path)
    {
        string ext = Path.GetExtension(path);
        if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase)) return FileKind.Svg;
        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return FileKind.Pdf;
        if (Images.Contains(ext)) return FileKind.Image;
        if (Videos.Contains(ext)) return FileKind.Video;
        return IsBinary(path) ? FileKind.Binary : FileKind.Text;
    }

    // 先頭ブロックに NUL バイトが含まれていればバイナリとみなす（git と同じ簡易判定）。
    // 拡張子では判断できないファイルを振り分けるための最後の砦。
    static bool IsBinary(string path)
    {
        try
        {
            using FileStream fs = File.OpenRead(path);
            byte[] buffer = new byte[8000];
            int read = fs.Read(buffer, 0, buffer.Length);
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == 0) return true;
            }
            return false;
        }
        catch
        {
            // 読めないファイルはテキスト扱いにしてエディタ側でエラーを見せる
            return false;
        }
    }
}
