using System.Net;
using System.Net.Sockets;

namespace http_server;

public static class Program
{
    private const int Port = 4221;
    private static readonly CancellationTokenSource CancellationTokenSource = new();

    public static void Main()
    {
        var server = new TcpListener(IPAddress.Any, Port);

        try
        {
            server.Start();
            Console.WriteLine("Server started on port " + Port);
            while (!CancellationTokenSource.IsCancellationRequested)
            {
                var client = server.AcceptTcpClient();
                Console.WriteLine("Client connected!");
                Task.Run(() => ResponseHandler.ServeAsync(client));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error {e} occurred");
        }
        finally
        {
            server.Stop();
        }
    }
}