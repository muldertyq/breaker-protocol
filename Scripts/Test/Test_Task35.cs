using Godot;
using BreakerProtocol.Audio;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-35 交互式验证场景：5 级动态混音与 Low-pass 战术聚焦降压演练场
	/// </summary>
	public partial class Test_Task35 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private AudioManager _audio = null!;
		private RichTextLabel _hudLabel = null!;

		private bool _isTacticalFocusActive = false;
		private float _simulatedBgmTimer = 0.0f;

		public override void _Ready()
		{
			// 1. 初始化音频总控
			_audio = new AudioManager { Name = "AudioManager" };
			AddChild(_audio);

			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 2. 生成玩家战舰
			_playerShip = new ShipEntity
			{
				Name = "PlayerCruiser",
				Position = Vector2.Zero
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 3. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateUI();
		}

		private void CreateUI()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(30, 20),
				Size = new Vector2(1220, 180),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 14);
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;

			// 模拟背景引擎脉冲低音循环
			_simulatedBgmTimer += dt;
			if (_simulatedBgmTimer >= 0.8f)
			{
				_simulatedBgmTimer = 0.0f;
				_audio.PlaySfx(SoundType.EngineThrust, 0.02f, AudioManager.BusBgm);
			}

			// [按住 F 键]: 开启战地飞线 + 触发 Low-pass 400Hz 沉浸真空低通滤波
			bool isHotwiring = Input.IsKeyPressed(Key.F);
			if (isHotwiring != _isTacticalFocusActive)
			{
				_isTacticalFocusActive = isHotwiring;
				_audio.SetTacticalFocusLowPass(_isTacticalFocusActive, 400.0f);
				if (_isTacticalFocusActive)
				{
					_audio.PlaySfx(SoundType.HotwireConnect, 0.05f, AudioManager.BusUI);
				}
			}

			// [鼠标左键]: 开火动能机枪点射
			if (Input.IsMouseButtonPressed(MouseButton.Left) && !isHotwiring)
			{
				if (Engine.GetProcessFrames() % 8 == 0)
				{
					_audio.PlaySfx(SoundType.ShootKinetic, 0.1f);
					foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
					{
						_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
					}
				}
			}

			// [按 1 键]: 触发等离子电浆主炮 (自动触发 Ducking 降压)
			if (Input.IsKeyPressed(Key.Key1))
			{
				_audio.PlaySfx(SoundType.ShootPlasma, 0.05f);
				_juice.TriggerExplosionJuice(_playerShip.GlobalPosition, 0.6f);
			}
			// [按 2 键]: 触发装甲大倾角跳弹金属鸣响
			else if (Input.IsKeyPressed(Key.Key2))
			{
				_audio.PlaySfx(SoundType.Ricochet, 0.12f);
				_vfx.SpawnModuleExplosion(_playerShip.GlobalPosition, new Vector2(20, 20), Colors.LightSkyBlue, 8);
				_vfx.SpawnFloatingText(_playerShip.GlobalPosition, "⚡ 大倾角跳弹！", Colors.Cyan);
			}
			// [按 3 键]: 触发巨型战舰殉爆大爆炸 (自动触发 Ducking 降压)
			else if (Input.IsKeyPressed(Key.Key3))
			{
				_audio.PlaySfx(SoundType.ExplosionHuge, 0.05f);
				_juice.TriggerExplosionJuice(_playerShip.GlobalPosition, 1.8f);
				_vfx.SpawnModuleExplosion(_playerShip.GlobalPosition, new Vector2(100, 100), Colors.OrangeRed, 32);
				_vfx.SpawnFloatingText(_playerShip.GlobalPosition, "💥 核心殉爆！", Colors.OrangeRed);
			}
			// [按 4 键]: 触发主动爆甲冲击波
			else if (Input.IsKeyPressed(Key.Key4))
			{
				_audio.PlaySfx(SoundType.AblativeDetonate, 0.05f);
				_audio.SetTacticalFocusLowPass(true, 500.0f);
				_vfx.SpawnFloatingText(_playerShip.GlobalPosition, "💥 战术过载爆甲！", Colors.Yellow);
			}
			// [按 5 键]: 触发动力炉熔毁高危警报
			else if (Input.IsKeyPressed(Key.Key5))
			{
				_audio.PlaySfx(SoundType.WarningAlarm, 0.02f, AudioManager.BusUI);
				_vfx.SpawnFloatingText(_playerShip.GlobalPosition, "🚨 动力炉过热熔断告警！", Colors.Red);
			}

			// [松开 4 键复位滤波]:
			if (!Input.IsKeyPressed(Key.Key4) && !_isTacticalFocusActive)
			{
				_audio.SetTacticalFocusLowPass(false);
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			float cutoffHz = _audio.GetCurrentCutoffHz();
			float duckingDb = _audio.GetCurrentBgmDuckingDb();

			string focusTag = cutoffHz < 1000.0f
				? $"[color=red]🚨 战术真空聚焦已激活 (Low-Pass: {cutoffHz:F0} Hz 沉闷空间音效)[/color]"
				: $"[color=green]🟢 全频段通畅 (Full Spectrum: {cutoffHz:F0} Hz)[/color]";

			string duckTag = duckingDb < -2.0f
				? $"[color=orange]⚠️ 动态降压中 (Audio Ducking: {duckingDb:F1} dB)[/color]"
				: $"[color=cyan]正常输出 ({duckingDb:F1} dB)[/color]";

			DisplayServer.WindowSetTitle($"《断路协议》| 音频混音总控 | 帧率: {fps:F0} FPS | 滤波: {cutoffHz:F0} Hz | Ducking: {duckingDb:F1} dB");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-35 5级动态音频混音与 Low-pass 战术聚焦演练场】[/color][/b]\n" +
							 $"• 低通滤波状态: {focusTag} | 动态降压 (Ducking): {duckTag}\n" +
							 $"• 5 级总线拓扑: [color=cyan]Master ──► [ BGM / SFX (挂载 LowPass) / Engine / UI ][/color]\n" +
							 $"------------------------------------------------------------------------------------\n" +
							 $"[音效与降压调试热键]:\n" +
							 $"• [按住 F 键]: [b][color=white]战地应急飞线 ── 瞬切 400Hz 真空低通滤波 (体验深空窒息沉浸感)[/color][/b]\n" +
							 $"• [鼠标左键]: 动能机枪点射 | [按 1 键]: 等离子电浆炮 (触发降压)\n" +
							 $"• [按 2 键]: 大倾角装甲跳弹鸣响 | [按 3 键]: 巨型要塞核心殉爆大爆炸\n" +
							 $"• [按 4 键]: 战术主动爆甲冲击波 | [按 5 键]: 动力炉高危熔断警报蜂鸣";
		}
	}
}
