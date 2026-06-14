using System;
using System.Text;

namespace HikariEditor;

public class Text(string text)
{
    public string EncodeBase64() => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
}
