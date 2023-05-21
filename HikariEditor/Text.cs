using System;
using System.Text;

namespace HikariEditor
{
    public class Text
    {
        public string text { get; set; }

        public Text(string text)
        {
            this.text = text;
        }

        public string EncodeBase64()
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }
    }
}
