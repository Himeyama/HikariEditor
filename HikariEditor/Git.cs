using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace HikariEditor;

internal static class Git
{
    // git をワーキングディレクトリ指定で実行する。git 未インストールやリポジトリ外
    // などの失敗はすべて終了コード非 0 として呼び出し側に伝える。
    static async Task<(int exitCode, string stdout, string stderr)> Run(string workingDir, string arguments)
    {
        if (string.IsNullOrEmpty(workingDir))
            return (-1, "", "");
        try
        {
            using Process process = new();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = "git";
            process.StartInfo.Arguments = arguments;
            process.StartInfo.WorkingDirectory = workingDir;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            process.Start();
            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, stdout, stderr);
        }
        catch (Exception e)
        {
            // git 自体が見つからない場合などはここに来る
            Debug.WriteLine(e.Message);
            return (-1, "", e.Message);
        }
    }

    // リポジトリ内なら現在のブランチ名、リポジトリ外・git 不在なら null を返す。
    public static async Task<string?> CurrentBranchAsync(string workingDir)
    {
        (int exitCode, string stdout, _) = await Run(workingDir, "rev-parse --abbrev-ref HEAD");
        if (exitCode != 0)
            return null;
        string branch = stdout.Trim();
        return branch == "" ? null : branch;
    }

    // ローカルブランチ名の一覧。リポジトリ外なら空リスト。
    public static async Task<List<string>> BranchesAsync(string workingDir)
    {
        (int exitCode, string stdout, _) = await Run(workingDir, "branch --format=%(refname:short)");
        List<string> branches = [];
        if (exitCode != 0)
            return branches;
        foreach (string line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            branches.Add(line);
        return branches;
    }

    // ブランチを切り替える。失敗時は git のエラーメッセージを返す。
    public static async Task<(bool ok, string message)> CheckoutAsync(string workingDir, string branch)
    {
        (int exitCode, string stdout, string stderr) = await Run(workingDir, $"checkout {branch}");
        // checkout の進捗・エラーは stderr に出る
        string message = stderr.Trim() == "" ? stdout.Trim() : stderr.Trim();
        return (exitCode == 0, message);
    }
}
