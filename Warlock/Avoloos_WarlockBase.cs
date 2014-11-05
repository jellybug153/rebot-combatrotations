
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReBot.API;
using WarlockCommon;
using System;
using System.Collections.Generic;

namespace Avoloos
{
    public class ExpirableUnitObject
    {
        private DateTime TimeCreated;
        public int ExpiresIn { get; set; } // milliseconds
        public UnitObject Unit { get; set; }

        public ExpirableUnitObject(UnitObject unit, int expire)
        {
            TimeCreated = DateTime.Now; // now
            this.Unit = unit;
            this.ExpiresIn = expire;
        }

        public bool IsExpired()
        {
            return DateTime.Now.Millisecond >= TimeCreated.Millisecond + ExpiresIn;
        }
    }
}

namespace Avoloos
{
    namespace Warlock
    {
        abstract public class WarlockBaseRotation : CombatRotation
        {
            [JsonProperty("SelectedPet"), JsonConverter(typeof(StringEnumConverter))]
            public WarlockPet SelectedPet = WarlockPet.AutoSelect;
            [JsonProperty("UsePet")]
            public bool UsePet = true;
            [JsonProperty("UseAdditionalDPSPet")]
            public bool UseAdditionalDPSPet = true;
            
            [JsonProperty("PvP-DoFear")]
            public bool FearDoFear = true;
            [JsonProperty("PvP-FearBanTime")]
            public int FearBanTime = 10000;
            

            protected List<ExpirableUnitObject> fearTrackingList;
            
            public WarlockBaseRotation()
            {
                fearTrackingList = new List<ExpirableUnitObject>();
            }

            protected bool CastFearIfFeasible()
            {
                if(!FearDoFear) return false;
                
                try{
                    fearTrackingList = fearTrackingList.Where(x => !x.IsExpired()).ToList(); // trim the list to all non expired feared objects.

                    // TODO: Find out which one is the right one...
                    //if (CastSelf("Howl of Terror", () => Target.IsPlayer && Target.DistanceSquared <= 8 * 8 || Adds.Any(x => x.IsPlayer && x.DistanceSquared <= 8 * 8))) return true;
                    if (CastSelf("Howl of Terror", () => Adds.Count(x => x.DistanceSquared < 11 * 11) >= 2))
                    {
                        foreach (var fearedAdd in Adds.Where(x => x.DistanceSquared < 11 * 11))
                        {
                            fearTrackingList.Add(new ExpirableUnitObject(fearedAdd, FearBanTime));
                        } // Do not fear them again (at least not with feat, howl of terror won't be affected by its fear descision, for the moment...)
                        return true;
                    }

                    foreach (var add in Adds.Where(x => x.HasAura("Fear") && fearTrackingList.Count((y) => y.Unit == x) == 0))
                    {
                        // Add feared adds which were not feared by us
                        fearTrackingList.Add(new ExpirableUnitObject(add, FearBanTime));
                    }
                } catch { // catch everything
                    API.PrintError("Got an error in fear management logic. Disabling fear for now... Please Report to Avoloos: {0}", e);
                    FearDoFear = false;
                }

                UnitObject add2 = Adds.FirstOrDefault(x => x.Target != null && x.DistanceSquared <= SpellMaxRangeSq("Fear"));
                if (add2 != null && add2.DistanceSquared < SpellMaxRangeSq("Fear"))
                {
                    var add = add2;
                    // Fear only real close targets which attack us
                    if (CastPreventDouble(
                        "Fear", () =>
                               (!add.HasAura("Fear")                             // its not already feared by other ppl.
                            && !add.HasAura("Howl of Terror")                    // and its not already feared by howl
                            && Target.DistanceSquared <= 6.5 * 6.5               // and its close
                            && add.Target == Me                                  // and its targetting me
                            || add.IsCastingAndInterruptible())                  // or its casting and we can interrupt it with fear :D
                            && (FearDoFear && fearTrackingList.Count((x) => x.Unit == add) == 0) // and its not banned from fear
                        , add, 1000
                    )) return true;
                }

                return false;
            }

            protected bool HasFelguard()
            {
                return ((Me.Pet.EntryID == 17252) || (Me.Pet.EntryID == 58965));
            }

            protected bool CastOnAdds(string spellName)
            {
                foreach (var add in Adds)
                {
                    if (Cast(
                        spellName,
                        add
                    )) return true;
                }
                return false;
            }

            protected bool CastOnAdds(string spellName, Func<ReBot.API.UnitObject, bool> castCondition)
            {
                foreach (var add in Adds)
                {
                    if (Cast(
                        spellName,
                        () => castCondition(add),
                        add
                    )) return true;
                }
                return false;
            }

            protected bool CastOnAdds(string spellName, Func<ReBot.API.UnitObject, bool> castCondition, Func<ReBot.API.UnitObject, bool> addsCondition)
            {
                foreach (var add in Adds.Where(addsCondition))
                {
                    if (Cast(
                        spellName,
                        () => castCondition(add),
                        add
                    )) return true;
                }
                return false;
            }

            protected bool CastPreventDoubleOnAdds(string spellName, Func<ReBot.API.UnitObject, bool> castCondition)
            {
                foreach (var add in Adds)
                {
                    if (CastPreventDouble(
                        spellName,
                        () => castCondition(add),
                        add
                    )) return true;
                }
                return false;
            }

            protected bool CastPreventDoubleOnAdds(string spellName, Func<ReBot.API.UnitObject, bool> castCondition, Func<ReBot.API.UnitObject, bool> addsCondition)
            {
                foreach (var add in Adds.Where(addsCondition))
                {
                    if (CastPreventDouble(
                        spellName,
                        () => castCondition(add),
                        add
                    )) return true;
                }
                return false;
            }

            public override bool OutOfCombat()
            {
                if (HasSpell("Fire and Brimstone") && CastSelf("Fire and Brimstone", () => Me.HasAura("Fire and Brimstone")) && HasGlobalCooldown()) return true;
                if (CastSelf("Dark Intent", () => !Me.HasAura("Dark Intent"))) return true;
                if (CastSelf("Unending Breath", () => Me.IsSwimming && !Me.HasAura("Unending Breath"))) return true;
                if (CastSelf("Soulstone", () => CurrentBotName != "Combat" && !Me.HasAura("Soulstone"))) return true;

                if (HasSpell("Grimoire of Sacrifice") && !Me.HasAura("Grimoire of Sacrifice"))
                {
                    if (this.SummonPet(SelectedPet)) return true;
                    if (CastSelf("Grimoire of Sacrifice", () => Me.HasAlivePet)) return true;
                }
                else if (UsePet)
                {
                    if (HasSpell("Flames of Xoroth") && CastSelf("Flames of Xoroth", () => !Me.HasAlivePet && Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers) >= 1)) return true;
                    if (this.SummonPet(SelectedPet)) return true;
                }
                else if (Me.HasAlivePet)
                {
                    Me.PetDismiss();
                }

                if (CastSelfPreventDouble("Create Healthstone", () => Inventory.Healthstone == null, 10000)) return true;

                return false;
            }

            public override bool AfterCombat()
            {
                if (HasSpell("Fire and Brimstone") && CastSelf("Fire and Brimstone", () => Me.HasAura("Fire and Brimstone"))) return true;
                if (HasSpell("Metamorphosis") && CastSelf("Metamorphosis", () => Me.HasAura("Metamorphosis"))) return true;
                return false;
            }

            protected bool doGlobalStuff()
            {
                //no globalcd
                if (HasSpell("Dark Soul: Instability")) CastSelfPreventDouble("Dark Soul: Instability", () => Target.IsInCombatRangeAndLoS, 120000);
                if (HasSpell("Dark Soul: Knowledge")) CastSelfPreventDouble("Dark Soul: Knowledge", () => Target.IsInCombatRangeAndLoS, 120000);
                if (HasSpell("Dark Soul: Misery")) CastSelfPreventDouble("Dark Soul: Misery", () => Target.IsInCombatRangeAndLoS, 120000);

                Cast("Command Demon", () => (this.IsPetActive("Summon Felhunter") || SelectedPet == WarlockPet.Felhunter) && Target.IsCastingAndInterruptible());
                Cast("Command Demon", () => !HasSpell("Grimoire of Sacrifice") && (this.IsPetActive("Summon Imp") || SelectedPet == WarlockPet.SoulImp) && Me.HealthFraction <= 0.75);
                Cast("Command Demon", () => (this.IsPetActive("Summon Voidwalker") || SelectedPet == WarlockPet.Voidwalker) && Me.HealthFraction < 0.5);
                Cast("Axe Toss", () => HasFelguard() && Target.IsCastingAndInterruptible()); // Should have no gc as its a pet ability

                //Heal
                if (HasSpell("Unbound Will")) CastSelf("Unbound Will", () => !Me.CanParticipateInCombat); //no gc
                if (HasSpell("Dark Bargain")) CastSelf("Dark Bargain", () => Me.HealthFraction < 0.5); //no gc
                if (HasSpell("Sacrificial Pact")) CastSelf("Sacrificial Pact", () => Me.HealthFraction < 0.6); //no gc
                if (HasSpell("Unending Resolve")) CastSelf("Unending Resolve", () => Me.HealthFraction <= 0.5); //no gc

                if (Me.HasAlivePet)
                {
                    UnitObject add = Adds.FirstOrDefault(x => x.Target == Me);
                    if (add != null)
                        Me.PetAttack(add);
                }

                //GLOBAL CD CHECK
                if (HasGlobalCooldown())
                    return true;

                return false;
            }

            protected bool doSomePetAndHealingStuff()
            {
                if (HasSpell("Mortal Coil") && Cast("Mortal Coil", () => Me.HealthFraction <= 0.5)) return true;
                if (HasSpell("Dark Regeneration") && CastSelf("Dark Regeneration", () => Me.HealthFraction <= 0.6)) return true;
                if (HasSpell("Flames of Xoroth") && CastSelf("Flames of Xoroth", () => !HasSpell("Grimoire of Sacrifice") && !Me.HasAlivePet && Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers) >= 1)) return true;
                if (UseAdditionalDPSPet && HasSpell("Summon Terrorguard") && Cast("Summon Terrorguard", () => Me.HpLessThanOrElite(0.5))) return true;
                if (UseAdditionalDPSPet && HasSpell("Summon Doomguard") && Cast("Summon Doomguard", () => Me.HpLessThanOrElite(0.5))) return true;
                if (UseAdditionalDPSPet && HasSpell("Summon Infernal") && Cast("Summon Infernal", () => Me.HpLessThanOrElite(0.5) || Adds.Count >= 4)) return true;
                if (UseAdditionalDPSPet && HasSpell("Summon Abyssal") && Cast("Summon Abyssal", () => Me.HpLessThanOrElite(0.5) || Adds.Count >= 4)) return true;
                if (HasSpell("Demonic Rebirth") && CastSelf("Demonic Rebirth", () => Me.HealthFraction < 0.9 && Target.IsInCombatRangeAndLoS)) return true;
                if (HasSpell("Ember Tap") && CastSelf("Ember Tap", () => Me.HealthFraction <= 0.35 && Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers) >= 1)) return true;

                return false;
            }
        }
    }
}