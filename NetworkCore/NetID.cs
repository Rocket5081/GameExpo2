using Godot;

[GlobalClass]
[Tool]
public partial class NetID : MultiplayerSynchronizer
{
	[Export] public bool IsLocal;
	[Export] public bool IsServer;
	[Export] public long OwnerId;
	[Export] public uint netObjectID;
	[Export] public NetworkCore _myNetworkCore;
	[Export] public bool IsNetworkReady = false;
	[Export] public bool IsSynced = false;

	
	[Signal]
	public delegate void NetIDReadyEventHandler();
	public override void _EnterTree()
{
	base._EnterTree();
	Name = "MultiplayerSynchronizer";

	if (ReplicationConfig == null)
	{
		GD.Print("No replication config found, creating one.");
		ReplicationConfig = new SceneReplicationConfig();
	}

	var config = ReplicationConfig as SceneReplicationConfig;
	if (config == null)
	{
		GD.PushError("ReplicationConfig is not a SceneReplicationConfig!");
		return;
	}

	if (!config.HasProperty("MultiplayerSynchronizer:IsNetworkReady"))
		config.AddProperty("MultiplayerSynchronizer:IsNetworkReady");
	if (!config.HasProperty("MultiplayerSynchronizer:IsSynced"))
		config.AddProperty("MultiplayerSynchronizer:IsSynced");
	if (!config.HasProperty("MultiplayerSynchronizer:OwnerId"))
		config.AddProperty("MultiplayerSynchronizer:OwnerId");

	// If OwnerId was pre-set before AddChild, apply authority here
	// This is the ONLY safe place Godot allows setting synchronizer authority
	if (OwnerId != 0 && Multiplayer != null && Multiplayer.MultiplayerPeer != null)
		SetMultiplayerAuthority((int)OwnerId);
}

	public override void _Ready()
	{
		base._Ready();
		Synchronized += NetID_Synchronized;
		slowStart();
		
	}

	private void NetID_Synchronized()
	{
		IsSynced = true;
	}

	public async void slowStart()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		while (GenericCore.Instance == null || !GenericCore.Instance.IsGenericCoreConnected)
		{
			await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);

		}
		if(GenericCore.Instance.IsServer && OwnerId ==0)
		{
			OwnerId = 1;
			// Server's unique ID is always 1
			IsLocal = (Multiplayer.GetUniqueId() == 1);
			SetMultiplayerAuthority(1); // 1 = server
			IsNetworkReady = true;
		}
	   //There is a problem with this ---- There is no way to know if it was created by spawner or 
	   //Drag and Drop.
		await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
		if (!GenericCore.Instance.IsServer)
		{
			// Poll at 100 ms so we detect IsSynced within one tick, not up to 1 s late.
			// (The old 1 s interval caused up to 1 s of timer skew between clients.)
			for (int i = 0; i < 100; i++)
			{
				await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
				if (IsSynced) break;
			}

			if (!IsSynced)
			{
				// IsSynced never fired via the synchronizer.
				// The Initialize RPC may still have arrived and stamped OwnerId — check that first.
				if (OwnerId != 0)
				{
					// We have enough info to continue — don't delete the player.
					IsLocal = (Multiplayer.GetUniqueId() == OwnerId);
					IsNetworkReady = true;
				}
				else
				{
					GD.Print("Deleting the inscene object: " + GetParent().Name);
					GetParent().QueueFree();
				}
			}
			else
			{
				// IsSynced = true. OwnerId arrives in the same sync packet normally,
				// but poll briefly in case it lags by one more cycle.
				for (int retry = 0; retry < 10 && OwnerId == 0; retry++)
					await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);

				// Derive IsLocal from the synced OwnerId.
				if (OwnerId != 0)
					IsLocal = (Multiplayer.GetUniqueId() == OwnerId);

				IsNetworkReady = true;
			}
		}
	   
		EmitSignalNetIDReady();
		//Emit a signal.
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void Initialize(long peerIdOwner)
	{
	   
		OwnerId = peerIdOwner;
		if (peerIdOwner == 1)
			IsServer = true;
		// Use GetUniqueId() — the only reliable way to know if this peer owns this object
		if (Multiplayer.GetUniqueId() == OwnerId){
			IsLocal = true;
		}
			
	}

	~NetID()
	{

		if (Multiplayer.IsServer())
		{
			GD.Print("Destroying a network object from the destructor. "+Name);
			_myNetworkCore.NetDestroyObject(this);
		}
	}


	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public async void ManualDelete()
	{
		GD.Print("Trying to remote destroy an object: " + GetParent().Name);
		if(ReplicationConfig != null)
		{
			try
			{
				ReplicationConfig = null;
			}
			catch {//Stop stupid chatty error
				   }
		}
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		GetParent().QueueFree();
	}
}
