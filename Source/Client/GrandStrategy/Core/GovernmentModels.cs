using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimNations.Core.GrandStrategy.Core
{
    public enum GovernmentType
    {
        Monarchy,
        Republic,
        MilitaryJunta,
        Technocracy,
        MerchantGuild
    }

    public enum MarketCategory
    {
        All,
        Weapons,
        Apparel,
        Resources,
        Medicine,
        Food,
        TechArtifacts
    }

    public enum MinistryPermission
    {
        DeclareWar,
        AuthorizePeace,
        InviteBanishCitizens,
        AdjustCustomsTariff,
        AccessNationalTreasury
    }

    [Serializable]
    public class MinistryOffice : IExposable
    {
        public string OfficeId = Guid.NewGuid().ToString();
        public string Title = "Minister of War";
        public string AssignedPlayerLogin = "";

        public bool CanDeclareWar = false;
        public bool CanAuthorizePeace = false;
        public bool CanInviteBanishCitizens = false;
        public bool CanAdjustCustomsTariff = false;
        public bool CanAccessNationalTreasury = false;

        public MinistryOffice() { }

        public MinistryOffice(string title, string assignedLogin = "")
        {
            Title = title;
            AssignedPlayerLogin = assignedLogin;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref OfficeId, "OfficeId");
            Scribe_Values.Look(ref Title, "Title", "Minister");
            Scribe_Values.Look(ref AssignedPlayerLogin, "AssignedPlayerLogin", "");
            Scribe_Values.Look(ref CanDeclareWar, "CanDeclareWar", false);
            Scribe_Values.Look(ref CanAuthorizePeace, "CanAuthorizePeace", false);
            Scribe_Values.Look(ref CanInviteBanishCitizens, "CanInviteBanishCitizens", false);
            Scribe_Values.Look(ref CanAdjustCustomsTariff, "CanAdjustCustomsTariff", false);
            Scribe_Values.Look(ref CanAccessNationalTreasury, "CanAccessNationalTreasury", false);
        }
    }

    [Serializable]
    public class CitizenRecord : IExposable
    {
        public string PlayerLogin = "";
        public string JoinDateStr = "";
        public string AssignedOfficeTitle = "Citizen";
        public int OwnedSettlementsCount = 1;

        public CitizenRecord() { }

        public CitizenRecord(string login, string office = "Citizen", int settlements = 1)
        {
            PlayerLogin = login;
            AssignedOfficeTitle = office;
            OwnedSettlementsCount = settlements;
            JoinDateStr = DateTime.Now.ToString("yyyy-MM-dd");
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref PlayerLogin, "PlayerLogin", "");
            Scribe_Values.Look(ref JoinDateStr, "JoinDateStr", "");
            Scribe_Values.Look(ref AssignedOfficeTitle, "AssignedOfficeTitle", "Citizen");
            Scribe_Values.Look(ref OwnedSettlementsCount, "OwnedSettlementsCount", 1);
        }
    }

    [Serializable]
    public class MarketTradeLot : IExposable
    {
        public string LotId = Guid.NewGuid().ToString();
        public string SellerLogin = "Imperial Foundry";
        public string SellerStateName = "Sovereign Realm";
        public string ItemDefName = "Steel";
        public string ItemLabel = "Steel";
        public int StackCount = 100;
        public float UnitPrice = 2.5f;
        public MarketCategory Category = MarketCategory.Resources;
        public QualityCategory Quality = QualityCategory.Normal;

        public float TotalPrice => StackCount * UnitPrice;

        public MarketTradeLot() { }

        public MarketTradeLot(string seller, string state, string defName, string label, int count, float price, MarketCategory cat)
        {
            SellerLogin = seller;
            SellerStateName = state;
            ItemDefName = defName;
            ItemLabel = label;
            StackCount = count;
            UnitPrice = price;
            Category = cat;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref LotId, "LotId");
            Scribe_Values.Look(ref SellerLogin, "SellerLogin", "");
            Scribe_Values.Look(ref SellerStateName, "SellerStateName", "");
            Scribe_Values.Look(ref ItemDefName, "ItemDefName", "Steel");
            Scribe_Values.Look(ref ItemLabel, "ItemLabel", "Steel");
            Scribe_Values.Look(ref StackCount, "StackCount", 100);
            Scribe_Values.Look(ref UnitPrice, "UnitPrice", 2.5f);
            Scribe_Values.Look(ref Category, "Category", MarketCategory.Resources);
            Scribe_Values.Look(ref Quality, "Quality", QualityCategory.Normal);
        }
    }

    [Serializable]
    public class MercenaryPlatoonContract : IExposable
    {
        public string ContractId = Guid.NewGuid().ToString();
        public string ProviderLogin = "Fortress Commander";
        public string ProviderState = "Iron March";
        public string PlatoonName = "4th Flak Vanguard Platoon";
        public int SoldierCount = 4;
        public float DailyFeeSilver = 150f;
        public int DurationDays = 5;
        public bool IsHired = false;
        public string RenterLogin = "";

        public MercenaryPlatoonContract() { }

        public MercenaryPlatoonContract(string provider, string state, string name, int soldiers, float fee, int days)
        {
            ProviderLogin = provider;
            ProviderState = state;
            PlatoonName = name;
            SoldierCount = soldiers;
            DailyFeeSilver = fee;
            DurationDays = days;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ContractId, "ContractId");
            Scribe_Values.Look(ref ProviderLogin, "ProviderLogin", "");
            Scribe_Values.Look(ref ProviderState, "ProviderState", "");
            Scribe_Values.Look(ref PlatoonName, "PlatoonName", "");
            Scribe_Values.Look(ref SoldierCount, "SoldierCount", 4);
            Scribe_Values.Look(ref DailyFeeSilver, "DailyFeeSilver", 150f);
            Scribe_Values.Look(ref DurationDays, "DurationDays", 5);
            Scribe_Values.Look(ref IsHired, "IsHired", false);
            Scribe_Values.Look(ref RenterLogin, "RenterLogin", "");
        }
    }

    [Serializable]
    public class SovereignLoanContract : IExposable
    {
        public string LoanId = Guid.NewGuid().ToString();
        public string LenderLogin = "Central Banking Syndicate";
        public string BorrowerLogin = "";
        public float PrincipalSilver = 5000f;
        public float InterestRate = 0.10f; // 10%
        public int RepaymentDueTick = 60000 * 15; // 15 days
        public int CollateralSettlementTile = -1;
        public string CollateralSettlementName = "Border Outpost";
        public bool IsActive = false;
        public bool IsDefaulted = false;
        public bool IsRepaid = false;

        public float TotalRepaymentDue => PrincipalSilver * (1f + InterestRate);

        public SovereignLoanContract() { }

        public SovereignLoanContract(string lender, float principal, float interest, int tile, string colName)
        {
            LenderLogin = lender;
            PrincipalSilver = principal;
            InterestRate = interest;
            CollateralSettlementTile = tile;
            CollateralSettlementName = colName;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref LoanId, "LoanId");
            Scribe_Values.Look(ref LenderLogin, "LenderLogin", "");
            Scribe_Values.Look(ref BorrowerLogin, "BorrowerLogin", "");
            Scribe_Values.Look(ref PrincipalSilver, "PrincipalSilver", 5000f);
            Scribe_Values.Look(ref InterestRate, "InterestRate", 0.10f);
            Scribe_Values.Look(ref RepaymentDueTick, "RepaymentDueTick", 600000);
            Scribe_Values.Look(ref CollateralSettlementTile, "CollateralSettlementTile", -1);
            Scribe_Values.Look(ref CollateralSettlementName, "CollateralSettlementName", "Border Outpost");
            Scribe_Values.Look(ref IsActive, "IsActive", false);
            Scribe_Values.Look(ref IsDefaulted, "IsDefaulted", false);
            Scribe_Values.Look(ref IsRepaid, "IsRepaid", false);
        }
    }
}
