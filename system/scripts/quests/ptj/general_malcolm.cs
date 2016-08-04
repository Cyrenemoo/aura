// MISSINGNO prevents this script from compiling.
// The following MISSINGNO have unknown values at the time of writing.
// (Tip: Perform a find and replace all operation to define them.)
/* MISSINGNO000  QuestID: Basic  Garment Delivery (Nora)
 * MISSINGNO002  QuestID: Basic  Garment Delivery (Caitin)
 * MISSINGNO003  QuestID: Basic  Item Delivery (Dilys)
 * MISSINGNO005  QuestID: Basic  Tailor 2 Wizard Hats
 * MISSINGNO006  QuestID: Basic  Tailor 2 Headbands
 * MISSINGNO030  QuestID: Int    Garment Delivery (Bebhinn)
 * MISSINGNO032  QuestID: Int    Garment Delivery (Lassar)
 * MISSINGNO033  QuestID: Int    Garment Delivery (Caitin)
 * MISSINGNO034  QuestID: Int    Weave Thin Thread Ball
 * MISSINGNO035  QuestID: Int    Weave Thick Thread Ball
 * MISSINGNO036  QuestID: Int    Weave 2 Thin Thread Balls
 * MISSINGNO037  QuestID: Int    Weave 2 Thick Thread Balls
 * MISSINGNO038  QuestID: Int    Tailor 2 Cores Ninja Suits (M)
 * MISSINGNO039  QuestID: Int    Tailor 2 Magic School Uniforms (M)
 * MISSINGNO040  QuestID: Int    Tailor 2 Guardian Gloves
 * MISSINGNO041  QuestID: Int    Tailor 2 Cores' Healer Suits
 * MISSINGNO060  QuestID: Adv    Garment Delivery (Lassar)
 * MISSINGNO061  QuestID: Adv    Weave 2 Thin Thread Balls
 * MISSINGNO062  QuestID: Adv    Weave 2 Thick Thread Balls
 * MISSINGNO063  QuestID: Adv    Tailor 2 Lirina's Long Skirts
 * MISSINGNO064  QuestID: Adv    Tailor 2 Mongo's Hats
 * MISSINGNO065  QuestID: Adv    Tailor 2 Cloth Mails
 * MISSINGNO066  QuestID: Adv    Tailor 2 Light Leather Mails (M)
 */

// Class name resolution - go go gadget intellisense
using Aura.Mabi;
using Aura.Mabi.Const;
using System.Threading.Tasks;
using Aura.Channel.Scripting.Scripts;
using Aura.Shared.Util;
using System;
using Aura.Channel.World.Entities;
using Aura.Channel.World.Quests;

//--- Aura Script -----------------------------------------------------------
// Malcolm's General Store Part-Time Job
//--- Description -----------------------------------------------------------
// All quests used by the PTJ, and a script to handle the PTJ via hooks.
//--- Notes -----------------------------------------------------------------
// Apparently the rewards have been increased, as the Wiki now lists
// higher ones than in the past. We'll use the G1 rewards for now.
// Due to limited G1 data, some current rewards are downscaled to 50% gold, 33% exp.
// Tailoring jobs have remained unchanged.
// Ref: http://wiki.mabinogiworld.com/index.php?title=Malcolm&oldid=145857#Part-time_Jobs
//
// Also, starting materials for tailoring jobs are most likely inaccurate.
//---------------------------------------------------------------------------

public class MalcolmPtjScript : GeneralScript
{
	const PtjType JobType = PtjType.GeneralShop;

	const int Start = 7;
	//int Report; // Variable - Extracted from Player's current PTJ quest data.
	const int Deadline = 19;
	const int PerDay = 8;

	int remaining = PerDay;

	readonly int[] QuestIds = new int[]
	{
		MISSINGNO005, // Basic  Tailor 2 Wizard Hats
		MISSINGNO006, // Basic  Tailor 2 Headbands
		508207, // Basic  Tailor 2 Popo's Skirts (F)
		508210, // Basic  Tailor 2 Mongo's Traveler Suits (F)
		508212, // Basic  Tailor 2 Leather Bandanas
		MISSINGNO003, // Basic  Garment Delivery (Dilys)
		MISSINGNO000, // Basic  Garment Delivery (Nora) // Appears twice on mabiwiki?
		508401, // Basic  Garment Delivery (Caitin) // Confirm?
		508402, // Int    Garment Delivery (Nora)
		508403, // Basic  Garment Delivery (Lassar)
		508405, // Basic  Garment Delivery (Bebhinn)
		508501, // Basic  Weave Thick Thread Ball
		508502, // Basic  Weave Thin Thread Ball
		MISSINGNO030, // Int    Garment Delivery (Bebhinn)
		MISSINGNO032, // Int    Garment Delivery (Lassar)
		MISSINGNO033, // Int    Garment Delivery (Caitin)
		MISSINGNO036, // Int    Weave 2 Thin Thread Balls
		MISSINGNO037, // Int    Weave 2 Thick Thread Balls
		MISSINGNO038, // Int    Tailor 2 Cores Ninja Suits (M)
		MISSINGNO039, // Int    Tailor 2 Magic School Uniforms (M)
		MISSINGNO040, // Int    Tailor 2 Guardian Gloves
		MISSINGNO041, // Int    Tailor 2 Cores' Healer Suits
		MISSINGNO060, // Adv    Garment Delivery (Lassar)
		MISSINGNO061, // Adv    Weave 2 Thin Thread Balls
		MISSINGNO062, // Adv    Weave 2 Thick Thread Balls
		MISSINGNO063, // Adv    Tailor 2 Lirina's Long Skirts
		MISSINGNO064, // Adv    Tailor 2 Mongo's Hats
		MISSINGNO065, // Adv    Tailor 2 Cloth Mails
		MISSINGNO066, // Adv    Tailor 2 Light Leather Mails (M)
	};

	/// <summary>
	/// Precondition: <paramref name="player"/> is already working for Malcolm.
	/// </summary>
	/// <returns>
	/// This <paramref name="player"/>'s report time for their PTJ.
	/// <para>If unable to obtain (invalid state), a fallback of 1 AM will be returned.</para>
	/// </returns>
	/// <remarks>
	/// Report time changes depending on the task Malcolm gives to the player:
	/// Noon - Basic tailoring or weaving jobs
	/// 5 PM - Advanced tailoring jobs
	/// 9 AM - Everything else
	/// </remarks>
	private int GetPersonalReportTime(Creature player)
	{
		Quest quest = player.Quests.GetPtjQuest();
		if (quest == null)
		{ // This should not normally happen.
			Log.Error("Player {0} does not have a PTJ report time for Malcolm. Used fallback of 1 AM.", player.Name);
			return 1; // Fallback
		}
		else return quest.Data.ReportHour;
	}

	public override void Load()
	{
		AddHook("_malcolm", "after_intro", AfterIntro);
		AddHook("_malcolm", "before_keywords", BeforeKeywords);

		// Pre-populate report time and job description variables (Is this necessary?)
		//OnErinnMidnightTick(ErinnTime.Now);
	}

	public async Task<HookResult> AfterIntro(NpcScript npc, params object[] args)
	{
		// Handle receiving of Flowerpot from Bebhinn
		const int Flowerpot = 70010;
		int id;
		if (npc.QuestActive(id = 508405, "ptj3") || npc.QuestActive(id = MISSINGNO030, "ptj3"))
		{
			if (!npc.Player.Inventory.Has(Flowerpot))
				return HookResult.Continue;

			npc.Player.Inventory.Remove(Flowerpot, 1);
			npc.FinishQuest(id, "ptj3");

			npc.Notice(L("You have given Flowerpot to be Delivered to Malcolm."));

			npc.Msg(L("Oh, no.<br/>That Flowerpot, did Bebhinn give it to you instead of the money for the clothes?"));
			npc.Msg(Hide.Name, L("(Delivered the small Flowerpot to Malcolm.)"));
			npc.Msg(L("I have no idea why everyone brings me flowerpots instead of paying me in cash.<br/>Anyway, thank you.<br/>Hmm, it's weird, but I think I kind of like this flowerpot."));
			npc.Msg(Hide.Name, L("(Another Flowerpot is added to Malcolm's collection.)"));
		}

		// Call PTJ method after intro if it's time to report
		if (npc.DoingPtjForNpc() && npc.ErinnHour(GetPersonalReportTime(npc.Player), Deadline))
		{
			await AboutArbeit(npc);
			return HookResult.Break;
		}

		return HookResult.Continue;
	}

	[On("ErinnMidnightTick")]
	private void OnErinnMidnightTick(ErinnTime time)
	{
		// Reset available jobs
		remaining = PerDay;
	}

	public async Task<HookResult> BeforeKeywords(NpcScript npc, params object[] args)
	{
		var keyword = args[0] as string;

		// Hook PTJ keyword
		if (keyword == "about_arbeit")
		{
			await AboutArbeit(npc);
			await npc.Conversation();
			npc.End();

			return HookResult.End;
		}

		return HookResult.Continue;
	}

	public async Task AboutArbeit(NpcScript npc)
	{
		// Check if already doing another PTJ
		if (npc.DoingPtjForOtherNpc())
		{
			npc.Msg(L("You seem to be on another job.<br/>You should finish it first."));
			return;
		}

		// Check if PTJ is in progress
		if (npc.DoingPtjForNpc())
		{
			var result = npc.GetPtjResult();

			// Check if report time
			if (!npc.ErinnHour(GetPersonalReportTime(npc.Player), Deadline))
			{
				if (result == QuestResult.Perfect)
				{
					npc.Msg(L("Oh...<br/>Would you come after the deadline starts?"));
					npc.Msg(L("Thanks for your help."));
				}
				else
				{
					npc.Msg(L("How's it going?<br/>"));
					npc.Msg(L("I'm expecting good things from you."));
				}
				return;
			}

			// Report?
			npc.Msg(L("Ah, did you finish the work?<br/>Would you like to report now?<br/>If you haven't finished the job, you can report later."),
				npc.Button(L("Report Now"), "@report"),
				npc.Button(L("Report Later"), "@later")
				);

			if (await npc.Select() != "@report")
			{
				npc.Msg(L("You can report any time before the deadline ends."));
				return;
			}

			// Nothing done
			if (result == QuestResult.None)
			{
				npc.GiveUpPtj();

				npc.Msg(npc.FavorExpression(), L("Did I ask too much of you?<br/>Sorry, but I can't pay you because you didn't work at all. Please understand."));
				npc.ModifyRelation(0, -Random(3), 0);
			}
			// Low~Perfect result
			else
			{
				npc.Msg(L("To tell you the truth, I was worried.<br/>But... <username/>, I think I can count on you from here on out.<br/>This is just a token of my gratitude.<br/>Choose whatever you like."),
					npc.Button(L("Report Later"), "@later"),
					npc.PtjReport(result)
					);
				var reply = await npc.Select();

				// Report later
				if (!reply.StartsWith("@reward:"))
				{
					npc.Msg(npc.FavorExpression(), L("OK then. Come again later.<br/>... But don't be late."));
					return;
				}

				// Complete
				npc.CompletePtj(reply);
				remaining--;

				// Result msg
				if (result == QuestResult.Perfect)
				{
					npc.Msg(npc.FavorExpression(), L("Oh... Thank you.<br/>I appreciate your help."));
					npc.ModifyRelation(0, Random(3), 0);
				}
				else if (result == QuestResult.Mid)
				{
					npc.Msg(npc.FavorExpression(), L("Well... It is not enough,<br/>but I'm grateful for your help. However, I have to reduce your pay. I hope you'll understand."));
					npc.ModifyRelation(0, Random(1), 0);
				}
				else if (result == QuestResult.Low)
				{
					npc.Msg(npc.FavorExpression(), L("I guess you were busy with something else...<br/>It's not much, but I'll pay you for what you've done."));
					npc.ModifyRelation(0, -Random(2), 0);
				}
			}
			return;
		}

		// Check if PTJ time
		if (!npc.ErinnHour(Start, Deadline))
		{
			npc.Msg(L("Sorry, but it is not time for part-time jobs.<br/>Would you come later?"));
			return;
		}

		// Check if not done today and if there are jobs remaining
		if (!npc.CanDoPtj(JobType, remaining))
		{
			npc.Msg(L("I don't have anymore work to give you today.<br/>Would you come back tomorrow?"));
			return;
		}

		// Offer PTJ
		var randomPtj = npc.RandomPtj(JobType, QuestIds);

		var msg = "";
		if (npc.GetPtjDoneCount(JobType) == 0)
			msg = L("Our town may be small, but running the General Shop<br/>can really get hectic since I'm running this all by myself.<br/>Fortunately, many people are helping me out, so it's a lot easier for me to handle.<br/>Are you also interested in working here, <username/>?<p/>I'll pay you if you can help me.");
		else
			msg = L("Are you here to work at the General Shop?");

		var ptjDescTitle = "";
		switch (/* Pattern against QuestId to determine job type */) // Pending: QuestIDs (How should it be matched?)
		{
			case "WeavingJob":
				ptjDescTitle = L("Looking for weavers."); 
				break;
			case "TailoringJob":
				ptjDescTitle = L("Looking for help with crafting items needed for General Shop."); 
				break;
			default:
				ptjDescTitle = L("Looking for help with delivery of goods in General Shop.");
				break;
		}

		npc.Msg(msg, npc.PtjDesc(randomPtj,
			L("Malcolm's General Shop Part-Time Job"),
			ptjDescTitle,
			PerDay, remaining, npc.GetPtjDoneCount(JobType)));

		if (await npc.Select() == "@accept")
		{
			GiveStartingPtjItems(randomPtj, npc.Player);

			if (npc.GetPtjDoneCount(JobType) == 0)
				npc.Msg(L("Thank you.<br/>Then I'll see you when the deadline starts.<br/>Please report your work even if you couldn't finish it on time. That way, I can get on with other jobs without worry."));
			else
				npc.Msg(L("Thanks, and good luck!"));

			npc.StartPtj(randomPtj);
		}
		else
		{
			if (npc.GetPtjDoneCount(JobType) == 0)
				npc.Msg(L("Oh, well.<br/>If you change your mind, let me know."));
			else
				npc.Msg(L("Oh, I misunderstood.<br/>I'm sorry."));
		}
	}

	/// <summary>
	/// Depending on the supplied <paramref name="questId"/>, will give the player the needed materials to complete their task.
	/// </summary>
	private void GiveStartingPtjItems(int questId, Creature player)
	{
		Action<int> PlayerGivePattern = (int formId) =>
		{
			Item pattern = new Item(60600);
			pattern.MetaData1.SetInt("FORMID", formId);
			player.GiveItem(pattern);
		};

		switch (questId)
		{
			case MISSINGNO005: // Basic  Tailor 2 Wizard Hats
				PlayerGivePattern(?);
				player.GiveItem(60424, 5);  // Common Leather (Part-Time Job)
				player.GiveItem(60415, 2);  // Cheap Finishing Thread (Part-Time Job)
				player.GiveItem(60424, 5);  // Common Leather (Part-Time Job)
				break;

			case MISSINGNO006: // Basic  Tailor 2 Headbands
				PlayerGivePattern(?);
				player.GiveItem(60419, 2);  // Cheap Fabric (Part-Time Job)
				player.GiveItem(60415, 2);  // Cheap Finishing Thread (Part-Time Job)
				player.GiveItem(60419, 2);  // Cheap Fabric (Part-Time Job)
				break;

			case 508207: // Basic  Tailor 2 Popo's Skirts (F)
				PlayerGivePattern(10106);   // Apprentice Sewing Pattern - Popo's Skirt (F)
				player.GiveItem(60419, 2);  // Cheap Fabric (Part-Time Job)
				player.GiveItem(60415, 2);  // Cheap Finishing Thread (Part-Time Job)
				player.GiveItem(60419, 2);  // Cheap Fabric (Part-Time Job)
				break;

			case 508210: // Basic  Tailor 2 Mongo's Traveler Suits (F)
				PlayerGivePattern(10107);   // Apprentice Sewing Pattern - Mongo Traveler Suit (F)
				player.GiveItem(60411, 10); // Cheap Silk (Part-Time Job)
				player.GiveItem(60407, 5);  // Thin Thread Ball (Part-Time Job)
				player.GiveItem(60407, 5);  // Thin Thread Ball (Part-Time Job)
				player.GiveItem(60416, 2);  // Common Finishing Thread (Part-Time Job)
				player.GiveItem(60411, 10); // Cheap Silk (Part-Time Job)
				player.GiveItem(60407, 5);  // Thin Thread Ball (Part-Time Job)
				player.GiveItem(60407, 5);  // Thin Thread Ball (Part-Time Job)
				break;

			case 508212: // Basic  Tailor 2 Leather Bandanas
				PlayerGivePattern(10113);   // Apprentice Sewing Pattern - Leather Bandana
				player.GiveItem(60419, 3);  // Cheap Fabric (Part-Time Job)
				player.GiveItem(60424, 3);  // Common Leather (Part-Time Job)
				player.GiveItem(60416, 2);  // Common Finishing Thread (Part-Time Job)
				player.GiveItem(60419, 3);  // Cheap Fabric (Part-Time Job)
				player.GiveItem(60424, 3);  // Common Leather (Part-Time Job)
				break;

			case MISSINGNO038: // Int    Tailor 2 Cores Ninja Suits (M)
				PlayerGivePattern(?);
				player.GiveItem(60420, 8);  // Common Fabric (Part-Time Job)
				player.GiveItem(60412, 8);  // Common Silk (Part-Time Job)
				player.GiveItem(60406, 5);  // Thick Thread Ball (Part-Time Job)
				player.GiveItem(60406, 5);  // Thick Thread Ball (Part-Time Job)
				player.GiveItem(60406, 5);  // Thick Thread Ball (Part-Time Job)
				player.GiveItem(60415, 2);  // Cheap Finishing Thread (Part-Time Job)
				player.GiveItem(60420, 8);  // Common Fabric (Part-Time Job)
				player.GiveItem(60412, 8);  // Common Silk (Part-Time Job)
				player.GiveItem(60406, 5);  // Thick Thread Ball (Part-Time Job)
				player.GiveItem(60406, 5);  // Thick Thread Ball (Part-Time Job)
				player.GiveItem(60406, 5);  // Thick Thread Ball (Part-Time Job)
				break;

			case MISSINGNO039: // Int    Tailor 2 Magic School Uniforms (M)
				PlayerGivePattern(?);
				player.GiveItem(60419, 10); // Cheap Fabric (Part-Time Job)
				player.GiveItem(60419, 5);  // Cheap Fabric (Part-Time Job)
				player.GiveItem(60412, 10); // Common Silk (Part-Time Job)
				player.GiveItem(60412, 5);  // Common Silk (Part-Time Job)
				player.GiveItem(60428, 10); // Common Leather Strap (Part-Time Job)
				player.GiveItem(60428, 5);  // Common Leather Strap (Part-Time Job)
				player.GiveItem(60417, 2);  // Fine Finishing Thread (Part-Time Job)
				player.GiveItem(60419, 10); // Cheap Fabric (Part-Time Job)
				player.GiveItem(60419, 5);  // Cheap Fabric (Part-Time Job)
				player.GiveItem(60412, 10); // Common Silk (Part-Time Job)
				player.GiveItem(60412, 5);  // Common Silk (Part-Time Job)
				player.GiveItem(60428, 10); // Common Leather Strap (Part-Time Job)
				player.GiveItem(60428, 5);  // Common Leather Strap (Part-Time Job)
				break;

			case MISSINGNO040: // Int    Tailor 2 Guardian Gloves
				PlayerGivePattern(?);
				player.GiveItem(60424, 10); // Common Leather (Part-Time Job)
				player.GiveItem(60406, 5);  // Thick Thread Ball (Part-Time Job)
				player.GiveItem(60406, 5);  // Thick Thread Ball (Part-Time Job)
				player.GiveItem(60417, 2);  // Fine Finishing Thread (Part-Time Job)
				player.GiveItem(60418, 2);  // Finest Finishing Thread (Part-Time Job)
				player.GiveItem(60424, 10); // Common Leather (Part-Time Job)
				player.GiveItem(60406, 5);  // Thick Thread Ball (Part-Time Job)
				player.GiveItem(60406, 5);  // Thick Thread Ball (Part-Time Job)
				break;

			case MISSINGNO041: // Int    Tailor 2 Cores' Healer Suits
				PlayerGivePattern(?);
				player.GiveItem(60420, 5);  // Common Fabric (Part-Time Job)
				player.GiveItem(60419, 5);  // Cheap Fabric (Part-Time Job)
				player.GiveItem(60427, 5);  // Cheap Leather Strap (Part-Time Job)
				player.GiveItem(60418, 2);  // Finest Finishing Thread (Part-Time Job)
				player.GiveItem(60420, 5);  // Common Fabric (Part-Time Job)
				player.GiveItem(60419, 5);  // Cheap Fabric (Part-Time Job)
				player.GiveItem(60427, 5);  // Cheap Leather Strap (Part-Time Job)
				break;

			case MISSINGNO063: // Adv    Tailor 2 Lirina's Long Skirts
				PlayerGivePattern(?);
				player.GiveItem(60413, 5);  // Fine Silk (Part-Time Job)
				player.GiveItem(60407, 10); // Thin Thread Ball (Part-Time Job)
				player.GiveItem(60417, 2);  // Fine Finishing Thread (Part-Time Job)
				player.GiveItem(60413, 5);  // Fine Silk (Part-Time Job)
				player.GiveItem(60407, 10); // Thin Thread Ball (Part-Time Job)
				break;

			case MISSINGNO064: // Adv    Tailor 2 Mongo's Hats
				PlayerGivePattern(?);
				player.GiveItem(60422, 5);  // Finest Fabric (Part-Time Job)
				player.GiveItem(60412, 5);  // Common Silk (Part-Time Job)
				player.GiveItem(60418, 2);  // Finest Finishing Thread (Part-Time Job)
				player.GiveItem(60422, 5);  // Finest Fabric (Part-Time Job)
				player.GiveItem(60412, 5);  // Common Silk (Part-Time Job)
				break;

			case MISSINGNO065: // Adv    Tailor 2 Cloth Mails
				PlayerGivePattern(?);
				player.GiveItem(60422, 10); // Finest Fabric (Part-Time Job)
				player.GiveItem(60422, 10); // Finest Fabric (Part-Time Job)
				player.GiveItem(60407, 10); // Thin Thread Ball (Part-Time Job)
				player.GiveItem(60404, 5);  // Braid (Part-Time Job)
				player.GiveItem(60404, 5);  // Braid (Part-Time Job)
				player.GiveItem(60418, 2);  // Finest Finishing Thread (Part-Time Job)
				player.GiveItem(60422, 10); // Finest Fabric (Part-Time Job)
				player.GiveItem(60422, 10); // Finest Fabric (Part-Time Job)
				player.GiveItem(60407, 10); // Thin Thread Ball (Part-Time Job)
				player.GiveItem(60404, 5);  // Braid (Part-Time Job)
				player.GiveItem(60404, 5);  // Braid (Part-Time Job)
				break;

			case MISSINGNO066: // Adv    Tailor 2 Light Leather Mails (M)
				PlayerGivePattern(?);
				player.GiveItem(60421, 8); // Fine Fabric (Part-Time Job)
				player.GiveItem(60412, 8); // Common Silk (Part-Time Job)
				player.GiveItem(60428, 8); // Common Leather Strap (Part-Time Job)
				player.GiveItem(60417, 2);  // Fine Finishing Thread (Part-Time Job)
				player.GiveItem(60404, 2);  // Braid (Part-Time Job)
				player.GiveItem(60421, 8); // Fine Fabric (Part-Time Job)
				player.GiveItem(60412, 8); // Common Silk (Part-Time Job)
				player.GiveItem(60428, 8); // Common Leather Strap (Part-Time Job)
				break;

			default:
				break;
		}
	}
}

// Delivery base script
public abstract class MalcolmDeliveryPtjBaseScript : QuestScript
{
	protected const int Garment = 70003;

	protected abstract int QuestId { get; }
	protected abstract string NpcIdent { get; }
	protected abstract string NpcName { get; }
	protected abstract string Objective { get; }

	public override void Load()
	{
		SetId(QuestId);
		SetName(L("General Shop Part-Time Job"));
		SetDescription(L("I need to [deliver the items] that arrived today, but I can't afford to leave the shop unattended. Could you deliver the goods for me? - Malcolm -"));

		if (IsEnabled("QuestViewRenewal"))
			SetCategory(QuestCategory.ById);

		SetType(QuestType.Deliver);
		SetPtjType(PtjType.GeneralShop);
		SetHours(start: 7, report: 9, deadline: 19);

		AddObjective("ptj", this.Objective, 0, 0, 0, Deliver(Garment, NpcIdent));
		AddHook(NpcIdent, "after_intro", AfterIntro);
	}

	public async Task<HookResult> AfterIntro(NpcScript npc, params object[] args)
	{
		if (!npc.QuestActive(this.Id, "ptj"))
			return HookResult.Continue;

		if (!npc.Player.Inventory.Has(Garment))
			return HookResult.Continue;

		npc.Player.Inventory.Remove(Garment, 1);
		npc.Notice(L(string.Format("You have given Garment to be Delivered to {0}.", NpcName)));
		npc.FinishQuest(this.Id, "ptj");

		await this.OnFinish(npc);

		return HookResult.Break;
	}

	protected virtual async Task OnFinish(NpcScript npc)
	{
		await Task.Yield();
	}

	protected void SetBasicLevelAndRewards()
	{
		SetLevel(QuestLevel.Basic);

		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(200));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(250));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(100));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(125));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(40));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(50));

		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Exp(65));
		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Gold(260));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Exp(33));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Gold(130));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Exp(13));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Gold(52));

		AddReward(3, RewardGroupType.Exp, QuestResult.Perfect, Exp(320));
		AddReward(3, RewardGroupType.Exp, QuestResult.Perfect, Gold(65));
		AddReward(3, RewardGroupType.Exp, QuestResult.Mid, Exp(160));
		AddReward(3, RewardGroupType.Exp, QuestResult.Mid, Gold(33));
		AddReward(3, RewardGroupType.Exp, QuestResult.Low, Exp(64));
		AddReward(3, RewardGroupType.Exp, QuestResult.Low, Gold(13));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(50004)); // Bread
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Gold(600));

		AddReward(5, RewardGroupType.Item, QuestResult.Perfect, Item(51001, 6)); // HP 10 Potion
		AddReward(5, RewardGroupType.Item, QuestResult.Perfect, Gold(25));

		AddReward(6, RewardGroupType.Item, QuestResult.Perfect, Item(51011, 5)); // Stamina 10 Potion
		AddReward(6, RewardGroupType.Item, QuestResult.Perfect, Gold(275));
	}

	protected void SetIntLevelAndRewards()
	{
		SetLevel(QuestLevel.Int);

		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(300));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(250));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(150));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(125));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(60));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(50));

		AddReward(2, RewardGroupType.Item, QuestResult.Perfect, Item(50004)); // Bread
		AddReward(2, RewardGroupType.Item, QuestResult.Perfect, Gold(444));

		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Item(51001, 6)); // HP 10 Potion
		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Gold(400));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(51011, 5)); // Stamina 10 Potion
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Gold(450));
	}

	protected void SetAdvLevelAndRewards()
	{
		SetLevel(QuestLevel.Adv);

		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(350));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(525));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(175));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(263));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(70));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(105));

		AddReward(2, RewardGroupType.Item, QuestResult.Perfect, Item(19001)); // Robe
		AddReward(2, RewardGroupType.Item, QuestResult.Perfect, Gold(410));

		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Item(19002)); // Slender Robe
		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Gold(410));
	}
}

public class MalcolmDeliveryCaitinBasicPtjScript : MalcolmDeliveryPtjBaseScript
{
	protected override int QuestId { get { return 508401; } }
	protected override string NpcIdent { get { return "_caitin"; } }
	protected override string NpcName { get { return "Caitin"; } }
	protected override string Objective { get { return L("Deliver Garment to Caitin at the Grocery Store"); } }

	public override void Load()
	{
		SetBasicLevelAndRewards();

		base.Load();
	}

	protected override async Task OnFinish(NpcScript npc)
	{
		// (Caitin has no dialogue.)
	}
}

public class MalcolmDeliveryCaitinIntPtjScript : MalcolmDeliveryPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO033; } }
	protected override string NpcIdent { get { return "_caitin"; } }
	protected override string NpcName { get { return "Caitin"; } }
	protected override string Objective { get { return L("Deliver Garment to Caitin at the Grocery Store"); } }

	public override void Load()
	{
		SetIntLevelAndRewards();

		base.Load();
	}

	protected override async Task OnFinish(NpcScript npc)
	{
		// (Caitin has no dialogue.)
	}
}

public class MalcolmDeliveryNoraBasicPtjScript : MalcolmDeliveryPtjBaseScript
{
	protected override int QuestId { get { return 508402; } }
	protected override string NpcIdent { get { return "_nora"; } }
	protected override string NpcName { get { return "Nora"; } }
	protected override string Objective { get { return L("Deliver Garment to Nora at the Inn"); } }

	public override void Load()
	{
		SetBasicLevelAndRewards();

		base.Load();
	}

	protected override async Task OnFinish(NpcScript npc)
	{
		npc.Msg(L("Malcolm sent you here, right?<br/>I cannot understand this guy!"));
		npc.Msg(Hide.Name, L("(Delivered the clothes to Nora.)"));
		npc.Msg(L("Sorry, I'm not mad at you.<br/>The thing is, I never ordered this.<br/>I really don't understand why he keeps sending me stuff I didn't ask for."));
		npc.Msg(L("I might be tempted to take it, but it's not really my taste anyway.<br/>I guess I will just return it to him... again."));
		npc.Msg(Hide.Name, L("(It seems pretty obvious why Malcolm is doing this.)"));
	}
}

public class MalcolmDeliveryLassarBasicPtjScript : MalcolmDeliveryPtjBaseScript
{
	protected override int QuestId { get { return 508403; } }
	protected override string NpcIdent { get { return "_lassar"; } }
	protected override string NpcName { get { return "Lassar"; } }
	protected override string Objective { get { return L("Deliver Clothes to Lassar at the School"); } }

	public override void Load()
	{
		SetBasicLevelAndRewards();

		base.Load();
	}

	protected override async Task OnFinish(NpcScript npc)
	{
		npc.Msg(L("Oh, this is for me?<br/>You're so sweet. How did you know my favorite style?"));
		npc.Msg(Hide.Name, L("(Delivered the clothes to Lassar.)"));
		npc.Msg(L("Oh, I see now... This is what I ordered from Malcolm.<br/>Ha ha, sorry for my mistake.<br/>I didn't mean to say that in hopes of getting free clothes. Don't worry about that."));
		npc.Msg(L("Anyway, look at it. This is the style that I like.<br/>I thought you would like to remember it, just in case. That's all.<br/>Really."));
		npc.Msg(Hide.Name, L("(The pressure becomes unbearable.)"));
	}
}

public class MalcolmDeliveryLassarIntPtjScript : MalcolmDeliveryPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO032; } }
	protected override string NpcIdent { get { return "_lassar"; } }
	protected override string NpcName { get { return "Lassar"; } }
	protected override string Objective { get { return L("Deliver Clothes to Lassar at the School"); } }

	public override void Load()
	{
		SetIntLevelAndRewards();

		base.Load();
	}

	protected override async Task OnFinish(NpcScript npc)
	{
		npc.Msg(L("Oh, this is for me?<br/>You're so sweet. How did you know my favorite style?"));
		npc.Msg(Hide.Name, L("(Delivered the clothes to Lassar.)"));
		npc.Msg(L("Oh, I see now... This is what I ordered from Malcolm.<br/>Ha ha, sorry for my mistake.<br/>I didn't mean to say that in hopes of getting free clothes. Don't worry about that."));
		npc.Msg(L("Anyway, look at it. This is the style that I like.<br/>I thought you would like to remember it, just in case. That's all.<br/>Really."));
		npc.Msg(Hide.Name, L("(The pressure becomes unbearable.)"));
	}
}

public class MalcolmDeliveryLassarAdvPtjScript : MalcolmDeliveryPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO060; } }
	protected override string NpcIdent { get { return "_lassar"; } }
	protected override string NpcName { get { return "Lassar"; } }
	protected override string Objective { get { return L("Deliver Clothes to Lassar at the School"); } }

	public override void Load()
	{
		SetAdvLevelAndRewards();

		base.Load();
	}

	protected override async Task OnFinish(NpcScript npc)
	{
		npc.Msg(L("Oh, this is for me?<br/>You're so sweet. How did you know my favorite style?"));
		npc.Msg(Hide.Name, L("(Delivered the clothes to Lassar.)"));
		npc.Msg(L("Oh, I see now... This is what I ordered from Malcolm.<br/>Ha ha, sorry for my mistake.<br/>I didn't mean to say that in hopes of getting free clothes. Don't worry about that."));
		npc.Msg(L("Anyway, look at it. This is the style that I like.<br/>I thought you would like to remember it, just in case. That's all.<br/>Really."));
		npc.Msg(Hide.Name, L("(The pressure becomes unbearable.)"));
	}
}

// Extended delivery quest - Bebhinn
public abstract class MalcolmExtDeliveryBebhinnPtjBaseScript : QuestScript
{
	protected const int Garment = 70003;
	protected const int Flowerpot = 70010;

	public override void Load()
	{
		SetName(L("General Shop Part-Time Job"));
		SetDescription(L("I need to [deliver the items] that arrived today, but I can't afford to leave the shop unattended. Could you deliver the goods for me? - Malcolm -"));

		if (IsEnabled("QuestViewRenewal"))
			SetCategory(QuestCategory.ById);

		SetType(QuestType.Deliver);
		SetPtjType(PtjType.GeneralShop);
		SetHours(start: 7, report: 9, deadline: 19);

		AddObjective("ptj1", L("Deliver Garment to Bebhinn at the Bank"), 0, 0, 0, Deliver(Garment, "_bebhinn"));
		AddObjective("ptj2", L("Keep talking to Bebhinn about payment"), 0, 0, 0, Talk("_bebhinn"));
		AddObjective("ptj3", L("Deliver Flowerpot to Malcolm at the General Shop."), 0, 0, 0, Deliver(Flowerpot, "_malcolm"));

		AddHook("_bebhinn", "after_intro", BebhinnAfterIntro);
		// Malcolm is handled above, as we have to deliver the item to him 
		// before he asks us about the PTJ.
	}

	public async Task<HookResult> BebhinnAfterIntro(NpcScript npc, params object[] args)
	{
		if (npc.QuestActive(this.Id, "ptj1"))
		{
			if (!npc.Player.Inventory.Has(Garment))
				return HookResult.Continue;

			npc.FinishQuest(this.Id, "ptj1");

			npc.Player.RemoveItem(Garment, 1);
			npc.Notice(L("You have given Garment to be Delivered to Bebhinn."));

			npc.Msg(L("Wow, so the clothes I ordered have finally arrived.<br/>Thank you so much! Wow, I really like this style!"));
			npc.Msg(Hide.Name, L("(Delivered the clothes to Bebhinn.)"));
			npc.Msg(L("Yes? Payment?<br/>What? 1500G???!!!!!<br/>So, Malcolm's done it again. That guy always relies on others to do his dirty work!"));
			npc.Msg(L("Anyway, I can't pay you. I don't have it! It's the bank that has money, not the banker!<br/>Tell him to come get it himself!"));
			npc.Msg(Hide.Name, L("(Intimidated by Bebhinn's rant, you failed to receive any payment for the clothes.)"));

			return HookResult.Break;
		}
		else if (npc.QuestActive(this.Id, "ptj2"))
		{
			npc.FinishQuest(this.Id, "ptj2");

			npc.Player.GiveItem(Flowerpot, 1);
			npc.Notice(L("You have received Flowerpot to be Delivered from Bebhinn."));

			npc.Msg(L("Oh, give me a break! Go tell Malcolm to put it on my bill and I'll pay him later."));
			npc.Msg(Hide.Name, L("(Keep asking Bebhinn for payment, saying you won't be able to get a reward otherwise.)"));
			npc.Msg(L("OK, OK, I know it's not your fault after all.<br/>Stupid Malcolm, he should have come himself."));
			npc.Msg(L("But, I don't have the money with me now.<br/>So, can you take this to him instead and make sure to tell him this?<br/>I'll definitely pay the bill later."));
			npc.Msg(Hide.Name, L("(Received a small Flowerpot from Bebhinn.)"));

			return HookResult.Break;
		}
		else return HookResult.Continue;
	}
}

public class MalcolmExtDeliveryBebhinnBasicPtjScript : MalcolmExtDeliveryBebhinnPtjBaseScript
{
	public override void Load()
	{
		SetId(508405);
		SetLevel(QuestLevel.Basic);

		AddReward(1, RewardGroupType.Exp, QuestResult.Perfect, Exp(200));
		AddReward(1, RewardGroupType.Exp, QuestResult.Perfect, Gold(175));
		AddReward(1, RewardGroupType.Exp, QuestResult.Mid, Exp(100));
		AddReward(1, RewardGroupType.Exp, QuestResult.Mid, Gold(87));
		AddReward(1, RewardGroupType.Exp, QuestResult.Low, Exp(40));
		AddReward(1, RewardGroupType.Exp, QuestResult.Low, Gold(35));

		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Exp(70));
		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Gold(260));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Exp(35));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Gold(130));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Exp(14));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Gold(52));

		base.Load();
	}
}

public class MalcolmExtDeliveryBebhinnIntPtjScript : MalcolmExtDeliveryBebhinnPtjBaseScript
{
	public override void Load()
	{
		SetId(MISSINGNO030);
		SetLevel(QuestLevel.Int);

		AddReward(1, RewardGroupType.Exp, QuestResult.Perfect, Exp(350));
		AddReward(1, RewardGroupType.Exp, QuestResult.Perfect, Gold(250));
		AddReward(1, RewardGroupType.Exp, QuestResult.Mid, Exp(175));
		AddReward(1, RewardGroupType.Exp, QuestResult.Mid, Gold(125));
		AddReward(1, RewardGroupType.Exp, QuestResult.Low, Exp(70));
		AddReward(1, RewardGroupType.Exp, QuestResult.Low, Gold(50));

		AddReward(2, RewardGroupType.Item, QuestResult.Perfect, Item(63020)); // Empty Bottle
		AddReward(2, RewardGroupType.Item, QuestResult.Perfect, Gold(100));

		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Item(50004)); // Bread
		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Gold(450));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(51001, 6)); // HP 10 Potion
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Gold(400));

		base.Load();
	}
}

public abstract class MalcolmThreadBallPtjBaseScript : QuestScript
{
	public const int ThickThreadBall = 60006;
	public const int ThinThreadBall = 60007;

	protected abstract int QuestId { get; }
	protected abstract string QuestDescription { get; }

	public override void Load()
	{
		SetId(QuestId);
		SetName(L("General Shop Part-Time Job"));
		SetDescription(QuestDescription);

		if (IsEnabled("QuestViewRenewal"))
			SetCategory(QuestCategory.ById);

		SetType(QuestType.Deliver);
		SetPtjType(PtjType.GeneralShop);
		SetHours(start: 7, report: 12, deadline: 19);
	}

	protected void SetBasicLevelAndRewards()
	{
		SetLevel(QuestLevel.Basic);

		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(350));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(120));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(175));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(60));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(70));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(24));

		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Exp(90));
		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Gold(320));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Exp(45));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Gold(160));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Exp(18));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Gold(64));

		AddReward(3, RewardGroupType.Exp, QuestResult.Perfect, Exp(400));
		AddReward(3, RewardGroupType.Exp, QuestResult.Perfect, Gold(80));
		AddReward(3, RewardGroupType.Exp, QuestResult.Mid, Exp(200));
		AddReward(3, RewardGroupType.Exp, QuestResult.Mid, Gold(40));
		AddReward(3, RewardGroupType.Exp, QuestResult.Low, Exp(80));
		AddReward(3, RewardGroupType.Exp, QuestResult.Low, Gold(16));
	}

	protected void SetIntLevelAndRewards()
	{
		SetLevel(QuestLevel.Int);

		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(600));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(150));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(300));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(75));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(120));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(30));

		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Exp(630));
		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Gold(130));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Exp(315));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Gold(65));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Exp(126));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Gold(26));

		AddReward(3, RewardGroupType.Gold, QuestResult.Perfect, Exp(140));
		AddReward(3, RewardGroupType.Gold, QuestResult.Perfect, Gold(500));
		AddReward(3, RewardGroupType.Gold, QuestResult.Mid, Exp(70));
		AddReward(3, RewardGroupType.Gold, QuestResult.Mid, Gold(250));
		AddReward(3, RewardGroupType.Gold, QuestResult.Low, Exp(28));
		AddReward(3, RewardGroupType.Gold, QuestResult.Low, Gold(100));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(19002)); // Slender Robe
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Gold(125));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(18024)); // Hairband
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Gold(25));
	}

	protected void SetAdvLevelAndRewards()
	{
		SetLevel(QuestLevel.Adv);

		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(700));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(240));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(350));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(120));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(140));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(48));

		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Exp(180));
		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Gold(640));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Exp(90));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Gold(320));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Exp(36));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Gold(128));

		AddReward(3, RewardGroupType.Exp, QuestResult.Perfect, Exp(800));
		AddReward(3, RewardGroupType.Exp, QuestResult.Perfect, Gold(160));
		AddReward(3, RewardGroupType.Exp, QuestResult.Mid, Exp(400));
		AddReward(3, RewardGroupType.Exp, QuestResult.Mid, Gold(90));
		AddReward(3, RewardGroupType.Exp, QuestResult.Low, Exp(160));
		AddReward(3, RewardGroupType.Exp, QuestResult.Low, Gold(32));
	}
}

public class MalcolmThinThreadBallBasicPtjScript : MalcolmThreadBallPtjBaseScript
{
	protected override int QuestId { get { return 508501; } }
	protected override string QuestDescription { get { return L("This job is to make and supply a ball of thin thread to the General Shop. Make [1 ball of thin thread] today and then submit it. Use cobwebs to spin thin thread with a spinning wheel."); } }

	public override void Load()
	{
		SetBasicLevelAndRewards();

		AddObjective("ptj1", L("Make 1 Ball of Thin Thread."), 0, 0, 0, Create(ThinThreadBall, 1, SkillId.Weaving));
		AddObjective("ptj2", L("Collect 1 Ball of Thin Thread."), 0, 0, 0, Collect(ThinThreadBall, 1));

		base.Load();
	}
}

public class MalcolmThickThreadBallBasicPtjScript : MalcolmThreadBallPtjBaseScript
{
	protected override int QuestId { get { return 508502; } }
	protected override string QuestDescription { get { return L("This job is to make and supply a ball of thick thread to the General Shop. Make [1 ball of thick thread] today and then deliver it. Use wool to spin thick thread with a spinning wheel."); } }

	public override void Load()
	{
		SetBasicLevelAndRewards();

		AddObjective("ptj1", L("Make 1 Ball of Thick Thread."), 0, 0, 0, Create(ThickThreadBall, 1, SkillId.Weaving));
		AddObjective("ptj2", L("Collect 1 Ball of Thick Thread."), 0, 0, 0, Collect(ThickThreadBall, 1));

		base.Load();
	}
}

public class MalcolmThinThreadBallIntPtjScript : MalcolmThreadBallPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO036; } }
	protected override string QuestDescription { get { return L("This job is to make and supply a ball of thin thread to the General Shop. Make [1 ball of thin thread] today and then submit it. Use cobwebs to spin thin thread with a spinning wheel."); } }

	public override void Load()
	{
		SetIntLevelAndRewards();

		AddObjective("ptj1", L("Make 2 Balls of Thin Thread."), 0, 0, 0, Create(ThinThreadBall, 2, SkillId.Weaving));
		AddObjective("ptj2", L("Collect 1 Ball of Thin Thread."), 0, 0, 0, Collect(ThinThreadBall, 1));

		base.Load();
	}
}

public class MalcolmThickThreadBallIntPtjScript : MalcolmThreadBallPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO037; } }
	protected override string QuestDescription { get { return L("This job is to make and supply a ball of thick thread to the General Shop. Make [1 ball of thick thread] today and then deliver it. Use wool to spin thick thread with a spinning wheel."); } }

	public override void Load()
	{
		SetIntLevelAndRewards();

		AddObjective("ptj1", L("Make 2 Balls of Thick Thread."), 0, 0, 0, Create(ThickThreadBall, 2, SkillId.Weaving));
		AddObjective("ptj2", L("Collect 1 Ball of Thick Thread."), 0, 0, 0, Collect(ThickThreadBall, 1));

		base.Load();
	}
}

public class MalcolmThinThreadBallAdvPtjScript : MalcolmThreadBallPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO061; } }
	protected override string QuestDescription { get { return L("This job is to make and supply two balls of thin thread to the General Shop. Make [2 balls of thin thread] today and then submit it. Use cobwebs to spin thin thread with a spinning wheel."); } }

	public override void Load()
	{
		SetAdvLevelAndRewards();

		AddObjective("ptj1", L("Make 2 Balls of Thin Thread."), 0, 0, 0, Create(ThinThreadBall, 2, SkillId.Weaving));
		AddObjective("ptj2", L("Collect 2 Balls of Thin Thread."), 0, 0, 0, Collect(ThinThreadBall, 2));

		base.Load();
	}
}

public class MalcolmThickThreadBallAdvPtjScript : MalcolmThreadBallPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO062; } }
	protected override string QuestDescription { get { return L("This job is to make and supply two balls of thick thread to the General Shop. Make [2 balls of thick thread] today and then deliver it. Use wool to spin thick thread with a spinning wheel."); } }

	public override void Load()
	{
		SetAdvLevelAndRewards();

		AddObjective("ptj1", L("Make 2 Balls of Thick Thread."), 0, 0, 0, Create(ThickThreadBall, 2, SkillId.Weaving));
		AddObjective("ptj2", L("Collect 2 Balls of Thick Thread."), 0, 0, 0, Collect(ThickThreadBall, 2));

		base.Load();
	}
}

public abstract class MalcolmTailorPtjBaseScript : QuestScript
{
	protected abstract int QuestId { get; }
	protected abstract string ItemsName { get; }
	protected abstract int ItemId { get; }
	protected abstract QuestLevel QuestLevel { get; }
	protected abstract void AddRewards();

	public override void Load()
	{
		SetId(QuestId);
		SetName(L("General Shop Part-Time Job"));
		SetDescription(L(string.Format("This job is tailoring and supplying clothes to the General Shop. Today's order is tailoring [2 {0}], using the materials given for this part-time job. Make sure to bring it to me no earlier than noon. Keep that in mind when delivering the goods, since I can't use them before then.", ItemsName)));

		if (IsEnabled("QuestViewRenewal"))
			SetCategory(QuestCategory.ById);

		SetType(QuestType.Deliver);
		SetPtjType(PtjType.GeneralShop);
		SetLevel(QuestLevel);
		SetHours(start: 7, report: QuestLevel == QuestLevel.Adv ? 17 : 12, deadline: 19);

		AddObjective("ptj1", L(string.Format("Make 2 {0} (Part-Time Job)", ItemsName)), 0, 0, 0, Create(ItemId, 2, SkillId.Tailoring));
		AddObjective("ptj2", L(string.Format("2 {0} (Part-Time Job)", ItemsName)), 0, 0, 0, Collect(ItemId, 2));

		AddRewards();
	}
}

public class MalcolmTailorWizardHatBasicPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO005; } }
	protected override string ItemsName { get { return "Wizard Hats"; } }
	protected override int ItemId { get { return 60612; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Basic; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(250));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(600));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(125));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(300));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(50));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(120));

		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Exp(180));
		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Gold(650));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Exp(90));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Gold(325));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Exp(36));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Gold(130));

		AddReward(3, RewardGroupType.Exp, QuestResult.Perfect, Exp(830));
		AddReward(3, RewardGroupType.Exp, QuestResult.Perfect, Gold(160));
		AddReward(3, RewardGroupType.Exp, QuestResult.Mid, Exp(415));
		AddReward(3, RewardGroupType.Exp, QuestResult.Mid, Gold(80));
		AddReward(3, RewardGroupType.Exp, QuestResult.Low, Exp(166));
		AddReward(3, RewardGroupType.Exp, QuestResult.Low, Gold(32));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(40025)); // Pickaxe
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Gold(62));
	}
}

public class MalcolmTailorHeadbandHatBasicPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO006; } }
	protected override string ItemsName { get { return "Headbands"; } }
	protected override int ItemId { get { return 60614; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Basic; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(250));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(600));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(125));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(300));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(50));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(120));

		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Exp(180));
		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Gold(650));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Exp(90));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Gold(325));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Exp(36));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Gold(130));

		AddReward(3, RewardGroupType.Exp, QuestResult.Perfect, Exp(830));
		AddReward(3, RewardGroupType.Exp, QuestResult.Perfect, Gold(160));
		AddReward(3, RewardGroupType.Exp, QuestResult.Mid, Exp(415));
		AddReward(3, RewardGroupType.Exp, QuestResult.Mid, Gold(80));
		AddReward(3, RewardGroupType.Exp, QuestResult.Low, Exp(166));
		AddReward(3, RewardGroupType.Exp, QuestResult.Low, Gold(32));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(40025)); // Pickaxe
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Gold(62));
	}
}

public class MalcolmTailorPoposSkirtBasicPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return 508207; } }
	protected override string ItemsName { get { return "Popo's Skirts (F)"; } }
	protected override int ItemId { get { return 60606; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Basic; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(250));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(600));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(125));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(300));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(50));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(120));

		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Exp(180));
		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Gold(650));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Exp(90));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Gold(325));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Exp(36));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Gold(130));

		AddReward(3, RewardGroupType.Exp, QuestResult.Perfect, Exp(830));
		AddReward(3, RewardGroupType.Exp, QuestResult.Perfect, Gold(160));
		AddReward(3, RewardGroupType.Exp, QuestResult.Mid, Exp(415));
		AddReward(3, RewardGroupType.Exp, QuestResult.Mid, Gold(80));
		AddReward(3, RewardGroupType.Exp, QuestResult.Low, Exp(166));
		AddReward(3, RewardGroupType.Exp, QuestResult.Low, Gold(32));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(40025)); // Pickaxe
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Gold(62));
	}
}

public class MalcolmTailorMongosTravelerSuitBasicPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return 508210; } }
	protected override string ItemsName { get { return "Mongo's Traveler Suits (F)"; } }
	protected override int ItemId { get { return 60607; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Basic; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(250));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(650));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(125));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(325));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(50));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(130));

		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Exp(200));
		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Gold(690));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Exp(100));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Gold(345));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Exp(40));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Gold(138));

		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Item(18024)); // Hairband
		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Gold(262));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(40025)); // Pickaxe
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Gold(112));

		AddReward(5, RewardGroupType.Item, QuestResult.Perfect, Item(18012)); // Tork's Merchant Cap
		AddReward(5, RewardGroupType.Item, QuestResult.Perfect, Gold(262));
	}
}

public class MalcolmTailorLeatherBandanaBasicPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return 508212; } }
	protected override string ItemsName { get { return "Leather Bandanas"; } }
	protected override int ItemId { get { return 60613; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Basic; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(250));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(650));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(125));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(325));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(50));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(130));

		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Exp(200));
		AddReward(2, RewardGroupType.Gold, QuestResult.Perfect, Gold(690));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Exp(100));
		AddReward(2, RewardGroupType.Gold, QuestResult.Mid, Gold(345));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Exp(40));
		AddReward(2, RewardGroupType.Gold, QuestResult.Low, Gold(138));

		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Item(18024)); // Hairband
		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Gold(262));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(40025)); // Pickaxe
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Gold(112));

		AddReward(5, RewardGroupType.Item, QuestResult.Perfect, Item(18012)); // Tork's Merchant Cap
		AddReward(5, RewardGroupType.Item, QuestResult.Perfect, Gold(262));
	}
}

public class MalcolmTailorCoresNinjaSuitMIntPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO038; } }
	protected override string ItemsName { get { return "Cores Ninja Suits (M)"; } }
	protected override int ItemId { get { return 60618; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Int; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(300));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(900));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(150));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(450));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(60));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(180));

		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Exp(1200));
		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Gold(230));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Exp(600));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Gold(115));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Exp(240));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Gold(46));

		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Item(41086)); // Lute
		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Gold(150));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(17025)); // Sandal
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Gold(250));
	}
}

public class MalcolmTailorMagicSchoolUniformMIntPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO039; } }
	protected override string ItemsName { get { return "Magic School Uniforms (M)"; } }
	protected override int ItemId { get { return 60602; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Int; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(300));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(900));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(150));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(450));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(60));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(180));

		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Exp(1200));
		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Gold(230));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Exp(600));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Gold(115));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Exp(240));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Gold(46));

		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Item(60034, 300)); // Bait Tin
		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Gold(250));
	}
}

public class MalcolmTailorGuarduanGloveIntPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO040; } }
	protected override string ItemsName { get { return "Guardian Gloves"; } }
	protected override int ItemId { get { return 60611; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Int; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(300));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(1000));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(150));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(500));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(60));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(200));

		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Exp(1300));
		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Gold(250));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Exp(650));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Gold(125));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Exp(260));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Gold(50));

		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Item(40044)); // Ladle
		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Gold(375));
	}
}

public class MalcolmTailorCoresHealerSuitIntPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO041; } }
	protected override string ItemsName { get { return "Cores' Healer Suits"; } }
	protected override int ItemId { get { return 60610; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Int; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(300));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(900));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(150));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(450));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(60));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(180));

		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Exp(1200));
		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Gold(220));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Exp(600));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Gold(110));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Exp(240));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Gold(44));
	}
}

public class MalcolmTailorLirinaLongSkirtAdvPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO063; } }
	protected override string ItemsName { get { return "Lirina's Long Skirts"; } }
	protected override int ItemId { get { return 60617; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Adv; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(400));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(1200));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(200));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(600));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(80));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(240));

		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Exp(1600));
		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Gold(300));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Exp(800));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Gold(150));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Exp(320));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Gold(60));

		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Item(40004)); // Lute
		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Gold(525));
	}
}

public class MalcolmTailorMongoHatsAdvPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO064; } }
	protected override string ItemsName { get { return "Mongo's Hats"; } }
	protected override int ItemId { get { return 60605; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Adv; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(400));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(1300));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(200));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(650));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(80));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(260));

		AddReward(2, RewardGroupType.Item, QuestResult.Perfect, Item(40004)); // Lute
		AddReward(2, RewardGroupType.Item, QuestResult.Perfect, Gold(625));

		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Item(60034, 300)); // Bait Tin
		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Gold(725));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(17025)); // Sandal
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Gold(725));
	}
}

public class MalcolmTailorClothMailsAdvPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO065; } }
	protected override string ItemsName { get { return "Cloth Mails"; } }
	protected override int ItemId { get { return 60609; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Adv; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(400));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(1400));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(200));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(700));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(80));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(280));

		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Exp(1800));
		AddReward(2, RewardGroupType.Exp, QuestResult.Perfect, Gold(350));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Exp(900));
		AddReward(2, RewardGroupType.Exp, QuestResult.Mid, Gold(175));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Exp(360));
		AddReward(2, RewardGroupType.Exp, QuestResult.Low, Gold(70));

		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Item(18016)); // Hat
		AddReward(3, RewardGroupType.Item, QuestResult.Perfect, Gold(25));

		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(60034, 90)); // Bait Tin
		AddReward(4, RewardGroupType.Item, QuestResult.Perfect, Item(40045));     // Fishing Rod
	}
}

public class MalcolmTailorLightLeatherMailMAdvPtjScript : MalcolmTailorPtjBaseScript
{
	protected override int QuestId { get { return MISSINGNO066; } }
	protected override string ItemsName { get { return "Light Leather Mails (M)"; } }
	protected override int ItemId { get { return 60620; } } // confirm?
	protected override QuestLevel QuestLevel { get { return QuestLevel.Adv; } }

	protected override void AddRewards()
	{
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Exp(400));
		AddReward(1, RewardGroupType.Gold, QuestResult.Perfect, Gold(1500));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Exp(200));
		AddReward(1, RewardGroupType.Gold, QuestResult.Mid, Gold(750));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Exp(80));
		AddReward(1, RewardGroupType.Gold, QuestResult.Low, Gold(300));

		AddReward(2, RewardGroupType.Item, QuestResult.Perfect, Item(60034, 140)); // Bait Tin
		AddReward(2, RewardGroupType.Item, QuestResult.Perfect, Item(40045));     // Fishing Rod
	}
}