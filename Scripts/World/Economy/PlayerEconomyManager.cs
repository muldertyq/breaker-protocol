using System;
using Godot;

namespace BreakerProtocol.World.Economy
{
	/// <summary>
	/// 玩家战局经济与货币总控中枢 (单例)
	/// </summary>
	public class PlayerEconomyManager
	{
		private static PlayerEconomyManager? _instance;
		public static PlayerEconomyManager Instance => _instance ??= new PlayerEconomyManager();

		// 基础通货：金属废料 (Scraps)
		public int Scraps { get; private set; } = 450;

		// 高级代币：算力核心 (Compute Cores)
		public int ComputeCores { get; private set; } = 2;

		public event Action<int, int>? OnCurrencyChanged;

		public void AddScraps(int amount)
		{
			if (amount <= 0) return;
			Scraps += amount;
			OnCurrencyChanged?.Invoke(Scraps, ComputeCores);
		}

		public bool SpendScraps(int amount)
		{
			if (amount <= 0) return true;
			if (Scraps >= amount)
			{
				Scraps -= amount;
				OnCurrencyChanged?.Invoke(Scraps, ComputeCores);
				return true;
			}
			return false;
		}

		public void AddComputeCores(int amount)
		{
			if (amount <= 0) return;
			ComputeCores += amount;
			OnCurrencyChanged?.Invoke(Scraps, ComputeCores);
		}

		public bool SpendComputeCores(int amount)
		{
			if (amount <= 0) return true;
			if (ComputeCores >= amount)
			{
				ComputeCores -= amount;
				OnCurrencyChanged?.Invoke(Scraps, ComputeCores);
				return true;
			}
			return false;
		}

		public void Reset(int initialScraps = 450, int initialCores = 2)
		{
			Scraps = initialScraps;
			ComputeCores = initialCores;
			OnCurrencyChanged?.Invoke(Scraps, ComputeCores);
		}
	}
}
