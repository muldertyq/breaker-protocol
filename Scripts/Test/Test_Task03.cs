using Godot;
using BreakerProtocol.Core;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-03 交互式热重载验证场景
	/// 实时显示当前注册表中的磁轨炮参数，直观验证外部 JSON 修改后瞬间生效
	/// </summary>
	public partial class Test_Task03 : Control
	{
		private Label _infoLabel = null!;
		private Label _tipsLabel = null!;

		public override void _Ready()
		{
			// 1. 创建 UI 显示面板
			_infoLabel = new Label
			{
				Position = new Vector2(50, 50),
				Size = new Vector2(800, 400)
			};
			_infoLabel.AddThemeFontSizeOverride("font_size", 20);
			_infoLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			AddChild(_infoLabel);

			_tipsLabel = new Label
			{
				Position = new Vector2(50, 480),
				Size = new Vector2(800, 100),
				Text = "【热重载实时测试指南】\n" +
					   "1. 保持游戏窗口运行；\n" +
					   "2. 打开 core_data/modules/heavy_railgun.json；\n" +
					   "3. 修改 'mass' (比如改为 99.0) 或 'name' 并保存 (Ctrl+S)；\n" +
                       "4. 观察上方数据面板是否瞬间发生变化！"
			};
			_tipsLabel.AddThemeFontSizeOverride("font_size", 16);
			_tipsLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.3f));
			AddChild(_tipsLabel);

			// 2. 监听全局热重载信号
			DataManager.Instance.DataReloaded += RefreshDisplay;

			// 3. 初始刷新
			RefreshDisplay();
		}

		private void RefreshDisplay()
		{
			if (DataManager.Instance.Modules.TryGet("hf_wep_railgun_h", out var railgun))
			{
				_infoLabel.Text = $"【《断路协议》TASK-03 构件热重载实时监控】\n" +
								  $"------------------------------------------------------------\n" +
								  $"构件 ID:      {railgun!.Id}\n" +
								  $"显示名称:    {railgun.Name}\n" +
								  $"所属阵营:    {railgun.Faction}\n" +
								  $"构件尺寸:    {railgun.Width} x {railgun.Height} GU\n" +
								  $"装载质量:    {railgun.Mass:F1} 吨\n" +
								  $"基础耐久:    {railgun.BaseHp:F0} HP\n" +
								  $"装甲抗性:    {railgun.ArmorResistance:F1}\n" +
								  $"引脚数量:    {railgun.Pins.Length} 个\n" +
								  $"更新时间:    {System.DateTime.Now:HH:mm:ss.fff}\n" +
								  $"------------------------------------------------------------\n" +
								  $"状态: [color=green]热重载监听器运行中 (Ready)[/color]";
			}
			else
			{
				_infoLabel.Text = "[✘] 未能查询到 hf_wep_railgun_h！";
			}
		}
	}
}
