// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HikariEditor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Editor : Page
    {

        public Editor()
        {
            InitializeComponent();
            waitServer();
        }

        async void waitServer()
        {
            while (true)
            {
                await server();
            }
        }

        async Task server()
        {
            IPAddress ipaddr = IPAddress.Parse("127.0.0.1");
            IPEndPoint ipEndPoint = new(ipaddr, 8086);
            TcpListener listener = new(ipEndPoint);
            //new Span<byte>(new byte[1024]);

            try
            {
                listener.Start();

                using TcpClient handler = await listener.AcceptTcpClientAsync();
                await using NetworkStream stream = handler.GetStream();
                //StreamReader cReader = new(stream, Encoding.UTF8);

                //using (StreamReader reader = new(stream, Encoding.UTF8))
                //{
                //    text.Text = reader.ReadToEnd();
                //}
                byte[] buffer = new byte[1024];
                stream.Read(buffer, 0, buffer.Length);
                text.Text = Encoding.UTF8.GetString(buffer, 0, buffer.Length);

                var message = $"📅 {DateTime.Now} 🕛";
                var dateTimeBytes = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(dateTimeBytes);

                //Debug.WriteLine($"Sent message: \"{message}\"");
                //text.Text = $"Sent message: \"{message}\"";
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
