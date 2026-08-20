using Godot;
using BreakerProtocol.World.Sector;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-30 交互式验证场景：星区 DAG 分支星图生成与追击舰队压力演练场
	/// </summary>
	public partial class Test_Task30 : Node2D
	{
		private SectorMapUI _mapUI = null!;
		private RichTextLabel _hudLabel = null!;
		private RichTextLabel _logLabel = null!;
		private string _lastEventLog = "🚀 战备就绪，请在第 1 列选择初始跃迁节点！";

		public override void _Ready()
		{
			CreateUI();
			GenerateNewSector();
		}

		private void CreateUI()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_mapUI = new SectorMapUI();
			_mapUI.OnNodeSelected += HandleNodeSelected;
			canvasLayer.AddChild(_mapUI);

			// 顶部 HUD
			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(30, 15),
				Size = new Vector2(1220, 75),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 14);
			canvasLayer.AddChild(_hudLabel);

			// 底部事件日志
			_logLabel = new RichTextLabel
			{
				Position = new Vector2(80, 645),
				Size = new Vector2(1120, 55),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_logLabel.AddThemeFontSizeOverride("normal_font_size", 13);
			canvasLayer.AddChild(_logLabel);
		}

		private void GenerateNewSector()
		{
			var graph = SectorMapGenerator.GenerateSector(totalColumns: 8);
			_mapUI.SetGraph(graph);
			_lastEventLog = "🌌 新星区拓扑已生成！追击舰队尚在 1.5 星区距离外。";
			UpdateHUD();
		}

		private void HandleNodeSelected(SectorNode node)
		{
			_lastEventLog = $"🚀 [超空间跃迁成功] 抵达 【{node.GetDisplayName()}】 ({node.Id})！\n" +
							$"⚠️ 敌方追击舰队已向前封锁至第 {Mathf.Max(0, (int)_mapUI.Graph.PursuitWavefrontColumn)} 列！";
			UpdateHUD();
		}

		public override void _Process(double delta)
		{
			if (Input.IsKeyPressed(Key.R))
			{
				GenerateNewSector();
			}
		}

		private void UpdateHUD()
		{
			if (_mapUI.Graph == null) return;

			double fps = Engine.GetFramesPerSecond();
			int currentCol = _mapUI.Graph.CurrentNodeId != null && _mapUI.Graph.AllNodes.TryGetValue(_mapUI.Graph.CurrentNodeId, out var cur) ? cur.Column : 0;
			float pursuitCol = _mapUI.Graph.PursuitWavefrontColumn;
			float safeDistance = (currentCol + 1) - pursuitCol;

			string safeTag = safeDistance switch
			{
				> 2.0f => "[color=green]🟢 安全 (优势领先)[/color]",
				> 1.0f => "[color=yellow]🟡 警惕 (追兵逼近)[/color]",
				_      => "[color=red]🚨 极度危急 (即将沦陷！)[/color]"
			};

			DisplayServer.WindowSetTitle($"《断路协议》| 星图探索 | 帧率: {fps:F0} FPS | 节点: {_mapUI.Graph.CurrentNodeId ?? "起始"}");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-30 星区 DAG 分支星图生成与追击前线演练场】[/color][/b]\n" +
							 $"• 当前深度: [color=cyan]第 {currentCol + 1} / {_mapUI.Graph.TotalColumns} 列[/color] | 追击浪潮线: [color=orange]第 {Mathf.Max(0, pursuitCol):F1} 列[/color] | 战术前线状态: {safeTag}\n" +
							 $"• 操作指南: [b][color=white]鼠标悬停查看【战术侦察情报卡】 | 点击青绿发光节点跃迁 | [按 R 键]: 重新生成随机星区[/color][/b]";

			_logLabel.Text = $"[color=yellow][战地遥测日志][/color] {_lastEventLog}";
		}
	}
}
