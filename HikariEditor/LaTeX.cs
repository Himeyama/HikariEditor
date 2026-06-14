using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HikariEditor;

internal class LaTeX
{
    public static async Task<bool> Compile(MainWindow mainWindow, FileItem fileItem, Editor editor)
    {
        try
        {
            bool compileError;
            using (Process process = new())
            {
                process.StartInfo.UseShellExecute = false;
                // ptex2pdf.exe は PATH 上にある前提（CLAUDE.md のビルド要件を参照）
                process.StartInfo.FileName = "ptex2pdf.exe";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.Arguments = $"-l -ot -interaction=nonstopmode -halt-on-error -kanji=utf8 -output-directory=\"{fileItem.Dirname}\" \"{fileItem.Path}\"";
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                string stdout = process.StandardOutput.ReadToEnd();
                await process.WaitForExitAsync();

                compileError = process.ExitCode != 0;
                if (compileError)
                {
                    mainWindow.StatusBar.Text = $"{fileItem.Name} のコンパイルに失敗しました。";
                    LogPage.AddLog(mainWindow, $"{fileItem.Name} のコンパイルに失敗しました。");
                    Error.Dialog("LaTeX コンパイルエラー", stdout, mainWindow.Content.XamlRoot);
                }
                else
                {
                    mainWindow.StatusBar.Text = $"{fileItem.Name} のコンパイルに成功しました。";
                    LogPage.AddLog(mainWindow, $"{fileItem.Name} のコンパイルに成功しました。");
                }
                editor.Counter++;
                editor.DelayResetStatusBar(1000);
            }

            if (compileError)
                return false;

            FileItem pdfFileItem = new(fileItem.Dirname, $"{fileItem.WithoutName}.pdf");
            mainWindow.previewFrame.Navigate(typeof(PDF), new PDFPageInfo(mainWindow, pdfFileItem));
            return true;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
            return false;
        }
    }
}
