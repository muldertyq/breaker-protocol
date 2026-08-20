using Godot;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Combat
{
	/// <summary>
	/// 靶场受击测试假人目标
	/// </summary>
	[GlobalClass]
	public partial class TargetDummy : Area2D
	{
		[Export] public float MaxHp { get; set; } = 1500.0f;
		public float CurrentHp { get; private set; }

		private float _flashTimer = 0.0f;
		private Label _hpLabel = null!;

		public override void _Ready()
		{
			CurrentHp = MaxHp;

			// 1. 创建圆形碰撞箱 (半径 32px)
			var colShape = new CollisionShape2D
			{
				Shape = new CircleShape2D { Radius = 32.0f }
			};
			AddChild(colShape);

			// 2. 创建血量显示标签
			_hpLabel = new Label
			{
				Position = new Vector2(-40, -50),
				Size = new Vector2(80, 20),
				HorizontalAlignment = HorizontalAlignment.Center
			};
			_hpLabel.AddThemeFontSizeOverride("font_size", 14);
			_hpLabel.AddThemeColorOverride("font_color", Colors.White);
			AddChild(_hpLabel);

			UpdateLabel();
		}

		public void TakeDamage(float damage, ElementFlags elements)
		{
			CurrentHp = Mathf.Max(0.0f, CurrentHp - damage);
			_flashTimer = 0.15f; // 受击白光闪烁
			UpdateLabel();

			GD.PrintRich($"[color=red][TargetDummy] 受击！受到 {damage:F0} 点伤害 (附带属性: {elements})，剩余 HP: {CurrentHp:F0}/{MaxHp:F0}[/color]");

			if (CurrentHp <= 0.0f)
			{
				// 击破自愈复活（保持靶场可循环测试）
				CurrentHp = MaxHp;
				UpdateLabel();
			}
		}

		public override void _Process(double delta)
		{
			if (_flashTimer > 0.0f)
			{
				_flashTimer -= (float)delta;
				QueueRedraw();
			}
		}

		private void UpdateLabel()
		{
			_hpLabel.Text = $"{CurrentHp:F0}/{MaxHp:F0}";
		}

		public override void _Draw()
		{
			Color bodyColor = _flashTimer > 0.0f ? Colors.White : new Color(0.8f, 0.2f, 0.2f, 0.85f);
			DrawCircle(Vector2.Zero, 32.0f, bodyColor);
			DrawArc(Vector2.Zero, 32.0f, 0, Mathf.Tau, 32, Colors.Yellow, 2.0f);
		}
	}
}
