using System;
using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Audio
{
	public enum SoundType
	{
		ShootKinetic,      // 动能机枪连续点射
		ShootLaser,        // 聚焦激光脉冲
		ShootPlasma,       // 重型等离子电浆炮
		Ricochet,          // 大倾角装甲跳弹清脆金属鸣响
		HitArmor,          // 穿甲重击与外壳凹陷
		ExplosionSmall,    // 构件爆裂小爆炸
		ExplosionHuge,     // 战舰核心殉爆与泰坦大爆炸
		HotwireConnect,    // 战地飞线成功搭桥高科技蜂鸣
		AblativeDetonate,  // 战术过载爆甲冲击波
		WarningAlarm,      // 动力炉熔断高危蜂鸣
		EngineThrust,      // 尾喷引擎加速脉冲
		UIClick            // 战术终端清脆微点击
	}

	/// <summary>
	/// 纯程序化 16-Bit PCM 太空音效合成引擎 (免外部资源依赖)
	/// </summary>
	public static class ProceduralAudioGenerator
	{
		private static readonly Dictionary<SoundType, AudioStreamWav> _cache = new();
		private const int SampleRate = 44100;

		public static AudioStreamWav GetOrCreate(SoundType type)
		{
			if (_cache.TryGetValue(type, out var cached)) return cached;

			var stream = SynthesizeSound(type);
			_cache[type] = stream;
			return stream;
		}

		private static AudioStreamWav SynthesizeSound(SoundType type)
		{
			float duration = type switch
			{
				SoundType.ShootKinetic     => 0.12f,
				SoundType.ShootLaser       => 0.20f,
				SoundType.ShootPlasma      => 0.35f,
				SoundType.Ricochet         => 0.18f,
				SoundType.HitArmor         => 0.22f,
				SoundType.ExplosionSmall   => 0.45f,
				SoundType.ExplosionHuge    => 1.20f,
				SoundType.HotwireConnect   => 0.25f,
				SoundType.AblativeDetonate => 0.85f,
				SoundType.WarningAlarm     => 0.30f,
				SoundType.EngineThrust     => 0.25f,
				SoundType.UIClick          => 0.05f,
				_                          => 0.10f
			};

			int totalSamples = (int)(SampleRate * duration);
			byte[] pcmData = new byte[totalSamples * 2]; // 16-bit 单声道

			for (int i = 0; i < totalSamples; i++)
			{
				float t = (float)i / SampleRate;
				float progress = (float)i / totalSamples;
				float sampleVal = 0.0f;

				switch (type)
				{
					case SoundType.ShootKinetic:
						// 初始方波冲击 + 快速衰减白噪声
						float kick = Mathf.Sin(t * Mathf.Tau * (350.0f - progress * 200.0f));
						float noise = ((float)GD.RandRange(-1.0, 1.0)) * 0.5f;
						sampleVal = (kick * 0.6f + noise * 0.4f) * Mathf.Exp(-progress * 28.0f);
						break;

					case SoundType.ShootLaser:
						// 调频激光扫频 (从 1400Hz 极速滑降至 220Hz)
						float laserFreq = Mathf.Lerp(1400.0f, 220.0f, progress * progress);
						sampleVal = Mathf.Sin(t * Mathf.Tau * laserFreq) * (1.0f - progress);
						break;

					case SoundType.ShootPlasma:
						// 重低音等离子轰鸣 + 锯齿波
						float plasmaFreq = Mathf.Lerp(280.0f, 60.0f, progress);
						float saw = (t * plasmaFreq) - Mathf.Floor(t * plasmaFreq) - 0.5f;
						sampleVal = (Mathf.Sin(t * Mathf.Tau * plasmaFreq) * 0.6f + saw * 0.4f) * Mathf.Exp(-progress * 10.0f);
						break;

					case SoundType.Ricochet:
						// 高频金属泛音 (2200Hz + 4400Hz 铃音)
						float ping = Mathf.Sin(t * Mathf.Tau * 2200.0f) * 0.7f + Mathf.Sin(t * Mathf.Tau * 4400.0f) * 0.3f;
						sampleVal = ping * Mathf.Exp(-progress * 22.0f);
						break;

					case SoundType.HitArmor:
						// 钝感金属撞击重击
						float thump = Mathf.Sin(t * Mathf.Tau * 120.0f) * 0.7f + ((float)GD.RandRange(-1.0, 1.0)) * 0.3f;
						sampleVal = thump * Mathf.Exp(-progress * 18.0f);
						break;

					case SoundType.ExplosionSmall:
					case SoundType.ExplosionHuge:
						// 低通白噪声 + 超重低音次声波衰减
						float boomFreq = (type == SoundType.ExplosionHuge) ? 45.0f : 85.0f;
						float decayRate = (type == SoundType.ExplosionHuge) ? 3.5f : 8.0f;
						float rumble = Mathf.Sin(t * Mathf.Tau * boomFreq);
						float expNoise = ((float)GD.RandRange(-1.0, 1.0));
						sampleVal = (rumble * 0.6f + expNoise * 0.4f) * Mathf.Exp(-progress * decayRate);
						break;

					case SoundType.HotwireConnect:
						// 双音电子和弦 (523Hz C5 + 659Hz E5)
						float chord = Mathf.Sin(t * Mathf.Tau * 523.25f) * 0.5f + Mathf.Sin(t * Mathf.Tau * 659.25f) * 0.5f;
						sampleVal = chord * (1.0f - progress * 0.8f);
						break;

					case SoundType.AblativeDetonate:
						// 巨型爆炸破片散布冲击
						float blast = Mathf.Sin(t * Mathf.Tau * 65.0f) * 0.8f + ((float)GD.RandRange(-1.0, 1.0)) * 0.2f;
						sampleVal = blast * Mathf.Exp(-progress * 4.5f);
						break;

					case SoundType.WarningAlarm:
						// 880Hz 间歇警报音
						sampleVal = Mathf.Sin(t * Mathf.Tau * 880.0f) * Mathf.Sin(progress * Mathf.Pi);
						break;

					case SoundType.EngineThrust:
						// 引擎低沉等离子喷射白噪
						sampleVal = (((float)GD.RandRange(-1.0, 1.0)) * 0.6f + Mathf.Sin(t * Mathf.Tau * 90.0f) * 0.4f) * (1.0f - progress);
						break;

					case SoundType.UIClick:
						// 清脆超短脉冲
						sampleVal = Mathf.Sin(t * Mathf.Tau * 1800.0f) * (1.0f - progress);
						break;
				}

				short sampleShort = (short)Mathf.Clamp(sampleVal * 31000.0f, -32768.0f, 32767.0f);
				pcmData[i * 2] = (byte)(sampleShort & 0xFF);
				pcmData[i * 2 + 1] = (byte)((sampleShort >> 8) & 0xFF);
			}

			return new AudioStreamWav
			{
				Format = AudioStreamWav.FormatEnum.Format16Bits,
				MixRate = SampleRate,
				Stereo = false,
				Data = pcmData
			};
		}
	}
}
