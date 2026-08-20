using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Audio
{
	/// <summary>
	/// 5 级动态音频混音与 Low-pass 战术聚焦降压总控中枢 (单例)
	/// </summary>
	public partial class AudioManager : Node
	{
		public static AudioManager Instance { get; private set; } = null!;

		// 总线名称常量
		public const string BusMaster = "Master";
		public const string BusBgm = "BGM";
		public const string BusSfx = "SFX";
		public const string BusEngine = "Engine";
		public const string BusUI = "UI";

		private AudioEffectLowPassFilter _lowPassFilter = null!;
		private int _sfxBusIdx = 0;
		private int _bgmBusIdx = 0;

		// 战术聚焦低通滤波目标值 (Hz)
		private float _targetCutoffHz = 20000.0f;
		private float _currentCutoffHz = 20000.0f;

		// 动态音频降压 (Audio Ducking)
		private float _duckingTimer = 0.0f;
		private float _targetBgmDb = 0.0f;
		private float _currentBgmDb = 0.0f;

		// 对象池：复用 AudioStreamPlayer
		private readonly List<AudioStreamPlayer> _playerPool = new();
		private const int PoolSize = 24;

		public override void _Ready()
		{
			Instance = this;
			InitializeAudioBuses();
			CreatePlayerPool();
		}

		private void InitializeAudioBuses()
		{
			EnsureBusExists(BusBgm, BusMaster);
			EnsureBusExists(BusSfx, BusMaster);
			EnsureBusExists(BusEngine, BusMaster);
			EnsureBusExists(BusUI, BusMaster);

			_sfxBusIdx = AudioServer.GetBusIndex(BusSfx);
			_bgmBusIdx = AudioServer.GetBusIndex(BusBgm);

			// 在 SFX 与 BGM 总线上挂载高品质低通滤波器
			_lowPassFilter = new AudioEffectLowPassFilter
			{
				CutoffHz = 20000.0f,
				Resonance = 0.5f
			};

			AudioServer.AddBusEffect(_sfxBusIdx, _lowPassFilter);
		}

		private void EnsureBusExists(string busName, string sendTo)
		{
			int idx = AudioServer.GetBusIndex(busName);
			if (idx == -1)
			{
				idx = AudioServer.BusCount;
				AudioServer.AddBus(idx);
				AudioServer.SetBusName(idx, busName);
				AudioServer.SetBusSend(idx, sendTo);
			}
		}

		private void CreatePlayerPool()
		{
			for (int i = 0; i < PoolSize; i++)
			{
				var player = new AudioStreamPlayer { Name = $"SfxPlayer_{i}" };
				AddChild(player);
				_playerPool.Add(player);
			}
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;

			// 1. 低通滤波平滑插值 (Lerp)
			_currentCutoffHz = Mathf.Lerp(_currentCutoffHz, _targetCutoffHz, dt * 14.0f);
			_lowPassFilter.CutoffHz = Mathf.Clamp(_currentCutoffHz, 300.0f, 20000.0f);

			// 2. Audio Ducking 降压回弹计算
			if (_duckingTimer > 0.0f)
			{
				_duckingTimer -= dt;
				_targetBgmDb = -10.0f; // 降压压低 -10dB
			}
			else
			{
				_targetBgmDb = 0.0f; // 回弹至标准音量
			}

			_currentBgmDb = Mathf.Lerp(_currentBgmDb, _targetBgmDb, dt * 10.0f);
			if (_bgmBusIdx >= 0)
			{
				AudioServer.SetBusVolumeDb(_bgmBusIdx, _currentBgmDb);
			}
		}

		/// <summary>
		/// 播放战术音效 (带随机微音高与总线指定)
		/// </summary>
		public void PlaySfx(SoundType sound, float pitchRandomness = 0.08f, string busName = BusSfx)
		{
			var stream = ProceduralAudioGenerator.GetOrCreate(sound);

			// 查找空闲播放器
			AudioStreamPlayer? targetPlayer = null;
			foreach (var p in _playerPool)
			{
				if (!p.Playing)
				{
					targetPlayer = p;
					break;
				}
			}

			targetPlayer ??= _playerPool[0]; // 繁忙时抢占首个

			targetPlayer.Stream = stream;
			targetPlayer.Bus = busName;
			targetPlayer.PitchScale = 1.0f + (float)GD.RandRange(-pitchRandomness, pitchRandomness);
			targetPlayer.Play();

			// 大爆炸或重创自动触发 Audio Ducking
			if (sound == SoundType.ExplosionHuge || sound == SoundType.AblativeDetonate || sound == SoundType.ShootPlasma)
			{
				TriggerDucking(0.45f);
			}
		}

		/// <summary>
		/// 触发 Audio Ducking 瞬时降压
		/// </summary>
		public void TriggerDucking(float duration = 0.35f)
		{
			_duckingTimer = Mathf.Max(_duckingTimer, duration);
		}

		/// <summary>
		/// 开启/关闭战术聚焦低通滤波 (F键飞线 / 爆甲 / 核心濒死)
		/// </summary>
		public void SetTacticalFocusLowPass(bool active, float cutoffHz = 450.0f)
		{
			_targetCutoffHz = active ? cutoffHz : 20000.0f;
		}

		public float GetCurrentCutoffHz() => _currentCutoffHz;
		public float GetCurrentBgmDuckingDb() => _currentBgmDb;
	}
}
