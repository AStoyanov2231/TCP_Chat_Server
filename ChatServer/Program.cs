using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Numerics;

class MyTcpListener
{
    static Dictionary<string, TcpClient> clients = new Dictionary<string, TcpClient>();
    static int port = 3456;
    static int max_clients = 10;
    static int connected_clients = 0;

    public static void Main(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith("-p"))
            {
                port = int.Parse(arg.Substring(3));
            }
            else if (arg.StartsWith("-c"))
            {
                max_clients = int.Parse(arg.Substring(3));
            }
        }

        TcpListener server = null;
        try
        {
            IPAddress localAddress = IPAddress.Parse("127.0.0.1");

            server = new TcpListener(localAddress, port);
            server.Start();
            Console.WriteLine("Listening for connections on port {0}...", port);

            while (true)
            {
                Console.Write("Waiting for a connection... ");
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Client connected!");

                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();
            }
        }
        catch (SocketException e)
        {
            Console.WriteLine("SocketException: {0}", e);
        }
        finally
        {
            if (server != null)
            {
                server.Stop();
                Console.WriteLine("Server stopped.");
            }
        }

        Console.WriteLine("\nHit enter to continue...");
        Console.Read();
    }

    static void HandleClient(TcpClient client)
    {
        using (client)
        {
            NetworkStream stream = client.GetStream();
            StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            if(connected_clients == max_clients)
            {
                writer.WriteLine("Chat is full : (");
                return;
            }

            // Send welcome message
            writer.WriteLine("Welcome to the chat server. Please identify yourself with the command ':meet YourName'.");
            connected_clients++;

            string clientName = null;
            bool clientIdentified = false;

            while (!clientIdentified)
            {
                string receivedMessage = reader.ReadLine();

                if (receivedMessage != null && receivedMessage.StartsWith(":meet"))
                {
                    clientName = receivedMessage.Substring(6).Trim();
                    if (!clients.ContainsKey(clientName))
                    {
                        clients.Add(clientName, client);
                        clientIdentified = true;
                        writer.WriteLine("Hello, " + clientName + "! You are now connected.");
                    }
                    else
                    {
                        writer.WriteLine("Error: This name is already in use. Please choose a different name.");
                    }
                }
                else
                {
                    writer.WriteLine("Error: Please identify yourself with the command ':meet YourName'.");
                }
            }

            Console.WriteLine("Client '{0}' identified. Ready to accept commands and chat messages.", clientName);

            try
            {
                while (true)
                {
                    string receivedMessage = reader.ReadLine();
                    if (receivedMessage != null)
                    {
                        if (receivedMessage.StartsWith(":"))
                        {
                            string[] parts = receivedMessage.Split(' ');
                            string command = parts[0].Substring(1).Trim().ToLower();

                            string responseMessage;

                            switch (command)
                            {
                                case "p":
                                    responseMessage = port.ToString();
                                    break;
                                case "n":
                                    responseMessage = "Members: " + connected_clients;
                                    break;
                                case "m":
                                    responseMessage = "Welcome to the server";
                                    break;
                                case "who":
                                    responseMessage = "Connected clients: " + string.Join(", ", clients.Keys);
                                    break;
                                case "quit":
                                    clients.Remove(clientName);
                                    writer.WriteLine("You have been disconnected from the server.");
                                    connected_clients--;
                                    return; // Break out of the loop and end client handling
                                case "whisper":
                                    if (parts.Length >= 3)
                                    {
                                        string targetClient = parts[1].Trim();
                                        if (clients.ContainsKey(targetClient))
                                        {
                                            string message = string.Join(" ", parts, 2, parts.Length - 2);
                                            StreamWriter whisperWriter = new StreamWriter(clients[targetClient].GetStream(), Encoding.UTF8) { AutoFlush = true };

                                            responseMessage = "Whisper sent to " + targetClient;
                                            whisperWriter.WriteLine("[Whisper from {0}]: {1}", clientName, message);
                                        }
                                        else
                                        {
                                            responseMessage = "Error: Client '" + targetClient + "' not found.";
                                        }
                                    }
                                    else
                                    {
                                        responseMessage = "Error: Usage: :whisper <client name>, <message>";
                                    }
                                    break;
                                default:
                                    responseMessage = "Unknown command";
                                    break;
                            }

                            writer.WriteLine(responseMessage);
                        }
                        else
                        {
                            Console.WriteLine("[{0}]: {1}", clientName, receivedMessage);
                            // Broadcast the message to all clients
                            foreach (var pair in clients)
                            {
                                if (pair.Key != clientName) // Avoid sending message back to sender
                                {
                                    StreamWriter broadcastWriter = new StreamWriter(pair.Value.GetStream(), Encoding.UTF8) { AutoFlush = true };
                                    broadcastWriter.WriteLine("[{0}]: {1}", clientName, receivedMessage);
                                }
                            }
                        }
                    }
                }
            }
            catch (SocketException)
            {
                Console.WriteLine("Client '{0}' disconnected.", clientName);
                clients.Remove(clientName);
                // Notify server about disconnection
                Console.WriteLine("Client '{0}' disconnected.", clientName);
                connected_clients--;
            }
        }
    }
}