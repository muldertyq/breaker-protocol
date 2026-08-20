using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Combat;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Ship.AI;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Sector;
using BreakerProtocol.World.Session;

namespace BreakerProtocol.World.Director
{
	public class EnemySpawnUnit
	{
		public string BlueprintId { get; set; } = string.Empty;
		public AiArchetype Archetype { get; set; } = AiArchetype.Brawler;
		public ShipPalette Palette { get; set; } = FactionPalettes.HeavyFoundry;
		public string Role { get; set; } = "Scout";
		public float ThreatValue { get; set; } = 10.0f;
	}

	public class EncounterWaveConfig
	{
		public string WaveTitle { get; set; } = "第一波：前哨散兵侦察";
		public List<EnemySpawnUnit> Units { get; set; } = new();
		public float SpawnInterval { get; set; } = 0.4f;
	}

	/// <summary>
	/// 程序化战术遭遇战导演系统 (集成动态波次生成、死舰自检与跃迁撤离门调度)
	/// </summary>
	public partial class CombatEncounterDirector : Node2D
	{
		public static CombatEncounterDirector? Instance { get; private set; }

		public ShipEntity? TargetPlayerShip { get; set; }
		public bool IsEncounterActive { get; private set; } = false;

		public int CurrentWaveIndex { get; private set; } = 0;
		public int TotalWaves { get; private set; } = 3;
		public float CurrentThreatLevel { get; private set; } = 0.0f;

		public readonly List<ShipEntity> ActiveEnemies = new();
		private readonly List<EncounterWaveConfig> _waveConfigs = new();

		public HyperspaceGateEntity? ActiveJumpGate { get; private set; }

		private float _waveTransitionTimer = 0.0f;
		private bool _isWaitingForNextWave = false;
		private int _pendingSpawnCount = 0;

		public event Action<int, int, string>? OnWaveStarted;
		public event Action<int>? OnWaveCleared;
		public event Action? OnEncounterCompleted;
		public event Action<HyperspaceGateEntity>? OnJumpGateSpawned;
		public event Action<float>? OnThreatChanged;

		public override void _Ready()
		{
			Instance = this;
		}

		public void StartEncounter(SectorNodeType nodeType, int sectorColumn, ShipEntity playerShip)
		{
			TargetPlayerShip = playerShip;
			ActiveEnemies.Clear();
			_waveConfigs.Clear();
			CurrentWaveIndex = 0;
			_isWaitingForNextWave = false;
			_pendingSpawnCount = 0;
			_waveTransitionTimer = 0.0f;

			if (ActiveJumpGate != null && GodotObject.IsInstanceValid(ActiveJumpGate))
			{
				ActiveJumpGate.QueueFree();
				ActiveJumpGate = null;
			}

			BuildWaveConfigs(nodeType, sectorColumn);
			TotalWaves = _waveConfigs.Count;
			IsEncounterActive = true;

			SpawnNextWave();
		}

		private void BuildWaveConfigs(SectorNodeType nodeType, int sectorColumn)
		{
			int depth = Mathf.Clamp(sectorColumn, 1, 8);

			switch (nodeType)
			{
				case SectorNodeType.Elite:
					_waveConfigs.Add(new EncounterWaveConfig
					{
						WaveTitle = "WAVE 1/2 : 虚空护航巡逻队",
						Units = new List<EnemySpawnUnit>
						{
							new() { BlueprintId = "bp_vs_s_ghost", Archetype = AiArchetype.KiteSniper, Palette = FactionPalettes.VoidSyndicate, Role = "Scout", ThreatValue = 15.0f },
							new() { BlueprintId = "bp_vs_s_ghost", Archetype = AiArchetype.KiteSniper, Palette = FactionPalettes.VoidSyndicate, Role = "Scout", ThreatValue = 15.0f }
						}
					});
					_waveConfigs.Add(new EncounterWaveConfig
					{
						WaveTitle = "WAVE 2/2 : ⚠️ 精英旗舰亲临截击",
						Units = new List<EnemySpawnUnit>
						{
							new() { BlueprintId = "bp_hf_l_ironclad", Archetype = AiArchetype.Brawler, Palette = FactionPalettes.HeavyFoundry, Role = "Elite", ThreatValue = 80.0f },
							new() { BlueprintId = "bp_bc_m_carapace", Archetype = AiArchetype.Swarm, Palette = FactionPalettes.BioChitin, Role = "Striker", ThreatValue = 30.0f }
						}
					});
					break;

				default:
					_waveConfigs.Add(new EncounterWaveConfig
					{
						WaveTitle = "WAVE 1/3 : 先锋斥候轻艇",
						Units = new List<EnemySpawnUnit>
						{
							new() { BlueprintId = "bp_hf_s_hammerhead", Archetype = AiArchetype.Brawler, Palette = FactionPalettes.HeavyFoundry, Role = "Scout", ThreatValue = 10.0f + depth * 2 },
							new() { BlueprintId = "bp_bc_s_leech", Archetype = AiArchetype.Swarm, Palette = FactionPalettes.BioChitin, Role = "Scout", ThreatValue = 10.0f + depth * 2 }
						}
					});

					_waveConfigs.Add(new EncounterWaveConfig
					{
						WaveTitle = "WAVE 2/3 : 战术战列编队",
						Units = new List<EnemySpawnUnit>
						{
							new() { BlueprintId = "bp_hf_s_hammerhead", Archetype = AiArchetype.Brawler, Palette = FactionPalettes.HeavyFoundry, Role = "Scout", ThreatValue = 12.0f },
							new() { BlueprintId = "bp_vs_s_ghost", Archetype = AiArchetype.KiteSniper, Palette = FactionPalettes.VoidSyndicate, Role = "Scout", ThreatValue = 15.0f }
						}
					});

					_waveConfigs.Add(new EncounterWaveConfig
					{
						WaveTitle = "WAVE 3/3 : ⚠️ 主力突击战群",
						Units = new List<EnemySpawnUnit>
						{
							new() { BlueprintId = "bp_hf_m_anvil", Archetype = AiArchetype.Brawler, Palette = FactionPalettes.HeavyFoundry, Role = "Striker", ThreatValue = 35.0f },
							new() { BlueprintId = "bp_bc_m_carapace", Archetype = AiArchetype.Swarm, Palette = FactionPalettes.BioChitin, Role = "Striker", ThreatValue = 35.0f }
						}
					});
					break;
			}
		}

		private void SpawnNextWave()
		{
			if (CurrentWaveIndex >= _waveConfigs.Count)
			{
				CompleteEncounter();
				return;
			}

			var wave = _waveConfigs[CurrentWaveIndex];
			CurrentWaveIndex++;
			_isWaitingForNextWave = false;
			_pendingSpawnCount = wave.Units.Count;

			OnWaveStarted?.Invoke(CurrentWaveIndex, TotalWaves, wave.WaveTitle);
			JuiceManager.Instance?.AddCameraTrauma(0.35f);

			for (int i = 0; i < wave.Units.Count; i++)
			{
				var unit = wave.Units[i];
				float delay = i * wave.SpawnInterval;
				GetTree().CreateTimer(delay).Connect("timeout", Callable.From(() =>
				{
					_pendingSpawnCount = Mathf.Max(0, _pendingSpawnCount - 1);
					SpawnSingleEnemy(unit);
				}));
			}
		}

		private void SpawnSingleEnemy(EnemySpawnUnit unit)
		{
			if (TargetPlayerShip == null || !IsInsideTree()) return;

			float angle = (float)GD.RandRange(0, Mathf.Tau);
			float dist = (float)GD.RandRange(800, 1100);
			Vector2 spawnPos = TargetPlayerShip.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

			var enemy = new ShipEntity
			{
				Name = $"Enemy_{unit.Role}_{Guid.NewGuid().ToString()[..4]}",
				Position = spawnPos,
				Rotation = angle + Mathf.Pi,
				CurrentPalette = unit.Palette
			};
			enemy.AddToGroup("Enemy");
			enemy.AddToGroup("Ship");
			GetParent().AddChild(enemy);

			if (DataManager.Instance.Blueprints.TryGet(unit.BlueprintId, out var bp) && bp != null)
			{
				ShipBlueprintLoader.ApplyBlueprint(enemy, bp);
			}

			enemy.AttachAI(unit.Archetype, TargetPlayerShip);
			ActiveEnemies.Add(enemy);

			VfxManager.Instance?.SpawnFloatingText(spawnPos, "⚡ 超空间折跃已切入 ⚡", unit.Palette.AccentColor);

			enemy.TreeExited += () =>
			{
				ActiveEnemies.Remove(enemy);
				GameRunSession.Instance.RecordEnemyKilled(unit.Role);
				RecalculateThreat();
			};

			RecalculateThreat();
		}

		public override void _Process(double delta)
		{
			if (!IsEncounterActive) return;

			float dt = (float)delta;

			// 1. 清理无效引用与死舰
			ActiveEnemies.RemoveAll(e =>
			{
				if (!GodotObject.IsInstanceValid(e) || e.IsQueuedForDeletion()) return true;

				bool hasAliveCore = false;
				foreach (var m in e.Grid.Modules)
				{
					if (!m.IsDestroyed && m.Definition.Category == "PowerSource")
					{
						hasAliveCore = true;
						break;
					}
				}
				if (!hasAliveCore)
				{
					e.QueueFree();
					return true;
				}
				return false;
			});

			// 2. 检查当前波次是否全部肃清
			if (ActiveEnemies.Count == 0 && _pendingSpawnCount == 0 && !_isWaitingForNextWave)
			{
				_isWaitingForNextWave = true;
				_waveTransitionTimer = 2.0f;
				OnWaveCleared?.Invoke(CurrentWaveIndex);
			}

			if (_isWaitingForNextWave)
			{
				_waveTransitionTimer -= dt;
				if (_waveTransitionTimer <= 0.0f)
				{
					_isWaitingForNextWave = false;
					if (CurrentWaveIndex < TotalWaves)
					{
						SpawnNextWave();
					}
					else
					{
						CompleteEncounter();
					}
				}
			}
		}

		private void RecalculateThreat()
		{
			float totalThreat = 0.0f;
			foreach (var enemy in ActiveEnemies)
			{
				if (GodotObject.IsInstanceValid(enemy))
				{
					totalThreat += 15.0f + (enemy.Grid.Modules.Count * 2.0f);
				}
			}
			CurrentThreatLevel = totalThreat;
			OnThreatChanged?.Invoke(CurrentThreatLevel);
		}

		private void CompleteEncounter()
		{
			IsEncounterActive = false;
			CurrentThreatLevel = 0.0f;

			// 在玩家前方 320px 处折跃展开超空间撤离门
			SpawnExtractionGate();

			OnEncounterCompleted?.Invoke();
			GD.PrintRich("[color=green]✦ 区域敌军已全歼！超空间跳跃信标门已展开 ✦[/color]");
		}

		public void SpawnExtractionGate()
		{
			if (ActiveJumpGate != null && GodotObject.IsInstanceValid(ActiveJumpGate)) return;

			Vector2 gatePos = Vector2.Zero;
			if (TargetPlayerShip != null && GodotObject.IsInstanceValid(TargetPlayerShip))
			{
				Vector2 forward = -TargetPlayerShip.Transform.Y;
				gatePos = TargetPlayerShip.GlobalPosition + forward * 320.0f;
			}

			ActiveJumpGate = new HyperspaceGateEntity
			{
				GlobalPosition = gatePos,
				TargetShip = TargetPlayerShip
			};
			GetParent().AddChild(ActiveJumpGate);
			OnJumpGateSpawned?.Invoke(ActiveJumpGate);
		}
	}
}
