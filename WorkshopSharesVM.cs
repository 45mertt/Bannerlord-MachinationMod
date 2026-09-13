using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.CampaignSystem.Settlements;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace RebellionsAndDemographics
{
    // ─── Tab enum ───────────────────────────────────────────────────────────────
    public enum WorkshopUITab { Browse, Manage }

    // ─── Root VM ────────────────────────────────────────────────────────────────
    public class WorkshopSharesVM : ViewModel
    {
        private Settlement _town;
        private MBBindingList<CityWorkshopSlotVM> _slots;
        private MBBindingList<OwnedWorkshopVM> _ownedSlots;
        private string _statusText;
        private CityWorkshopSlotVM _selectedSlot;
        private OwnedWorkshopVM _selectedOwned;
        private bool _isBrowseTab = true;
        private bool _isManageTab;

        [DataSourceProperty] public MBBindingList<CityWorkshopSlotVM> Slots { get => _slots; set { if (_slots != value) { _slots = value; OnPropertyChangedWithValue(value, "Slots"); } } }
        [DataSourceProperty] public MBBindingList<OwnedWorkshopVM> OwnedSlots { get => _ownedSlots; set { if (_ownedSlots != value) { _ownedSlots = value; OnPropertyChangedWithValue(value, "OwnedSlots"); } } }
        [DataSourceProperty] public string StatusText { get => _statusText; set { if (_statusText != value) { _statusText = value; OnPropertyChangedWithValue(value, "StatusText"); } } }
        [DataSourceProperty] public CityWorkshopSlotVM SelectedSlot { get => _selectedSlot; set { if (_selectedSlot != value) { _selectedSlot = value; OnPropertyChangedWithValue(value, "SelectedSlot"); } } }
        [DataSourceProperty] public OwnedWorkshopVM SelectedOwned { get => _selectedOwned; set { if (_selectedOwned != value) { _selectedOwned = value; OnPropertyChangedWithValue(value, "SelectedOwned"); } } }
        [DataSourceProperty] public bool IsBrowseTab { get => _isBrowseTab; set { if (_isBrowseTab != value) { _isBrowseTab = value; OnPropertyChangedWithValue(value, "IsBrowseTab"); } } }
        [DataSourceProperty] public bool IsManageTab { get => _isManageTab; set { if (_isManageTab != value) { _isManageTab = value; OnPropertyChangedWithValue(value, "IsManageTab"); } } }

        public WorkshopSharesVM(Settlement town)
        {
            _town = town;
            Slots = new MBBindingList<CityWorkshopSlotVM>();
            OwnedSlots = new MBBindingList<OwnedWorkshopVM>();
            StatusText = "Workshop Monopoly & Trade  -  " + town.Name.ToString();
            ShowBrowseTab();
        }

        private void RefreshBrowse()
        {
            Slots.Clear();
            var slots = WorkshopKingdomBehavior.Instance.GetOrInitializeSlots(_town);
            foreach (var slot in slots)
                Slots.Add(new CityWorkshopSlotVM(slot, this, _town));
            if (Slots.Count > 0) ExecuteSelectSlot(Slots[0]);
        }

        private void RefreshManage()
        {
            OwnedSlots.Clear();
            var myShares = WorkshopKingdomBehavior.Instance.GetSharesInSettlement(_town.StringId);
            foreach (var share in myShares)
                OwnedSlots.Add(new OwnedWorkshopVM(share, this, _town));
            if (OwnedSlots.Count > 0) ExecuteSelectOwned(OwnedSlots[0]);
            else SelectedOwned = null;
        }

        public void ExecuteSelectSlot(CityWorkshopSlotVM slot)
        {
            if (SelectedSlot != null) SelectedSlot.IsSelected = false;
            SelectedSlot = slot;
            if (SelectedSlot != null) SelectedSlot.IsSelected = true;
        }

        public void ExecuteSelectOwned(OwnedWorkshopVM slot)
        {
            if (SelectedOwned != null) SelectedOwned.IsSelected = false;
            SelectedOwned = slot;
            if (SelectedOwned != null) SelectedOwned.IsSelected = true;
        }

        public void ShowBrowseTab()
        {
            IsBrowseTab = true;
            IsManageTab = false;
            RefreshBrowse();
        }

        public void ShowManageTab()
        {
            IsBrowseTab = false;
            IsManageTab = true;
            RefreshManage();
        }

        public void ExecuteClose() => WorkshopSharesUIManager.Close();
    }

    // ─── Browse tab: one slot (owned by any lord or empty) ─────────────────────
    public class CityWorkshopSlotVM : ViewModel
    {
        private MonopolySlot _slot;
        private WorkshopSharesVM _parent;
        private Settlement _town;

        private string _nameText, _ownerNameText;
        private bool _isSelected;
        private string _portraitId, _portraitArgs, _portraitProvider;
        private bool _hasPortrait;
        private string _detailNameText, _relationValueText, _estimatedPriceText, _actionText;

        [DataSourceProperty] public string NameText { get => _nameText; set { if (_nameText != value) { _nameText = value; OnPropertyChangedWithValue(value, "NameText"); } } }
        [DataSourceProperty] public string OwnerNameText { get => _ownerNameText; set { if (_ownerNameText != value) { _ownerNameText = value; OnPropertyChangedWithValue(value, "OwnerNameText"); } } }
        [DataSourceProperty] public bool IsSelected { get => _isSelected; set { if (_isSelected != value) { _isSelected = value; OnPropertyChangedWithValue(value, "IsSelected"); } } }
        [DataSourceProperty] public string PortraitId { get => _portraitId; set { if (_portraitId != value) { _portraitId = value; OnPropertyChangedWithValue(value, "PortraitId"); } } }
        [DataSourceProperty] public string PortraitArgs { get => _portraitArgs; set { if (_portraitArgs != value) { _portraitArgs = value; OnPropertyChangedWithValue(value, "PortraitArgs"); } } }
        [DataSourceProperty] public string PortraitProvider { get => _portraitProvider; set { if (_portraitProvider != value) { _portraitProvider = value; OnPropertyChangedWithValue(value, "PortraitProvider"); } } }
        [DataSourceProperty] public bool HasPortrait { get => _hasPortrait; set { if (_hasPortrait != value) { _hasPortrait = value; OnPropertyChangedWithValue(value, "HasPortrait"); } } }
        [DataSourceProperty] public string DetailNameText { get => _detailNameText; set { if (_detailNameText != value) { _detailNameText = value; OnPropertyChangedWithValue(value, "DetailNameText"); } } }
        [DataSourceProperty] public string RelationValueText { get => _relationValueText; set { if (_relationValueText != value) { _relationValueText = value; OnPropertyChangedWithValue(value, "RelationValueText"); } } }
        [DataSourceProperty] public string EstimatedPriceText { get => _estimatedPriceText; set { if (_estimatedPriceText != value) { _estimatedPriceText = value; OnPropertyChangedWithValue(value, "EstimatedPriceText"); } } }
        [DataSourceProperty] public string ActionText { get => _actionText; set { if (_actionText != value) { _actionText = value; OnPropertyChangedWithValue(value, "ActionText"); } } }

        public CityWorkshopSlotVM(MonopolySlot slot, WorkshopSharesVM parent, Settlement town)
        {
            _slot = slot; _parent = parent; _town = town;
            NameText = "Workshop " + (slot.SlotIndex + 1);

            if (slot.OwnerHeroId == Hero.MainHero.StringId)
            {
                OwnerNameText = "You";
                DetailNameText = "Owned by You";
                RelationValueText = "";
                EstimatedPriceText = new TextObject("{=rad_ws_manage_hint}Go to the Manage tab to control this workshop.").ToString();
                ActionText = new TextObject("{=rad_ws_go_manage}Go to Manage Tab").ToString();
                SetPortrait(Hero.MainHero);
            }
            else if (slot.IsEmpty || string.IsNullOrEmpty(slot.OwnerHeroId))
            {
                OwnerNameText = new TextObject("{=rad_ws_empty}Empty").ToString();
                DetailNameText = new TextObject("{=rad_ws_empty_detail}Unoccupied slot").ToString();
                RelationValueText = "";
                EstimatedPriceText = "Cost: 15,000 Denars";
                ActionText = new TextObject("{=rad_ws_buy_empty}Buy Slot").ToString();
                HasPortrait = false; PortraitId = ""; PortraitArgs = ""; PortraitProvider = "";
            }
            else
            {
                var ownerHero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == slot.OwnerHeroId);
                if (ownerHero != null)
                {
                    OwnerNameText = ownerHero.Name.ToString();
                    DetailNameText = ownerHero.Name.ToString();
                    int rel = ownerHero.GetRelation(Hero.MainHero);
                    string relLabel = rel >= 20 ? "Friendly" : (rel >= 0 ? "Neutral" : "Hostile");
                    RelationValueText = string.Format("Relation: {0}  ({1})", rel, relLabel);

                    int permitPrice = Math.Max(15000, Math.Min(80000, 50000 - rel * 500));
                    int buyoutPrice = Math.Max(15000, Math.Min(45000, 30000 - rel * 250));
                    bool hasPermit = WorkshopKingdomBehavior.Instance.HasTradePermit(town);
                    EstimatedPriceText = hasPermit
                        ? string.Format("Buyout estimate: ~{0:N0} Denars", buyoutPrice)
                        : string.Format("Trade Permit required: ~{0:N0} Denars", permitPrice);

                    ActionText = new TextObject("{=rad_ws_negotiate}Find & Negotiate").ToString();
                    SetPortrait(ownerHero);
                }
                else
                {
                    OwnerNameText = "Unknown"; DetailNameText = "Unknown Owner";
                    RelationValueText = ""; EstimatedPriceText = ""; ActionText = "";
                    HasPortrait = false; PortraitId = ""; PortraitArgs = ""; PortraitProvider = "";
                }
            }
        }

        private void SetPortrait(Hero hero)
        {
            var vm = new CharacterImageIdentifierVM(CharacterCode.CreateFrom(hero.CharacterObject));
            PortraitId = vm.Id; PortraitArgs = vm.AdditionalArgs; PortraitProvider = vm.TextureProviderName;
            HasPortrait = true;
        }

        public void ExecuteSelect() => _parent.ExecuteSelectSlot(this);

        public void ExecuteAction()
        {
            if (_slot.OwnerHeroId == Hero.MainHero.StringId)
            {
                _parent.ShowManageTab();
            }
            else if (_slot.IsEmpty || string.IsNullOrEmpty(_slot.OwnerHeroId))
            {
                if (!WorkshopKingdomBehavior.Instance.HasTradePermit(_town))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_auto_245}You do not have a trade permit for this city. Find the town lord and negotiate first.").ToString(), Colors.Red));
                    return;
                }
                if (Hero.MainHero.Gold < 15000)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_ws_no_gold}Not enough gold. You need 15,000 Denars.").ToString(), Colors.Red));
                    return;
                }
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 15000, true);
                _slot.OwnerHeroId = Hero.MainHero.StringId;
                _slot.IsEmpty = false;
                string newTag = Guid.NewGuid().ToString("N");
                WorkshopKingdomBehavior.Instance.AddPlayerShare(new VirtualWorkshopShare
                {
                    SettlementId = _town.StringId, WorkshopTypeId = _slot.WorkshopTypeId,
                    Name = "Workshop Share", Trait = WorkshopKingdomBehavior.Instance.GetRandomTrait(), Tag = newTag
                });
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=rad_ws_bought}Workshop slot purchased successfully!").ToString(), Colors.Green));
                _parent.ExecuteClose();
            }
            else
            {
                var ownerHero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == _slot.OwnerHeroId);
                if (ownerHero != null)
                {
                    _parent.ExecuteClose();
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=rad_ws_find_lord}You need to find {HERO} and negotiate in person.").SetTextVariable("HERO", ownerHero.Name).ToString(), Colors.Yellow));
                }
            }
        }
    }

    // ─── Manage tab: one of the player's owned workshops ─────────────────────────
    public class OwnedWorkshopVM : ViewModel
    {
        private VirtualWorkshopShare _share;
        private WorkshopSharesVM _parent;
        private Settlement _town;

        private string _nameText, _levelText, _strategyText, _rawMatText, _incomeText;
        private string _upgradeCostText, _actionUpgradeText;
        private bool _isSelected, _canUpgrade;

        [DataSourceProperty] public string NameText { get => _nameText; set { if (_nameText != value) { _nameText = value; OnPropertyChangedWithValue(value, "NameText"); } } }
        [DataSourceProperty] public string LevelText { get => _levelText; set { if (_levelText != value) { _levelText = value; OnPropertyChangedWithValue(value, "LevelText"); } } }
        [DataSourceProperty] public string StrategyText { get => _strategyText; set { if (_strategyText != value) { _strategyText = value; OnPropertyChangedWithValue(value, "StrategyText"); } } }
        [DataSourceProperty] public string RawMatText { get => _rawMatText; set { if (_rawMatText != value) { _rawMatText = value; OnPropertyChangedWithValue(value, "RawMatText"); } } }
        [DataSourceProperty] public string IncomeText { get => _incomeText; set { if (_incomeText != value) { _incomeText = value; OnPropertyChangedWithValue(value, "IncomeText"); } } }
        [DataSourceProperty] public string UpgradeCostText { get => _upgradeCostText; set { if (_upgradeCostText != value) { _upgradeCostText = value; OnPropertyChangedWithValue(value, "UpgradeCostText"); } } }
        [DataSourceProperty] public string ActionUpgradeText { get => _actionUpgradeText; set { if (_actionUpgradeText != value) { _actionUpgradeText = value; OnPropertyChangedWithValue(value, "ActionUpgradeText"); } } }
        [DataSourceProperty] public bool IsSelected { get => _isSelected; set { if (_isSelected != value) { _isSelected = value; OnPropertyChangedWithValue(value, "IsSelected"); } } }
        [DataSourceProperty] public bool CanUpgrade { get => _canUpgrade; set { if (_canUpgrade != value) { _canUpgrade = value; OnPropertyChangedWithValue(value, "CanUpgrade"); } } }

        private static readonly string[] StrategyNames = {
            "Normal (Default)",
            "Guild Standards",
            "War Profiteering",
            "Black Market / Smuggling",
            "Cheap Labor (Mass Production)",
            "Hoarding"
        };

        public OwnedWorkshopVM(VirtualWorkshopShare share, WorkshopSharesVM parent, Settlement town)
        {
            _share = share; _parent = parent; _town = town;
            Refresh();
        }

        public void Refresh()
        {
            NameText = _share.Name;
            LevelText = string.Format("Level: {0} / 5", _share.Level);
            int strat = _share.StrategyType;
            string stratName = (strat >= 0 && strat < StrategyNames.Length) ? StrategyNames[strat] : "Unknown";
            StrategyText = "Strategy: " + stratName;
            RawMatText = string.Format("Raw Materials: {0}", _share.RawMaterials);

            int cost = WorkshopKingdomBehavior.Instance.GetUpgradeCost(_share.Level);
            if (_share.Level >= 5)
            {
                UpgradeCostText = new TextObject("{=rad_ws_max_level}Max Level Reached").ToString();
                ActionUpgradeText = new TextObject("{=rad_ws_max_level}Max Level").ToString();
                CanUpgrade = false;
            }
            else
            {
                UpgradeCostText = string.Format("Upgrade Cost: {0:N0} Denars", cost);
                ActionUpgradeText = string.Format("Upgrade to Lv.{0}", _share.Level + 1);
                CanUpgrade = Hero.MainHero.Gold >= cost;
            }
        }

        public void ExecuteSelect() => _parent.ExecuteSelectOwned(this);

        public void ExecuteUpgrade()
        {
            int cost = WorkshopKingdomBehavior.Instance.GetUpgradeCost(_share.Level);
            if (_share.Level >= 5) { InformationManager.DisplayMessage(new InformationMessage("Already max level.", Colors.Yellow)); return; }
            if (Hero.MainHero.Gold < cost) { InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_ws_no_gold}Not enough gold.").ToString(), Colors.Red)); return; }
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, true);
            _share.Level++;
            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=rad_ws_upgraded}Workshop upgraded to Level {LEVEL}!").SetTextVariable("LEVEL", _share.Level).ToString(), Colors.Green));
            Refresh();
        }

        public void ExecuteChangeStrategy()
        {
            var vm = new CitySelectionVM();
            vm.Description = new TextObject("{=rad_ws_strat_title}Select Strategy for {NAME}").SetTextVariable("NAME", _share.Name).ToString();
            vm.ShowList = true; vm.ShowInput = false; vm.HasAcceptButton = true; vm.HasCancelButton = true;
            vm.AcceptButtonText = new TextObject("{=rad_btn_accept}Apply").ToString();
            vm.CancelButtonText = new TextObject("{=rad_btn_cancel}Cancel").ToString();

            string sel = null;
            Action<CitySelectionItemVM, string> onSel = (item, id) => { foreach (var x in vm.Items) x.IsSelected = false; item.IsSelected = true; sel = id; };

            bool isAtWar = Kingdom.All.Any(k => k == Hero.MainHero.MapFaction && Kingdom.All.Any(k2 => k2 != k && k.IsAtWarWith(k2)));

            vm.Items.Add(new CitySelectionItemVM(0, new TextObject("{=rad_strat_0}Normal (Default)").ToString() + (_share.StrategyType == 0 ? " (Active)" : ""), new TextObject("{=rad_prof_0}Profit: Low").ToString(), _share.StrategyType == 0, i => onSel(i, "0")));
            vm.Items.Add(new CitySelectionItemVM(1, new TextObject("{=rad_strat_1}Guild Standards").ToString() + (_share.StrategyType == 1 ? " (Active)" : ""), new TextObject("{=rad_prof_1}Profit: Standard").ToString(), _share.StrategyType == 1, i => onSel(i, "1")));
            vm.Items.Add(new CitySelectionItemVM(2, new TextObject("{=rad_strat_2}War Profiteering").ToString() + (_share.StrategyType == 2 ? " (Active)" : "") + (!isAtWar ? " (No War)" : ""), new TextObject("{=rad_prof_2}Profit: High (At War)").ToString(), !isAtWar, i => onSel(i, "2")));
            vm.Items.Add(new CitySelectionItemVM(3, new TextObject("{=rad_strat_3}Black Market / Smuggling").ToString() + (_share.StrategyType == 3 ? " (Active)" : ""), new TextObject("{=rad_prof_3}Profit: Very High").ToString(), _share.StrategyType == 3, i => onSel(i, "3")));
            vm.Items.Add(new CitySelectionItemVM(4, new TextObject("{=rad_strat_4}Cheap Labor (Mass Production)").ToString() + (_share.StrategyType == 4 ? " (Active)" : ""), new TextObject("{=rad_prof_4}Profit: High").ToString(), _share.StrategyType == 4, i => onSel(i, "4")));
            vm.Items.Add(new CitySelectionItemVM(5, new TextObject("{=rad_strat_5}Hoarding").ToString() + (_share.StrategyType == 5 ? " (Active)" : ""), new TextObject("{=rad_prof_5}Profit: None").ToString(), _share.StrategyType == 5, i => onSel(i, "5")));

            vm.OnAccept = () =>
            {
                CitySelectionUIManager.Close();
                if (!string.IsNullOrEmpty(sel)) { _share.StrategyType = int.Parse(sel); InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_strat_changed}Strategy changed!").ToString(), Colors.Green)); Refresh(); }
            };
            vm.OnCancel = () => CitySelectionUIManager.Close();
            CitySelectionUIManager.Open(vm);
        }

        public void ExecuteBuyMaterials()
        {
            if (Hero.MainHero.Gold < 1000) { InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_ws_no_gold}Not enough gold.").ToString(), Colors.Red)); return; }
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 1000, true);
            _share.RawMaterials += 50;
            InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=rad_mat_bought}Bought 50 Raw Materials for 1,000 Denars.").ToString(), Colors.Green));
            Refresh();
        }
    }

    // ─── UI Manager ─────────────────────────────────────────────────────────────
    public static class WorkshopSharesUIManager
    {
        private static TaleWorlds.Engine.GauntletUI.GauntletLayer _layer;
        private static WorkshopSharesVM _vm;

        public static void Open(Settlement town)
        {
            if (_layer != null) return;
            _vm = new WorkshopSharesVM(town);
            _layer = new TaleWorlds.Engine.GauntletUI.GauntletLayer("WorkshopSharesUI", 4700);
            _layer.LoadMovie("WorkshopSharesUI", _vm);
            _layer.IsFocusLayer = true;
            _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            TaleWorlds.ScreenSystem.ScreenManager.TopScreen.AddLayer(_layer);
            TaleWorlds.ScreenSystem.ScreenManager.TrySetFocus(_layer);
            if (Campaign.Current != null)
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
        }

        public static void Close()
        {
            if (_layer != null)
            {
                TaleWorlds.ScreenSystem.ScreenManager.TopScreen.RemoveLayer(_layer);
                _layer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.All);
                _layer = null; _vm = null;
            }
        }
    }
}
