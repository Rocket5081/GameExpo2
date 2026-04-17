using Godot;
using System;
using System.Text;
using System.Threading.Tasks;

[GlobalClass]
public partial class LobbyStreamlined : Node
{
	[Export] public string PublicIP;
	[Export] public string PrivateIP;
	[Export] public int PortMinimum;
	[Export] private int portOffset = 1;

	public string LobbyServerIP;
	private bool UsePublic;
	private bool UsePrivate;
	private bool UseLocal;

	public bool IsWanLobbyConnected;
	public bool IsWanLobbyServer;

	public static LobbyStreamlined Instance;

	[Export] private MultiplayerSpawner AgentSpawner;

	private ENetMultiplayerPeer AgentPeer;
	private Godot.MultiplayerApi AgentAPI;
	private NodePath LobbyRootPath;

	[Export] public TextEdit GameNameBox;
	public string tempGameName;
	[Export] public float MaxGameTime = 30;

	public override void _Ready()
	{
		Instance = this;
		AgentAPI = MultiplayerApi.CreateDefaultInterface();
		GetTree().SetMultiplayer(AgentAPI, GetPath());
		LobbyRootPath = GetPath();
		AgentAPI.PeerConnected += OnPeerConnected;
		AgentAPI.PeerDisconnected += OnPeerDisconnected;

		string[] args = OS.GetCmdlineArgs();
		if (AgentSpawner != null)
			AgentSpawner.SpawnFunction = new Callable(this, nameof(SpawnAgent));
		else
			GD.PushWarning("[LobbyStreamlined] AgentSpawner export is not assigned — check the scene file.");
		bool isGameServer = false;

		foreach (string arg in args)
		{
			if (arg == "MASTER")
				CreateMasterServer();

			if (arg.Contains("GAMENAME"))
			{
				tempGameName = arg.Split('#')[1];
				isGameServer = true;
			}
		}

		if (!IsWanLobbyConnected)
		{
			GD.Print("Connecting the agent to the master!");
			if (!isGameServer)
			{
				GD.Print("Connecting agent to master server using IP Ping");
				CheckIPAddresses();
			}
			else
			{
				GD.Print("Connecting game server to local master.");
				LobbyServerIP = "127.0.0.1";
				// Game servers connect to localhost which is always ready — use sync version
				JoinLobbyServer();
			}
		}
	}

	private void OnPeerConnected(long id)
	{
		if (IsWanLobbyServer)
		{
			AgentSpawner.Spawn(id);
			GD.Print("Spawning Agent");
			Rpc("UpdatePortOffset", portOffset);
		}
	}

	private Node SpawnAgent(Variant d)
	{
		long peerId = (long)d;
		var packedScene = GD.Load<PackedScene>(AgentSpawner._SpawnableScenes[0]);
		var node = packedScene.Instantiate();
		node.SetMultiplayerAuthority((int)peerId, true);
		return node;
	}

	private void OnPeerDisconnected(long id)
	{
		GD.Print($"Agent disconnected: {id}");
		if (!IsWanLobbyServer) return;

		Node spawnRoot = GetNode(AgentSpawner.GetPath() + "/" + AgentSpawner.SpawnPath);
		foreach (Node child in spawnRoot.GetChildren())
		{
			if (child.GetMultiplayerAuthority() == id)
			{
				GD.Print($"Freeing agent owned by {id}");
				child.QueueFree();
			}
		}
	}

	// ── IP detection + async lobby connection for regular clients ──────────
	public async Task CheckIPAddresses()
	{
		// Wait for MASTER instance to fully start before attempting connection
		// Wait for MASTER instance to fully start
		GD.Print("[LobbyStreamlined] Waiting 2s for master server to start...");
		await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);

		GD.Print("Attempting to connect to public IP.");
		GD.Print("Trying Public IP Address: " + PublicIP.ToString());
		System.Net.NetworkInformation.Ping ping = new System.Net.NetworkInformation.Ping();
		System.Net.NetworkInformation.PingOptions po = new System.Net.NetworkInformation.PingOptions();
		po.DontFragment = true;
		string data = "HELLLLOOOOO!";
		byte[] buffer = ASCIIEncoding.ASCII.GetBytes(data);
		int timeout = 500;
		System.Net.NetworkInformation.PingReply pr = ping.Send(PublicIP, timeout, buffer, po);
		await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
		GD.Print("Ping Return: " + pr.Status.ToString());

		if (pr.Status == System.Net.NetworkInformation.IPStatus.Success)
		{
			GD.Print("The public IP responded with a roundtrip time of: " + pr.RoundtripTime);
			UsePublic = true;
			LobbyServerIP = PublicIP;
		}
		else
		{
			GD.Print("The public IP failed to respond");
			if (!UsePublic)
			{
				GD.Print("Trying Private Address: " + PrivateIP.ToString());
				pr = ping.Send(PrivateIP, timeout, buffer, po);
				await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
				GD.Print("Ping Return: " + pr.Status.ToString());
				if (pr.Status.ToString() == "Success")
				{
					GD.Print("The Private IP responded with a roundtrip time of: " + pr.RoundtripTime);
					UsePrivate = true;
					LobbyServerIP = PrivateIP;
				}
				else
				{
					LobbyServerIP = "127.0.0.1";
					GD.Print("The Private IP failed to respond");
					UsePrivate = false;
				}
			}
		}

		// Use async version that waits for full ENet handshake
		if (await JoinLobbyServerAsync() != Error.Ok)
		{
			LobbyServerIP = "127.0.0.1";
			await JoinLobbyServerAsync();
		}
	}

	// ── Async lobby connect — waits for full ENet handshake ───────────────
	private async Task<Error> JoinLobbyServerAsync()
	{
		GD.Print($"LOBBY Attempting to connect to {LobbyServerIP}:{PortMinimum}");
		AgentPeer = new ENetMultiplayerPeer();
		Error error = AgentPeer.CreateClient(LobbyServerIP, PortMinimum);
		AgentAPI.MultiplayerPeer = AgentPeer;

		if (error != Error.Ok)
		{
			GD.PrintErr($"LOBBY CreateClient failed: {error}");
			return error;
		}

		// Wait for actual ENet handshake to complete (up to 5 seconds)
		float waited = 0f;
		while (AgentPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected && waited < 5f)
		{
			await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
			waited += 0.1f;
		}

		if (AgentPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected)
		{
			GD.PrintErr("LOBBY: Failed to confirm connection to master after 5s.");
			return Error.Failed;
		}

		GD.Print("Connected to MASTER (confirmed handshake)");
		IsWanLobbyConnected = true;
		return Error.Ok;
	}

	// ── Sync version — only used for game servers connecting to localhost ──
	private Error JoinLobbyServer()
	{
		GD.Print($"LOBBY Attempting to connect to {LobbyServerIP}:{PortMinimum}");
		AgentPeer = new ENetMultiplayerPeer();
		Error error = AgentPeer.CreateClient(LobbyServerIP, PortMinimum);
		AgentAPI.MultiplayerPeer = AgentPeer;
		if (error != Error.Ok)
			return error;
		GD.Print("Connected to MASTER");
		IsWanLobbyConnected = true;
		return Error.Ok;
	}

	public Error CreateMasterServer()
	{
		GD.Print("Attempting to create lobby system at port: " + PortMinimum);
		AgentPeer = new ENetMultiplayerPeer();
		Error err = AgentPeer.CreateServer(PortMinimum, 1000);
		AgentAPI.MultiplayerPeer = AgentPeer;
		if (err != Error.Ok)
		{
			GD.Print(err.ToString());
			return err;
		}
		GD.Print("Master Server Created!");
		IsWanLobbyConnected = true;
		IsWanLobbyServer = true;
		return Error.Ok;
	}

	// ── Create Game — waits for connection if not ready yet ───────────────
	public async void CreatNewGameServer()
	{
		if (GameNameBox.Text.Length < 2)
		{
			GD.PrintErr("[LobbyStreamlined] Game name too short.");
			return;
		}

		if (IsWanLobbyServer)
		{
			GD.PrintErr("[LobbyStreamlined] This instance IS the master server.");
			return;
		}

		// Wait up to 5 seconds for ENet handshake to be fully ready
		float waited = 0f;
		while ((AgentAPI.MultiplayerPeer == null ||
				AgentAPI.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected)
			   && waited < 5f)
		{
			GD.Print("[LobbyStreamlined] Waiting for lobby connection...");
			await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
			waited += 0.2f;
		}

		if (AgentAPI.MultiplayerPeer == null ||
			AgentAPI.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected)
		{
			GD.PrintErr("[LobbyStreamlined] Timed out waiting for lobby connection. Check master server.");
			return;
		}

		GD.Print("[LobbyStreamlined] Requesting game server creation...");
		RpcId(1, "ProcessSpawnServerSide", GameNameBox.Text.Replace(' ', '-').Replace('\n', '-').Replace('#', '-'));
		WaitForGameToStart(portOffset);
	}

	public async void WaitForGameToStart(int p)
	{
		GenericCore.Instance.SetPort((p + PortMinimum).ToString());
		GenericCore.Instance.SetIP(LobbyServerIP);
		while (p == portOffset)
		{
			await ToSignal(GetTree().CreateTimer(.1f), SceneTreeTimer.SignalName.Timeout);
		}
		await ToSignal(GetTree().CreateTimer(2.5f), SceneTreeTimer.SignalName.Timeout);
		GenericCore.Instance.JoinGame();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		AgentAPI.Poll();

		// Guard: stop processing if node is no longer in the tree
		if (!IsInsideTree()) return;
		if (GenericCore.Instance == null) return;
		if (GetChildCount() == 0) return;

		Node child0 = GetChild(0);
		if (child0 == null) return;

		if (!IsWanLobbyServer)
			UpdateVBoxChildren((VBoxContainer)GetNode(AgentSpawner.GetPath() + "/" + AgentSpawner.SpawnPath));
		{
			Node spawnerNode = GetNodeOrNull(AgentSpawner.GetPath() + "/" + AgentSpawner.SpawnPath);
			if (spawnerNode is VBoxContainer vbox)
				UpdateVBoxChildren(vbox);
		}

		// Only toggle the lobby list UI — never override GameRoot children visibility.
		// StartGame() in MainMenuLobby handles the 2D→3D switch itself.
		bool lobbyVisible = !(GenericCore.Instance.IsGenericCoreConnected || IsWanLobbyServer);
		((Control)GetChild(0)).Visible = lobbyVisible;
		if (child0 is Control c0) c0.Visible = lobbyVisible;
	}

	private void UpdateVBoxChildren(VBoxContainer vbox)
	{
		foreach (Node c in vbox.GetChildren())
		{
			if (c is Control child)
			{
				Button btn = child.GetNode<Button>("Button");
			}
		}
		vbox.QueueSort();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ProcessSpawnServerSide(string n)
	{
		if (IsWanLobbyServer)
		{
			try
			{
				System.Diagnostics.Process proc = new System.Diagnostics.Process();
				proc.StartInfo.UseShellExecute = true;
				proc.StartInfo.FileName = OS.GetExecutablePath();
				proc.StartInfo.Arguments += "--headless GAMESERVER " + (PortMinimum + portOffset) + " GAMENAME#" + n + " > " + n + ".log";
				GD.Print("Starting Game Server With: " + proc.StartInfo.Arguments);
				portOffset++;
				Rpc("UpdatePortOffset", portOffset);
				proc.Start();
				if (MaxGameTime > 0)
					GameMonitor(proc);
			}
			catch (System.Exception e)
			{
				GD.Print("EXCEPTION - in creating a game!!! - " + e.ToString());
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void UpdatePortOffset(int p)
	{
		if (!IsWanLobbyServer)
			portOffset = p;
	}

	public async void GameMonitor(System.Diagnostics.Process proc)
	{
		await ToSignal(GetTree().CreateTimer(MaxGameTime), SceneTreeTimer.SignalName.Timeout);
		if (!proc.HasExited)
			proc.Kill();
	}

	public void DisconnectFromLobbySystem()
	{
		if (AgentAPI.MultiplayerPeer != null)
		{
			GD.Print("Disconnecting from ENet session<Lobby>");
			AgentAPI.MultiplayerPeer.Close();
			AgentAPI.MultiplayerPeer = null;
		}
	}
}
