using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship.Physics;
using BreakerProtocol.Ship.Pipeline;
using BreakerProtocol.Ship.Thermal;
using BreakerProtocol.Ship.Hotwire;
using BreakerProtocol.Ship.Abilities;
using BreakerProtocol.Ship.AI;
using BreakerProtocol.Combat.Armor;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Weapons;

namespace BreakerProtocol.Ship
{
	/// <summary>
	/// 物理战舰实体 (全面支持 2D DDA 创伤、战地飞线与敌方三大流派行为树 AI)
	/// </summary>
	[GlobalClass]
	public partial class ShipEntity : RigidBody2D
	{
		public ShipGrid Grid { get; } = new();
		public StructuralGraph Graph { get; } = new();
		public PipelineNetwork Pipeline { get; } = new();
		public ShipPhysicsData PhysicsData { get; private set; }

		public FlightController Flight { get; private set; } = null!;
		public PulseSimulator Pulses { get; private set; } = null!;
		public WeaponTurretController Turrets { get; private set; } = null!;
		public BioArmorRegenSystem BioRegen { get; private set; } = null!;
		public ThermalSystem Thermal { get; private set; } = null!;
		public HotwireController Hotwire { get; private set; } = null!;
		public AblativeDetonationController AblativeDetonation { get; private set; } = null!;
		public EnemyAiController? AI { get; private set; }

		private ShipPalette _currentPalette = FactionPalettes.HeavyFoundry;
		public ShipPalette CurrentPalette
		{
			get => _currentPalette;
			set
			{
				_currentPalette = value;
				UpdatePaletteMaterial();
				QueueRedraw();
			}
		}

		private ShaderMaterial _shipShaderMaterial = null!;
		private readonly List<CollisionShape2D> _collisionShapes = new();
		private float _damageSmokeTimer = 0.0f;

		public override void _Ready()
		{
			GravityScale = 0.0f;
			CenterOfMassMode = CenterOfMassModeEnum.Custom;

			Flight = new FlightController(this);
			Pulses = new PulseSimulator(this);
			Turrets = new WeaponTurretController(this);
			BioRegen = new BioArmorRegenSystem(this);
			Thermal = new ThermalSystem(this);
			Hotwire = new HotwireController(this);
			AblativeDetonation = new AblativeDetonationController(this);

			InitShaderMaterial();

			Pulses.OnWeaponFired += (wId, p) =>
			{
				Thermal.AddHeat(12.0f);

				var status = ElementalSynthesisMatrix.GetOrCreateStatus(this);
				if (status.HasEntropyCurse)
				{
					var mod = Grid.Modules.FirstOrDefault();
					if (mod != null)
					{
						mod.CurrentHp = Mathf.Max(0.0f, mod.CurrentHp - 100.0f);
						OnModuleDamaged(mod, 100.0f);
						VfxManager.Instance?.SpawnElectricArc(GlobalPosition, GlobalPosition + new Vector2(30, 30), Colors.Purple);
						GD.PrintRich("[color=purple]⚡ [熵增反噬] 敌舰开火受到 EMP 炸膛，内部机组损毁 -100 HP！[/color]");
					}
				}
			};
		}

		/// <summary>
		/// 为该战舰挂载指定战术流派的 AI 决策中枢
		/// </summary>
		public void AttachAI(AiArchetype archetype, Node2D? initialTarget = null)
		{
			AI = new EnemyAiController(this, archetype)
			{
				CurrentTarget = initialTarget
			};
		}

		private void InitShaderMaterial()
		{
			var shader = GD.Load<Shader>("res://Shaders/ship_palette_damage.gdshader");
			_shipShaderMaterial = new ShaderMaterial { Shader = shader };
			Material = _shipShaderMaterial;
			UpdatePaletteMaterial();
		}

		private void UpdatePaletteMaterial()
		{
			if (_shipShaderMaterial == null) return;
			_shipShaderMaterial.SetShaderParameter("u_primary_color", _currentPalette.PrimaryColor);
			_shipShaderMaterial.SetShaderParameter("u_secondary_color", _currentPalette.SecondaryColor);
			_shipShaderMaterial.SetShaderParameter("u_accent_color", _currentPalette.AccentColor);
			_shipShaderMaterial.SetShaderParameter("u_raw_metal_color", _currentPalette.RawMetalColor);
		}

		public void RebuildPhysics()
		{
			PhysicsData = CenterOfMassSolver.Solve(Grid);

			Mass = PhysicsData.TotalMass;
			CenterOfMass = PhysicsData.CenterOfMassPixels;
			Inertia = PhysicsData.MomentOfInertia * (GlobalMetrics.PixelsPerMeter * GlobalMetrics.PixelsPerMeter);

			RebuildCollisionShapes();
			Graph.RebuildGraph(Grid);
			Flight?.RefreshThrusterCapabilities();
			Pulses?.RebuildBuffers();

			QueueRedraw();
		}

		private void RebuildCollisionShapes()
		{
			foreach (var shape in _collisionShapes)
			{
				shape.QueueFree();
			}
			_collisionShapes.Clear();

			foreach (var module in Grid.Modules)
			{
				if (module.IsDestroyed) continue;

				Vector2I size = module.GetRotatedSize();
				Vector2 center = GlobalMetrics.MetersToPixels(
					new Vector2(module.GridPosition.X + size.X * 0.5f, module.GridPosition.Y + size.Y * 0.5f)
				);
				Vector2 shapeSize = GlobalMetrics.MetersToPixels(new Vector2(size.X, size.Y));

				var colShape = new CollisionShape2D
				{
					Shape = new RectangleShape2D { Size = shapeSize },
					Position = center
				};

				AddChild(colShape);
				_collisionShapes.Add(colShape);
			}
		}

		public void OnModuleDamaged(ModuleInstance module, float damageDealt)
		{
			BioRegen.NotifyDamageTaken();

			if (module.IsDestroyed)
			{
				GD.PrintRich($"[color=red][ShipEntity] 💥 构件 [{module.Definition.Name}] 发生剧烈爆炸解体！[/color]");

				if (VfxManager.Instance != null)
				{
					Vector2I size = module.GetRotatedSize();
					Vector2 localCenter = GlobalMetrics.MetersToPixels(
						new Vector2(module.GridPosition.X + size.X * 0.5f, module.GridPosition.Y + size.Y * 0.5f)
					);
					Vector2 worldCenter = GlobalTransform * localCenter;
					Vector2 sizePixels = GlobalMetrics.MetersToPixels(new Vector2(size.X, size.Y));

					VfxManager.Instance.SpawnModuleExplosion(worldCenter, sizePixels, _currentPalette.PrimaryColor, shardCount: 18);
				}

				JuiceManager.Instance?.TriggerExplosionJuice(GlobalPosition, intensity: 1.0f);

				// 次生殉爆检查
				SympatheticDetonationEngine.CheckAndTrigger(this, module);

				// 清理相连导线
				Pipeline.RemoveWiresConnectedTo(module.InstanceId);

				foreach (var cellPos in module.GetOccupiedGridCells())
				{
					Pipeline.SeverWiresAt(cellPos);
				}

				Grid.RemoveModule(module.InstanceId);
				RebuildPhysics();
				HullSeveranceEngine.CheckAndSeverDisconnectedClusters(this);
			}
		}

		public IEnumerable<PinInstance> GetAllPins()
		{
			foreach (var module in Grid.Modules)
			{
				if (module.IsDestroyed) continue;

				foreach (var (pinDef, pinGridPos) in module.GetTransformedPins())
				{
					yield return new PinInstance(module.InstanceId, pinDef, pinGridPos);
				}
			}
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;

			if (IsInGroup("Player"))
			{
				Hotwire?.Update(dt);
				AblativeDetonation?.Update(dt);
			}
			else if (AI != null)
			{
				AI.Update(dt);
			}
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;
			
			var status = ElementalSynthesisMatrix.GetOrCreateStatus(this);
			status.Update(dt);

			if (!status.IsFrozen)
			{
				Flight?.PhysicsUpdate(delta);
			}
			else
			{
				LinearVelocity = LinearVelocity.Lerp(Vector2.Zero, dt * 5.0f);
			}

			Pulses?.Update(dt);
			BioRegen?.Update(dt);
			Thermal?.Update(dt);
			UpdateDamageVfx(dt);
			QueueRedraw();
		}

		private void UpdateDamageVfx(float dt)
		{
			if (VfxManager.Instance == null) return;

			_damageSmokeTimer += dt;
			if (_damageSmokeTimer >= 0.12f)
			{
				_damageSmokeTimer = 0.0f;

				foreach (var module in Grid.Modules)
				{
					if (module.IsDestroyed) continue;

					if (module.CurrentHp < module.MaxHp * 0.35f)
					{
						Vector2I size = module.GetRotatedSize();
						Vector2 localPos = GlobalMetrics.MetersToPixels(
							new Vector2(module.GridPosition.X + (float)GD.RandRange(0, size.X), module.GridPosition.Y + (float)GD.RandRange(0, size.Y))
						);
						Vector2 worldPos = GlobalTransform * localPos;

						VfxManager.Instance.SpawnSmoke(worldPos, -LinearVelocity * 0.15f, maxRadius: 8.0f, isFire: module.CurrentHp < module.MaxHp * 0.15f);

						if (GD.Randf() > 0.65f && (module.Definition.Category == "PowerSource" || module.Definition.Category == "Modifier" || module.Definition.Category == "Logic"))
						{
							Vector2 localPos2 = GlobalMetrics.MetersToPixels(
								new Vector2(module.GridPosition.X + (float)GD.RandRange(0, size.X), module.GridPosition.Y + (float)GD.RandRange(0, size.Y))
							);
							VfxManager.Instance.SpawnElectricArc(worldPos, GlobalTransform * localPos2, _currentPalette.AccentColor);
						}
					}
				}
			}
		}

		public override void _Draw()
		{
			float pxPerUnit = GlobalMetrics.PixelsPerMeter;

			// 1. 推进器尾焰
			var status = ElementalSynthesisMatrix.GetOrCreateStatus(this);
			if (!status.IsFrozen && Flight?.ThrustCapability.Thrusters != null)
			{
				foreach (var thruster in Flight.ThrustCapability.Thrusters)
				{
					if (thruster.CurrentThrottle > 0.05f)
					{
						DrawThrusterFlame(thruster);
					}
				}
			}

			// 2. 绘制构件实体
			float overheat = Thermal != null ? Thermal.OverheatRatio : 0.0f;
			_shipShaderMaterial.SetShaderParameter("u_overheat_ratio", overheat);

			foreach (var module in Grid.Modules)
			{
				Vector2I sizeGu = module.GetRotatedSize();
				Vector2 screenPos = GlobalMetrics.MetersToPixels(new Vector2(module.GridPosition.X, module.GridPosition.Y));
				Vector2 rectSize = GlobalMetrics.MetersToPixels(new Vector2(sizeGu.X, sizeGu.Y));

				var texture = ProceduralModuleTextureFactory.GetOrCreateModuleTexture(module.Definition.Category, sizeGu.X, sizeGu.Y);

				float hpRatio = Mathf.Clamp(module.CurrentHp / module.MaxHp, 0.0f, 1.0f);
				float scorch = (1.0f - hpRatio) * 0.7f;

				_shipShaderMaterial.SetShaderParameter("u_health_ratio", hpRatio);
				_shipShaderMaterial.SetShaderParameter("u_damage_scorch", scorch);

				DrawTextureRect(texture, new Rect2(screenPos, rectSize), tile: false);

				// 重瘫状态警告斜条纹
				if (hpRatio < 0.25f && hpRatio > 0.0f)
				{
					DrawLine(screenPos, screenPos + rectSize, new Color(1.0f, 0.2f, 0.2f, 0.75f), 2.0f);
				}
			}

			// 3. 绘制 PCB 导线与战地应急飞线
			foreach (var wire in Pipeline.Wires)
			{
				if (wire.GridPath.Count < 2) continue;

				if (wire.IsSevered)
				{
					for (int i = 0; i < wire.GridPath.Count - 1; i++)
					{
						Vector2 p1 = GlobalMetrics.MetersToPixels(new Vector2(wire.GridPath[i].X + 0.5f, wire.GridPath[i].Y + 0.5f));
						Vector2 p2 = GlobalMetrics.MetersToPixels(new Vector2(wire.GridPath[i + 1].X + 0.5f, wire.GridPath[i + 1].Y + 0.5f));
						DrawLine(p1, p2, new Color(1.0f, 0.25f, 0.25f, 0.85f), 2.5f);
					}

					int midIdx = wire.GridPath.Count / 2;
					Vector2 midPos = GlobalMetrics.MetersToPixels(new Vector2(wire.GridPath[midIdx].X + 0.5f, wire.GridPath[midIdx].Y + 0.5f));
					float xSize = 5.0f;
					DrawLine(midPos + new Vector2(-xSize, -xSize), midPos + new Vector2(xSize, xSize), Colors.Crimson, 2.5f);
					DrawLine(midPos + new Vector2(-xSize, xSize), midPos + new Vector2(xSize, -xSize), Colors.Crimson, 2.5f);
				}
				else if (wire.IsHotwire)
				{
					for (int i = 0; i < wire.GridPath.Count - 1; i++)
					{
						Vector2 p1 = GlobalMetrics.MetersToPixels(new Vector2(wire.GridPath[i].X + 0.5f, wire.GridPath[i].Y + 0.5f));
						Vector2 p2 = GlobalMetrics.MetersToPixels(new Vector2(wire.GridPath[i + 1].X + 0.5f, wire.GridPath[i + 1].Y + 0.5f));
						DrawDashedLine(p1, p2, Colors.Gold, 2.5f, dashLength: 4.0f);
					}
				}
				else
				{
					for (int i = 0; i < wire.GridPath.Count - 1; i++)
					{
						Vector2 p1 = GlobalMetrics.MetersToPixels(new Vector2(wire.GridPath[i].X + 0.5f, wire.GridPath[i].Y + 0.5f));
						Vector2 p2 = GlobalMetrics.MetersToPixels(new Vector2(wire.GridPath[i + 1].X + 0.5f, wire.GridPath[i + 1].Y + 0.5f));
						DrawLine(p1, p2, new Color(1.0f, 0.75f, 0.15f, 0.85f), 3.0f);
					}
				}
			}

			// 4. 在途流动脉冲
			if (Pulses != null)
			{
				foreach (var pulse in Pulses.InFlightPulses)
				{
					Vector2 localGridPos = pulse.GetCurrentLocalGridPos() + new Vector2(0.5f, 0.5f);
					Vector2 pulsePixelPos = GlobalMetrics.MetersToPixels(localGridPos);

					Color glowColor = _currentPalette.AccentColor;
					if ((pulse.Packet.Elements & ElementFlags.Cryo) != 0) glowColor = Colors.Cyan;
					if ((pulse.Packet.Elements & ElementFlags.Thermal) != 0) glowColor = Colors.Orange;

					DrawCircle(pulsePixelPos, 4.0f, glowColor);
					DrawCircle(pulsePixelPos, 2.0f, Colors.White);
				}
			}

			// 5. 战地飞线模式高亮透视层 (按住 F 键)
			if (Hotwire != null && Hotwire.IsInHotwireMode)
			{
				foreach (var pin in GetAllPins())
				{
					Vector2 pinPos = GlobalMetrics.MetersToPixels((Vector2)pin.AbsoluteGridPos + new Vector2(0.5f, 0.5f));
					Color pinColor = pin.Type == PinType.OUT ? Colors.LimeGreen : Colors.Orange;
					DrawCircle(pinPos, 5.0f, pinColor);
					DrawArc(pinPos, 8.0f, 0, Mathf.Tau, 16, pinColor, 1.5f);
				}

				if (Hotwire.DragStartPin != null)
				{
					Vector2 startPos = GlobalMetrics.MetersToPixels((Vector2)Hotwire.DragStartPin.AbsoluteGridPos + new Vector2(0.5f, 0.5f));
					Vector2 mouseLocalPos = ToLocal(Hotwire.DragCurrentMouseWorldPos);
					DrawDashedLine(startPos, mouseLocalPos, Colors.Yellow, 3.0f, dashLength: 6.0f);
				}
			}

			// 6. 定身/噬灭光环
			if (status.IsFrozen)
			{
				DrawArc(PhysicsData.CenterOfMassPixels, 45.0f, 0, Mathf.Tau, 32, Colors.Cyan, 3.0f);
			}
			if (status.HasEntropyCurse)
			{
				DrawArc(PhysicsData.CenterOfMassPixels, 52.0f, 0, Mathf.Tau, 32, Colors.Purple, 2.5f);
			}
		}

		private void DrawDashedLine(Vector2 from, Vector2 to, Color color, float width, float dashLength)
		{
			float totalDist = from.DistanceTo(to);
			if (totalDist < 1.0f) return;

			Vector2 dir = (to - from).Normalized();
			float drawn = 0.0f;
			bool isDash = true;

			while (drawn < totalDist)
			{
				float segLen = Mathf.Min(dashLength, totalDist - drawn);
				if (isDash)
				{
					DrawLine(from + (dir * drawn), from + (dir * (drawn + segLen)), color, width);
				}
				drawn += segLen;
				isDash = !isDash;
			}
		}

		private void DrawThrusterFlame(ThrusterRuntimeData thruster)
		{
			Vector2 nozzle = thruster.LocalNozzlePixelPos;
			float flameLen = thruster.FlameLength * thruster.CurrentThrottle * (float)GD.RandRange(0.85, 1.15);
			float flameWidth = 10.0f;

			Vector2 tip = nozzle + new Vector2(0, flameLen);
			Vector2 left = nozzle + new Vector2(-flameWidth * 0.5f, 0);
			Vector2 right = nozzle + new Vector2(flameWidth * 0.5f, 0);

			Vector2[] points = { left, right, tip };
			DrawColoredPolygon(points, thruster.FlameColor);
		}
	}
}
