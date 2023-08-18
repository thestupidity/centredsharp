﻿using System.Net;
using System.Net.Sockets;
using CentrED.Client.Map;
using CentrED.Network;
using CentrED.Utility;

namespace CentrED.Client;

public sealed class CentrEDClient : IDisposable {
    private NetState<CentrEDClient> NetState { get; }
    private ClientLandscape _landscape { get; set; }
    public bool CentrEdPlus { get; internal set; }
    public bool Initialized { get; internal set; }
    public string Username { get; }
    public string Password { get; }
    public AccessLevel AccessLevel { get; internal set; }
    public ushort X { get; private set; }
    public ushort Y { get; private set; }
    public List<String> Clients { get; } = new();
    public bool Running = true;
    
    public CentrEDClient(string hostname, int port, string username, string password) {
        Username = username;
        Password = password;
        var ipAddress = Dns.GetHostAddresses(hostname)[0];
        var ipEndPoint = new IPEndPoint(ipAddress, port);
        var socket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(ipEndPoint);
        NetState = new NetState<CentrEDClient>(this, socket, PacketHandlers.Handlers);

        NetState.Send(new LoginRequestPacket(username, password));

        do {
            Update();
        } while (!Initialized);
    }

    ~CentrEDClient() {
        Dispose(false);
    }
    
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Dispose(bool disposing) {
        if (disposing) {
            Running = false;
            while (NetState.FlushPending)
                NetState.Flush();
            NetState.Dispose();
        }
    }

    public void Update() {
        try {
            if(DateTime.Now - TimeSpan.FromMinutes(1) > NetState.LastAction)
            {
                Send(new NoOpPacket());
            }
            NetState.Receive();

            if (NetState.FlushPending) {
                NetState.Flush();
            }
        }
        catch {
            NetState.Dispose();
        }
    }

    public ushort Width => _landscape.Width;
    public ushort Height => _landscape.Height;

    public void InitLandscape(ushort width, ushort height) {
        _landscape = new ClientLandscape(this, width, height);
        _landscape.BlockCache.Resize(1024);
        Initialized = true;
    }

    public void LoadBlocks(List<BlockCoords> blockCoords) {
        var filteredBlocks = blockCoords.FindAll(b => !_landscape.BlockCache.Contains(Block.Id(b.X, b.Y)));
        if (filteredBlocks.Count <= 0) return;
        Send(new RequestBlocksPacket(filteredBlocks));
        foreach (var block in filteredBlocks) {
            while (!_landscape.BlockCache.Contains(Block.Id(block.X, block.Y))) {
                Thread.Sleep(1);
                Update();
            }
        }
    }

    public bool isValidX(int x) {
        return x >= 0 && x < Width * 8;
    }
    
    public bool isValidY(int y) {
        return y >= 0 && y < Height * 8;
    }

    public ushort ClampX(int x) {
        return (ushort)Math.Min(x, Width - 1);
    }
    
    public ushort ClampY(int y) {
        return (ushort)Math.Min(y, Height - 1);
    }

    public void SetPos(ushort x, ushort y) {
        if (x == X && y == Y) return;
        
        X = x;
        Y = y;
        Send(new UpdateClientPosPacket(x, y));
    }

    public void ChatMessage(string sender, ushort message) {
        Logger.LogInfo($"{sender}: {message}");
    }
    
    public LandTile GetLandTile(int x, int y) {
        return _landscape.GetLandTile(Convert.ToUInt16(x), Convert.ToUInt16(y));
    }
    
    public IEnumerable<StaticTile> GetStaticTiles(int x, int y) {
        return _landscape.GetStaticTiles(Convert.ToUInt16(x), Convert.ToUInt16(y));
    }

    public void AddStaticTile(StaticTile tile) {
        NetState.Send(new InsertStaticPacket(tile));
    }

    public void RemoveStaticTile(StaticTile tile) {
        NetState.Send(new DeleteStaticPacket(tile));
    }
    
    internal void Send(Packet p) {
        NetState.Send(p);
    }

    public void ResizeCache(int newSize) {
        _landscape.BlockCache.Resize(newSize);
    }

    public void Flush() {
        NetState.Send(new ServerFlushPacket());
    }

    #region events
    
    /*
     * Client emits events of changes that came from the server
     */
    public event MapChanged? MapChanged;
    public event BlockChanged? BlockUnloaded;
    public event BlockChanged? BlockLoaded;
    public event LandReplaced? LandTileReplaced;
    public event LandElevated? LandTileElevated;
    public event StaticChanged? StaticTileAdded;
    public event StaticChanged? StaticTileRemoved;
    public event StaticReplaced? StaticTileReplaced;
    public event StaticMoved? StaticTileMoved;
    public event StaticElevated? StaticTileElevated;
    public event StaticHued? StaticTileHued;
    
    internal void OnMapChanged() {
        MapChanged?.Invoke();
    }

    internal void OnBlockReleased(Block block) {
        BlockUnloaded?.Invoke(block);
        OnMapChanged();
    }

    internal void OnBlockLoaded(Block block) {
        BlockLoaded?.Invoke(block);
        OnMapChanged();
    }

    internal void OnLandReplaced(LandTile landTile, ushort newId) {
        LandTileReplaced?.Invoke(landTile, newId);
        OnMapChanged();
    }

    internal void OnLandElevated(LandTile landTile, sbyte newZ) {
        LandTileElevated?.Invoke(landTile, newZ);
        OnMapChanged();
    }

    internal void OnStaticTileAdded(StaticTile staticTile) {
        StaticTileAdded?.Invoke(staticTile);
        OnMapChanged();
    }

    internal void OnStaticTileRemoved(StaticTile staticTile) {
        StaticTileRemoved?.Invoke(staticTile);
        OnMapChanged();
    }
    
    internal void OnStaticTileReplaced(StaticTile staticTile, ushort newId) {
        StaticTileReplaced?.Invoke(staticTile, newId);
        OnMapChanged();
    }
    
    internal void OnStaticTileMoved(StaticTile staticTile, ushort newX, ushort newY) {
        StaticTileMoved?.Invoke(staticTile, newX, newY);
        OnMapChanged();
    }

    internal void OnStaticTileElevated(StaticTile staticTile, sbyte newZ) {
        StaticTileElevated?.Invoke(staticTile, newZ);
        OnMapChanged();
    }

    internal void OnStaticTileHued(StaticTile staticTile, ushort newHue) {
        StaticTileHued?.Invoke(staticTile, newHue);
        OnMapChanged();
    }
    
    #endregion
}