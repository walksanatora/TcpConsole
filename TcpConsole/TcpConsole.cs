using System.Net;
using System.Net.Sockets;
using System.Text;
using MelonLoader;
using HarmonyLib;
using TcpConsole;
[assembly: MelonInfo(typeof(TcpConsoleMod), "TcpConsole", "1.0.0", "Walksanator")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace TcpConsole;
public class TcpConsoleMod : MelonMod
{
    public static List<NetworkStream> Streams = [];
    private static MelonLogger.Instance? logger;
    private static MelonPreferences_Category? Config;
    private static MelonPreferences_Entry<int>? Port;
    private static TcpListener? Listener;
    public override void OnInitializeMelon()
    {
        Config = MelonPreferences.CreateCategory("TcpConsole");
        Port = Config.CreateEntry("Port", 8120);
        
        logger = LoggerInstance;
        logger.Msg("TcpConsole has Loaded");

        var ipAddress = IPAddress.Parse("127.0.0.1");
        Listener = new TcpListener(ipAddress, Port.Value);
        
        Listener.Start();

        HandleConnections().Start();
        
        base.OnInitializeMelon();
    }

    public override void OnApplicationQuit()
    {
        Listener!.Stop();
        foreach (var stream in Streams)
        {
            stream.Close();
        }
        base.OnApplicationQuit();
    }

    public void WriteMessages(string message)
    {
        List<int> toRemove = [];
        var bytes = Encoding.UTF8.GetBytes(message).AsSpan();
        foreach (var (stream,idx) in Streams.AsEnumerable().Select((value, index) => (value, index)))
        {
            if (stream.CanWrite)
            {
                stream.Write(bytes);
                stream.Flush();
            }
            else
            {
                toRemove.Add(idx);
            }
        }

        foreach (var snipe in toRemove.Select((val, _) => Streams[val]))
        {
            Streams.Remove(snipe);
        }
        //yknow what. screw them. we ain't waiting for them to read our messages
    }

    private async Task HandleConnections()
    {
        while (true)
        {
            var client = await Listener!.AcceptTcpClientAsync();
            var stream = client.GetStream();
            Streams.Add(stream);
        }
    }
    
    [HarmonyPatch(typeof(NetworkDedicatedServerConsole), "ReadStreamAsync")]
    [HarmonyPrefix]
    private static bool NetworkDedicatedServerConsole_ReadStreamAsync(NetworkDedicatedServerConsole __instance,ref bool ___Ready,ref StreamReader ___Reader,ref string ___QueueCommand)
    {
        while (___Ready)
        {
            var str = ___Reader.ReadLineAsync().GetAwaiter().GetResult();
            if (string.IsNullOrEmpty(str))
            {
                foreach (var reader in Streams.Select(stream => new StreamReader(stream)))
                {
                    str = reader.ReadLineAsync().GetAwaiter().GetResult();
                    if (!string.IsNullOrEmpty(str))
                    {
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(str)) continue;
            logger!.Msg((object) ("Patched REMOTE: " + str));
            ___QueueCommand = str;
        }
        return false;
    }
}