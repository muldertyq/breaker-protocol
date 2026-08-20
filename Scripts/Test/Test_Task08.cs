using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-08 验证场景：全功能 Tab 装配台、动态增删改查与物理合法性校验
	/// </summary>
	public partial class Test_Task08 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private RefitModeManager _refitManager = null!;

		public override void _Ready()
		{
			// 确保本节点在游戏暂停时依然能接收输入
			ProcessMode = ProcessModeEnum.Always;

			// 1. 创建飞船实体
			_playerShip = new ShipEntity
			{
				Name = "PlayerShip",
				Position = new Vector2(640, 360)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			// 2. 组装初始基础战舰
			BuildInitialShip();

			// 3. 创建摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);

			// 4. 创建并初始化 Tab 装配管理器
			_refitManager = new RefitModeManager();
			AddChild(_refitManager);
			_refitManager.Setup(_playerShip, _camera);
		}

		private void BuildInitialShip()
		{
			_playerShip.Grid.Clear();

			// 动力堆 (2x2, -1, 0)
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			_playerShip.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 0), rotation: 0, out _);

			// 冷凝舱 (2x2, -1, -2)
			var cryoDef = DataManager.Instance.Modules.Get("hf_mod_cryo_chamber");
			_playerShip.Grid.TryPlaceModule(cryoDef, new Vector2I(-1, -2), rotation: 0, out _);

			// 磁轨主炮 (3x1, -1, -3)
			var gunDef = DataManager.Instance.Modules.Get("hf_wep_railgun_h");
			_playerShip.Grid.TryPlaceModule(gunDef, new Vector2I(-1, -3), rotation: 0, out _);

			// 泰坦主推 (3x2, -1, 2)
			var engineDef = DataManager.Instance.Modules.Get("hf_eng_titan_main");
			_playerShip.Grid.TryPlaceModule(engineDef, new Vector2I(-1, 2), rotation: 0, out _);

			_playerShip.RebuildPhysics();
		}
	}
}
