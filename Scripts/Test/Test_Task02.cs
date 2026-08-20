using Godot;
using BreakerProtocol.Core;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-02 验证驱动脚本：测试注册表查询与 Mod 注入
	/// </summary>
	public partial class Test_Task02 : Node
	{
		public override void _Ready()
		{
			GD.PrintRich("\n[color=yellow]>>> 开始验证 TASK-02 注册表与数据查询...[/color]");

			// 1. 验证官方构件是否已正确注册
			if (DataManager.Instance.Modules.TryGet("hf_wep_railgun_h", out var railgun))
			{
				GD.PrintRich($"[color=green][✔] 官方构件查询成功！[/color] 名称: {railgun!.Name}, 质量: {railgun.Mass}t, 血量: {railgun.BaseHp}");
			}
			else
			{
				GD.PrintErr("[✘] 未能查询到官方构件 hf_wep_railgun_h！");
			}

			// 2. 验证玩家自制 Mod 构件是否被成功加载
			if (DataManager.Instance.Modules.TryGet("mod_custom_gauss_s", out var gauss))
			{
				GD.PrintRich($"[color=green][✔] 自定义 Mod 构件注入成功！[/color] 名称: {gauss!.Name}, 所属阵营: {gauss.Faction}");
			}
			else
			{
				GD.PrintErr("[✘] 未能查询到 Mod 构件 mod_custom_gauss_s！");
			}

			// 3. 打印全量汇总
			GD.PrintRich($"[color=cyan]>>> 注册表当前累计装载构件数: {DataManager.Instance.Modules.Count} 个[/color]\n");
		}
	}
}
