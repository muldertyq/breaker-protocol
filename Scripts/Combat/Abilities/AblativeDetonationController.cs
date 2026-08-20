using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Combat.Abilities;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship.Pipeline;

namespace BreakerProtocol.Ship.Abilities
{
	public enum DetonationSection
	{
		LeftWing,   // [Q 键] 左翼
		RightWing,  // [E 键] 右翼
		AftSection  // [Z 键] 舰尾推进舱
	}

	/// <summary>
	/// 三大势力战术主动过载爆甲控制器 (规范 06《战术主动爆甲系统规范》)
	/// </summary>
	public class AblativeDetonationController
	{
		private readonly ShipEntity _ship;

		// 各部位爆甲冷却计时器
		private float _cooldownQ = 0.0f;
		private float _cooldownE = 0.0f;
		private float _cooldownZ = 0.0f;

		public const float AbilityCooldown = 1.0f;

		public AblativeDetonationController(ShipEntity ship)
		{
			_ship = ship;
		}

		public void Update(float dt)
		{
			if (_cooldownQ > 0.0f) _cooldownQ -= dt;
			if (_cooldownE > 0.0f) _cooldownE -= dt;
			if (_cooldownZ > 0.0f) _cooldownZ -= dt;

			// 监听 Q / E / Z 战术按键
			if (Input.IsKeyPressed(Key.Q) && _cooldownQ <= 0.0f)
			{
				_cooldownQ = AbilityCooldown;
				TriggerDetonation(DetonationSection.LeftWing);
			}
			else if (Input.IsKeyPressed(Key.E) && _cooldownE <= 0.0f)
			{
				_cooldownE = AbilityCooldown;
				TriggerDetonation(DetonationSection.RightWing);
			}
			else if (Input.IsKeyPressed(Key.Z) && _cooldownZ <= 0.0f)
			{
				_cooldownZ = AbilityCooldown;
				TriggerDetonation(DetonationSection.AftSection);
			}
		}

		/// <summary>
		/// 执行指定部位的主动过载爆甲
		/// </summary>
		public bool TriggerDetonation(DetonationSection section)
		{
			// 1. 严格按几何质心筛选目标部位构件 (严禁误炸中央龙骨)
			var targetModules = GetModulesInSection(section);
			if (targetModules.Count == 0)
			{
				VfxManager.Instance?.SpawnFloatingText(
					_ship.GlobalPosition,
					$"⚠️ {GetSectionName(section)} 无可爆甲构件",
					Colors.Gray
				);
				return false;
			}

			// 2. 计算剥离构件的总质量与重心方向
			float totalEjectedMass = 0.0f;
			Vector2 ejectionCenterGrid = Vector2.Zero;

			foreach (var mod in targetModules)
			{
				float m = mod.Definition.Mass;
				totalEjectedMass += m;
				Vector2I size = mod.GetRotatedSize();
				Vector2 modCenter = (Vector2)mod.GridPosition + ((Vector2)size * 0.5f);
				ejectionCenterGrid += modCenter * m;
			}

			ejectionCenterGrid /= Mathf.Max(1.0f, totalEjectedMass);
			Vector2 ejectionWorldPos = _ship.GlobalTransform * GlobalMetrics.MetersToPixels(ejectionCenterGrid);

			// 3. 计算向外抛射方向
			Vector2 ejectionLocalDir = ejectionCenterGrid.Normalized();
			if (ejectionLocalDir == Vector2.Zero)
			{
				ejectionLocalDir = section == DetonationSection.LeftWing ? -Vector2.Right :
								  (section == DetonationSection.RightWing ? Vector2.Right : Vector2.Down);
			}

			Vector2 ejectionWorldDir = _ship.GlobalTransform.BasisXform(ejectionLocalDir).Normalized();

			// 动量反冲冲量：瞬间后坐闪避
			float totalShipMass = Mathf.Max(50.0f, _ship.Mass);
			float recoilSpeed = 420.0f;
			Vector2 recoilImpulse = -ejectionWorldDir * (totalShipMass * recoilSpeed);

			_ship.ApplyCentralImpulse(recoilImpulse);

			// 4. 彻底剥离构件与导线
			foreach (var mod in targetModules)
			{
				_ship.Pipeline.RemoveWiresConnectedTo(mod.InstanceId);
				_ship.Grid.RemoveModule(mod.InstanceId);

				Vector2 modWorld = _ship.GlobalTransform * GlobalMetrics.MetersToPixels((Vector2)mod.GridPosition + new Vector2(0.5f, 0.5f));
				VfxManager.Instance?.SpawnModuleExplosion(modWorld, new Vector2(32, 32), _ship.CurrentPalette.AccentColor, shardCount: 8);
			}

			// 5. 触发势力专属特技
			ExecuteFactionSpecialAbility(section, ejectionWorldPos, ejectionWorldDir);

			// 6. 全局打击感与屏幕震颤
			JuiceManager.Instance?.TriggerExplosionJuice(ejectionWorldPos, intensity: 1.2f);
			JuiceManager.Instance?.ApplyDirectionalKick(-ejectionWorldDir * 24.0f);
			JuiceManager.Instance?.AddCameraTrauma(0.60f);

			// 7. 动态物理重构与拓扑断裂检查
			_ship.RebuildPhysics();
			HullSeveranceEngine.CheckAndSeverDisconnectedClusters(_ship);

			GD.PrintRich($"[color=yellow][AblativeDetonation] 💥 [{GetSectionName(section)}] 爆炸螺栓引爆！剥离 {targetModules.Count} 个构件 ({totalEjectedMass:F0}t)，反冲冲量: {recoilImpulse.Length():F0} N·s[/color]");
			return true;
		}

		private void ExecuteFactionSpecialAbility(DetonationSection section, Vector2 ejectionPos, Vector2 ejectionDir)
		{
			string faction = _ship.CurrentPalette.Faction;

			// ============================================================
			// 1. 重工联合 (HeavyFoundry)：24 枚破片散弹向外全向清屏
			// ============================================================
			if (faction == "HeavyFoundry" || faction.Contains("Foundry"))
			{
				int shrapnelCount = 24;
				float baseAngle = ejectionDir.Angle();
				float spread = Mathf.Pi * 0.85f;
				float angleStep = spread / (shrapnelCount - 1);

				for (int i = 0; i < shrapnelCount; i++)
				{
					float angle = (baseAngle - (spread * 0.5f)) + (i * angleStep);
					Vector2 shotDir = Vector2.FromAngle(angle);

					var shrapnel = new ProjectileEntity
					{
						GlobalPosition = ejectionPos + (shotDir * 20.0f),
						AttackerShip = _ship,
						Velocity = shotDir * 620.0f,
						BaseDamage = 45.0f,
						RemainingPierce = 1,
						Elements = ElementFlags.Kinetic | ElementFlags.Thermal,
						RemainingLifeTime = 1.2f
					};
					_ship.GetTree().CurrentScene.AddChild(shrapnel);
				}

				VfxManager.Instance?.SpawnFloatingText(ejectionPos, "💥 重工过载爆甲：24枚破片散弹清屏！", Colors.Orange);
			}
			// ============================================================
			// 2. 虚空财团 (VoidSyndicate)：向外抛射微型引力黑洞
			// ============================================================
			else if (faction == "VoidSyndicate" || faction.Contains("Void"))
			{
				var blackHole = new VoidSingularityEntity
				{
					GlobalPosition = ejectionPos + (ejectionDir * 35.0f),
					Velocity = ejectionDir * 260.0f,
					OwnerShip = _ship,
					RemainingDuration = 2.0f,
					PullRadiusPixels = 420.0f
				};
				_ship.GetTree().CurrentScene.AddChild(blackHole);

				VfxManager.Instance?.SpawnFloatingText(ejectionPos, "🌌 虚空战术爆甲：奇点投掷与反冲折跃！", Colors.MediumPurple);
			}
			// ============================================================
			// 3. 深空生化 (BioChitin)：释放强酸火海毒雾 + 3 只自爆毒刺弹
			// ============================================================
			else
			{
				VfxManager.Instance?.SpawnAcidPool(ejectionPos, radius: 50.0f, duration: 5.0f);

				for (int i = 0; i < 3; i++)
				{
					Vector2 sporeDir = ejectionDir.Rotated((float)GD.RandRange(-0.4, 0.4));
					var spore = new ProjectileEntity
					{
						GlobalPosition = ejectionPos + (sporeDir * 20.0f),
						AttackerShip = _ship,
						Velocity = sporeDir * 360.0f,
						BaseDamage = 60.0f,
						RemainingPierce = 0,
						Elements = ElementFlags.Acid | ElementFlags.Thermal,
						RemainingLifeTime = 2.0f
					};
					_ship.GetTree().CurrentScene.AddChild(spore);
				}

				VfxManager.Instance?.SpawnFloatingText(ejectionPos, "☣️ 生化过载爆甲：腐蚀毒雾与自爆虫群！", Colors.LimeGreen);
			}
		}

		/// <summary>
		/// 核心算法：基于构件几何质心严格独立划分部位，绝对不会误伤中央龙骨与对侧机翼
		/// </summary>
		private List<ModuleInstance> GetModulesInSection(DetonationSection section)
		{
			var result = new List<ModuleInstance>();

			foreach (var mod in _ship.Grid.Modules)
			{
				if (mod.IsDestroyed) continue;
				// 核心动力堆严禁被引爆
				if (mod.Definition.Category == "PowerSource") continue;

				Vector2I size = mod.GetRotatedSize();
				Vector2 center = (Vector2)mod.GridPosition + ((Vector2)size * 0.5f);

				switch (section)
				{
					case DetonationSection.LeftWing:
						// 仅选中心位于左侧 (X < -0.6) 且不在尾部的构件
						if (center.X < -0.6f && center.Y < 2.5f)
						{
							result.Add(mod);
						}
						break;

					case DetonationSection.RightWing:
						// 仅选中心位于右侧 (X > 0.6) 且不在尾部的构件
						if (center.X > 0.6f && center.Y < 2.5f)
						{
							result.Add(mod);
						}
						break;

					case DetonationSection.AftSection:
						// 仅选尾部推进舱 (Y > 2.0 且位于核心后方)
						if (center.Y > 2.0f)
						{
							result.Add(mod);
						}
						break;
				}
			}

			return result;
		}

		private string GetSectionName(DetonationSection section) => section switch
		{
			DetonationSection.LeftWing => "[Q 键] 左舷机翼",
			DetonationSection.RightWing => "[E 键] 右舷机翼",
			DetonationSection.AftSection => "[Z 键] 舰尾舱段",
			_ => "未知部位"
		};
	}
}
