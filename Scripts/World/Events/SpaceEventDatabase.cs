using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.World.Events
{
	/// <summary>
	/// 包含 5 大经典太空 Roguelike 叙事异象的事件数据库
	/// </summary>
	public static class SpaceEventDatabase
	{
		public static Dictionary<string, SpaceEventNode> Events { get; } = new();

		static SpaceEventDatabase()
		{
			BuildDatabase();
		}

		private static void BuildDatabase()
		{
			// =========================================================
			// 事件 1: 废弃的先驱者科研船
			// =========================================================
			var ev1 = new SpaceEventNode
			{
				Id = "ev_pioneer_derelict",
				Title = "【异象】废弃的先驱者科研船",
				FactionTag = "古代科技遗迹",
				ThemeColor = new Color(0.35f, 0.85f, 0.95f),
				Description = "雷达在一片碎石带中捕获到微弱的求救信标。一艘隶属于旧时代的先驱者级科研船在真空中静默漂流，舰桥外壳已被等离子烧蚀，但其主动力舱仍有微弱的能量读数。"
			};
			ev1.Choices.Add(new EventChoice
			{
				ChoiceText = "强行黑入主服务器：尝试逆向破译算力核心与蓝图数据",
				RequiredConditionTag = "[高风险 65% 成功率]",
				SuccessRate = 0.65f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.GainCurrency,
					CoreDelta = 1,
					ScrapDelta = 120,
					ResultLog = "破译成功！主机未损毁的数据阵列中提取出 1 颗【算力核心】和 120 废料！"
				},
				FailureOutcome = new EventOutcome
				{
					Type = OutcomeType.DamageModule,
					DamageAmount = 60.0f,
					ResultLog = "触发了反入侵防御电涌！全舰电子系统过载，外部构件受到 60 点电涌损伤！"
				}
			});
			ev1.Choices.Add(new EventChoice
			{
				ChoiceText = "派工程无人机切割外装甲板：稳妥搜刮金属废料",
				SuccessRate = 1.0f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.GainCurrency,
					ScrapDelta = 80,
					ResultLog = "无人机安全回收了 80 单位的高纯度装甲废料。"
				}
			});
			ev1.Choices.Add(new EventChoice
			{
				ChoiceText = "忽视漂流舰，保持航向离开",
				SuccessRate = 1.0f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.GainCurrency,
					ResultLog = "你选择不冒任何未知风险，战舰平稳加速驶离了碎片区。"
				}
			});
			Events[ev1.Id] = ev1;

			// =========================================================
			// 事件 2: 生化废料走私船遇险
			// =========================================================
			var ev2 = new SpaceEventNode
			{
				Id = "ev_biohazard_smuggler",
				Title = "【异象】生化走私船的求救信号",
				FactionTag = "废土走私帮",
				ThemeColor = new Color(0.45f, 0.90f, 0.35f),
				Description = "一艘舷号被刮花的走私轻护卫舰发生了严重的生化酸蚀泄漏，绿色的腐蚀酸雾正从其引擎排气口喷涌而出，受困船员向你的战舰发送了紧急过载压制求援。"
			};
			ev2.Choices.Add(new EventChoice
			{
				ChoiceText = "消耗 60 废料提供紧急中和剂救援船员",
				RequiredConditionTag = "[需要 60 ⚙]",
				RequiredScraps = 60,
				SuccessRate = 1.0f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.GainCurrency,
					ScrapDelta = -60,
					CoreDelta = 1,
					ResultLog = "获救的走私船长感激涕零，将藏在走私暗格中的 1 颗【算力核心】赠送给你！"
				}
			});
			ev2.Choices.Add(new EventChoice
			{
				ChoiceText = "无视求救，直接用磁力抓钩拖拽其外部货箱",
				RequiredConditionTag = "[50% 成功率 / 50% 酸蚀腐蚀]",
				SuccessRate = 0.50f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.GainCurrency,
					ScrapDelta = 140,
					ResultLog = "你成功掠夺了货箱，获得 140 单位走私废料！"
				},
				FailureOutcome = new EventOutcome
				{
					Type = OutcomeType.DamageModule,
					DamageAmount = 80.0f,
					ResultLog = "货箱破裂泄漏！强酸直接腐蚀了你的舰首模块，造成 80 点严重酸蚀损伤！"
				}
			});
			ev2.Choices.Add(new EventChoice
			{
				ChoiceText = "广播通报巡逻队坐标并撤离",
				SuccessRate = 1.0f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.GainCurrency,
					ResultLog = "你发送了遇险坐标信标，避免了卷入走私帮的麻烦。"
				}
			});
			Events[ev2.Id] = ev2;

			// =========================================================
			// 事件 3: 流亡机械神教传道者
			// =========================================================
			var ev3 = new SpaceEventNode
			{
				Id = "ev_mech_cultist",
				Title = "【异象】流亡机械神教传道者",
				FactionTag = "机械神教流亡者",
				ThemeColor = new Color(0.95f, 0.65f, 0.25f),
				Description = "一艘装饰着金色齿轮和粗大铜导线的重型修道船靠近了你。一位自称「第七逻辑使徒」的赛博主教请求对你的飞船配线与动力炉进行『神圣过载超频』。"
			};
			ev3.Choices.Add(new EventChoice
			{
				ChoiceText = "接受机械主教的『神圣布线祝福』(彻底修补全舰)",
				SuccessRate = 0.80f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.RepairShip,
					RepairRatio = 0.50f,
					ResultLog = "神圣仪式重整了战舰的配线，全舰构件耐久度瞬间恢复 +50%！"
				},
				FailureOutcome = new EventOutcome
				{
					Type = OutcomeType.DamageModule,
					DamageAmount = 45.0f,
					ResultLog = "过载短路！神圣仪式引发了配线火花，部分逻辑构件损耗了 45 点耐久。"
				}
			});
			ev3.Choices.Add(new EventChoice
			{
				ChoiceText = "向机械教会捐献 100 废料以换取高阶运算组件",
				RequiredConditionTag = "[需要 100 ⚙]",
				RequiredScraps = 100,
				SuccessRate = 1.0f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.GainCurrency,
					ScrapDelta = -100,
					CoreDelta = 1,
					ResultLog = "教会收下废料，授予你 1 颗经过机械圣水浸润的【军用算力核心】！"
				}
			});
			ev3.Choices.Add(new EventChoice
			{
				ChoiceText = "拒绝接触，礼貌关闭通讯通道",
				SuccessRate = 1.0f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.GainCurrency,
					ResultLog = "你婉拒了使徒的传道，修道船缓缓滑入暗区。"
				}
			});
			Events[ev3.Id] = ev3;

			// =========================================================
			// 事件 4: 虚空海盗隐匿雷区
			// =========================================================
			var ev4 = new SpaceEventNode
			{
				Id = "ev_pirate_minefield",
				Title = "【异象】海盗潜伏雷区与战利品浮标",
				FactionTag = "虚空海盗",
				ThemeColor = new Color(0.95f, 0.25f, 0.35f),
				Description = "前方的跃迁信道被海盗布设了大量的隐匿等离子水雷，中央漂浮着一颗被遗弃的武器补给浮标，周围电磁信号极为紊乱。"
			};
			ev4.Choices.Add(new EventChoice
			{
				ChoiceText = "利用断路破译技术：远程引爆雷区并打捞补给",
				RequiredConditionTag = "[70% 成功率]",
				SuccessRate = 0.70f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.GainCurrency,
					ScrapDelta = 150,
					ResultLog = "精准引爆！连锁爆炸清空了雷区，打捞获得 150 单位军用废料！"
				},
				FailureOutcome = new EventOutcome
				{
					Type = OutcomeType.DamageModule,
					DamageAmount = 70.0f,
					ResultLog = "破译延迟导致近距水雷引爆！爆炸破片重创了装甲外壳 (-70 HP)！"
				}
			});
			ev4.Choices.Add(new EventChoice
			{
				ChoiceText = "开启主引擎全向阻尼，极慢速绕道通过",
				SuccessRate = 1.0f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.GainCurrency,
					ResultLog = "飞船安全穿过了雷区，未造成任何损失与消耗。"
				}
			});
			Events[ev4.Id] = ev4;

			// =========================================================
			// 事件 5: 脉冲恒星耀斑爆发
			// =========================================================
			var ev5 = new SpaceEventNode
			{
				Id = "ev_solar_flare",
				Title = "【异象】超临界脉冲耀斑爆发",
				FactionTag = "恒星天文灾害",
				ThemeColor = new Color(1.0f, 0.80f, 0.20f),
				Description = "伴星爆发了极其剧烈的超临界电磁耀斑，强烈的带电粒子风暴即将横扫你所在的战术扇区，飞船护盾与电容系统发出刺耳的告警声！"
			};
			ev5.Choices.Add(new EventChoice
			{
				ChoiceText = "张开电容储能矩阵：直面粒子风暴强行吸收电能",
				RequiredConditionTag = "[高收益 / 50% 核心过载风险]",
				SuccessRate = 0.50f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.GainCurrency,
					ScrapDelta = 180,
					ResultLog = "电容矩阵完美蓄能！吸收的高纯度等离子废料提炼出 +180 废料！"
				},
				FailureOutcome = new EventOutcome
				{
					Type = OutcomeType.DamageModule,
					DamageAmount = 85.0f,
					ResultLog = "电容过载炸裂！高压电流在舱内乱窜，造成 85 点严重热力损伤！"
				}
			});
			ev5.Choices.Add(new EventChoice
			{
				ChoiceText = "躲入小行星阴影区规避风暴",
				SuccessRate = 1.0f,
				SuccessOutcome = new EventOutcome
				{
					Type = OutcomeType.GainCurrency,
					ResultLog = "小行星岩体为你挡住了致命的粒子风暴，飞船安然无恙。"
				}
			});
			Events[ev5.Id] = ev5;
		}

		public static SpaceEventNode GetRandomEvent()
		{
			var list = new List<SpaceEventNode>(Events.Values);
			return list[(int)GD.RandRange(0, list.Count - 1)];
		}
	}
}
