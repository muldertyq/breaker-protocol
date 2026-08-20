using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Ship
{
	/// <summary>
	/// 独立太空漂流残骸物理实体 (支持燃烧尾烟与火星抛射)
	/// </summary>
	public partial class DebrisEntity : RigidBody2D
	{
		public List<ModuleInstance> SeveredModules { get; } = new();
		private Vector2 _localComGrid = Vector2.Zero;
		private float _lifeTimer = 0.0f;
		private const float MaxLifeTime = 25.0f;
		private float _fadeAlpha = 1.0f;

		private float _smokeEmitTimer = 0.0f;

		public void Initialize(
			List<ModuleInstance> modules, 
			Vector2 orphanComGrid, 
			Vector2 initialVelocity, 
			float initialAngularVelocity, 
			Vector2 separationImpulse,
			ShipEntity motherShip)
		{
			SeveredModules.AddRange(modules);
			_localComGrid = orphanComGrid;

			GravityScale = 0.0f;
			LinearDamp = 0.05f;
			AngularDamp = 0.15f;

			CenterOfMassMode = CenterOfMassModeEnum.Custom;
			CenterOfMass = Vector2.Zero;

			float totalMass = 0.0f;
			foreach (var m in SeveredModules)
			{
				totalMass += m.Definition.Mass;
			}
			Mass = Mathf.Max(1.0f, totalMass);

			foreach (var m in SeveredModules)
			{
				Vector2I size = m.GetRotatedSize();
				Vector2 localGrid = (Vector2)m.GridPosition - _localComGrid;
				Vector2 center = GlobalMetrics.MetersToPixels(localGrid + (Vector2)size * 0.5f);
				Vector2 shapeSize = GlobalMetrics.MetersToPixels(new Vector2(size.X, size.Y));

				var colShape = new CollisionShape2D
				{
					Shape = new RectangleShape2D { Size = shapeSize },
					Position = center
				};
				AddChild(colShape);
			}

			AddCollisionExceptionWith(motherShip);
			motherShip.AddCollisionExceptionWith(this);

			LinearVelocity = initialVelocity + separationImpulse;
			AngularVelocity = initialAngularVelocity + (float)GD.RandRange(-3.5, 3.5);

			ZIndex = 8;
			AddToGroup("Debris");
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;
			_lifeTimer += dt;

			// 尾迹浓烟定时发射 (每 0.06s 抛出烟雾)
			_smokeEmitTimer += dt;
			if (_smokeEmitTimer >= 0.06f && VfxManager.Instance != null && _lifeTimer < 10.0f)
			{
				_smokeEmitTimer = 0.0f;
				Vector2 smokeVel = -LinearVelocity * 0.25f + new Vector2((float)GD.RandRange(-20, 20), (float)GD.RandRange(-20, 20));
				VfxManager.Instance.SpawnSmoke(GlobalPosition, smokeVel, maxRadius: 10.0f, isFire: GD.Randf() > 0.5f);
			}

			if (_lifeTimer >= MaxLifeTime - 3.0f)
			{
				_fadeAlpha = Mathf.Clamp((MaxLifeTime - _lifeTimer) / 3.0f, 0.0f, 1.0f);
			}

			if (_lifeTimer >= MaxLifeTime)
			{
				QueueFree();
				return;
			}

			QueueRedraw();
		}

		public override void _Draw()
		{
			float pxPerUnit = GlobalMetrics.PixelsPerMeter;

			foreach (var m in SeveredModules)
			{
				Color moduleColor = m.Definition.Category switch
				{
					"Weapon"   => new Color(0.65f, 0.25f, 0.25f, _fadeAlpha * 0.75f),
					"Armor"    => new Color(0.35f, 0.35f, 0.40f, _fadeAlpha * 0.75f),
					"Thruster" => new Color(0.65f, 0.35f, 0.1f, _fadeAlpha * 0.75f),
					_          => new Color(0.45f, 0.45f, 0.45f, _fadeAlpha * 0.75f)
				};

				foreach (var cellPos in m.GetOccupiedGridCells())
				{
					Vector2 localCell = (Vector2)cellPos - _localComGrid;
					Vector2 screenPos = GlobalMetrics.MetersToPixels(localCell);

					DrawRect(new Rect2(screenPos + Vector2.One, new Vector2(pxPerUnit - 2, pxPerUnit - 2)), moduleColor, filled: true);
					DrawRect(new Rect2(screenPos, new Vector2(pxPerUnit, pxPerUnit)), new Color(0.1f, 0.1f, 0.1f, _fadeAlpha * 0.9f), filled: false, width: 1.0f);
				}
			}
		}
	}
}
