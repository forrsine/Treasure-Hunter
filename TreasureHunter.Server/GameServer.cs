using GameServer.Managers;
using GameServer.Services;
using Network;

namespace GameServer;

internal sealed class GameServer
{
    private NetService? _network;
    private Thread? _thread;
    private bool _running;

    public bool Init()
    {
        DBService.Instance.Init();
        CharacterManager.Instance.Init();
        UserService.Instance.Init();

        _network = new NetService();
        _network.Init(Settings.ServerHost, Settings.ServerPort, Settings.ConnectionBacklog, Settings.MessageThreads);
        _thread = new Thread(Update)
        {
            IsBackground = true
        };

        return true;
    }

    public void Start()
    {
        if (_network == null || _thread == null)
        {
            throw new InvalidOperationException("GameServer has not been initialized.");
        }

        _network.Start();
        _running = true;
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join();
        _network?.Stop();
    }

    private void Update()
    {
        while (_running)
        {
            Thread.Sleep(100);
        }
    }
}
