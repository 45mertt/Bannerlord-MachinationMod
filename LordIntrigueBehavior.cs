using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace RebellionsAndDemographics {

public class LordIntrigueBehavior : CampaignBehaviorBase
{
	public enum IntrigueOfferType
	{
		None,
		Slander,
		Sabotage,
		SecretPact
	}

	private Dictionary<Hero, IntrigueOfferType> _activeAiOffers = new Dictionary<Hero, IntrigueOfferType>();

	private Hero _currentTargetLord;

	private IntrigueOfferType _currentIntrigueType;

	private Dictionary<Hero, CampaignTime> _intrigueCooldowns = new Dictionary<Hero, CampaignTime>();

	private Dictionary<Hero, CampaignTime> _intrigueHateCooldowns = new Dictionary<Hero, CampaignTime>();

	private bool _isRumorSeeded;

	private bool _isRumorActive;

	private int _rumorTimerDays;

	private Hero _rumorSourceLord;

	private int _finalGoldCost;

	private int _finalInfCost;

	public static Hero PendingTreasonTarget;

	private List<PersuasionTask> _currentTasks;

	private static Hero _slanderTarget1;

	private static Hero _slanderTarget2;

	private static Hero _slanderTarget3;

	private static Settlement _sabotageTarget1;

	private static Settlement _sabotageTarget2;

	private static Settlement _sabotageTarget3;

	public Dictionary<Hero, IntrigueOfferType> ActiveAiOffers
	{
		get
		{
			return _activeAiOffers ?? (_activeAiOffers = new Dictionary<Hero, IntrigueOfferType>());
		}
		set
		{
			_activeAiOffers = value;
		}
	}

	public Dictionary<Hero, CampaignTime> IntrigueCooldowns
	{
		get
		{
			return _intrigueCooldowns ?? (_intrigueCooldowns = new Dictionary<Hero, CampaignTime>());
		}
		set
		{
			_intrigueCooldowns = value;
		}
	}

	public Dictionary<Hero, CampaignTime> IntrigueHateCooldowns
	{
		get
		{
			return _intrigueHateCooldowns ?? (_intrigueHateCooldowns = new Dictionary<Hero, CampaignTime>());
		}
		set
		{
			_intrigueHateCooldowns = value;
		}
	}

	public override void RegisterEvents()
	{
		CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
		CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
	}

	public override void SyncData(IDataStore dataStore)
	{
		if (dataStore.IsSaving)
		{
                foreach (var pair in ActiveAiOffers.ToList()) { if (!(pair.Key != null && pair.Key.IsAlive)) ActiveAiOffers.Remove(pair.Key); }
                foreach (var pair in IntrigueCooldowns.ToList()) { if (!(pair.Key != null && pair.Key.IsAlive)) IntrigueCooldowns.Remove(pair.Key); }
                foreach (var pair in IntrigueHateCooldowns.ToList()) { if (!(pair.Key != null && pair.Key.IsAlive)) IntrigueHateCooldowns.Remove(pair.Key); }
		}
		dataStore.SyncData("_activeAiOffers", ref _activeAiOffers);
		dataStore.SyncData("_isRumorSeeded", ref _isRumorSeeded);
		dataStore.SyncData("_isRumorActive", ref _isRumorActive);
		dataStore.SyncData("_rumorTimerDays", ref _rumorTimerDays);
		dataStore.SyncData("_rumorSourceLord", ref _rumorSourceLord);
		dataStore.SyncData("_intrigueCooldowns", ref _intrigueCooldowns);
		dataStore.SyncData("_intrigueHateCooldowns", ref _intrigueHateCooldowns);
		if (dataStore.IsLoading)
		{
			if (_activeAiOffers == null)
			{
				_activeAiOffers = new Dictionary<Hero, IntrigueOfferType>();
			}
			if (_intrigueCooldowns == null)
			{
				_intrigueCooldowns = new Dictionary<Hero, CampaignTime>();
			}
			if (_intrigueHateCooldowns == null)
			{
				_intrigueHateCooldowns = new Dictionary<Hero, CampaignTime>();
			}
                foreach (var pair in _activeAiOffers.ToList()) { if (!(pair.Key != null && pair.Key.IsAlive)) _activeAiOffers.Remove(pair.Key); }
                foreach (var pair in _intrigueCooldowns.ToList()) { if (!(pair.Key != null && pair.Key.IsAlive)) _intrigueCooldowns.Remove(pair.Key); }
                foreach (var pair in _intrigueHateCooldowns.ToList()) { if (!(pair.Key != null && pair.Key.IsAlive)) _intrigueHateCooldowns.Remove(pair.Key); }
		}
	}

	private void OnDailyTick()
	{
		if (_isRumorSeeded)
		{
			_rumorTimerDays--;
			if (_rumorTimerDays <= 0)
			{
				_isRumorSeeded = false;
				_isRumorActive = true;
				ApplyRumorPenalties();
				InformationManager.DisplayMessage(new InformationMessage($"Rumors are spreading in the palace that you are plotting dark intrigues! You have learned that the source of the gossip is {_rumorSourceLord?.Name}. You must go and confront him!", Colors.Red));
				_rumorTimerDays = MBRandom.RandomInt(4, 7);
			}
		}
		else if (_isRumorActive)
		{
			_rumorTimerDays--;
			if (_rumorTimerDays <= 0)
			{
				ApplyRumorPenalties();
				InformationManager.DisplayMessage(new InformationMessage($"Dark rumors about you continue to circulate in the palace! Your relationships are damaged. Confront {_rumorSourceLord?.Name} immediately!", Colors.Red));
				_rumorTimerDays = MBRandom.RandomInt(4, 7);
			}
		}
		if (Hero.MainHero.Clan == null || Hero.MainHero.Clan.Tier < 2 || Hero.MainHero.MapFaction == null || !Hero.MainHero.MapFaction.IsKingdomFaction || !(MBRandom.RandomFloat < 0.05f) || !(Hero.MainHero.MapFaction is Kingdom kingdom))
		{
			return;
		}
		List<Hero> list = kingdom.Heroes.Where((Hero h) => h != Hero.MainHero && h.IsAlive && h.Clan != Hero.MainHero.Clan && h.IsLord).ToList();
		if (list.Count > 0)
		{
			Hero hero = list[MBRandom.RandomInt(list.Count)];
			if (!_activeAiOffers.ContainsKey(hero))
			{
				IntrigueOfferType value = (IntrigueOfferType)MBRandom.RandomInt(1, 4);
				_activeAiOffers[hero] = value;
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_loc_lib_04}{NAME} sent you a secret message. He wants to meet face to face.").SetTextVariable("NAME", hero.Name).ToString(), Colors.Magenta));
			}
		}
	}

	private void OnSessionLaunched(CampaignGameStarter starter)
	{
		starter.AddDialogLine("intrigue_cooldown_reject", "start", "close_window", "{=rad_int_cooldown}You have exceeded your limits! Get out of my sight, I have nothing to talk to you about again!", () => (Hero.OneToOneConversationHero != null && _intrigueHateCooldowns != null && _intrigueHateCooldowns.TryGetValue(Hero.OneToOneConversationHero, out var value) && value.ElapsedDaysUntilNow < 7f) ? true : false, null, 2000);
		AddPlayerIntrigueDialogs(starter);
		AddAiIntrigueDialogs(starter);
		AddNegotiationDialogs(starter);
		AddRumorDialogs(starter);
	}

	private void AddPlayerIntrigueDialogs(CampaignGameStarter starter)
	{
		starter.AddPlayerLine("intrigue_player_start", "hero_main_options", "intrigue_player_select", "{=rad_auto_280}There are some private matters we need to discuss regarding the future of the kingdom.", delegate
		{
			if (Hero.OneToOneConversationHero != null && _intrigueCooldowns != null && _intrigueCooldowns.TryGetValue(Hero.OneToOneConversationHero, out var value) && value.ElapsedDaysUntilNow < 7f)
			{
				return false;
			}
			return Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.IsLord && !Hero.OneToOneConversationHero.IsFactionLeader && Hero.MainHero.Clan != null && Hero.OneToOneConversationHero.Clan != Hero.MainHero.Clan && Hero.MainHero.MapFaction != null && Hero.MainHero.MapFaction.IsKingdomFaction && Hero.MainHero.MapFaction.Leader != Hero.MainHero && Hero.OneToOneConversationHero.MapFaction == Hero.MainHero.MapFaction;
		}, null);
		starter.AddDialogLine("intrigue_player_select_resp", "intrigue_player_select", "intrigue_player_options", "{=rad_auto_250}{INTRIGUE_SELECT_RESP_TEXT}", delegate
		{
			if (Hero.OneToOneConversationHero.Clan.Tier > Hero.MainHero.Clan.Tier)
			{
				MBTextManager.SetTextVariable("INTRIGUE_SELECT_RESP_TEXT", "I am listening to you. However, these conversations should remain between us.");
			}
			else
			{
				MBTextManager.SetTextVariable("INTRIGUE_SELECT_RESP_TEXT", "I am listening to you, my lord. Order, it will remain between us.");
			}
			return true;
		}, null);
		starter.AddPlayerLine("intrigue_opt_slander", "intrigue_player_options", "intrigue_slander_select_target", "{=rad_auto_281}{SLANDER_OPTION_TEXT}", null, null, 100, delegate(out TextObject hintText)
		{
			MBTextManager.SetTextVariable("SLANDER_OPTION_TEXT", (Hero.OneToOneConversationHero.Clan.Tier > Hero.MainHero.Clan.Tier) ? "My lord, I think some of the names in the palace are harming the kingdom. Can you use your influence to start a rumor about this?" : "We need to discredit someone. I want you to spread rumors about him in the palace.");
			hintText = new TextObject("{=!}Not: Müzakere sonucunda bedel Nüfuz ve/veya Altýn olarak belirlenecektir.");
			return true;
		});
		starter.AddDialogLine("intrigue_slander_who", "intrigue_slander_select_target", "intrigue_slander_targets", "{=rad_auto_251}Interesting... Whose reputation are we damaging?", delegate
		{
			List<Hero> multipleTargetLords = GetMultipleTargetLords(3);
			_slanderTarget1 = ((multipleTargetLords.Count > 0) ? multipleTargetLords[0] : null);
			_slanderTarget2 = ((multipleTargetLords.Count > 1) ? multipleTargetLords[1] : null);
			_slanderTarget3 = ((multipleTargetLords.Count > 2) ? multipleTargetLords[2] : null);
			return true;
		}, null);
		starter.AddPlayerLine("intrigue_slander_target_1", "intrigue_slander_targets", "intrigue_negotiation_start", "{=rad_auto_282}Let's talk about {TARGET_LORD_NAME_1}.", delegate
		{
			if (_slanderTarget1 != null)
			{
				MBTextManager.SetTextVariable("TARGET_LORD_NAME_1", _slanderTarget1.Name);
				return true;
			}
			return false;
		}, delegate
		{
			_currentTargetLord = _slanderTarget1;
			_currentIntrigueType = IntrigueOfferType.Slander;
			SetupNegotiation();
		});
		starter.AddPlayerLine("intrigue_slander_target_2", "intrigue_slander_targets", "intrigue_negotiation_start", "{=rad_auto_283}Let's talk about {TARGET_LORD_NAME_2}.", delegate
		{
			if (_slanderTarget2 != null)
			{
				MBTextManager.SetTextVariable("TARGET_LORD_NAME_2", _slanderTarget2.Name);
				return true;
			}
			return false;
		}, delegate
		{
			_currentTargetLord = _slanderTarget2;
			_currentIntrigueType = IntrigueOfferType.Slander;
			SetupNegotiation();
		});
		starter.AddPlayerLine("intrigue_slander_target_3", "intrigue_slander_targets", "intrigue_negotiation_start", "{=rad_auto_284}Let's talk about {TARGET_LORD_NAME_3}.", delegate
		{
			if (_slanderTarget3 != null)
			{
				MBTextManager.SetTextVariable("TARGET_LORD_NAME_3", _slanderTarget3.Name);
				return true;
			}
			return false;
		}, delegate
		{
			_currentTargetLord = _slanderTarget3;
			_currentIntrigueType = IntrigueOfferType.Slander;
			SetupNegotiation();
		});
		starter.AddPlayerLine("intrigue_slander_target_cancel", "intrigue_slander_targets", "intrigue_cancel_reaction", "{=rad_auto_285}Never mind, I gave up.", null, null);
		starter.AddPlayerLine("intrigue_opt_sabotage", "intrigue_player_options", "intrigue_sabotage_select_target", "{=rad_auto_286}{SABOTAGE_OPTION_TEXT}", null, null, 100, delegate(out TextObject hintText)
		{
			MBTextManager.SetTextVariable("SABOTAGE_OPTION_TEXT", (Hero.OneToOneConversationHero.Clan.Tier > Hero.MainHero.Clan.Tier) ? "My lord, it may be to your advantage to create a disturbance on our rival's property. Can we use your men?" : "We must light the fire of rebellion on a rival's property. He sends his men there and causes trouble.");
			hintText = new TextObject("{=!}Not: Sabotajýn baþarýlý olmasý için müzakere sonunda belli bir miktar Altýn ödemeniz istenecektir.");
			return true;
		});
		starter.AddDialogLine("intrigue_sabotage_who", "intrigue_sabotage_select_target", "intrigue_sabotage_targets", "{=rad_auto_252}{SABOTAGE_WHO_TEXT}", delegate
		{
			List<Settlement> multipleTargetFiefs = GetMultipleTargetFiefs(3);
			_sabotageTarget1 = ((multipleTargetFiefs.Count > 0) ? multipleTargetFiefs[0] : null);
			_sabotageTarget2 = ((multipleTargetFiefs.Count > 1) ? multipleTargetFiefs[1] : null);
			_sabotageTarget3 = ((multipleTargetFiefs.Count > 2) ? multipleTargetFiefs[2] : null);
			MBTextManager.SetTextVariable("SABOTAGE_WHO_TEXT", "Dangerous waters... Which city are we confusing? (Note: If the sabotage is successful, the target's Safety and Loyalty drops by 30 points, and the risk of Rebellion increases greatly.)");
			return true;
		}, null);
		starter.AddPlayerLine("intrigue_sabotage_target_1", "intrigue_sabotage_targets", "intrigue_negotiation_start", "{=rad_auto_287}The city {TARGET_FIEF_NAME_1}.", delegate
		{
			if (_sabotageTarget1 != null)
			{
				MBTextManager.SetTextVariable("TARGET_FIEF_NAME_1", _sabotageTarget1.Name);
				return true;
			}
			return false;
		}, delegate
		{
			_currentTargetLord = _sabotageTarget1.OwnerClan.Leader;
			_currentIntrigueType = IntrigueOfferType.Sabotage;
			SetupNegotiation();
		});
		starter.AddPlayerLine("intrigue_sabotage_target_2", "intrigue_sabotage_targets", "intrigue_negotiation_start", "{=rad_auto_288}The city {TARGET_FIEF_NAME_2}.", delegate
		{
			if (_sabotageTarget2 != null)
			{
				MBTextManager.SetTextVariable("TARGET_FIEF_NAME_2", _sabotageTarget2.Name);
				return true;
			}
			return false;
		}, delegate
		{
			_currentTargetLord = _sabotageTarget2.OwnerClan.Leader;
			_currentIntrigueType = IntrigueOfferType.Sabotage;
			SetupNegotiation();
		});
		starter.AddPlayerLine("intrigue_sabotage_target_3", "intrigue_sabotage_targets", "intrigue_negotiation_start", "{=rad_auto_289}The city {TARGET_FIEF_NAME_3}.", delegate
		{
			if (_sabotageTarget3 != null)
			{
				MBTextManager.SetTextVariable("TARGET_FIEF_NAME_3", _sabotageTarget3.Name);
				return true;
			}
			return false;
		}, delegate
		{
			_currentTargetLord = _sabotageTarget3.OwnerClan.Leader;
			_currentIntrigueType = IntrigueOfferType.Sabotage;
			SetupNegotiation();
		});
		starter.AddPlayerLine("intrigue_sabotage_target_cancel", "intrigue_sabotage_targets", "intrigue_cancel_reaction", "{=rad_auto_290}Never mind, I gave up.", null, null);
		starter.AddPlayerLine("intrigue_opt_pact", "intrigue_player_options", "intrigue_negotiation_start", "{=rad_auto_291}{PACT_OPTION_TEXT}", () => Hero.OneToOneConversationHero.MapFaction == Hero.MainHero.MapFaction, delegate
		{
			_currentTargetLord = null;
			_currentIntrigueType = IntrigueOfferType.SecretPact;
			SetupNegotiation();
		}, 100, delegate(out TextObject hintText)
		{
			MBTextManager.SetTextVariable("PACT_OPTION_TEXT", (Hero.OneToOneConversationHero.Clan.Tier > Hero.MainHero.Clan.Tier) ? "My lord, if you support me in the parliament, I am ready to support you in everything." : "Let's support each other in the kingdom votes. Let's form a secret alliance among ourselves.");
			hintText = new TextObject("{=!}Not: Bedel müzakere sonucunda belirlenecektir.");
			return true;
		});
		starter.AddPlayerLine("intrigue_player_cancel", "intrigue_player_options", "intrigue_cancel_reaction", "{=rad_auto_292}It wasn't a big deal, forget it.", null, null);
		starter.AddDialogLine("intrigue_cancel_ai", "intrigue_cancel_reaction", "hero_main_options", "{=rad_auto_253}I understand. Was there anything else you wanted to talk about?", delegate
		{
			if (Hero.OneToOneConversationHero != null && !_isRumorSeeded && !_isRumorActive && MBRandom.RandomFloat < 0.1f)
			{
				SeedRumor(Hero.OneToOneConversationHero);
			}
			return true;
		}, null);
	}

	private void AddAiIntrigueDialogs(CampaignGameStarter starter)
	{
		starter.AddDialogLine("intrigue_ai_start", "start", "intrigue_ai_response", "{=rad_auto_254}{INTRIGUE_AI_START_TEXT}", delegate
		{
			if (Hero.OneToOneConversationHero != null && Hero.MainHero.MapFaction != null && Hero.OneToOneConversationHero.MapFaction == Hero.MainHero.MapFaction && _activeAiOffers.ContainsKey(Hero.OneToOneConversationHero))
			{
				if (Hero.OneToOneConversationHero.Clan.Tier > Hero.MainHero.Clan.Tier && Hero.MainHero != Hero.MainHero.MapFaction?.Leader)
				{
					MBTextManager.SetTextVariable("INTRIGUE_AI_START_TEXT", "I was waiting for you. There is a confidential matter that must remain between us...");
				}
				else
				{
					MBTextManager.SetTextVariable("INTRIGUE_AI_START_TEXT", "I've been waiting for you, my lord. There is a confidential matter that must remain between us...");
				}
				return true;
			}
			return false;
		}, null, 2000);
		starter.AddPlayerLine("intrigue_ai_response_1", "intrigue_ai_response", "intrigue_ai_offer", "{=rad_auto_293}I am listening to you.", null, null);
		starter.AddPlayerLine("intrigue_ai_response_2", "intrigue_ai_response", "intrigue_cancel_reaction", "{=rad_auto_294}I don't have time to deal with these right now.", null, delegate
		{
			_activeAiOffers.Remove(Hero.OneToOneConversationHero);
		});
		starter.AddDialogLine("intrigue_ai_offer_slander", "intrigue_ai_offer", "intrigue_ai_offer_response", "{=rad_auto_255}Thank you for coming with the secret news I sent you. The point is, I want you to spread rumors about {TARGET_LORD_NAME} in the palace. We must destroy his reputation. In return, I will pay you 5000 Denars.", delegate
		{
			if (_activeAiOffers[Hero.OneToOneConversationHero] == IntrigueOfferType.Slander)
			{
				Hero randomTargetLord = GetRandomTargetLord();
				if (randomTargetLord != null)
				{
					_currentTargetLord = randomTargetLord;
					MBTextManager.SetTextVariable("TARGET_LORD_NAME", randomTargetLord.Name);
					return true;
				}
			}
			return false;
		}, null);
		starter.AddDialogLine("intrigue_ai_offer_sabotage", "intrigue_ai_offer", "intrigue_ai_offer_response", "{=rad_auto_256}I will send my men to the city of {TARGET_FIEF_NAME} and cause chaos there. I need 5000 Denars for this job, if you cover the expenses, this job is done.", delegate
		{
			if (_activeAiOffers[Hero.OneToOneConversationHero] == IntrigueOfferType.Sabotage)
			{
				Settlement randomTargetFief = GetRandomTargetFief();
				if (randomTargetFief != null)
				{
					_currentTargetLord = randomTargetFief.OwnerClan.Leader;
					MBTextManager.SetTextVariable("TARGET_FIEF_NAME", randomTargetFief.Name);
					return true;
				}
			}
			return false;
		}, null);
		starter.AddDialogLine("intrigue_ai_offer_pact", "intrigue_ai_offer", "intrigue_ai_offer_response", "{=rad_auto_257}We must support each other in parliament and in voting. Let's form a secret alliance among ourselves.", () => _activeAiOffers[Hero.OneToOneConversationHero] == IntrigueOfferType.SecretPact, null);
		starter.AddPlayerLine("intrigue_ai_accept", "intrigue_ai_offer_response", "intrigue_ai_end_accept", "{=rad_auto_295}I accept.", null, delegate
		{
			IntrigueOfferType currentIntrigueType = _activeAiOffers[Hero.OneToOneConversationHero];
			_activeAiOffers.Remove(Hero.OneToOneConversationHero);
			_currentIntrigueType = currentIntrigueType;
			ApplyIntrigueSuccessAI();
		});
		starter.AddPlayerLine("intrigue_ai_decline", "intrigue_ai_offer_response", "intrigue_ai_end_decline", "{=rad_auto_296}I will not be a tool in such games.", null, delegate
		{
			_activeAiOffers.Remove(Hero.OneToOneConversationHero);
		});
		starter.AddDialogLine("intrigue_ai_end_accept_line", "intrigue_ai_end_accept", "hero_main_options", "{=rad_auto_258}Perfect. Between us.", null, null);
		starter.AddDialogLine("intrigue_ai_end_decline_line", "intrigue_ai_end_decline", "hero_main_options", "{=rad_auto_259}However you like. But forget this conversation.", null, null);
	}

	private void SetupNegotiation()
	{
	}

	private void ApplyDynamicPricing()
	{
		float num = 0f;
		float num2 = 0f;
		if (_currentIntrigueType == IntrigueOfferType.Sabotage)
		{
			num = 100000f;
		}
		if (_currentIntrigueType == IntrigueOfferType.Slander)
		{
			num2 = 50f;
		}
		if (_currentIntrigueType == IntrigueOfferType.SecretPact)
		{
			num = 50000f;
			num2 = 20f;
		}
		int relation = Hero.OneToOneConversationHero.GetRelation(Hero.MainHero);
		float num3 = 1f - MathF.Clamp(relation, -50f, 50f) / 100f;
		_finalGoldCost = (int)(num * num3);
		_finalInfCost = (int)(num2 * num3);
		if (num > 0f && _finalGoldCost < 100)
		{
			_finalGoldCost = 100;
		}
		if (num2 > 0f && _finalInfCost < 5)
		{
			_finalInfCost = 5;
		}
	}

	private string GetResistanceString(float resistance)
	{
		if (resistance <= 25f)
		{
			return "Very little";
		}
		if (resistance <= 50f)
		{
			return "Az";
		}
		if (resistance <= 80f)
		{
			return "Normal";
		}
		if (resistance <= 120f)
		{
			return "A lot";
		}
		return "Too much";
	}

	private string GetTensionString(float tension, float maxTension)
	{
		float num = tension / maxTension;
		if (num <= 0.2f)
		{
			return "Very little";
		}
		if (num <= 0.4f)
		{
			return "Az";
		}
		if (num <= 0.7f)
		{
			return "Normal";
		}
		if (num <= 0.9f)
		{
			return "A lot";
		}
		return "Too much";
	}

	private string GetResDropString(float drop)
	{
		if (drop <= 20f)
		{
			return "Low Drop";
		}
		if (drop <= 35f)
		{
			return "Normal Drop";
		}
		return "High Drop";
	}

	private void AddNegotiationDialogs(CampaignGameStarter starter)
	{
		starter.AddDialogLine("intrigue_negotiation_start_resp", "intrigue_negotiation_start", "intrigue_persuasion_options", "{=rad_auto_260}This is a serious matter. Why would I say yes to something like this?", delegate
		{
			SetupPersuasion();
			return true;
		}, null);
		starter.AddPlayerLine("intrigue_arg_1", "intrigue_persuasion_options", "intrigue_negotiation_reaction", "{=!}{ARG1_TEXT} {ARG1_CHANCE}", Condition_SetupOption1Text, null, 100, Condition_Clickable1, Delegate_GetOption1Args);
		starter.AddPlayerLine("intrigue_arg_2", "intrigue_persuasion_options", "intrigue_negotiation_reaction", "{=!}{ARG2_TEXT} {ARG2_CHANCE}", Condition_SetupOption2Text, null, 100, Condition_Clickable2, Delegate_GetOption2Args);
		starter.AddPlayerLine("intrigue_arg_3", "intrigue_persuasion_options", "intrigue_negotiation_reaction", "{=!}{ARG3_TEXT} {ARG3_CHANCE}", Condition_SetupOption3Text, null, 100, Condition_Clickable3, Delegate_GetOption3Args);
		starter.AddPlayerLine("intrigue_arg_4", "intrigue_persuasion_options", "intrigue_negotiation_reaction", "{=!}{ARG4_TEXT} {ARG4_CHANCE}", Condition_SetupOption4Text, null, 100, Condition_Clickable4, Delegate_GetOption4Args);
		starter.AddPlayerLine("intrigue_nego_player_cancel", "intrigue_persuasion_options", "intrigue_cancel_reaction", "{=rad_auto_301}Forget it, I gave up.", null, delegate
		{
			ConversationManager.EndPersuasion();
		});
		starter.AddDialogLine("intrigue_negotiation_reaction_line", "intrigue_negotiation_reaction", "intrigue_negotiation_next", "{PERSUASION_REACTION}", Condition_ReactionAndApply, null);
		starter.AddDialogLine("intrigue_negotiation_next_line", "intrigue_negotiation_next", "intrigue_persuasion_options", "Do you have another argument?", Condition_ShouldContinue, null);
		starter.AddDialogLine("intrigue_negotiation_reaction_fail", "intrigue_negotiation_next", "intrigue_fail_exit", "{=rad_int_fail_kick}You have exceeded your limits! Get out of my sight, I have nothing to talk to you about again!", () => ConversationManager.GetPersuasionIsFailure(), delegate
		{
			ConversationManager.EndPersuasion();
			ApplyIntrigueFail();
		});
		starter.AddDialogLine("intrigue_negotiation_reaction_success", "intrigue_negotiation_next", "intrigue_dynamic_price_eval", "{=rad_auto_262}Hmm... You may be right. It's in both our interests that we do this. But it doesn't happen for free.", () => ConversationManager.GetPersuasionProgressSatisfied(), delegate
		{
			ConversationManager.EndPersuasion();
			ApplyDynamicPricing();
		});
		starter.AddDialogLine("intrigue_dynamic_price_ask", "intrigue_dynamic_price_eval", "intrigue_dynamic_price_options", "{=rad_auto_264}{PRICE_TEXT}", delegate
		{
			if (_finalGoldCost > 0 && _finalInfCost > 0)
			{
				MBTextManager.SetTextVariable("PRICE_TEXT", $"For this job, I request {_finalGoldCost} Denars and {_finalInfCost} Influence from you. Is it accepted?");
			}
			else if (_finalGoldCost > 0)
			{
				MBTextManager.SetTextVariable("PRICE_TEXT", $"I request {_finalGoldCost} Denars from you for this job. Is it accepted?");
			}
			else if (_finalInfCost > 0)
			{
				MBTextManager.SetTextVariable("PRICE_TEXT", $"I request {_finalInfCost} Influence from you for this job. Is it accepted?");
			}
			else
			{
				MBTextManager.SetTextVariable("PRICE_TEXT", "OK, I'll do this job for you. Is it accepted?");
			}
			return true;
		}, null);
		starter.AddPlayerLine("intrigue_price_accept", "intrigue_dynamic_price_options", "intrigue_price_success", "{=rad_auto_303}I accept. Everything you need is here.", null, null, 100, delegate(out TextObject hint)
		{
			hint = new TextObject("{=!}You do not have enough resources.");
			return Hero.MainHero.Gold >= _finalGoldCost && Hero.MainHero.Clan != null && Hero.MainHero.Clan.Influence >= (float)_finalInfCost;
		});
		starter.AddPlayerLine("intrigue_price_decline", "intrigue_dynamic_price_options", "intrigue_cancel_reaction", "{=rad_auto_304}This is too much. The deal is cancelled.", null, null);
		starter.AddDialogLine("intrigue_price_success_line", "intrigue_price_success", "hero_main_options", "{=rad_auto_264}Excellent. We will take care of the rest.", null, null);
		starter.AddPlayerLine("intrigue_confront_rumor", "hero_main_options", "intrigue_confront_rumor_start", "{=rad_auto_305}I heard that you were spreading false rumors about me in the palace. You will pay for this!", () => _isRumorActive && Hero.OneToOneConversationHero == _rumorSourceLord && Hero.MainHero.MapFaction != null && Hero.OneToOneConversationHero.MapFaction == Hero.MainHero.MapFaction, null);
		starter.AddDialogLine("intrigue_confront_rumor_resp", "intrigue_confront_rumor_start", "intrigue_confront_rumor_options", "{=rad_auto_266}Everyone talks about something in the palace... I'm just expressing what I hear. Why were you so offended?", null, null);
		starter.AddPlayerLine("intrigue_confront_opt1", "intrigue_confront_rumor_options", "intrigue_confront_res1", "{=rad_auto_306}Don't play games with my name, or I will make you regret being born!", null, null);
		starter.AddDialogLine("intrigue_confront_res1_success", "intrigue_confront_res1", "hero_main_options", "{=rad_auto_267}Okay, calm down... I just said what I heard, I won't mention your name again.", delegate
		{
			if (Hero.MainHero.Clan.Tier > Hero.OneToOneConversationHero.Clan.Tier || MBRandom.RandomFloat < 0.4f)
			{
				_isRumorActive = false;
				_rumorTimerDays = 0;
				ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, -5);
				return true;
			}
			return false;
		}, null);
		starter.AddDialogLine("intrigue_confront_res1_fail", "intrigue_confront_res1", "intrigue_fail_exit", "{=rad_auto_268}Are you threatening me? Who do you think you're talking to! Get out of my face!", delegate
		{
			ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, -20);
			return true;
		}, null);
		starter.AddPlayerLine("intrigue_confront_opt2", "intrigue_confront_rumor_options", "intrigue_confront_res2", "{=rad_auto_307}These rumors harm the unity of the kingdom. We must put an end to this.", null, null);
		starter.AddDialogLine("intrigue_confront_res2_success", "intrigue_confront_res2", "hero_main_options", "{=rad_auto_269}You may be right. It was not right to spread the issues between us to the palace. The matter is closed.", delegate
		{
			if (Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Calculating) > 0 || MBRandom.RandomFloat < 0.6f)
			{
				_isRumorActive = false;
				_rumorTimerDays = 0;
				return true;
			}
			return false;
		}, null);
		starter.AddDialogLine("intrigue_confront_res2_fail", "intrigue_confront_res2", "hero_main_options", "{=rad_auto_270}I don't care about the unity of the kingdom. If hearing the truth bothers you, that's your problem.", delegate
		{
			ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, -5);
			return true;
		}, null);
		starter.AddPlayerLine("intrigue_confront_opt3", "intrigue_confront_rumor_options", "intrigue_confront_res3", "{=rad_auto_308}Take this 5000 Denars and keep your mouth shut. (5000 Denars)", null, delegate
		{
			GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, Hero.OneToOneConversationHero, 5000, disableNotification: true);
			_isRumorActive = false;
			_rumorTimerDays = 0;
		}, 100, delegate(out TextObject hint)
		{
			hint = new TextObject("{=!}Yeterli Denarýnýz yok.");
			return Hero.MainHero.Gold >= 5000;
		});
		starter.AddDialogLine("intrigue_confront_res3_success", "intrigue_confront_res3", "hero_main_options", "{=rad_auto_271}Hmm... I guess what I heard was unfounded. Don't worry, I will tell others about this.", null, null);
		starter.AddPlayerLine("intrigue_confront_opt4", "intrigue_confront_rumor_options", "intrigue_confront_res4", "{=rad_auto_309}My lord, if there is a misunderstanding between us, let's clear it up. Please stop these rumors. (20 Loss of Influence)", null, delegate
		{
			ChangeClanInfluenceAction.Apply(Clan.PlayerClan, -20f);
			_isRumorActive = false;
			_rumorTimerDays = 0;
			ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, 5);
		});
		starter.AddDialogLine("intrigue_confront_res4_success", "intrigue_confront_res4", "hero_main_options", "{=rad_auto_272}If you talk reasonably like that, why not? I will say that the rumors are unfounded.", null, null);
		starter.AddPlayerLine("intrigue_confront_opt5", "intrigue_confront_rumor_options", "intrigue_confront_res5", "{=rad_auto_310}Do you have the courage to say this to my face? Take back your words, or my sword will speak!", null, null);
		starter.AddDialogLine("intrigue_confront_res5_success", "intrigue_confront_res5", "hero_main_options", "{=rad_auto_273}Calm down! Just stupid gossip... I take it back, okay?", delegate
		{
			if (Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Valor) <= 0 && MBRandom.RandomFloat < 0.5f)
			{
				_isRumorActive = false;
				_rumorTimerDays = 0;
				ChangeClanInfluenceAction.Apply(Clan.PlayerClan, 10f);
				return true;
			}
			return false;
		}, null);
		starter.AddDialogLine("intrigue_confront_res5_fail", "intrigue_confront_res5", "intrigue_fail_exit", "{=rad_auto_274}You will draw your sword and I will remain silent, right? See you in battle!", delegate
		{
			ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, -30);
			return true;
		}, null);
	}

	private void ApplyIntrigueFail()
	{
		int relationChange = ((_currentIntrigueType == IntrigueOfferType.Sabotage) ? (-15) : (-10));
		ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, relationChange);
		InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_auto_317}The intrigue negotiation failed. Tensions are high!").ToString(), Colors.Red));
		if (_intrigueCooldowns == null)
		{
			_intrigueCooldowns = new Dictionary<Hero, CampaignTime>();
		}
		_intrigueCooldowns[Hero.OneToOneConversationHero] = CampaignTime.Now;
		if (_intrigueHateCooldowns == null)
		{
			_intrigueHateCooldowns = new Dictionary<Hero, CampaignTime>();
		}
		_intrigueHateCooldowns[Hero.OneToOneConversationHero] = CampaignTime.Now;
		if (Hero.OneToOneConversationHero != null && !_isRumorSeeded && !_isRumorActive && MBRandom.RandomFloat < 0.4f)
		{
			SeedRumor(Hero.OneToOneConversationHero);
		}
	}

	private void ApplyIntrigueSuccessPlayer()
	{
		if (_finalGoldCost > 0)
		{
			GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, Hero.OneToOneConversationHero, _finalGoldCost, disableNotification: true);
		}
		if (_finalInfCost > 0)
		{
			ChangeClanInfluenceAction.Apply(Hero.MainHero.Clan, -_finalInfCost);
		}
		if (_currentIntrigueType == IntrigueOfferType.Slander)
		{
			if (_currentTargetLord != null && _currentTargetLord.MapFaction != null && _currentTargetLord.MapFaction.Leader != null)
			{
				ChangeRelationAction.ApplyRelationChangeBetweenHeroes(_currentTargetLord, _currentTargetLord.MapFaction.Leader, -15);
				PendingTreasonTarget = _currentTargetLord;
				InformationManager.ShowInquiry(new InquiryData(new TextObject("{=rad_auto_323}The Great Council Meets").ToString(), new TextObject("{=rad_loc_lib_01}Rumors of treason spreading in the palace about {NAME} have reached the King's ears! The King convened an emergency Grand Council to discuss these allegations of treason.").SetTextVariable("NAME", _currentTargetLord.Name).ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: false, "Devam", "", null, null));
			}
		}
		else if (_currentIntrigueType == IntrigueOfferType.Sabotage)
		{
			if (_currentTargetLord != null)
			{
				Settlement settlement = _currentTargetLord.Clan.Settlements.FirstOrDefault((Settlement s) => s.IsTown);
				if (settlement != null && settlement.Town != null)
				{
					settlement.Town.Loyalty -= 30f;
					settlement.Town.Security -= 30f;
					InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_loc_lib_03}Sabotage in {SETTLEMENT} was successful! Loyalty and security have fallen.").SetTextVariable("SETTLEMENT", settlement.Name).ToString(), Colors.Green));
				}
			}
		}
		else if (_currentIntrigueType == IntrigueOfferType.SecretPact)
		{
			ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, 30);
			InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_loc_lib_02}A secret alliance was formed with {NAME}. Your relationship has increased.").SetTextVariable("NAME", Hero.OneToOneConversationHero.Name).ToString(), Colors.Green));
		}
		if (Hero.OneToOneConversationHero != null && !_isRumorSeeded && !_isRumorActive && MBRandom.RandomFloat < 0.1f)
		{
			SeedRumor(Hero.OneToOneConversationHero);
		}
		if (_intrigueCooldowns == null)
		{
			_intrigueCooldowns = new Dictionary<Hero, CampaignTime>();
		}
		_intrigueCooldowns[Hero.OneToOneConversationHero] = CampaignTime.Now;
	}

	private Hero GetRandomTargetLord()
	{
		List<Hero> multipleTargetLords = GetMultipleTargetLords(1);
		if (multipleTargetLords.Count <= 0)
		{
			return null;
		}
		return multipleTargetLords[0];
	}

	private Settlement GetRandomTargetFief()
	{
		List<Settlement> multipleTargetFiefs = GetMultipleTargetFiefs(1);
		if (multipleTargetFiefs.Count <= 0)
		{
			return null;
		}
		return multipleTargetFiefs[0];
	}

	private List<Hero> GetMultipleTargetLords(int count)
	{
		if (!(Hero.MainHero.MapFaction is Kingdom kingdom))
		{
			return new List<Hero>();
		}
		return (from x in kingdom.Heroes.Where((Hero h) => h != Hero.MainHero && h != Hero.OneToOneConversationHero && h.IsAlive && h.IsLord && h.Clan != Hero.MainHero.Clan && h.Clan != Hero.OneToOneConversationHero?.Clan).ToList()
			orderby MBRandom.RandomFloat
			select x).Take(count).ToList();
	}

	private List<Settlement> GetMultipleTargetFiefs(int count)
	{
		if (!(Hero.MainHero.MapFaction is Kingdom kingdom))
		{
			return new List<Settlement>();
		}
		return (from x in (from s in kingdom.Settlements
				where s.IsTown || s.IsCastle
				where s.OwnerClan != Hero.MainHero.Clan && s.OwnerClan != Hero.OneToOneConversationHero?.Clan
				select s).ToList()
			orderby MBRandom.RandomFloat
			select x).Take(count).ToList();
	}

	private void ApplyIntrigueSuccessAI()
	{
		GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, -5000, disableNotification: true);
		if (_currentIntrigueType == IntrigueOfferType.Slander && _currentTargetLord != null && _currentTargetLord.MapFaction != null)
		{
			ChangeRelationAction.ApplyRelationChangeBetweenHeroes(_currentTargetLord, _currentTargetLord.MapFaction.Leader, -15);
			PendingTreasonTarget = _currentTargetLord;
			InformationManager.ShowInquiry(new InquiryData(new TextObject("{=rad_auto_324}The Great Council Meets").ToString(), new TextObject("{=rad_loc_lib_01}Rumors of treason spreading in the palace about {NAME} have reached the King's ears! The King convened an emergency Grand Council to discuss these allegations of treason.").SetTextVariable("NAME", _currentTargetLord.Name).ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: false, "Devam", "", null, null));
		}
		if (Hero.OneToOneConversationHero != null && !_isRumorSeeded && !_isRumorActive && MBRandom.RandomFloat < 0.1f)
		{
			SeedRumor(Hero.OneToOneConversationHero);
		}
		if (_intrigueCooldowns == null)
		{
			_intrigueCooldowns = new Dictionary<Hero, CampaignTime>();
		}
		_intrigueCooldowns[Hero.OneToOneConversationHero] = CampaignTime.Now;
	}

	private void SeedRumor(Hero sourceLord)
	{
		_isRumorSeeded = true;
		_rumorSourceLord = sourceLord;
		_rumorTimerDays = MBRandom.RandomInt(4, 7);
	}

	private void ApplyRumorPenalties()
	{
		if (_rumorSourceLord == null || !_rumorSourceLord.IsAlive || _rumorSourceLord.MapFaction == null)
		{
			_isRumorActive = false;
			return;
		}
		ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, _rumorSourceLord, -5);
		if (_rumorSourceLord.MapFaction.Leader != null && _rumorSourceLord.MapFaction.Leader != Hero.MainHero)
		{
			ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, _rumorSourceLord.MapFaction.Leader, -5);
		}
		foreach (Hero item in _rumorSourceLord.MapFaction.Heroes.Where((Hero h) => h != Hero.MainHero && h != _rumorSourceLord && h.IsAlive && h.GetRelation(_rumorSourceLord) > 20).ToList())
		{
			ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, item, -5);
		}
		if (Hero.MainHero.Clan != null)
		{
			ChangeClanInfluenceAction.Apply(Hero.MainHero.Clan, -10f);
		}
	}

	private void SetupPersuasion()
	{
		_currentTasks = new List<PersuasionTask>();
		PersuasionTask persuasionTask = new PersuasionTask(TaleWorlds.Core.MBRandom.RandomInt(100000, 999999));
		persuasionTask.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Roguery, DefaultTraits.Calculating, TraitEffect.Positive, PersuasionArgumentStrength.Normal, givesCriticalSuccess: false, new TextObject("{=!}Let's arrange an accident for them. A little sabotage never hurt anyone... except them.")));
		persuasionTask.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Honor, TraitEffect.Positive, PersuasionArgumentStrength.Hard, givesCriticalSuccess: false, new TextObject("{=!}For the good of the realm, this necessary evil must be done. It's the honorable path.")));
		persuasionTask.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Valor, TraitEffect.Positive, PersuasionArgumentStrength.Easy, givesCriticalSuccess: true, new TextObject("{=!}We must act decisively! Delay means weakness, and we are not weak!")));
		persuasionTask.AddOptionToTask(new PersuasionOptionArgs(DefaultSkills.Trade, DefaultTraits.Calculating, TraitEffect.Positive, PersuasionArgumentStrength.Normal, givesCriticalSuccess: false, new TextObject("{=!}Think of the wealth and stability this move will secure for your clan.")));
		_currentTasks.Add(persuasionTask);
		ConversationManager.StartPersuasion(3f, 1f, 0f, 2f, 2f);
	}

	private bool Condition_SetupOption1Text()
	{
		return SetupOptionText(0, "ARG1_TEXT", "ARG1_CHANCE");
	}

	private bool Condition_SetupOption2Text()
	{
		return SetupOptionText(1, "ARG2_TEXT", "ARG2_CHANCE");
	}

	private bool Condition_SetupOption3Text()
	{
		return SetupOptionText(2, "ARG3_TEXT", "ARG3_CHANCE");
	}

	private bool Condition_SetupOption4Text()
	{
		return SetupOptionText(3, "ARG4_TEXT", "ARG4_CHANCE");
	}

	private bool SetupOptionText(int index, string textVar, string chanceVar)
	{
		PersuasionOptionArgs optionArgs = GetOptionArgs(index);
		if (optionArgs == null)
		{
			return false;
		}
		MBTextManager.SetTextVariable(textVar, optionArgs.Line);
		MBTextManager.SetTextVariable(chanceVar, PersuasionHelper.ShowSuccess(optionArgs, showToPlayer: false));
		return true;
	}

	private bool Condition_Clickable1(out TextObject h)
	{
		return CheckClickable(0, out h);
	}

	private bool Condition_Clickable2(out TextObject h)
	{
		return CheckClickable(1, out h);
	}

	private bool Condition_Clickable3(out TextObject h)
	{
		return CheckClickable(2, out h);
	}

	private bool Condition_Clickable4(out TextObject h)
	{
		return CheckClickable(3, out h);
	}

	private bool CheckClickable(int index, out TextObject hint)
	{
		hint = new TextObject("{=!} ");
		PersuasionOptionArgs optionArgs = GetOptionArgs(index);
		if (optionArgs == null)
		{
			return false;
		}
		if (optionArgs.IsBlocked)
		{
			hint = new TextObject("{=!}You already used this argument.");
			return false;
		}
		return true;
	}

	private PersuasionOptionArgs Delegate_GetOption1Args()
	{
		return GetOptionArgs(0);
	}

	private PersuasionOptionArgs Delegate_GetOption2Args()
	{
		return GetOptionArgs(1);
	}

	private PersuasionOptionArgs Delegate_GetOption3Args()
	{
		return GetOptionArgs(2);
	}

	private PersuasionOptionArgs Delegate_GetOption4Args()
	{
		return GetOptionArgs(3);
	}

	private PersuasionOptionArgs GetOptionArgs(int index)
	{
		PersuasionTask persuasionTask = _currentTasks?.FirstOrDefault();
		if (persuasionTask == null || persuasionTask.Options.Count <= index)
		{
			return null;
		}
		return persuasionTask.Options[index];
	}

	private bool Condition_ShouldContinue()
	{
		if (!ConversationManager.GetPersuasionProgressSatisfied())
		{
			return !ConversationManager.GetPersuasionIsFailure();
		}
		return false;
	}

	private bool Condition_ReactionAndApply()
	{
		Tuple<PersuasionOptionArgs, PersuasionOptionResult> chosen = ConversationManager.GetPersuasionChosenOptions().LastOrDefault();
		if (chosen != null)
		{
			PersuasionTask persuasionTask = _currentTasks.FirstOrDefault((PersuasionTask t) => t.Options.Contains(chosen.Item1));
			float difficulty = Campaign.Current.Models.PersuasionModel.GetDifficulty(PersuasionDifficulty.Medium);
			Campaign.Current.Models.PersuasionModel.GetEffectChances(chosen.Item1, out var moveToNextStageChance, out var blockRandomOptionChance, difficulty);
			persuasionTask?.ApplyEffects(moveToNextStageChance, blockRandomOptionChance);
			switch (chosen.Item2)
			{
			case PersuasionOptionResult.Success:
			case PersuasionOptionResult.CriticalSuccess:
				MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("{=!}You have a point..."));
				break;
			case PersuasionOptionResult.CriticalFailure:
				MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("{=!}Nonsense! Are you out of your mind?"));
				break;
			default:
				MBTextManager.SetTextVariable("PERSUASION_REACTION", new TextObject("{=!}I'm not so sure about that..."));
				break;
			}
		}
		return true;
	}

	private void AddRumorDialogs(CampaignGameStarter starter)
	{
		starter.AddDialogLine("intrigue_rumor_confront_ai", "lord_pretalk", "intrigue_rumor_confront_options", "{=rad_auto_275}If what I heard about you is true, you will taste my sword. Watch your feet!", () => _isRumorActive && Hero.OneToOneConversationHero == _rumorSourceLord && Hero.MainHero.MapFaction != null && Hero.OneToOneConversationHero.MapFaction == Hero.MainHero.MapFaction, null, 200);
		starter.AddPlayerLine("intrigue_rumor_confront_light", "intrigue_rumor_confront_options", "intrigue_rumor_confront_light_resp", "{=rad_auto_311}[Slight] Calm down. Everything you heard is a big misunderstanding.", null, null);
		starter.AddDialogLine("intrigue_rumor_confront_light_ai", "intrigue_rumor_confront_light_resp", "hero_main_options", "{=rad_auto_276}I hope so. Just so you know, I'm keeping an eye on you.", delegate
		{
			if (Hero.MainHero.GetSkillValue(DefaultSkills.Charm) >= 100 || MBRandom.RandomFloat < 0.5f)
			{
				_isRumorActive = false;
				_isRumorSeeded = false;
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_auto_318}You managed to convince the lord. The rumors stopped.").ToString(), Colors.Green));
			}
			else
			{
				InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_auto_319}The Lord did not believe you. Rumors will continue.").ToString(), Colors.Red));
			}
			return true;
		}, null);
		starter.AddPlayerLine("intrigue_rumor_confront_medium", "intrigue_rumor_confront_options", "intrigue_rumor_confront_medium_resp", "{=rad_auto_312}[Middle] We both know this is all bullshit. Take this purse and stop this nonsense. (10,000 Denars)", null, null, 100, delegate(out TextObject hint)
		{
			hint = new TextObject("{=!}Yeterli altýnýnýz yok.");
			return Hero.MainHero.Gold >= 10000;
		});
		starter.AddDialogLine("intrigue_rumor_confront_medium_ai", "intrigue_rumor_confront_medium_resp", "hero_main_options", "{=rad_auto_277}The voice of gold is always loudest. Okay, we're closing this topic.", delegate
		{
			GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, Hero.OneToOneConversationHero, 10000, disableNotification: true);
			_isRumorActive = false;
			_isRumorSeeded = false;
			InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_auto_320}You bribed the lord into silence.").ToString(), Colors.Green));
			return true;
		}, null);
		starter.AddPlayerLine("intrigue_rumor_confront_severe", "intrigue_rumor_confront_options", "intrigue_rumor_confront_severe_resp", "{=rad_auto_313}[Bad] Look at me! Give me one reason why I shouldn't bring it up to the council that you're the real traitor! Shut up or your head will be gone! (100 Influence)", null, null, 100, delegate(out TextObject hint)
		{
			hint = new TextObject("{=rad_int_nohint}You do not have enough influence.");
			return Hero.MainHero.Clan != null && Hero.MainHero.Clan.Influence >= 100f;
		});
		starter.AddDialogLine("intrigue_rumor_confront_severe_ai", "intrigue_rumor_confront_severe_resp", "hero_main_options", "{=rad_auto_278}You... You are such a snake! Wait and see, the council will not believe you!", delegate
		{
			_isRumorActive = false;
			_isRumorSeeded = false;
			PendingTreasonTarget = Hero.OneToOneConversationHero;
			InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_auto_321}You threatened the lord with slander. You can return to the city and convene the 'Grand Council' to begin the slander process and the Kingdom vote!").ToString(), Colors.Magenta));
			return true;
		}, null);
		starter.AddPlayerLine("intrigue_rumor_confront_very_severe", "intrigue_rumor_confront_options", "intrigue_rumor_confront_vsevere_resp", "{=rad_auto_314}[Very Bad] (Walk up to him and grab him by the collar) I'll tear out that tongue of yours and throw it to the dogs! Who gave you the courage to stand in front of me and talk!", null, null);
		starter.AddDialogLine("intrigue_rumor_confront_vsevere_ai", "intrigue_rumor_confront_vsevere_resp", "hero_main_options", "{=rad_auto_279}Argh! Let me go! OK... OK! I won't say anything to anyone... This is crazy!", delegate
		{
			_isRumorActive = false;
			_isRumorSeeded = false;
			Hero.MainHero.SetTraitLevel(DefaultTraits.Honor, Hero.MainHero.GetTraitLevel(DefaultTraits.Honor) - 1);
			Hero.MainHero.SetTraitLevel(DefaultTraits.Valor, Hero.MainHero.GetTraitLevel(DefaultTraits.Valor) + 1);
			ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, Hero.OneToOneConversationHero, -40);
			InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_auto_322}You forced the lord into silence by physically battering him! A major feud with his clan began.").ToString(), Colors.Red));
			return true;
		}, null);
	}
}

}