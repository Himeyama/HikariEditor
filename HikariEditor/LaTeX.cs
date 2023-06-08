using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HikariEditor
{
    internal class LaTeX
    {
        async public static Task<bool> Compile(MainWindow mainWindow, FileItem fileItem, Editor editor)
        {
            bool tex_compile_error = false;
            try
            {
                using (Process process = new())
                {
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName = "C:\\texlive\\2022\\bin\\win32\\ptex2pdf.exe";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.Arguments = $"-l -ot -interaction=nonstopmode -halt-on-error -kanji=utf8 -output-directory=\"{fileItem.Dirname}\" \"{fileItem.Path}\"";
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    string stdout = process.StandardOutput.ReadToEnd();
                    await process.WaitForExitAsync();

                    //Debug.WriteLine(stdout);
                    if (process.ExitCode == 0)
                    {
                        mainWindow.StatusBar.Text = $"{fileItem.Name} のコンパイルに成功しました。";
                        LogPage.AddLog(mainWindow, $"{fileItem.Name} のコンパイルに成功しました。");
                    }
                    else
                    {
                        mainWindow.StatusBar.Text = $"{fileItem.Name} のコンパイルに失敗しました。";
                        LogPage.AddLog(mainWindow, $"{fileItem.Name} のコンパイルに失敗しました。");
                        Error.Dialog("LaTeX コンパイルエラー", stdout, mainWindow.Content.XamlRoot);
                        tex_compile_error = true;
                    }
                    editor.counter++;
                    editor.DelayResetStatusBar(1000);
                }

                if (!tex_compile_error)
                {
                    FileItem pdfFileItem = new(fileItem.Dirname, $"{fileItem.WithoutName}.pdf");
                    PDFPageInfo pdfPageInfo = new();
                    pdfPageInfo.mainWindow = mainWindow;
                    pdfPageInfo.fileItem = pdfFileItem;
                    Debug.WriteLine(pdfFileItem.Path);
                    mainWindow.previewFrame.Navigate(typeof(PDF), pdfPageInfo);
                }

            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            return !tex_compile_error;
        }
    }
}
