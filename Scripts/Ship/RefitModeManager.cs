using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Ship.Validation;
using BreakerProtocol.UI.Refit;

namespace BreakerProtocol.Ship
{
	/// <summary>
	/// 战斗态 ↔ Tab 装配态模式切换管理器
	/// </summary>
	public partial class RefitModeManager : Node
	{
		private ShipEntity _ship = null!;
		private CombatCameraController? _camera;
		private RefitCanvasUI _refitUI = null!;

		public bool IsRefitModeActive => _refitUI != null && _refitUI.Visible;

		public void Setup(ShipEntity ship, CombatCameraController? camera)
		{
			_ship = ship;
			_camera = camera;

			// 创建置顶的 UI CanvasLayer (层级 100)
			var canvasLayer = new CanvasLayer { Layer = 100 };
			AddChild(canvasLayer);

			_refitUI = new RefitCanvasUI();
			canvasLayer.AddChild(_refitUI);
			_refitUI.Initialize(_ship);
			_refitUI.Hide();
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
			{
				// 按 Tab 键切换装配模式
				if (keyEvent.Keycode == Key.Tab)
				{
					ToggleRefitMode();
				}
			}
		}

		public void ToggleRefitMode()
		{
			if (IsRefitModeActive)
			{
				// 退出装配态：执行安全校验与物理实装
				var report = ShipValidator.Validate(_ship.Grid, _ship.Graph);
				if (report.IsAllPassed)
				{
					_ship.RebuildPhysics();
					_refitUI.Hide();
					_camera?.SetOverrideZoom(false);
					GetTree().Paused = false;
					GD.PrintRich("[color=green][RefitMode] 退出改装模式，恢复战斗！[/color]");
				}
				else
				{
					GD.PrintErr("[RefitMode] 当前装配未通过安全校验，无法启航！请修正红灯错误项。");
				}
			}
			else
			{
				// 进入装配态：暂停游戏，拉近镜头，打开 UI
				GetTree().Paused = true;
				_camera?.SetOverrideZoom(true, 1.5f);
				_refitUI.Show();
				_refitUI.Initialize(_ship);
				GD.PrintRich("[color=cyan][RefitMode] 进入全屏装配改装模式 (游戏已暂停)...[/color]");
			}
		}
	}
}
